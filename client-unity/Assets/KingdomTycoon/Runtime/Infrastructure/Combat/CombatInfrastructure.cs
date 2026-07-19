using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Combat;
using KingdomTycoon.Domain.Combat;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Inventory;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using CombatSplitMix64 = KingdomTycoon.Domain.Combat.SplitMix64;

namespace KingdomTycoon.Infrastructure.Combat
{
    public sealed class CombatJobProfile
    {
        public CombatJobProfile(IReadOnlyDictionary<string, string> row)
        {
            JobId = row["job_id"];
            MaxHp = Int(row, "max_hp"); Attack = Int(row, "attack"); Defense = Int(row, "defense");
            HealPower = Int(row, "heal_power"); AttackSpeedMilli = Int(row, "attack_speed_milli");
            MoveSpeedMilli = Int(row, "move_speed_milli"); RangeMilli = Int(row, "range_milli");
        }
        public string JobId { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int HealPower { get; }
        public int AttackSpeedMilli { get; }
        public int MoveSpeedMilli { get; }
        public int RangeMilli { get; }
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    public sealed class MonsterCombatProfile
    {
        public MonsterCombatProfile(IReadOnlyDictionary<string, string> row)
        {
            MonsterId = row["monster_id"];
            MaxHp = Int(row, "hp"); Attack = Int(row, "attack"); Defense = Int(row, "defense"); Bounty = Int(row, "bounty_personal_gold");
        }
        public string MonsterId { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Bounty { get; }
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    public sealed class EncounterCombatProfile
    {
        public EncounterCombatProfile(IReadOnlyDictionary<string, string> row)
        {
            RegionId = row["region_id"]; MonsterId = row["monster_id"];
            Weight = Int(row, "weight"); WaveMin = Int(row, "wave_min"); WaveMax = Int(row, "wave_max"); SpawnGroup = row["spawn_group"];
        }
        public string RegionId { get; }
        public string MonsterId { get; }
        public int Weight { get; }
        public int WaveMin { get; }
        public int WaveMax { get; }
        public string SpawnGroup { get; }
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    public sealed class CanonicalCombatCatalog
    {
        private readonly Dictionary<string, CombatJobProfile> jobs;
        private readonly Dictionary<string, MonsterCombatProfile> monsters;
        private readonly Dictionary<string, EncounterCombatProfile[]> encounters;

        public CanonicalCombatCatalog(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.ContentVersion is not (CompileTimeActiveContentVersionProvider.P06ContentVersion or CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion))
                throw new CombatDomainException("P06_CONTENT_VERSION_UNSUPPORTED");
            ContentVersion = catalog.ContentVersion;
            jobs = catalog.GetTable("combat_job_profiles.csv").Rows.Where(Enabled).Select(row => new CombatJobProfile(row)).ToDictionary(value => value.JobId, StringComparer.Ordinal);
            monsters = catalog.GetTable("monsters.csv").Rows.Where(Enabled).Select(row => new MonsterCombatProfile(row)).ToDictionary(value => value.MonsterId, StringComparer.Ordinal);
            encounters = catalog.GetTable("region_encounter_profiles.csv").Rows.Where(Enabled).Select(row => new EncounterCombatProfile(row))
                .GroupBy(value => value.RegionId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(value => value.MonsterId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            AutonomyRules = catalog.GetTable("autonomy_rules.csv").Rows.Select(row => new AutonomyRule(
                row["state"], Int(row, "rule_no"), Int(row, "priority"), row["condition_type"], row["condition_value"], row["reason_code"], row["next_state"], row["enabled"] == "TRUE")).ToArray();
            if (jobs.Count != 5 || !encounters.TryGetValue("REGION_R01", out EncounterCombatProfile[] r01) || r01.Sum(value => value.Weight) != 100)
                throw new CombatDomainException("P06_CONTENT_MISSING");
        }

        public IReadOnlyList<AutonomyRule> AutonomyRules { get; }
        public string ContentVersion { get; }
        public CombatJobProfile Job(string id) => jobs.TryGetValue(id, out CombatJobProfile value) ? value : throw new CombatDomainException("P06_CONTENT_MISSING");
        public MonsterCombatProfile Monster(string id) => monsters.TryGetValue(id, out MonsterCombatProfile value) ? value : throw new CombatDomainException("P06_CONTENT_MISSING");
        public (EncounterCombatProfile Profile, int WaveSize) SelectEncounter(string regionId, CombatSplitMix64 random)
        {
            if (!encounters.TryGetValue(regionId, out EncounterCombatProfile[] values)) throw new CombatDomainException("P06_REGION_NOT_AVAILABLE");
            int draw = (int)random.NextBounded((ulong)values.Sum(value => value.Weight));
            EncounterCombatProfile selected = null;
            foreach (EncounterCombatProfile value in values)
            {
                if (draw < value.Weight) { selected = value; break; }
                draw -= value.Weight;
            }
            selected ??= values[^1];
            int wave = selected.WaveMin + (int)random.NextBounded((ulong)(selected.WaveMax - selected.WaveMin + 1));
            return (selected, wave);
        }
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public sealed class CombatRequestHasher : ICombatRequestHasher
    {
        public string ComputeHash(StartHuntCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
        public string ComputeHash(RecallHuntCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class CombatGameService : IAppService, ICombatUnitOfWork
    {
        private readonly ITrustedUtcClock clock;
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private InventoryGameService inventory;
        private CanonicalCombatCatalog catalog;
        private RuntimeSession session;
        private RecallHuntResult lastTerminalResult;

        public CombatGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 70;
        public bool IsBootstrapped { get; private set; }
        public long Revision => game?.Revision ?? 0;
        public event EventHandler<HuntSnapshotDto> SnapshotChanged;
        public event EventHandler<RecallHuntResult> HuntSettled;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>();
            content = services.Get<ContentCatalogService>();
            game = services.Get<FacilityGameService>();
            try { inventory = services.Get<InventoryGameService>(); }
            catch (InvalidOperationException) { inventory = null; }
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog == null) throw new CombatDomainException("P06_CONTENT_MISSING");
            catalog = new CanonicalCombatCatalog(content.Catalog);
            JObject current = game.Snapshot();
            bool needsWrite = false;
            if (current.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P05ContentVersion)
            {
                current = new P05ToP06ContentMigration(clock).Apply(current);
                needsWrite = true;
            }
            else if (current.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P06ContentVersion or CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion))
            {
                throw new CombatDomainException("P06_CONTENT_VERSION_UNSUPPORTED");
            }
            else
            {
                needsWrite = P05ToP06ContentMigration.NormalizeTransientAutonomy(current, clock.UtcNow);
            }
            if (needsWrite) Commit(current, game.Revision);
            IsBootstrapped = true;
        }

        public StartHuntResult StartHunt(StartHuntCommand command)
        {
            EnsureReady();
            if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new CombatRequestHasher().ComputeHash(command));
            JObject existing = FindJournal(command.OperationId);
            if (existing != null)
            {
                VerifyHash(command.RequestHash, existing.Value<string>("requestHash"));
                return new StartHuntResult(command.OperationId, command.HuntOperationId, Revision, Revision, command.RegionId,
                    command.PartyMercenaryInstanceIds.Select(value => value.ToString("D")).ToArray(), "TRAVEL_TO_REGION", true, existing.Value<string>("resultDigest"));
            }
            Require(session == null, "P06_COMBAT_INVARIANT");
            Require(command.ExpectedRevision == Revision, "P06_SAVE_REVISION_CONFLICT");
            Require(command.RegionId == "REGION_R01" && RegionUnlocked(command.RegionId), "P06_REGION_NOT_AVAILABLE");
            string[] party = command.PartyMercenaryInstanceIds.Select(value => value.ToString("D")).ToArray();
            Require(party.Distinct(StringComparer.Ordinal).Count() == party.Length, "P06_PARTY_DUPLICATE");
            JObject current = game.Snapshot();
            var mercenaries = current["payload"]!["mercenaries"]!.Children<JObject>().ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            foreach (string id in party)
            {
                Require(mercenaries.TryGetValue(id, out JObject value), "P06_PARTY_MEMBER_NOT_FOUND");
                Require(value.Value<bool>("active") && value["autonomy"]!.Value<string>("state") == "IDLE_TOWN", "P06_PARTY_MEMBER_NOT_ELIGIBLE");
            }

            long before = Revision;
            string now = FormatUtc(clock.UtcNow);
            foreach (string id in party)
            {
                JObject autonomy = (JObject)mercenaries[id]["autonomy"]!;
                autonomy["state"] = "TRAVEL_TO_REGION"; autonomy["reasonCode"] = "POLICY"; autonomy["currentRegionId"] = command.RegionId;
                autonomy["targetInstanceId"] = null; autonomy["stateStartedAtUtc"] = now; autonomy["nextDecisionAtUtc"] = now;
            }
            string digest = Rfc8785Canonicalizer.ComputeSha256(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["huntOperationId"] = command.HuntOperationId.ToString("D"),
                ["revisionBefore"] = before, ["revisionAfter"] = before + 1, ["regionId"] = command.RegionId,
                ["canonicalPartyMercenaryInstanceIds"] = new JArray(party), ["persistedState"] = "TRAVEL_TO_REGION",
                ["eventCodes"] = new JArray("HuntPrepared", "HuntStarted"), ["replayed"] = false
            });
            AppendJournal(current, command.OperationId, command.RequestHash, digest, clock.UtcNow);
            Commit(current, before);
            session = NewSession(command.HuntOperationId, command.RegionId, party, mercenaries);
            var result = new StartHuntResult(command.OperationId, command.HuntOperationId, before, Revision, command.RegionId, party, "TRAVEL_TO_REGION", false, digest);
            SnapshotChanged?.Invoke(this, GetSnapshot());
            return result;
        }

        public RecallHuntResult RecallHunt(RecallHuntCommand command)
        {
            EnsureReady();
            if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new CombatRequestHasher().ComputeHash(command));
            JObject existing = FindJournal(command.OperationId);
            if (existing != null && session == null)
            {
                VerifyHash(command.RequestHash, existing.Value<string>("requestHash"));
                return new RecallHuntResult(command.OperationId, Revision, Revision, "IDLE_TOWN", 0, Array.Empty<long>(), Array.Empty<long>(), true, existing.Value<string>("resultDigest"));
            }
            Require(session != null && session.HuntOperationId == command.OperationId, "P06_HUNT_NOT_ACTIVE");
            Require(command.ExpectedRevision == Revision, "P06_SAVE_REVISION_CONFLICT");
            Require(command.ReasonCode == "PLAYER_RECALL", "P06_COMBAT_INVARIANT");
            session.Simulation.Recall();
            session.Simulation.Step();
            AccumulateCurrentEncounter(session);
            return CommitTerminal(command.RequestHash, "PLAYER_RECALL");
        }

        public void Tick()
        {
            EnsureReady();
            if (session == null) return;
            session.Simulation.Step();
            if (!session.Simulation.IsComplete) { SnapshotChanged?.Invoke(this, GetSnapshot()); return; }
            AccumulateCurrentEncounter(session);
            if (session.Simulation.TerminalReason == "LOOT_COMPLETE")
            {
                session.EncounterIndex++;
                session.Simulation = BuildEncounter(session);
                SnapshotChanged?.Invoke(this, GetSnapshot());
            }
            else
            {
                CommitTerminal(Rfc8785Canonicalizer.ComputeSha256(new JObject { ["reason"] = session.Simulation.TerminalReason, ["huntOperationId"] = session.HuntOperationId.ToString("D") }), session.Simulation.TerminalReason);
            }
        }

        public HuntSnapshotDto GetSnapshot()
        {
            if (session == null) return new HuntSnapshotDto(false, null, 0, "IDLE_TOWN", lastTerminalResult?.TerminalState ?? "NONE", 0, 0, Array.Empty<HuntMemberDto>());
            var members = session.Simulation.Entities.Where(value => value.Team == CombatTeam.Party).Select((value, index) =>
                new HuntMemberDto(session.Party[index], value.RuntimeId, value.CurrentHp, value.MaxHp, value.DamageDealt, value.IsDown)).ToArray();
            int hostile = session.Simulation.Entities.Count(value => value.Team == CombatTeam.Hostile && !value.IsDown);
            return new HuntSnapshotDto(true, session.RegionId, session.Simulation.Tick, "COMBAT", "TARGET_FOUND", session.EncounterIndex, hostile, members);
        }

        public void Shutdown()
        {
            SnapshotChanged = null; HuntSettled = null; session = null; catalog = null; inventory = null; game = null; content = null; save = null; IsBootstrapped = false;
        }

        private RuntimeSession NewSession(Guid huntId, string regionId, string[] party, IReadOnlyDictionary<string, JObject> mercenaries)
        {
            var runtime = new RuntimeSession(huntId, regionId, party, mercenaries.ToDictionary(pair => pair.Key, pair => pair.Value.Value<string>("jobId"), StringComparer.Ordinal));
            runtime.Simulation = BuildEncounter(runtime);
            return runtime;
        }

        private HuntSimulation BuildEncounter(RuntimeSession runtime)
        {
            ulong seed = CombatDeterminism.EncounterSeed(catalog.ContentVersion, game.ActiveProfileId, runtime.HuntOperationId.ToString("D"), runtime.EncounterIndex);
            var random = new CombatSplitMix64(seed);
            (EncounterCombatProfile profile, int waveSize) = catalog.SelectEncounter(runtime.RegionId, random);
            MonsterCombatProfile monster = catalog.Monster(profile.MonsterId);
            var simulation = new HuntSimulation();
            for (int index = 0; index < runtime.Party.Length; index++)
            {
                string id = runtime.Party[index];
                CombatJobProfile job = catalog.Job(runtime.JobIds[id]);
                var equipment = new EquipmentStatBlock();
                if (inventory?.IsBootstrapped == true)
                {
                    JObject document = game.Snapshot();
                    JObject mercenary = document["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == id);
                    equipment = inventory.EquipmentModifier(document, mercenary);
                }
                simulation.Add(new Combatant($"{runtime.HuntOperationId:D}:{runtime.EncounterIndex:0000}:P:{index:000}", CombatTeam.Party,
                    checked(job.MaxHp + equipment.MaxHp), checked(job.Attack + equipment.Attack), checked(job.Defense + equipment.Defense),
                    job.RangeMilli, job.AttackSpeedMilli, index * 700, 0, checked(job.MoveSpeedMilli + equipment.MoveSpeed)));
            }
            for (int index = 0; index < waveSize; index++)
                simulation.Add(new Combatant($"{runtime.HuntOperationId:D}:{runtime.EncounterIndex:0000}:M:{index:000}", CombatTeam.Hostile, monster.MaxHp, monster.Attack, monster.Defense, 1500, 900, 2500 + index * 700, 0, 3000));
            return simulation;
        }

        private RecallHuntResult CommitTerminal(string requestHash, string reason)
        {
            RuntimeSession completed = session ?? throw new CombatDomainException("P06_HUNT_NOT_ACTIVE");
            long before = Revision;
            JObject draft = game.Snapshot();
            string now = FormatUtc(clock.UtcNow);
            var mercenaries = draft["payload"]!["mercenaries"]!.Children<JObject>().ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            var contribution = new List<long>();
            var gold = new List<long>();
            for (int index = 0; index < completed.Party.Length; index++)
            {
                JObject value = mercenaries[completed.Party[index]];
                JObject autonomy = (JObject)value["autonomy"]!;
                autonomy["state"] = reason == "HP_LOW" ? "INJURED" : "IDLE_TOWN";
                autonomy["reasonCode"] = reason == "HP_LOW" ? "HP_LOW" : "NONE";
                autonomy["currentRegionId"] = null; autonomy["targetInstanceId"] = null; autonomy["stateStartedAtUtc"] = now; autonomy["nextDecisionAtUtc"] = now;
                long contributionDelta = completed.Contribution.GetValueOrDefault(completed.Party[index]);
                long goldDelta = completed.KillCount > 0 ? 7 : 0;
                value["contribution"] = value.Value<long>("contribution") + contributionDelta;
                value["personalGold"] = value.Value<long>("personalGold") + goldDelta;
                value["records"]!["killCount"] = value["records"]!.Value<long>("killCount") + completed.KillCount;
                if (completed.KillCount > 0) value["records"]!["huntCount"] = value["records"]!.Value<long>("huntCount") + 1;
                contribution.Add(contributionDelta); gold.Add(goldDelta);
            }
            InventorySettlementMutation loot = inventory?.IsBootstrapped == true
                ? inventory.ApplyTerminalLoot(draft, completed.HuntOperationId, completed.KillCount, completed.Party)
                : new InventorySettlementMutation();
            var terminalDigest = new JObject
            {
                ["operationId"] = completed.HuntOperationId.ToString("D"), ["revisionBefore"] = before, ["revisionAfter"] = before + 1,
                ["terminalState"] = "IDLE_TOWN", ["killCountDelta"] = completed.KillCount, ["huntCountDeltaPerPartyMember"] = completed.KillCount > 0 ? 1 : 0,
                ["contributionDelta"] = new JArray(contribution), ["personalGoldDelta"] = new JArray(gold), ["rewardJournalCount"] = 1, ["replayed"] = false
            };
            if (inventory?.IsBootstrapped == true)
            {
                terminalDigest["retainedItems"] = loot.RetainedItems;
                terminalDigest["retainedEquipment"] = loot.RetainedEquipment;
                terminalDigest["equipmentInstanceId"] = loot.EquipmentInstanceId == null
                    ? JValue.CreateNull()
                    : new JValue(loot.EquipmentInstanceId);
            }
            string digest = Rfc8785Canonicalizer.ComputeSha256(terminalDigest);
            AppendJournal(draft, completed.HuntOperationId, requestHash, digest, clock.UtcNow);
            Commit(draft, before);
            var result = new RecallHuntResult(completed.HuntOperationId, before, Revision, "IDLE_TOWN", completed.KillCount, contribution, gold, false, digest);
            session = null; lastTerminalResult = result; HuntSettled?.Invoke(this, result); SnapshotChanged?.Invoke(this, GetSnapshot());
            return result;
        }

        private void Commit(JObject draft, long expectedRevision)
        {
            SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, expectedRevision, clock.UtcNow);
            if (!written.Success) throw new CombatDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P06_SAVE_REVISION_CONFLICT" : "P06_SAVE_WRITE_FAILED");
            game.SynchronizeCommittedDocument(written.Document);
        }

        private static void AccumulateCurrentEncounter(RuntimeSession runtime)
        {
            runtime.KillCount += runtime.Simulation.KillCount;
            Combatant[] members = runtime.Simulation.Entities.Where(value => value.Team == CombatTeam.Party).ToArray();
            for (int index = 0; index < members.Length; index++)
            {
                string mercenaryId = runtime.Party[index];
                runtime.Contribution[mercenaryId] = runtime.Contribution.GetValueOrDefault(mercenaryId) + members[index].DamageDealt;
            }
        }

        private bool RegionUnlocked(string regionId) => game.Snapshot()["payload"]!["regions"]!["progress"]!.Children<JObject>().Any(value => value.Value<string>("regionId") == regionId && value.Value<bool>("unlocked"));
        private JObject FindJournal(Guid operationId) => game.Snapshot()["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == operationId.ToString("D"));
        private static void AppendJournal(JObject document, Guid operationId, string requestHash, string resultDigest, DateTimeOffset now)
        {
            string timestamp = FormatUtc(now);
            ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = operationId.ToString("D"), ["operationType"] = "REWARD", ["facilityJobType"] = null,
                ["requestHash"] = requestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp,
                ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = resultDigest,
                ["failureResolution"] = null, ["resolvedAtUtc"] = null
            });
        }
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static void VerifyHash(string expected, string actual) { if (!FixedTimeEquals(expected, actual)) throw new CombatDomainException("P06_OPERATION_HASH_MISMATCH"); }
        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0; for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0;
        }
        private static void Require(bool condition, string code) { if (!condition) throw new CombatDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped) throw new CombatDomainException("P06_CONTENT_MISSING"); }

        private sealed class RuntimeSession
        {
            public RuntimeSession(Guid huntOperationId, string regionId, string[] party, IReadOnlyDictionary<string, string> jobIds)
            { HuntOperationId = huntOperationId; RegionId = regionId; Party = party; JobIds = jobIds; }
            public Guid HuntOperationId { get; }
            public string RegionId { get; }
            public string[] Party { get; }
            public IReadOnlyDictionary<string, string> JobIds { get; }
            public HuntSimulation Simulation { get; set; }
            public int EncounterIndex { get; set; }
            public int KillCount { get; set; }
            public Dictionary<string, long> Contribution { get; } = new(StringComparer.Ordinal);
        }
    }
}
