using System;
using System.Collections.Generic;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Recruitment
{
    public abstract class RecruitmentCommand
    {
        protected RecruitmentCommand(Guid operationId, long expectedRevision, string requestHash)
        {
            OperationId = operationId;
            ExpectedRevision = expectedRevision;
            RequestHash = requestHash;
        }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public abstract string OperationType { get; }
        public abstract JObject ToJson();
    }

    public sealed class RefreshTavernCommand : RecruitmentCommand
    {
        public RefreshTavernCommand(Guid operationId, long expectedRevision, string requestHash, bool free)
            : base(operationId, expectedRevision, requestHash) => Free = free;
        public bool Free { get; }
        public override string OperationType => "TAVERN_REFRESH";
        public override JObject ToJson() => BaseJson(this, new JObject { ["free"] = Free });
        internal static JObject BaseJson(RecruitmentCommand value, JObject body)
        {
            body["operationId"] = value.OperationId.ToString("D");
            body["expectedRevision"] = value.ExpectedRevision;
            body["operationType"] = value.OperationType;
            return body;
        }
    }

    public sealed class SetCandidateLockCommand : RecruitmentCommand
    {
        public SetCandidateLockCommand(Guid operationId, long expectedRevision, string requestHash, string candidateId, bool locked)
            : base(operationId, expectedRevision, requestHash) { CandidateId = candidateId; Locked = locked; }
        public string CandidateId { get; }
        public bool Locked { get; }
        public override string OperationType => "TAVERN_LOCK";
        public override JObject ToJson() => RefreshTavernCommand.BaseJson(this, new JObject { ["candidateId"] = CandidateId, ["locked"] = Locked });
    }

    public sealed class HireCandidateCommand : RecruitmentCommand
    {
        public HireCandidateCommand(Guid operationId, long expectedRevision, string requestHash, string candidateId)
            : base(operationId, expectedRevision, requestHash) => CandidateId = candidateId;
        public string CandidateId { get; }
        public override string OperationType => "TAVERN_HIRE";
        public override JObject ToJson() => RefreshTavernCommand.BaseJson(this, new JObject { ["candidateId"] = CandidateId });
    }

    public sealed class SpecialRecruitmentCommand : RecruitmentCommand
    {
        public SpecialRecruitmentCommand(Guid operationId, long expectedRevision, string requestHash, string poolId, string paymentType, int count = 1)
            : base(operationId, expectedRevision, requestHash) { PoolId = poolId; PaymentType = paymentType; Count = count; }
        public string PoolId { get; }
        public string PaymentType { get; }
        public int Count { get; }
        public override string OperationType => "SPECIAL_RECRUIT";
        public override JObject ToJson() => RefreshTavernCommand.BaseJson(this, new JObject { ["poolId"] = PoolId, ["paymentType"] = PaymentType, ["count"] = Count });
    }

    public sealed class RecruitmentRequestHasher
    {
        public string Compute(RecruitmentCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class RecruitmentCandidateDto
    {
        public RecruitmentCandidateDto(JObject value)
        {
            CandidateId = value.Value<string>("candidateId"); DisplayName = value.Value<string>("displayName");
            JobId = value.Value<string>("jobId"); GradeId = value.Value<string>("gradeId"); HireCost = value.Value<long>("hireCost"); Locked = value.Value<bool>("locked");
        }
        public string CandidateId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string GradeId { get; }
        public long HireCost { get; }
        public bool Locked { get; }
    }

    public sealed class RecruitmentOverviewDto
    {
        public RecruitmentOverviewDto(long revision, string authority, long kingdomGold, long tickets, long freePremium, int owned, int limit,
            DateTimeOffset? nextFreeRefreshAt, IReadOnlyList<RecruitmentCandidateDto> candidates, IReadOnlyDictionary<string, long> pity, int historyCount)
        {
            Revision = revision; Authority = authority; KingdomGold = kingdomGold; Tickets = tickets; FreePremium = freePremium;
            Owned = owned; Limit = limit; NextFreeRefreshAt = nextFreeRefreshAt; Candidates = candidates; Pity = pity; HistoryCount = historyCount;
        }
        public long Revision { get; }
        public string Authority { get; }
        public long KingdomGold { get; }
        public long Tickets { get; }
        public long FreePremium { get; }
        public int Owned { get; }
        public int Limit { get; }
        public DateTimeOffset? NextFreeRefreshAt { get; }
        public IReadOnlyList<RecruitmentCandidateDto> Candidates { get; }
        public IReadOnlyDictionary<string, long> Pity { get; }
        public int HistoryCount { get; }
    }

    public sealed class RecruitmentOperationResult
    {
        public RecruitmentOperationResult(Guid operationId, long revision, string resultCode, string instanceId, bool replayed)
        { OperationId = operationId; Revision = revision; ResultCode = resultCode; InstanceId = instanceId; Replayed = replayed; }
        public Guid OperationId { get; }
        public long Revision { get; }
        public string ResultCode { get; }
        public string InstanceId { get; }
        public bool Replayed { get; }
    }

    public sealed class SpecialRecruitmentReceipt
    {
        public SpecialRecruitmentReceipt(string receiptId, long serverRevision, JObject walletAfter, JArray pityAfter, JArray featuredAfter,
            JObject mercenarySnapshot, JArray guaranteeReasons, string resultDigest)
        {
            ReceiptId = receiptId; ServerRevision = serverRevision; WalletAfter = walletAfter; PityAfter = pityAfter;
            FeaturedAfter = featuredAfter; MercenarySnapshot = mercenarySnapshot; GuaranteeReasons = guaranteeReasons; ResultDigest = resultDigest;
        }
        public string ReceiptId { get; }
        public long ServerRevision { get; }
        public JObject WalletAfter { get; }
        public JArray PityAfter { get; }
        public JArray FeaturedAfter { get; }
        public JObject MercenarySnapshot { get; }
        public JArray GuaranteeReasons { get; }
        public string ResultDigest { get; }
    }

    public interface IRecruitmentGateway
    {
        string Authority { get; }
        bool IsOnline { get; }
        SpecialRecruitmentReceipt Recruit(SpecialRecruitmentCommand command, JObject recruitmentState, DateTimeOffset now);
    }
}
