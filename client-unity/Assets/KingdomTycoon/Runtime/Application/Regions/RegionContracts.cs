using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Regions
{
    public sealed class SetRegionAccessPolicyCommand
    {
        public SetRegionAccessPolicyCommand(Guid operationId, long expectedRevision, string requestHash, string regionId, bool allowed)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty; RegionId = regionId ?? throw new ArgumentNullException(nameof(regionId)); Allowed = allowed; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string RegionId { get; }
        public bool Allowed { get; }
        public JObject ToHashJson() => new()
        {
            ["commandType"] = "SET_REGION_ACCESS_POLICY", ["operationId"] = OperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision, ["regionId"] = RegionId, ["allowed"] = Allowed
        };
    }

    public sealed class RegionPolicyOperationResult
    {
        public RegionPolicyOperationResult(Guid operationId, long revisionBefore, long revisionAfter, bool replayed, string resultCode, string resultDigest)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; Replayed = replayed; ResultCode = resultCode; ResultDigest = resultDigest; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Replayed { get; }
        public string ResultCode { get; }
        public string ResultDigest { get; }
    }

    public sealed class RegionRequirementDto
    {
        public RegionRequirementDto(string code, string label, long current, long required, bool met)
        { Code = code; Label = label; Current = current; Required = required; Met = met; }
        public string Code { get; }
        public string Label { get; }
        public long Current { get; }
        public long Required { get; }
        public bool Met { get; }
    }

    public sealed class RegionMercenaryDto
    {
        public RegionMercenaryDto(string id, string name, string jobId, string rankId, int rankOrder, bool active, string autonomyState, string currentRegionId)
        { Id = id; Name = name; JobId = jobId; RankId = rankId; RankOrder = rankOrder; Active = active; AutonomyState = autonomyState; CurrentRegionId = currentRegionId; }
        public string Id { get; }
        public string Name { get; }
        public string JobId { get; }
        public string RankId { get; }
        public int RankOrder { get; }
        public bool Active { get; }
        public string AutonomyState { get; }
        public string CurrentRegionId { get; }
    }

    public sealed class RegionSummaryDto
    {
        public RegionSummaryDto(string id, string name, int order, int tier, string environmentTag, string minimumRankId, int minimumRankOrder,
            int recommendedPower, bool unlocked, bool allowed, int progressPercent, long huntCount, long eliteKillCount, string highestRankReachedId,
            int activeMercenaries, int recommendedPartySize, int recommendedPotions, int reserveSlots, string lockReason, IReadOnlyList<RegionRequirementDto> requirements)
        {
            Id = id; Name = name; Order = order; Tier = tier; EnvironmentTag = environmentTag; MinimumRankId = minimumRankId; MinimumRankOrder = minimumRankOrder;
            RecommendedPower = recommendedPower; Unlocked = unlocked; Allowed = allowed; ProgressPercent = progressPercent; HuntCount = huntCount;
            EliteKillCount = eliteKillCount; HighestRankReachedId = highestRankReachedId; ActiveMercenaries = activeMercenaries;
            RecommendedPartySize = recommendedPartySize; RecommendedPotions = recommendedPotions; ReserveSlots = reserveSlots; LockReason = lockReason; Requirements = requirements;
        }
        public string Id { get; }
        public string Name { get; }
        public int Order { get; }
        public int Tier { get; }
        public string EnvironmentTag { get; }
        public string MinimumRankId { get; }
        public int MinimumRankOrder { get; }
        public int RecommendedPower { get; }
        public bool Unlocked { get; }
        public bool Allowed { get; }
        public int ProgressPercent { get; }
        public long HuntCount { get; }
        public long EliteKillCount { get; }
        public string HighestRankReachedId { get; }
        public int ActiveMercenaries { get; }
        public int RecommendedPartySize { get; }
        public int RecommendedPotions { get; }
        public int ReserveSlots { get; }
        public string LockReason { get; }
        public IReadOnlyList<RegionRequirementDto> Requirements { get; }
    }

    public sealed class RegionOverviewDto
    {
        public RegionOverviewDto(long revision, string kingdomStageId, int unlockedCount, IReadOnlyList<RegionSummaryDto> regions,
            IReadOnlyList<RegionMercenaryDto> mercenaries, string lastResultCode)
        { Revision = revision; KingdomStageId = kingdomStageId; UnlockedCount = unlockedCount; Regions = regions; Mercenaries = mercenaries; LastResultCode = lastResultCode; }
        public long Revision { get; }
        public string KingdomStageId { get; }
        public int UnlockedCount { get; }
        public IReadOnlyList<RegionSummaryDto> Regions { get; }
        public IReadOnlyList<RegionMercenaryDto> Mercenaries { get; }
        public string LastResultCode { get; }
    }

    public interface IRegionDeploymentPolicy
    {
        void ValidateDeployment(JObject document, string regionId, IEnumerable<string> partyMercenaryInstanceIds);
        void ApplyHuntSettlement(JObject draft, string regionId, IEnumerable<string> partyMercenaryInstanceIds, int eliteKills, bool victory, DateTimeOffset now);
    }
}
