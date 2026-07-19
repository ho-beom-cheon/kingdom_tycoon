using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Recruitment;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Domain.Recruitment;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Facilities;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace KingdomTycoon.Infrastructure.Recruitment
{
    public sealed class ConfiguredRecruitmentGatewayFactory : IRecruitmentGatewayFactory
    {
        public IRecruitmentGateway Create(RecruitmentCatalog catalog, IUuidV7Provider ids)
        {
            string mode = Read("TYCOON_SERVER_MODE", "tycoon.server.mode", "MOCK_ONLY").ToUpperInvariant();
            if (mode == "MOCK_ONLY") return new DevelopmentRecruitmentGateway(catalog, ids);
            if (mode != "SERVER") throw new RecruitmentDomainException("P16_SERVER_MODE_INVALID");
            string baseUrl = Read("TYCOON_SERVER_BASE_URL", "tycoon.server.baseUrl", string.Empty);
            string token = Read("TYCOON_SERVER_ACCESS_TOKEN", "tycoon.server.accessToken", string.Empty);
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(token))
                throw new RecruitmentDomainException("P16_SERVER_CONFIG_MISSING");
            return new HttpRecruitmentGateway(catalog, ids, new UnityWebRequestServerApiTransport(baseUrl, token, 10));
        }

        private static string Read(string environmentKey, string playerPrefsKey, string fallback)
        {
            string environment = Environment.GetEnvironmentVariable(environmentKey);
            return !string.IsNullOrWhiteSpace(environment) ? environment : PlayerPrefs.GetString(playerPrefsKey, fallback);
        }
    }

    public interface IServerApiTransport
    {
        Task<ServerApiResponse> PostAsync(string path, string idempotencyKey, string body, CancellationToken cancellationToken);
    }

    public readonly struct ServerApiResponse
    {
        public ServerApiResponse(long statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }
        public long StatusCode { get; }
        public string Body { get; }
        public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;
    }

    public sealed class UnityWebRequestServerApiTransport : IServerApiTransport
    {
        private readonly string baseUrl;
        private readonly string accessToken;
        private readonly int timeoutSeconds;

        public UnityWebRequestServerApiTransport(string baseUrl, string accessToken, int timeoutSeconds)
        {
            this.baseUrl = (baseUrl ?? throw new ArgumentNullException(nameof(baseUrl))).TrimEnd('/');
            this.accessToken = string.IsNullOrWhiteSpace(accessToken) ? throw new ArgumentException("access token is required", nameof(accessToken)) : accessToken;
            this.timeoutSeconds = Math.Clamp(timeoutSeconds, 1, 30);
        }

        public async Task<ServerApiResponse> PostAsync(string path, string idempotencyKey, string body, CancellationToken cancellationToken)
        {
            using var request = new UnityWebRequest(baseUrl + "/" + path.TrimStart('/'), UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.SetRequestHeader("Idempotency-Key", idempotencyKey);
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }
            return new ServerApiResponse(request.responseCode, request.downloadHandler?.text);
        }
    }

    public sealed class HttpRecruitmentGateway : IRecruitmentGateway
    {
        private readonly RecruitmentCatalog catalog;
        private readonly RecruitmentMercenaryGenerator generator;
        private readonly IServerApiTransport transport;

        public HttpRecruitmentGateway(RecruitmentCatalog catalog, IUuidV7Provider ids, IServerApiTransport transport)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            generator = new RecruitmentMercenaryGenerator(catalog, ids ?? throw new ArgumentNullException(nameof(ids)));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public string Authority => "SERVER";
        public bool IsOnline => true;

        public async Task<SpecialRecruitmentReceipt> RecruitAsync(
            SpecialRecruitmentCommand command,
            JObject state,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var requestBody = new JObject
            {
                ["bannerKey"] = command.PoolId,
                ["pullCount"] = command.Count
            };
            ServerApiResponse response;
            try
            {
                response = await transport.PostAsync(
                    "api/v1/players/me/recruitments/special",
                    command.OperationId.ToString("D"),
                    requestBody.ToString(Newtonsoft.Json.Formatting.None),
                    cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception error) { throw new RecruitmentDomainException("P16_SERVER_UNAVAILABLE", error.Message); }

            JObject root;
            try { root = JObject.Parse(response.Body); }
            catch (Exception error) { throw new RecruitmentDomainException("P16_SERVER_RESPONSE_INVALID", error.Message); }
            if (!response.IsSuccess) throw MapError(root.Value<string>("code"));
            JObject data = root["data"] as JObject ?? throw new RecruitmentDomainException("P16_SERVER_RESPONSE_INVALID");
            JArray results = data["results"] as JArray;
            JObject result = results?.Children<JObject>().FirstOrDefault() ?? throw new RecruitmentDomainException("P16_SERVER_RESPONSE_INVALID");

            RecruitmentCatalog.Pool pool = catalog.GetPool(command.PoolId);
            string gradeId = "GRADE_" + result.Value<string>("rarity");
            string templateKey = result.Value<string>("templateKey");
            RecruitmentCatalog.PoolEntry entry = catalog.Entries(pool.Id).FirstOrDefault(value =>
                value.GenerationProfileId == templateKey && value.GradeId == gradeId)
                ?? catalog.Entries(pool.Id).FirstOrDefault(value => value.GradeId == gradeId)
                ?? throw new RecruitmentDomainException("P16_SERVER_RESULT_UNSUPPORTED");
            var random = new RecruitmentRandom(command.OperationId, data.Value<string>("contentVersion") ?? catalog.ContentVersion);
            JObject mercenary = generator.Special(
                entry, pool, result.Value<string>("jobKey"), Array.Empty<string>(), command.OperationId, random, now);
            mercenary["instanceId"] = result.Value<string>("instanceId");
            mercenary["gradeId"] = gradeId;

            JObject wallet = (JObject)state["premiumWalletCache"]!.DeepClone();
            foreach (JObject balance in data["walletAfter"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
            {
                string field = balance.Value<string>("currencyKey") switch
                {
                    "SPECIAL_TICKET" => "specialRecruitTickets",
                    "FREE_GEM" => "freePremium",
                    "PAID_GEM" => "paidPremium",
                    _ => null
                };
                if (field != null) wallet[field] = balance.Value<long>("balance");
            }
            JArray pity = (JArray)state["pityCounters"]!.DeepClone();
            foreach (JObject counter in data["pityAfter"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
            {
                string group = counter.Value<string>("pityGroupKey");
                long count = counter.Value<long>("pullCount");
                JObject[] matches = pity.Children<JObject>().Where(value => value.Value<string>("pityGroupId") == group).ToArray();
                if (matches.Length == 0) pity.Add(new JObject { ["pityGroupId"] = group, ["pityRuleId"] = "SERVER", ["pullCount"] = count, ["lastUpdatedContentVersion"] = data.Value<string>("contentVersion") });
                else foreach (JObject match in matches) { match["pullCount"] = count; match["lastUpdatedContentVersion"] = data.Value<string>("contentVersion"); }
            }
            JArray featured = (JArray)state["featuredGuarantees"]!.DeepClone();
            JArray guarantees = result.Value<bool>("guaranteed") ? new JArray("SERVER_PITY") : new JArray();
            JObject digestSource = new JObject
            {
                ["receiptId"] = data.Value<string>("receiptId"), ["walletAfter"] = wallet.DeepClone(),
                ["pityAfter"] = pity.DeepClone(), ["mercenary"] = mercenary.DeepClone(), ["guarantees"] = guarantees.DeepClone()
            };
            return new SpecialRecruitmentReceipt(
                data.Value<string>("receiptId"),
                (state.Value<long?>("serverRevision") ?? 0) + 1,
                wallet, pity, featured, mercenary, guarantees,
                Rfc8785Canonicalizer.ComputeSha256(digestSource));
        }

        private static RecruitmentDomainException MapError(string code) => code switch
        {
            "AUTH_INVALID" or "AUTH_REQUIRED" => new RecruitmentDomainException("P16_SESSION_INVALID"),
            "IDEMPOTENCY_CONFLICT" => new RecruitmentDomainException("P16_IDEMPOTENCY_CONFLICT"),
            "INSUFFICIENT_BALANCE" => new RecruitmentDomainException("P13_PREMIUM_BALANCE_INSUFFICIENT"),
            _ => new RecruitmentDomainException("P16_SERVER_UNAVAILABLE")
        };
    }
}
