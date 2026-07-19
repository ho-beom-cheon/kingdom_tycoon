using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Raids
{
    public sealed class ResolveRaidCommand
    {
        public ResolveRaidCommand(Guid operationId, long expectedRevision, string requestHash, string raidId, string difficulty,
            IReadOnlyList<string> partyMercenaryInstanceIds, string targetPartId, bool warningsAccepted)
        {
            OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty;
            RaidId = raidId ?? throw new ArgumentNullException(nameof(raidId)); Difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            PartyMercenaryInstanceIds = partyMercenaryInstanceIds ?? throw new ArgumentNullException(nameof(partyMercenaryInstanceIds));
            TargetPartId = targetPartId ?? throw new ArgumentNullException(nameof(targetPartId)); WarningsAccepted = warningsAccepted;
        }
        public Guid OperationId { get; } public long ExpectedRevision { get; } public string RequestHash { get; }
        public string RaidId { get; } public string Difficulty { get; } public IReadOnlyList<string> PartyMercenaryInstanceIds { get; }
        public string TargetPartId { get; } public bool WarningsAccepted { get; }
        public JObject ToHashJson() => new()
        {
            ["commandType"] = "RAID_RESOLVE", ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["raidId"] = RaidId, ["difficulty"] = Difficulty, ["partyMercenaryInstanceIds"] = new JArray(PartyMercenaryInstanceIds),
            ["targetPartId"] = TargetPartId, ["warningsAccepted"] = WarningsAccepted
        };
    }

    public sealed class RaidPartyMemberDto
    {
        public RaidPartyMemberDto(string id, string name, string jobId, string rankId, bool eligible, int power, int potions)
        { Id = id; Name = name; JobId = jobId; RankId = rankId; Eligible = eligible; Power = power; Potions = potions; }
        public string Id { get; } public string Name { get; } public string JobId { get; } public string RankId { get; }
        public bool Eligible { get; } public int Power { get; } public int Potions { get; }
    }

    public sealed class RaidPartDto
    {
        public RaidPartDto(string id, string behaviorChange, long breakCount, bool available)
        { Id = id; BehaviorChange = behaviorChange; BreakCount = breakCount; Available = available; }
        public string Id { get; } public string BehaviorChange { get; } public long BreakCount { get; } public bool Available { get; }
    }

    public sealed class RaidSummaryDto
    {
        public RaidSummaryDto(string id, string difficulty, bool unlocked, int recommendedPower, int partyMin, int partyMax,
            int timeLimitSeconds, long clearCount, long? bestClearTimeMs, IReadOnlyList<RaidPartDto> parts)
        { Id = id; Difficulty = difficulty; Unlocked = unlocked; RecommendedPower = recommendedPower; PartyMin = partyMin; PartyMax = partyMax; TimeLimitSeconds = timeLimitSeconds; ClearCount = clearCount; BestClearTimeMs = bestClearTimeMs; Parts = parts; }
        public string Id { get; } public string Difficulty { get; } public bool Unlocked { get; } public int RecommendedPower { get; }
        public int PartyMin { get; } public int PartyMax { get; } public int TimeLimitSeconds { get; } public long ClearCount { get; }
        public long? BestClearTimeMs { get; } public IReadOnlyList<RaidPartDto> Parts { get; }
    }

    public sealed class RaidOverviewDto
    {
        public RaidOverviewDto(long revision, IReadOnlyList<RaidSummaryDto> raids, IReadOnlyList<RaidPartyMemberDto> partyCandidates, string lastResultCode, int historyCount)
        { Revision = revision; Raids = raids; PartyCandidates = partyCandidates; LastResultCode = lastResultCode; HistoryCount = historyCount; }
        public long Revision { get; } public IReadOnlyList<RaidSummaryDto> Raids { get; } public IReadOnlyList<RaidPartyMemberDto> PartyCandidates { get; }
        public string LastResultCode { get; } public int HistoryCount { get; }
    }

    public sealed class RaidOperationResult
    {
        public RaidOperationResult(Guid operationId, long revision, string resultCode, bool success, long durationMs, IReadOnlyList<string> brokenPartIds,
            IReadOnlyList<string> injuredMercenaryInstanceIds, IReadOnlyList<string> rewardLines, IReadOnlyList<string> traceLines, bool replayed)
        { OperationId = operationId; Revision = revision; ResultCode = resultCode; Success = success; DurationMs = durationMs; BrokenPartIds = brokenPartIds; InjuredMercenaryInstanceIds = injuredMercenaryInstanceIds; RewardLines = rewardLines; TraceLines = traceLines; Replayed = replayed; }
        public Guid OperationId { get; } public long Revision { get; } public string ResultCode { get; } public bool Success { get; }
        public long DurationMs { get; } public IReadOnlyList<string> BrokenPartIds { get; } public IReadOnlyList<string> InjuredMercenaryInstanceIds { get; }
        public IReadOnlyList<string> RewardLines { get; } public IReadOnlyList<string> TraceLines { get; } public bool Replayed { get; }
    }
}
