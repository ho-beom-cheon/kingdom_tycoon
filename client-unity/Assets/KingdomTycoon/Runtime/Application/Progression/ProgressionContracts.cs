using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Progression
{
    public abstract class ProgressionCommand
    {
        protected ProgressionCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryInstanceId)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty; MercenaryInstanceId = mercenaryInstanceId; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string MercenaryInstanceId { get; }
        public abstract string CommandType { get; }
        public JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision, ["mercenaryInstanceId"] = MercenaryInstanceId
        };
    }

    public sealed class StartPromotionReviewCommand : ProgressionCommand
    {
        public StartPromotionReviewCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryInstanceId)
            : base(operationId, expectedRevision, requestHash, mercenaryInstanceId) { }
        public override string CommandType => "START_PROMOTION_REVIEW";
    }

    public sealed class ApplyPromotionCommand : ProgressionCommand
    {
        public ApplyPromotionCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryInstanceId)
            : base(operationId, expectedRevision, requestHash, mercenaryInstanceId) { }
        public override string CommandType => "APPLY_PROMOTION";
    }

    public sealed class PromotionOperationResult
    {
        public PromotionOperationResult(Guid operationId, long revisionBefore, long revisionAfter, bool replayed, string resultCode, string resultDigest, JObject payload)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; Replayed = replayed; ResultCode = resultCode; ResultDigest = resultDigest; Payload = payload; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Replayed { get; }
        public string ResultCode { get; }
        public string ResultDigest { get; }
        public JObject Payload { get; }
    }

    public sealed class PromotionRequirementDto
    {
        public PromotionRequirementDto(string label, long current, long required, bool met)
        { Label = label; Current = current; Required = required; Met = met; }
        public string Label { get; }
        public long Current { get; }
        public long Required { get; }
        public bool Met { get; }
    }

    public sealed class PromotionMercenaryDto
    {
        public PromotionMercenaryDto(string id, string name, string gradeId, string rankId, string nextRankId, int level, int maxLevel,
            long experience, long experienceToNext, long personalGold, long personalGoldCost, long kingdomGoldCost, string status,
            long secondsRemaining, long equipmentScore, long recommendedEquipmentScore, IReadOnlyList<PromotionRequirementDto> requirements)
        {
            Id = id; Name = name; GradeId = gradeId; RankId = rankId; NextRankId = nextRankId; Level = level; MaxLevel = maxLevel;
            Experience = experience; ExperienceToNext = experienceToNext; PersonalGold = personalGold; PersonalGoldCost = personalGoldCost;
            KingdomGoldCost = kingdomGoldCost; Status = status; SecondsRemaining = secondsRemaining; EquipmentScore = equipmentScore;
            RecommendedEquipmentScore = recommendedEquipmentScore; Requirements = requirements;
        }
        public string Id { get; }
        public string Name { get; }
        public string GradeId { get; }
        public string RankId { get; }
        public string NextRankId { get; }
        public int Level { get; }
        public int MaxLevel { get; }
        public long Experience { get; }
        public long ExperienceToNext { get; }
        public long PersonalGold { get; }
        public long PersonalGoldCost { get; }
        public long KingdomGoldCost { get; }
        public string Status { get; }
        public long SecondsRemaining { get; }
        public long EquipmentScore { get; }
        public long RecommendedEquipmentScore { get; }
        public IReadOnlyList<PromotionRequirementDto> Requirements { get; }
    }

    public sealed class ProgressionOverviewDto
    {
        public ProgressionOverviewDto(long revision, int guildLevel, string guildState, long kingdomGold, IReadOnlyList<PromotionMercenaryDto> mercenaries, string lastResult)
        { Revision = revision; GuildLevel = guildLevel; GuildState = guildState; KingdomGold = kingdomGold; Mercenaries = mercenaries; LastResult = lastResult; }
        public long Revision { get; }
        public int GuildLevel { get; }
        public string GuildState { get; }
        public long KingdomGold { get; }
        public IReadOnlyList<PromotionMercenaryDto> Mercenaries { get; }
        public string LastResult { get; }
    }
}
