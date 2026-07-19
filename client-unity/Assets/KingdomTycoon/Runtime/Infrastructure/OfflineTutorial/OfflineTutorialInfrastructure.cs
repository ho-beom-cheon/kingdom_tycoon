using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.OfflineTutorial;
using KingdomTycoon.Domain.OfflineTutorial;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.OfflineTutorial
{
    public sealed class P15OfflineTutorialCatalog
    {
        public sealed class OfflineRule
        {
            public OfflineRule(string type, long maxSeconds, int efficiencyBps) { Type = type; MaxSeconds = maxSeconds; EfficiencyBps = efficiencyBps; }
            public string Type { get; } public long MaxSeconds { get; } public int EfficiencyBps { get; }
        }
        public sealed class TutorialStep
        {
            public TutorialStep(string id, int order, string actionType, string targetId, string prerequisiteId, bool skippable)
            { Id = id; Order = order; ActionType = actionType; TargetId = targetId; PrerequisiteId = prerequisiteId; Skippable = skippable; }
            public string Id { get; } public int Order { get; } public string ActionType { get; } public string TargetId { get; } public string PrerequisiteId { get; } public bool Skippable { get; }
        }
        public sealed class TutorialGrant
        {
            public TutorialGrant(string id, string stepId, int lineNo, string rewardType, string rewardId, long quantity)
            { Id = id; StepId = stepId; LineNo = lineNo; RewardType = rewardType; RewardId = rewardId; Quantity = quantity; }
            public string Id { get; } public string StepId { get; } public int LineNo { get; } public string RewardType { get; } public string RewardId { get; } public long Quantity { get; }
        }

        private readonly Dictionary<string, long> config;
        private readonly Dictionary<string, OfflineRule> offline;
        private readonly Dictionary<string, TutorialGrant[]> grants;

        public P15OfflineTutorialCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion != CompileTimeActiveContentVersionProvider.P15ContentVersion)
                throw new OfflineTutorialDomainException("P15_CONTENT_VERSION_UNSUPPORTED");
            config = catalog.GetTable("runtime_config.csv").Rows.Where(row => Enabled(row) && row["config_key"].StartsWith("P15_", StringComparison.Ordinal)).ToDictionary(row => row["config_key"], row => long.Parse(row["value"], CultureInfo.InvariantCulture), StringComparer.Ordinal);
            offline = catalog.GetTable("offline_reward_rules.csv").Rows.Where(Enabled).ToDictionary(row => row["settlement_type"], row =>
                new OfflineRule(row["settlement_type"], Long(row, "max_seconds"), DecimalBps(row, "efficiency")), StringComparer.Ordinal);
            Steps = catalog.GetTable("tutorial_steps.csv").Rows.Where(Enabled).Select(row => new TutorialStep(
                row["tutorial_step_id"], Int(row, "order"), row["action_type"], row["target_id"], Empty(row["prerequisite_step_id"]), Bool(row, "skippable")))
                .OrderBy(value => value.Order).ToArray();
            grants = catalog.GetTable("tutorial_grants.csv").Rows.Where(Enabled).Select(row => new TutorialGrant(
                row["grant_id"], row["tutorial_step_id"], Int(row, "line_no"), row["reward_type"], row["reward_id"], Long(row, "quantity")))
                .GroupBy(value => value.StepId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.OrderBy(value => value.LineNo).ToArray(), StringComparer.Ordinal);
            if (Steps.Count != 10 || offline.Count != 6) throw new OfflineTutorialDomainException("P15_CONTENT_INVALID");
        }

        public IReadOnlyList<TutorialStep> Steps { get; }
        public long MinimumSeconds => Config("P15_OFFLINE_MIN_SECONDS");
        public long MaximumSeconds => Config("P15_OFFLINE_MAX_SECONDS");
        public long RollbackToleranceSeconds => Config("P15_CLOCK_ROLLBACK_TOLERANCE_SECONDS");
        public int HistoryLimit => checked((int)Config("P15_OFFLINE_HISTORY_MAX_ENTRIES"));
        public OfflineRule Rule(string type) => offline.TryGetValue(type, out OfflineRule value) ? value : throw new OfflineTutorialDomainException("P15_CONTENT_INVALID");
        public IReadOnlyList<TutorialGrant> Grants(string stepId) => grants.GetValueOrDefault(stepId) ?? Array.Empty<TutorialGrant>();
        private long Config(string key) => config.TryGetValue(key, out long value) ? value : throw new OfflineTutorialDomainException("P15_CONTENT_INVALID");
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static bool Bool(IReadOnlyDictionary<string, string> row, string key) => row[key] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], CultureInfo.InvariantCulture);
        private static int DecimalBps(IReadOnlyDictionary<string, string> row, string key) => checked((int)(decimal.Parse(row[key], CultureInfo.InvariantCulture) * 10000m));
        private static string Empty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }

    public sealed class OfflineTutorialGameService : IAppService
    {
        private static readonly string[] SettlementOrder = { "HUNT", "POTION_CONSUMPTION", "FACILITY", "NPC_PROFICIENCY", "INJURY_RECOVERY", "PROMOTION_REVIEW" };
        private static readonly IReadOnlyDictionary<string, string> LineKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["HUNT"] = "TXT_P15_HUNT_LINE", ["POTION_CONSUMPTION"] = "TXT_P15_POTION_LINE", ["FACILITY"] = "TXT_P15_FACILITY_LINE",
            ["NPC_PROFICIENCY"] = "TXT_P15_NPC_LINE", ["INJURY_RECOVERY"] = "TXT_P15_INJURY_LINE", ["PROMOTION_REVIEW"] = "TXT_P15_PROMOTION_LINE"
        };

        private readonly ITrustedUtcClock clock;
        private readonly SemaphoreSlim gate = new(1, 1);
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private P15OfflineTutorialCatalog catalog;

        public OfflineTutorialGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 145;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || game.CurrentDocument.Value<string>("contentVersion") != CompileTimeActiveContentVersionProvider.P15ContentVersion)
                throw new OfflineTutorialDomainException("P15_CONTENT_VERSION_UNSUPPORTED");
            catalog = new P15OfflineTutorialCatalog(content.Catalog); IsBootstrapped = true;
            SettleOffline();
        }

        public OfflineTutorialOverviewDto GetOverview()
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject state = (JObject)document["payload"]!["offline"]!;
            JObject tutorial = (JObject)document["payload"]!["tutorial"]!; var completed = new HashSet<string>(tutorial["completedStepIds"]!.Values<string>(), StringComparer.Ordinal);
            string current = tutorial.Value<string>("currentStepId"); JObject last = state["history"]!.Children<JObject>().LastOrDefault();
            var lines = (last?["lines"]?.Children<JObject>() ?? Enumerable.Empty<JObject>()).Select(value => new OfflineLineDto(
                value.Value<string>("type"), value.Value<long>("quantity"), value.Value<string>("labelTextKey"))).ToArray();
            var steps = catalog.Steps.Select(value => new TutorialStepDto(value.Id, value.Order, value.ActionType, value.TargetId,
                completed.Contains(value.Id), value.Id == current, value.Skippable)).ToArray();
            return new OfflineTutorialOverviewDto(game.Revision, state.Value<string>("lastStatus"), state.Value<long>("lastElapsedSeconds"),
                state.Value<long>("lastEligibleSeconds"), lines, steps, tutorial["completedAtUtc"]!.Type != JTokenType.Null, tutorial.Value<bool>("skipped"));
        }

        public void SettleOffline()
        {
            EnsureReady(); gate.Wait();
            try
            {
                JObject source = game.Snapshot(); JObject state = (JObject)source["payload"]!["offline"]!; DateTimeOffset now = clock.UtcNow;
                DateTimeOffset cursor = ParseUtc(state.Value<string>("accrualCursorUtc"));
                OfflineWindow window = OfflineWindowPolicy.Evaluate(cursor, now, catalog.MinimumSeconds, catalog.MaximumSeconds, catalog.RollbackToleranceSeconds);
                if (window.Status == "BELOW_MINIMUM") return;
                string settlementId = Rfc8785Canonicalizer.ComputeSha256(new JObject
                {
                    ["profileId"] = game.ActiveProfileId, ["cursorUtc"] = Format(cursor), ["nowUtc"] = Format(now),
                    ["contentVersion"] = CompileTimeActiveContentVersionProvider.P15ContentVersion
                });
                if (state["history"]!.Children<JObject>().Any(value => value.Value<string>("settlementId") == settlementId)) return;
                JObject draft = (JObject)source.DeepClone(); JObject draftState = (JObject)draft["payload"]!["offline"]!;
                JArray lines = window.Status is "APPLIED" or "CAPPED" ? ApplySettlement(draft, window.EligibleSeconds, now) : new JArray();
                JObject history = new()
                {
                    ["settlementId"] = settlementId, ["startUtc"] = Format(cursor), ["endUtc"] = Format(now),
                    ["elapsedSeconds"] = window.ElapsedSeconds, ["eligibleSeconds"] = window.EligibleSeconds,
                    ["status"] = window.Status, ["lines"] = lines
                };
                history["digest"] = Rfc8785Canonicalizer.ComputeSha256(history);
                JArray values = (JArray)draftState["history"]!; while (values.Count >= catalog.HistoryLimit) values.RemoveAt(0); values.Add(history);
                draftState["lastSettlementId"] = settlementId; draftState["lastStatus"] = window.Status;
                draftState["lastElapsedSeconds"] = window.ElapsedSeconds; draftState["lastEligibleSeconds"] = window.EligibleSeconds;
                if (window.Status != "CLOCK_ROLLBACK") draftState["accrualCursorUtc"] = Format(now);
                draftState["lastTrustedUtc"] = Format(now > ParseUtc(draftState.Value<string>("lastTrustedUtc")) ? now : ParseUtc(draftState.Value<string>("lastTrustedUtc")));
                AppendJournal(draft, Guid.Parse(UuidV7.NewString(now)), "OFFLINE_SETTLEMENT", Rfc8785Canonicalizer.ComputeSha256(history), history, now);
                Commit(draft, now); Changed?.Invoke(this, EventArgs.Empty);
            }
            finally { gate.Release(); }
        }

        public TutorialCommand CreateCurrentActionCommand(string mode = "COMPLETE")
        {
            OfflineTutorialOverviewDto overview = GetOverview(); TutorialStepDto step = overview.CurrentStep ?? throw new OfflineTutorialDomainException("P15_TUTORIAL_COMPLETE");
            var draft = new TutorialCommand(Guid.Parse(UuidV7.NewString(clock.UtcNow)), game.Revision, null, mode, step.ActionType, step.TargetId);
            return new TutorialCommand(draft.OperationId, draft.ExpectedRevision, new TutorialRequestHasher().Compute(draft), mode, step.ActionType, step.TargetId);
        }

        public TutorialCommand CreateSkipAllCommand()
        {
            TutorialStepDto step = GetOverview().CurrentStep ?? throw new OfflineTutorialDomainException("P15_TUTORIAL_COMPLETE");
            var draft = new TutorialCommand(Guid.Parse(UuidV7.NewString(clock.UtcNow)), game.Revision, null, "SKIP_ALL", step.ActionType, step.TargetId);
            return new TutorialCommand(draft.OperationId, draft.ExpectedRevision, new TutorialRequestHasher().Compute(draft), draft.Mode, draft.ActionType, draft.TargetId);
        }

        public TutorialOperationResult Execute(TutorialCommand command)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command));
            string computed = new TutorialRequestHasher().Compute(new TutorialCommand(command.OperationId, command.ExpectedRevision, null, command.Mode, command.ActionType, command.TargetId));
            Require(FixedTimeEquals(computed, command.RequestHash), "P15_OPERATION_HASH_MISMATCH"); gate.Wait();
            try
            {
                JObject source = game.Snapshot(); JObject old = FindJournal(source, command.OperationId);
                if (old != null)
                {
                    Require(FixedTimeEquals(old.Value<string>("requestHash"), command.RequestHash), "P15_OPERATION_HASH_MISMATCH");
                    JObject payload = (JObject)old["resultPayload"]!;
                    return new TutorialOperationResult(command.OperationId, game.Revision, payload.Value<string>("resultCode"), payload.Value<string>("stepId"), true);
                }
                Require(command.ExpectedRevision == game.Revision, "P15_SAVE_REVISION_CONFLICT"); JObject draft = (JObject)source.DeepClone();
                JObject tutorial = (JObject)draft["payload"]!["tutorial"]!; string currentId = tutorial.Value<string>("currentStepId");
                P15OfflineTutorialCatalog.TutorialStep current = catalog.Steps.SingleOrDefault(value => value.Id == currentId) ?? throw new OfflineTutorialDomainException("P15_TUTORIAL_COMPLETE");
                Require(command.ActionType == current.ActionType, "P15_TUTORIAL_ACTION_MISMATCH"); Require(command.TargetId == current.TargetId, "P15_TUTORIAL_TARGET_MISMATCH");
                bool skipAll = command.Mode == "SKIP_ALL"; Require(command.Mode is "COMPLETE" or "SKIP" or "SKIP_ALL", "P15_TUTORIAL_MODE_INVALID");
                if (command.Mode == "SKIP") Require(current.Skippable, "P15_TUTORIAL_STEP_NOT_SKIPPABLE");
                DateTimeOffset now = clock.UtcNow; var applied = new List<P15OfflineTutorialCatalog.TutorialStep>();
                if (skipAll) applied.AddRange(catalog.Steps.Where(value => value.Order >= current.Order)); else applied.Add(current);
                foreach (P15OfflineTutorialCatalog.TutorialStep step in applied)
                {
                    AddUnique((JArray)tutorial["completedStepIds"]!, step.Id); ApplyGrants(draft, tutorial, step.Id);
                }
                tutorial["completedStepIds"] = new JArray(tutorial["completedStepIds"]!.Values<string>().OrderBy(value => value, StringComparer.Ordinal));
                int nextOrder = skipAll ? int.MaxValue : current.Order + 1; P15OfflineTutorialCatalog.TutorialStep next = catalog.Steps.FirstOrDefault(value => value.Order == nextOrder);
                tutorial["currentStepId"] = next == null ? JValue.CreateNull() : next.Id; tutorial["skipped"] = tutorial.Value<bool>("skipped") || command.Mode != "COMPLETE";
                tutorial["startedAtUtc"] = tutorial["startedAtUtc"]!.Type == JTokenType.Null ? Format(now) : tutorial["startedAtUtc"]!.DeepClone();
                tutorial["lastAdvancedAtUtc"] = Format(now); tutorial["completedAtUtc"] = next == null ? Format(now) : JValue.CreateNull(); tutorial["lastOperationId"] = command.OperationId.ToString("D");
                JArray receipts = (JArray)tutorial["actionReceipts"]!; while (receipts.Count >= 32) receipts.RemoveAt(0);
                receipts.Add(new JObject { ["operationId"] = command.OperationId.ToString("D"), ["stepId"] = current.Id, ["actionType"] = command.ActionType,
                    ["targetId"] = command.TargetId, ["requestHash"] = command.RequestHash, ["appliedAtUtc"] = Format(now), ["result"] = command.Mode == "COMPLETE" ? "COMPLETED" : "SKIPPED" });
                JObject result = new() { ["operationId"] = command.OperationId.ToString("D"), ["resultCode"] = next == null ? "P15_TUTORIAL_COMPLETED" : command.Mode == "COMPLETE" ? "P15_TUTORIAL_STEP_COMPLETED" : "P15_TUTORIAL_STEP_SKIPPED", ["stepId"] = current.Id, ["nextStepId"] = next == null ? JValue.CreateNull() : next.Id };
                string digest = Rfc8785Canonicalizer.ComputeSha256(result); AppendJournal(draft, command.OperationId, "TUTORIAL_COMMAND", command.RequestHash, result, now, digest);
                Commit(draft, now); Changed?.Invoke(this, EventArgs.Empty);
                return new TutorialOperationResult(command.OperationId, game.Revision, result.Value<string>("resultCode"), current.Id, false);
            }
            finally { gate.Release(); }
        }

        private JArray ApplySettlement(JObject draft, long eligibleSeconds, DateTimeOffset now)
        {
            var quantities = SettlementOrder.ToDictionary(value => value, _ => 0L, StringComparer.Ordinal);
            JArray mercenaries = (JArray)draft["payload"]!["mercenaries"]!;
            JObject[] hunting = mercenaries.Children<JObject>().Where(value => value.Value<bool>("active") && IsHuntingState(value["autonomy"]!.Value<string>("state"))).ToArray();
            long huntSeconds = RuleSeconds("HUNT", eligibleSeconds); long huntMinutes = huntSeconds / 60;
            foreach (JObject member in hunting)
            {
                long exp = checked(huntMinutes * 6); long gold = checked(huntMinutes * 3); member["exp"] = checked(member.Value<long>("exp") + exp); member["personalGold"] = checked(member.Value<long>("personalGold") + gold); quantities["HUNT"] = checked(quantities["HUNT"] + exp + gold);
            }
            long materials = hunting.Length == 0 ? 0 : huntSeconds / 300; if (materials > 0) { AddItem(draft, "MAT_R01_WILD_HERB", materials); quantities["HUNT"] = checked(quantities["HUNT"] + materials); }
            long potionSeconds = RuleSeconds("POTION_CONSUMPTION", eligibleSeconds); long requestedPotions = checked(hunting.Length * (potionSeconds / 1800));
            quantities["POTION_CONSUMPTION"] = ConsumePotions(hunting, requestedPotions);
            long facilitySeconds = RuleSeconds("FACILITY", eligibleSeconds); JObject production = (JObject)draft["payload"]!["production"]!; long ticks = checked(facilitySeconds * 10); production["currentTick"] = checked(production.Value<long>("currentTick") + ticks); quantities["FACILITY"] = ticks;
            long npcSeconds = RuleSeconds("NPC_PROFICIENCY", eligibleSeconds); foreach (JObject npc in draft["payload"]!["managementNpcs"]!.Children<JObject>().Where(value => value.Value<bool>("working"))) { long xp = npcSeconds / 300; npc["proficiencyExp"] = checked(npc.Value<long>("proficiencyExp") + xp); quantities["NPC_PROFICIENCY"] += xp; }
            if (RuleSeconds("INJURY_RECOVERY", eligibleSeconds) >= 1800) foreach (JObject member in mercenaries.Children<JObject>().Where(value => value["autonomy"]!.Value<string>("state") == "INJURED")) { JObject autonomy = (JObject)member["autonomy"]!; autonomy["state"] = "IDLE_TOWN"; autonomy["reasonCode"] = "NONE"; autonomy["stateStartedAtUtc"] = Format(now); autonomy["nextDecisionAtUtc"] = Format(now); quantities["INJURY_RECOVERY"]++; }
            foreach (JObject member in mercenaries.Children<JObject>()) { JObject promotion = (JObject)member["promotion"]!; if (promotion.Value<string>("status") == "IN_REVIEW" && ParseUtc(promotion.Value<string>("finishesAtUtc")) <= now) { promotion["status"] = "COMPLETED_PENDING_APPLY"; quantities["PROMOTION_REVIEW"]++; } }
            return new JArray(SettlementOrder.Select(type => new JObject { ["type"] = type, ["quantity"] = quantities[type], ["labelTextKey"] = LineKeys[type] }));
        }

        private long RuleSeconds(string type, long eligible) { P15OfflineTutorialCatalog.OfflineRule rule = catalog.Rule(type); return OfflineWindowPolicy.Efficient(Math.Min(eligible, rule.MaxSeconds), rule.EfficiencyBps); }
        private static bool IsHuntingState(string state) => state is "PREPARE" or "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION";
        private static long ConsumePotions(IEnumerable<JObject> members, long requested)
        {
            long remaining = requested, consumed = 0; foreach (JObject member in members)
            {
                JArray stacks = (JArray)member["potions"]!; foreach (JObject stack in stacks.Children<JObject>().Where(value => value.Value<string>("potionId") == "POT_HEAL_SMALL").ToArray())
                { long amount = Math.Min(remaining, stack.Value<long>("quantity")); if (amount <= 0) continue; long left = stack.Value<long>("quantity") - amount; if (left == 0) stack.Remove(); else stack["quantity"] = left; consumed += amount; remaining -= amount; if (remaining == 0) return consumed; }
            }
            return consumed;
        }

        private void ApplyGrants(JObject document, JObject tutorial, string stepId)
        {
            JArray granted = (JArray)tutorial["grantedRewardIds"]!;
            foreach (IGrouping<string, P15OfflineTutorialCatalog.TutorialGrant> group in catalog.Grants(stepId).GroupBy(value => value.Id, StringComparer.Ordinal))
            {
                if (granted.Values<string>().Contains(group.Key, StringComparer.Ordinal)) continue;
                foreach (P15OfflineTutorialCatalog.TutorialGrant value in group)
                {
                    switch (value.RewardType)
                    {
                        case "CURRENCY" when value.RewardId == "KINGDOM_GOLD": document["payload"]!["kingdom"]!["kingdomGold"] = checked(document["payload"]!["kingdom"]!.Value<long>("kingdomGold") + value.Quantity); break;
                        case "ITEM": AddItem(document, value.RewardId, value.Quantity); break;
                        case "PROGRESSION_FLAG": AddUnique((JArray)document["payload"]!["kingdom"]!["progressionFlagIds"]!, value.RewardId); break;
                        default: throw new OfflineTutorialDomainException("P15_TUTORIAL_GRANT_INVALID");
                    }
                }
                granted.Add(group.Key);
            }
            tutorial["grantedRewardIds"] = new JArray(granted.Values<string>().OrderBy(value => value, StringComparer.Ordinal));
        }

        private void Commit(JObject draft, DateTimeOffset now)
        {
            SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, now);
            if (!written.Success) throw new OfflineTutorialDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P15_SAVE_REVISION_CONFLICT" : "P15_SAVE_WRITE_FAILED", written.ErrorCode + ": " + (written.Report == null ? string.Empty : string.Join(" | ", written.Report.Issues)));
            game.SynchronizeCommittedDocument(written.Document);
        }

        private static void AppendJournal(JObject draft, Guid operationId, string operationType, string requestHash, JObject payload, DateTimeOffset now, string digest = null)
        {
            string timestamp = Format(now); digest ??= Rfc8785Canonicalizer.ComputeSha256(payload);
            ((JArray)draft["payload"]!["operationJournal"]!).Add(new JObject { ["operationId"] = operationId.ToString("D"), ["operationType"] = operationType, ["facilityJobType"] = null, ["requestHash"] = requestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp, ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest, ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = payload.DeepClone() });
        }
        private static JObject FindJournal(JObject document, Guid id) => document["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == id.ToString("D"));
        private static void AddItem(JObject document, string id, long quantity) { if (quantity <= 0) return; JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!; JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == id); if (stack == null) stacks.Add(new JObject { ["itemId"] = id, ["quantity"] = quantity }); else stack["quantity"] = checked(stack.Value<long>("quantity") + quantity); document["payload"]!["inventory"]!["itemStacks"] = new JArray(stacks.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal)); }
        private static void AddUnique(JArray values, string id) { if (!values.Values<string>().Contains(id, StringComparer.Ordinal)) values.Add(id); }
        private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static bool FixedTimeEquals(string left, string right) { if (left == null || right == null || left.Length != right.Length) return false; int difference = 0; for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0; }
        private static void Require(bool condition, string code) { if (!condition) throw new OfflineTutorialDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped) throw new OfflineTutorialDomainException("P15_NOT_BOOTSTRAPPED"); }
        public void Shutdown() { Changed = null; IsBootstrapped = false; catalog = null; game = null; content = null; save = null; gate.Dispose(); }
    }
}
