using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Combat
{
    public interface ICombatUnitOfWork
    {
        long Revision { get; }
        StartHuntResult StartHunt(StartHuntCommand command);
        RecallHuntResult RecallHunt(RecallHuntCommand command);
        HuntSnapshotDto GetSnapshot();
        void Tick();
    }

    public interface ICombatRequestHasher
    {
        string ComputeHash(StartHuntCommand command);
        string ComputeHash(RecallHuntCommand command);
    }

    public sealed class StartHuntCommand
    {
        public StartHuntCommand(Guid operationId, Guid huntOperationId, long expectedRevision, string requestHash, string regionId, IEnumerable<Guid> partyMercenaryInstanceIds)
        {
            OperationId = operationId;
            HuntOperationId = huntOperationId;
            ExpectedRevision = expectedRevision;
            RequestHash = requestHash;
            RegionId = regionId ?? throw new ArgumentNullException(nameof(regionId));
            PartyMercenaryInstanceIds = (partyMercenaryInstanceIds ?? throw new ArgumentNullException(nameof(partyMercenaryInstanceIds))).ToArray();
            if (PartyMercenaryInstanceIds.Count is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(partyMercenaryInstanceIds));
        }
        public Guid OperationId { get; }
        public Guid HuntOperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string RegionId { get; }
        public IReadOnlyList<Guid> PartyMercenaryInstanceIds { get; }
        public JObject ToHashJson() => new()
        {
            ["commandType"] = "START_HUNT",
            ["operationId"] = OperationId.ToString("D"),
            ["huntOperationId"] = HuntOperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision,
            ["regionId"] = RegionId,
            ["partyMercenaryInstanceIds"] = new JArray(PartyMercenaryInstanceIds.Select(value => value.ToString("D")))
        };
    }

    public sealed class RecallHuntCommand
    {
        public RecallHuntCommand(Guid operationId, long expectedRevision, string requestHash, string reasonCode = "PLAYER_RECALL")
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash; ReasonCode = reasonCode ?? throw new ArgumentNullException(nameof(reasonCode)); }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string ReasonCode { get; }
        public JObject ToHashJson() => new()
        {
            ["commandType"] = "RECALL_HUNT",
            ["operationId"] = OperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision,
            ["reasonCode"] = ReasonCode
        };
    }

    public sealed class StartHuntResult
    {
        public StartHuntResult(Guid operationId, Guid huntOperationId, long revisionBefore, long revisionAfter, string regionId, IReadOnlyList<string> party, string persistedState, bool replayed, string resultDigest)
        { OperationId = operationId; HuntOperationId = huntOperationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; RegionId = regionId; PartyMercenaryInstanceIds = party; PersistedState = persistedState; Replayed = replayed; ResultDigest = resultDigest; }
        public Guid OperationId { get; }
        public Guid HuntOperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public string RegionId { get; }
        public IReadOnlyList<string> PartyMercenaryInstanceIds { get; }
        public string PersistedState { get; }
        public bool Replayed { get; }
        public string ResultDigest { get; }
    }

    public sealed class RecallHuntResult
    {
        public RecallHuntResult(Guid operationId, long revisionBefore, long revisionAfter, string terminalState, int killCountDelta, IReadOnlyList<long> contributionDelta, IReadOnlyList<long> personalGoldDelta, bool replayed, string resultDigest)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; TerminalState = terminalState; KillCountDelta = killCountDelta; ContributionDelta = contributionDelta; PersonalGoldDelta = personalGoldDelta; Replayed = replayed; ResultDigest = resultDigest; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public string TerminalState { get; }
        public int KillCountDelta { get; }
        public IReadOnlyList<long> ContributionDelta { get; }
        public IReadOnlyList<long> PersonalGoldDelta { get; }
        public bool Replayed { get; }
        public string ResultDigest { get; }
    }

    public sealed class HuntMemberDto
    {
        public HuntMemberDto(string instanceId, string runtimeId, int currentHp, int maxHp, long damageDealt, bool downed)
        { InstanceId = instanceId; RuntimeId = runtimeId; CurrentHp = currentHp; MaxHp = maxHp; DamageDealt = damageDealt; Downed = downed; }
        public string InstanceId { get; }
        public string RuntimeId { get; }
        public int CurrentHp { get; }
        public int MaxHp { get; }
        public long DamageDealt { get; }
        public bool Downed { get; }
    }

    public sealed class HuntSnapshotDto
    {
        public HuntSnapshotDto(bool active, string regionId, long tick, string state, string reasonCode, int encounterIndex, int hostileCount, IReadOnlyList<HuntMemberDto> members)
        { Active = active; RegionId = regionId; Tick = tick; State = state; ReasonCode = reasonCode; EncounterIndex = encounterIndex; HostileCount = hostileCount; Members = members ?? Array.Empty<HuntMemberDto>(); }
        public bool Active { get; }
        public string RegionId { get; }
        public long Tick { get; }
        public string State { get; }
        public string ReasonCode { get; }
        public int EncounterIndex { get; }
        public int HostileCount { get; }
        public IReadOnlyList<HuntMemberDto> Members { get; }
    }
}
