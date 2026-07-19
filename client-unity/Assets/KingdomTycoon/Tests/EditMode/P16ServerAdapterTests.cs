using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Recruitment;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Domain.Recruitment;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Recruitment;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P16ServerAdapterTests
    {
        private RecruitmentCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            var content = new ContentCatalogService(
                new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")),
                new DevelopmentActiveContentVersionProvider(CompileTimeActiveContentVersionProvider.P15ContentVersion));
            content.LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            catalog = new RecruitmentCatalog(content.Catalog);
        }

        [Test]
        public async Task HttpGatewayUsesServerReceiptAndMapsWalletPityAndMercenary()
        {
            Guid operationId = Guid.Parse("019fb150-0000-7000-8000-000000000161");
            var draft = new SpecialRecruitmentCommand(operationId, 7, null, "SPECIAL_STANDARD_TICKET", "TICKET");
            var command = new SpecialRecruitmentCommand(operationId, 7, new RecruitmentRequestHasher().Compute(draft), "SPECIAL_STANDARD_TICKET", "TICKET");
            var transport = new StubTransport(new ServerApiResponse(200, SuccessBody()));
            var gateway = new HttpRecruitmentGateway(catalog, new SystemUuidV7Provider(), transport);

            SpecialRecruitmentReceipt receipt = await gateway.RecruitAsync(command, State(), DateTimeOffset.Parse("2026-07-20T00:00:00Z"), CancellationToken.None);

            Assert.That(gateway.Authority, Is.EqualTo("SERVER"));
            Assert.That(transport.Path, Is.EqualTo("api/v1/players/me/recruitments/special"));
            Assert.That(transport.IdempotencyKey, Is.EqualTo(operationId.ToString("D")));
            Assert.That(JObject.Parse(transport.Body).Value<string>("bannerKey"), Is.EqualTo("SPECIAL_STANDARD_TICKET"));
            Assert.That(receipt.ReceiptId, Is.EqualTo("019fb150-0000-7000-8000-000000000199"));
            Assert.That(receipt.WalletAfter.Value<long>("specialRecruitTickets"), Is.EqualTo(2));
            Assert.That(receipt.MercenarySnapshot.Value<string>("instanceId"), Is.EqualTo("019fb150-0000-7000-8000-000000000198"));
            Assert.That(receipt.MercenarySnapshot.Value<string>("jobId"), Is.EqualTo("JOB_WARRIOR"));
            Assert.That(receipt.MercenarySnapshot.Value<string>("gradeId"), Is.EqualTo("GRADE_A"));
            Assert.That(receipt.GuaranteeReasons.Values<string>(), Does.Contain("SERVER_PITY"));
            Assert.That(receipt.ResultDigest, Has.Length.EqualTo(64));
        }

        [Test]
        public void HttpGatewayMapsIdempotencyConflictWithoutRunningLocalRecruitment()
        {
            Guid operationId = Guid.Parse("019fb150-0000-7000-8000-000000000162");
            var command = new SpecialRecruitmentCommand(operationId, 7, "hash", "SPECIAL_STANDARD_TICKET", "TICKET");
            var gateway = new HttpRecruitmentGateway(
                catalog,
                new SystemUuidV7Provider(),
                new StubTransport(new ServerApiResponse(409, "{\"code\":\"IDEMPOTENCY_CONFLICT\"}")));

            RecruitmentDomainException error = Assert.ThrowsAsync<RecruitmentDomainException>(async () =>
                await gateway.RecruitAsync(command, State(), DateTimeOffset.UtcNow, CancellationToken.None));

            Assert.That(error.Code, Is.EqualTo("P16_IDEMPOTENCY_CONFLICT"));
        }

        private static JObject State() => new()
        {
            ["serverRevision"] = 3,
            ["premiumWalletCache"] = new JObject
            {
                ["specialRecruitTickets"] = 3,
                ["freePremium"] = 100,
                ["paidPremium"] = 0
            },
            ["pityCounters"] = new JArray(new JObject
            {
                ["pityGroupId"] = "PITY_GROUP_SPECIAL_STANDARD",
                ["pityRuleId"] = "PITY_S_PLUS",
                ["pullCount"] = 4,
                ["lastUpdatedContentVersion"] = "1.0.0-content.13"
            }),
            ["featuredGuarantees"] = new JArray()
        };

        private static string SuccessBody() => new JObject
        {
            ["data"] = new JObject
            {
                ["receiptId"] = "019fb150-0000-7000-8000-000000000199",
                ["replayed"] = false,
                ["contentVersion"] = "1.0.0-content.13",
                ["walletAfter"] = new JArray(
                    new JObject { ["currencyKey"] = "FREE_GEM", ["balance"] = 100, ["version"] = 0 },
                    new JObject { ["currencyKey"] = "PAID_GEM", ["balance"] = 0, ["version"] = 0 },
                    new JObject { ["currencyKey"] = "SPECIAL_TICKET", ["balance"] = 2, ["version"] = 1 }),
                ["pityAfter"] = new JArray(new JObject
                {
                    ["pityGroupKey"] = "PITY_GROUP_SPECIAL_STANDARD", ["pullCount"] = 5
                }),
                ["results"] = new JArray(new JObject
                {
                    ["resultNo"] = 1,
                    ["instanceId"] = "019fb150-0000-7000-8000-000000000198",
                    ["templateKey"] = "GEN_MERC_STANDARD_V1",
                    ["jobKey"] = "JOB_WARRIOR",
                    ["rarity"] = "A",
                    ["guaranteed"] = true
                })
            },
            ["traceId"] = "019fb150-0000-7000-8000-000000000197",
            ["serverTimeUtc"] = "2026-07-20T00:00:00Z"
        }.ToString(Newtonsoft.Json.Formatting.None);

        private sealed class StubTransport : IServerApiTransport
        {
            private readonly ServerApiResponse response;
            public StubTransport(ServerApiResponse response) => this.response = response;
            public string Path { get; private set; }
            public string IdempotencyKey { get; private set; }
            public string Body { get; private set; }

            public Task<ServerApiResponse> PostAsync(string path, string idempotencyKey, string body, CancellationToken cancellationToken)
            {
                Path = path;
                IdempotencyKey = idempotencyKey;
                Body = body;
                return Task.FromResult(response);
            }
        }
    }
}
