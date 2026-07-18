using System;
using System.IO;
using System.Linq;
using System.Text;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class SaveContractTests
    {
        private string temporaryDirectory;
        private SaveDocumentValidator validator;

        [SetUp]
        public void SetUp()
        {
            TextAsset schema = Resources.Load<TextAsset>("Contracts/save.schema");
            Assert.That(schema, Is.Not.Null);
            validator = new SaveDocumentValidator(schema.text);
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-save-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void Canonicalizer_MatchesApprovedPayloadVector()
        {
            JToken input = StrictJson.Parse("{\"c\":[true,null,0.125],\"b\":\"한글\",\"a\":1}");

            byte[] canonical = Rfc8785Canonicalizer.Canonicalize(input);

            Assert.That(canonical.Length, Is.EqualTo(42));
            Assert.That(Encoding.UTF8.GetString(canonical), Is.EqualTo("{\"a\":1,\"b\":\"한글\",\"c\":[true,null,0.125]}"));
            Assert.That(
                Rfc8785Canonicalizer.ComputeSha256(input),
                Is.EqualTo("efb1c0e6a6ddcf9323d7c4ff1ed8636da77d7d2ea05d5d0d531ef2cb36575075"));
        }

        [Test]
        public void Canonicalizer_MatchesApprovedEnvelopeVector()
        {
            JToken input = StrictJson.Parse(
                "{\"schemaId\":\"urn:tycoon:save:v1\",\"saveVersion\":1,\"gameVersion\":\"1.0.0\"," +
                "\"contentVersion\":\"1.0.0-content.1\",\"saveId\":\"00000000-0000-0000-0000-000000000001\"," +
                "\"profileId\":\"00000000-0000-0000-0000-000000000002\",\"revision\":1," +
                "\"createdAtUtc\":\"2026-07-18T00:00:00.000Z\",\"savedAtUtc\":\"2026-07-18T00:00:01.000Z\"," +
                "\"integrityVersion\":1,\"algorithm\":\"SHA-256\",\"canonicalization\":\"RFC8785\"," +
                "\"payloadSha256\":\"efb1c0e6a6ddcf9323d7c4ff1ed8636da77d7d2ea05d5d0d531ef2cb36575075\"}");

            byte[] canonical = Rfc8785Canonicalizer.Canonicalize(input);

            Assert.That(canonical.Length, Is.EqualTo(455));
            Assert.That(
                Rfc8785Canonicalizer.ComputeSha256(input),
                Is.EqualTo("83d6a648bde781978ee5fd13f458c93a34a93a53092e180c1f7c036939443b0d"));
        }

        [Test]
        public void Validator_AcceptsStrictMinimalSave()
        {
            JObject document = CreateCommittedDocument();

            ValidationReport report = validator.Validate(document, "fixture:valid-minimal");

            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
        }

        [Test]
        public void Validator_RejectsUnknownProperty()
        {
            JObject document = CreateCommittedDocument();
            document["payload"]["profile"]["unknown"] = true;
            Rehash(document);

            ValidationReport report = validator.Validate(document, "fixture:unknown-property");

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "SAVE_SCHEMA_INVALID" && issue.Location == "/payload/profile/unknown"), Is.True);
        }

        [Test]
        public void Validator_RejectsFacilityUpgradeCounterMismatch()
        {
            JObject document = CreateCommittedDocument();
            document["payload"]["facilities"][0]["level"] = 2;
            Rehash(document);

            ValidationReport report = validator.Validate(document, "fixture:counter-mismatch");

            Assert.That(report.Issues.Any(issue => issue.Code == "SAVE_FACILITY_UPGRADE_COUNT_MISMATCH"), Is.True);
        }

        [Test]
        public void Repository_RotatesBackupAndRecoversCorruptActiveAsNewRevision()
        {
            JObject initial = CreateDraftDocument();
            string profileId = initial.Value<string>("profileId");
            var repository = new AtomicSaveRepository(temporaryDirectory, validator, TimeSpan.FromSeconds(1));

            SaveWriteResult first = repository.Save(profileId, initial, 0, Timestamp(1));
            Assert.That(first.Success, Is.True, first.ErrorCode);
            SaveWriteResult second = repository.Save(profileId, first.Document, 1, Timestamp(2));
            Assert.That(second.Success, Is.True, second.ErrorCode);

            string activePath = Path.Combine(temporaryDirectory, "saves", profileId, "save.json");
            File.WriteAllText(activePath, "{corrupt", new UTF8Encoding(false));

            SaveLoadResult loaded = repository.Load(profileId);

            Assert.That(loaded.Success, Is.True, loaded.ErrorCode);
            Assert.That(loaded.Recovered, Is.True);
            Assert.That(loaded.Source, Is.EqualTo("bak1"));
            Assert.That(loaded.Document.Value<long>("revision"), Is.EqualTo(2));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(activePath), "save.json.corrupt-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void Repository_RejectsSplitBrainAtSameRevision()
        {
            JObject first = validator.PrepareForCommit(CreateDraftDocument(), 0, Timestamp(1));
            JObject second = validator.PrepareForCommit(CreateDraftDocument(), 0, Timestamp(2));
            string profileId = first.Value<string>("profileId");
            string profileDirectory = Path.Combine(temporaryDirectory, "saves", profileId);
            Directory.CreateDirectory(profileDirectory);
            File.WriteAllText(Path.Combine(profileDirectory, "save.json"), first.ToString(Formatting.None), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(profileDirectory, "save.tmp"), second.ToString(Formatting.None), new UTF8Encoding(false));
            var repository = new AtomicSaveRepository(temporaryDirectory, validator, TimeSpan.FromSeconds(1));

            SaveLoadResult loaded = repository.Load(profileId);

            Assert.That(loaded.Success, Is.False);
            Assert.That(loaded.ErrorCode, Is.EqualTo("SAVE_SPLIT_BRAIN"));
        }

        [Test]
        public void Repository_RejectsStaleWriterAfterNewerRevisionWasCommitted()
        {
            JObject initial = CreateDraftDocument();
            string profileId = initial.Value<string>("profileId");
            var repository = new AtomicSaveRepository(temporaryDirectory, validator, TimeSpan.FromSeconds(1));
            SaveWriteResult first = repository.Save(profileId, initial, 0, Timestamp(1));
            SaveWriteResult second = repository.Save(profileId, first.Document, 1, Timestamp(2));
            Assert.That(second.Success, Is.True, second.ErrorCode);

            SaveWriteResult stale = repository.Save(profileId, first.Document, 1, Timestamp(3));

            Assert.That(stale.Success, Is.False);
            Assert.That(stale.ErrorCode, Is.EqualTo("SAVE_REVISION_CONFLICT"));
            SaveLoadResult loaded = repository.Load(profileId);
            Assert.That(loaded.Document.Value<long>("revision"), Is.EqualTo(2));
        }

        private JObject CreateCommittedDocument()
        {
            return validator.PrepareForCommit(CreateDraftDocument(), 0, Timestamp(1));
        }

        private static JObject CreateDraftDocument()
        {
            string profileId = "01982a00-0000-7000-8000-000000000002";
            string saveId = "01982a00-0000-7000-8000-000000000001";
            var facilities = new JArray(
                new[]
                {
                    "FAC_TAVERN",
                    "FAC_LODGE",
                    "FAC_GUILD",
                    "FAC_STORE",
                    "FAC_BLACKSMITH",
                    "FAC_ALCHEMY",
                    "FAC_WAREHOUSE",
                    "FAC_INFIRMARY"
                }.Select(id => new JObject
                {
                    ["facilityId"] = id,
                    ["level"] = 1,
                    ["state"] = "LOCKED",
                    ["assignedNpcInstanceId"] = null,
                    ["job"] = null,
                    ["storage"] = new JArray()
                }));

            return new JObject
            {
                ["schemaId"] = "urn:tycoon:save:v1",
                ["saveVersion"] = 1,
                ["gameVersion"] = "1.0.0",
                ["contentVersion"] = "1.0.0-content.1",
                ["saveId"] = saveId,
                ["profileId"] = profileId,
                ["revision"] = 0,
                ["createdAtUtc"] = Timestamp(0).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                ["savedAtUtc"] = Timestamp(0).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                ["integrity"] = new JObject
                {
                    ["integrityVersion"] = 1,
                    ["algorithm"] = "SHA-256",
                    ["canonicalization"] = "RFC8785",
                    ["payloadSha256"] = new string('0', 64),
                    ["fileSha256"] = new string('0', 64)
                },
                ["payload"] = new JObject
                {
                    ["profile"] = new JObject
                    {
                        ["nickname"] = "왕국",
                        ["locale"] = "ko-KR",
                        ["playerExp"] = 0,
                        ["progressionFlagIds"] = new JArray()
                    },
                    ["kingdom"] = new JObject
                    {
                        ["kingdomStageId"] = "KINGDOM_1",
                        ["kingdomGold"] = 0,
                        ["kingdomExp"] = 0,
                        ["activeMercenaryLimit"] = 4,
                        ["ownedMercenaryLimit"] = 8,
                        ["pricingPolicy"] = "STANDARD",
                        ["regionAccessPolicies"] = new JArray(),
                        ["inventoryPolicies"] = new JObject
                        {
                            ["autoSellMaxTier"] = 0,
                            ["protectQualityIds"] = new JArray()
                        },
                        ["progressionFlagIds"] = new JArray(),
                        ["facilityUpgradeCount"] = 0
                    },
                    ["mercenaries"] = new JArray(),
                    ["managementNpcs"] = new JArray(),
                    ["facilities"] = facilities,
                    ["inventory"] = new JObject
                    {
                        ["itemStacks"] = new JArray(),
                        ["equipment"] = new JArray(),
                        ["warehousePotions"] = new JArray()
                    },
                    ["regions"] = new JObject { ["progress"] = new JArray(), ["raids"] = new JArray() },
                    ["recruitmentMockState"] = new JObject
                    {
                        ["authority"] = "MOCK_ONLY",
                        ["serverRevision"] = null,
                        ["premiumWalletCache"] = new JObject
                        {
                            ["freePremium"] = 0,
                            ["paidPremium"] = 0,
                            ["specialRecruitTickets"] = 0
                        },
                        ["pityCounters"] = new JArray(),
                        ["featuredGuarantees"] = new JArray(),
                        ["pendingRequests"] = new JArray()
                    },
                    ["tutorial"] = new JObject
                    {
                        ["currentStepId"] = null,
                        ["completedStepIds"] = new JArray(),
                        ["grantedRewardIds"] = new JArray(),
                        ["skipped"] = false,
                        ["completedAtUtc"] = null
                    },
                    ["offline"] = new JObject
                    {
                        ["accrualCursorUtc"] = Timestamp(0).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                        ["lastTrustedUtc"] = Timestamp(0).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                        ["pendingSettlement"] = null
                    },
                    ["operationJournal"] = new JArray(),
                    ["settings"] = new JObject
                    {
                        ["masterVolume"] = 1.0m,
                        ["musicVolume"] = 0.8m,
                        ["sfxVolume"] = 1.0m,
                        ["vibrationEnabled"] = true,
                        ["battleSpeed"] = 1
                    },
                    ["extensions"] = new JObject()
                }
            };
        }

        private static void Rehash(JObject document)
        {
            document["integrity"]["payloadSha256"] = Rfc8785Canonicalizer.ComputeSha256(document["payload"]);
            document["integrity"]["fileSha256"] = Rfc8785Canonicalizer.ComputeSha256(SaveDocumentValidator.BuildEnvelopeDigestInput(document));
        }

        private static DateTimeOffset Timestamp(int seconds)
        {
            return new DateTimeOffset(2026, 7, 18, 0, 0, seconds, TimeSpan.Zero);
        }
    }
}
