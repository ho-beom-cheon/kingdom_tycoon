using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.OfflineTutorial
{
    public sealed class OfflineLineDto
    {
        public OfflineLineDto(string type, long quantity, string labelTextKey) { Type = type; Quantity = quantity; LabelTextKey = labelTextKey; }
        public string Type { get; }
        public long Quantity { get; }
        public string LabelTextKey { get; }
    }
    public sealed class TutorialStepDto
    {
        public TutorialStepDto(string id, int order, string actionType, string targetId, bool completed, bool current, bool skippable)
        { Id = id; Order = order; ActionType = actionType; TargetId = targetId; Completed = completed; Current = current; Skippable = skippable; }
        public string Id { get; }
        public int Order { get; }
        public string ActionType { get; }
        public string TargetId { get; }
        public bool Completed { get; }
        public bool Current { get; }
        public bool Skippable { get; }
    }

    public sealed class OfflineTutorialOverviewDto
    {
        public OfflineTutorialOverviewDto(long revision, string status, long elapsedSeconds, long eligibleSeconds,
            IReadOnlyList<OfflineLineDto> lines, IReadOnlyList<TutorialStepDto> steps, bool tutorialCompleted, bool tutorialSkipped)
        {
            Revision = revision; Status = status; ElapsedSeconds = elapsedSeconds; EligibleSeconds = eligibleSeconds;
            Lines = lines ?? Array.Empty<OfflineLineDto>(); Steps = steps ?? Array.Empty<TutorialStepDto>();
            TutorialCompleted = tutorialCompleted; TutorialSkipped = tutorialSkipped;
        }
        public long Revision { get; }
        public string Status { get; }
        public long ElapsedSeconds { get; }
        public long EligibleSeconds { get; }
        public IReadOnlyList<OfflineLineDto> Lines { get; }
        public IReadOnlyList<TutorialStepDto> Steps { get; }
        public bool TutorialCompleted { get; }
        public bool TutorialSkipped { get; }
        public int CompletedCount => Steps.Count(value => value.Completed);
        public int TotalCount => Steps.Count;
        public TutorialStepDto CurrentStep => Steps.FirstOrDefault(value => value.Current);
    }

    public sealed class TutorialCommand
    {
        public TutorialCommand(Guid operationId, long expectedRevision, string requestHash, string mode, string actionType, string targetId)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash; Mode = mode; ActionType = actionType; TargetId = targetId; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string Mode { get; }
        public string ActionType { get; }
        public string TargetId { get; }
        public JObject ToHashJson() => new()
        {
            ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mode"] = Mode, ["actionType"] = ActionType, ["targetId"] = TargetId
        };
    }

    public sealed class TutorialRequestHasher
    {
        public string Compute(TutorialCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class TutorialOperationResult
    {
        public TutorialOperationResult(Guid operationId, long revision, string resultCode, string stepId, bool replayed)
        { OperationId = operationId; Revision = revision; ResultCode = resultCode; StepId = stepId; Replayed = replayed; }
        public Guid OperationId { get; }
        public long Revision { get; }
        public string ResultCode { get; }
        public string StepId { get; }
        public bool Replayed { get; }
    }
}
