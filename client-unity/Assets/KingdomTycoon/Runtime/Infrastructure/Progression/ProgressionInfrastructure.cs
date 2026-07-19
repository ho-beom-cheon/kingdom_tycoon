using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Progression;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Domain.Progression;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Inventory;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Progression
{
    public sealed class ProgressionRequestHasher
    {
        public string Compute(ProgressionCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class P11ProgressionCatalog
    {
        public sealed class RankRule
        {
            public string Id; public int Order; public int MaxLevel; public string NextId; public string TokenId;
            public long PersonalGold; public long Contribution;
        }
        public sealed class ReviewRule
        {
            public string From; public string To; public int GuildLevel; public long KingdomGold; public int Seconds; public long RecommendedScore;
        }
        public sealed class RecordRule
        {
            public string From; public string Type; public string Subject; public long Required;
        }
        public sealed class SupplyRule
        {
            public string Id; public string From; public string ItemId; public long Quantity;
        }
        public sealed class CostItem
        {
            public CostItem(string id, long quantity) { Id = id; Quantity = quantity; }
            public string Id { get; }
            public long Quantity { get; }
        }

        private readonly Dictionary<string, RankRule> ranks;
        private readonly Dictionary<string, int> gradeMultiplierBps;
        private readonly Dictionary<string, ReviewRule> reviews;
        private readonly Dictionary<string, RecordRule> records;
        private readonly Dictionary<string, SupplyRule> supplies;
        private readonly Dictionary<string, long> curves;
        private readonly Dictionary<string, List<CostItem>> gradeItems;

        public P11ProgressionCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
                throw new ProgressionDomainException("P11_CONTENT_VERSION_UNSUPPORTED");
            ranks = catalog.GetTable("mercenary_ranks.csv").Rows.Where(Enabled).Select(row => new RankRule
            {
                Id = row["rank_id"], Order = Int(row, "order"), MaxLevel = Int(row, "max_level"),
                NextId = Null(row["promotion_to"]), TokenId = Null(row["promotion_token_id"]),
                PersonalGold = Long(row, "base_personal_gold"), Contribution = Long(row, "contribution_required")
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            gradeMultiplierBps = catalog.GetTable("mercenary_grades.csv").Rows.Where(Enabled).ToDictionary(
                row => row["grade_id"], row => DecimalBps(row, "promotion_cost_multiplier"), StringComparer.Ordinal);
            reviews = catalog.GetTable("promotion_review_rules.csv").Rows.Where(Enabled).Select(row => new ReviewRule
            {
                From = row["from_rank_id"], To = row["to_rank_id"], GuildLevel = Int(row, "guild_level_required"),
                KingdomGold = Long(row, "base_kingdom_gold"), Seconds = Int(row, "review_seconds"), RecommendedScore = Long(row, "recommended_equipment_score")
            }).ToDictionary(value => value.From, StringComparer.Ordinal);
            records = catalog.GetTable("promotion_record_requirements.csv").Rows.Where(Enabled).Select(row => new RecordRule
            {
                From = row["from_rank_id"], Type = row["record_type"], Subject = Null(row["subject_id"]), Required = Long(row, "required_count")
            }).ToDictionary(value => value.From, StringComparer.Ordinal);
            supplies = catalog.GetTable("promotion_supply_rules.csv").Rows.Where(Enabled).Select(row => new SupplyRule
            {
                Id = row["supply_rule_id"], From = row["from_rank_id"], ItemId = row["item_id"], Quantity = Long(row, "quantity")
            }).ToDictionary(value => value.From, StringComparer.Ordinal);
            curves = catalog.GetTable("mercenary_level_curves.csv").Rows.Where(Enabled).ToDictionary(
                row => row["rank_id"] + "\u001f" + row["level"], row => Long(row, "xp_to_next"), StringComparer.Ordinal);
            gradeItems = catalog.GetTable("promotion_grade_requirements.csv").Rows.Where(Enabled).GroupBy(
                row => Key(row["grade_id"], row["from_rank_id"], row["to_rank_id"]), StringComparer.Ordinal).ToDictionary(
                group => group.Key, group => group.Select(row => new CostItem(row["item_id"], Long(row, "item_quantity"))).OrderBy(value => value.Id, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
            if (ranks.Count != 6 || gradeMultiplierBps.Count != 5 || reviews.Count != 5 || records.Count != 5 || supplies.Count != 5 || curves.Count != 270)
                throw new ProgressionDomainException("P11_CONTENT_INVALID");
            foreach (ReviewRule review in reviews.Values)
                if (Rank(review.From).NextId != review.To) throw new ProgressionDomainException("P11_CONTENT_INVALID");
        }

        public RankRule Rank(string id) => ranks.TryGetValue(id ?? string.Empty, out RankRule value) ? value : throw new ProgressionDomainException("P11_CONTENT_INVALID");
        public ReviewRule Review(string rankId) => reviews.TryGetValue(rankId ?? string.Empty, out ReviewRule value) ? value : null;
        public RecordRule Record(string rankId) => records.TryGetValue(rankId ?? string.Empty, out RecordRule value) ? value : null;
        public SupplyRule Supply(string rankId) => supplies.TryGetValue(rankId ?? string.Empty, out SupplyRule value) ? value : null;
        public int GradeMultiplier(string id) => gradeMultiplierBps.TryGetValue(id ?? string.Empty, out int value) ? value : throw new ProgressionDomainException("P11_CONTENT_INVALID");
        public long ExperienceToNext(string rankId, int level) => curves.TryGetValue(rankId + "\u001f" + level.ToString(CultureInfo.InvariantCulture), out long value) ? value : throw new ProgressionDomainException("P11_CONTENT_INVALID");
        public IReadOnlyList<CostItem> Items(string grade, string from, string to)
        {
            var result = new List<CostItem>(); RankRule rank = Rank(from);
            if (!string.IsNullOrEmpty(rank.TokenId)) result.Add(new CostItem(rank.TokenId, 1));
            if (gradeItems.TryGetValue(Key(grade, from, to), out List<CostItem> extras)) result.AddRange(extras);
            return result.GroupBy(value => value.Id, StringComparer.Ordinal).Select(group => new CostItem(group.Key, group.Sum(value => value.Quantity))).OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        }
        public long RecordValue(JObject mercenary, RecordRule rule)
        {
            if (rule == null) return 0;
            JObject value = (JObject)mercenary["records"]!;
            return rule.Type switch
            {
                "HUNT_COUNT" => value.Value<long>("huntCount"),
                "REGION_BATTLE_COUNT" => value.Value<long>("region2BattleCount"),
                "ELITE_KILL_COUNT" => value.Value<long>("eliteKillCount"),
                "BOSS_CONTRIBUTION_COUNT" => value.Value<long>("bossContributionCount"),
                "RAID_CLEAR_COUNT" => value.Value<long>("raidClearCount"),
                _ => throw new ProgressionDomainException("P11_CONTENT_INVALID")
            };
        }
        private static string Key(string grade, string from, string to) => grade + "\u001f" + from + "\u001f" + to;
        private static string Null(string value) => string.IsNullOrEmpty(value) ? null : value;
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int DecimalBps(IReadOnlyDictionary<string, string> row, string key) => checked((int)Math.Round(decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture) * 10_000m, MidpointRounding.AwayFromZero));
    }

    public sealed class ProgressionGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private P11ProgressionCatalog catalog;
        private CanonicalInventoryCatalog inventoryCatalog;

        public ProgressionGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 71;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<ProgressionOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        { save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>(); }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || game.CurrentDocument.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
                throw new ProgressionDomainException("P11_CONTENT_VERSION_UNSUPPORTED");
            catalog = new P11ProgressionCatalog(content.Catalog);
            inventoryCatalog = new CanonicalInventoryCatalog(content.Catalog);
            IsBootstrapped = true;
            NormalizeExpiredReviews();
        }

        public ProgressionOverviewDto GetOverview()
        {
            EnsureReady(); NormalizeExpiredReviews(); JObject document = game.Snapshot(); JObject guild = Guild(document);
            long now = clock.UtcNow.ToUnixTimeSeconds();
            var rows = new List<PromotionMercenaryDto>();
            foreach (JObject mercenary in document["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal))
            {
                P11ProgressionCatalog.RankRule rank = catalog.Rank(mercenary.Value<string>("rankId"));
                P11ProgressionCatalog.ReviewRule review = catalog.Review(rank.Id);
                P11ProgressionCatalog.RecordRule record = catalog.Record(rank.Id);
                int multiplier = catalog.GradeMultiplier(mercenary.Value<string>("gradeId"));
                long recordValue = catalog.RecordValue(mercenary, record);
                string status = mercenary["promotion"]!.Value<string>("status");
                long remaining = status == "IN_REVIEW" ? Math.Max(0, ParseUtc(mercenary["promotion"]!.Value<string>("finishesAtUtc")).ToUnixTimeSeconds() - now) : 0;
                long score = EquipmentScore(document, mercenary);
                var requirementRows = new List<PromotionRequirementDto>();
                if (review != null)
                {
                    requirementRows.Add(new PromotionRequirementDto("레벨", mercenary.Value<int>("level"), rank.MaxLevel, mercenary.Value<int>("level") >= rank.MaxLevel));
                    requirementRows.Add(new PromotionRequirementDto("기여도", mercenary.Value<long>("contribution"), rank.Contribution, mercenary.Value<long>("contribution") >= rank.Contribution));
                    requirementRows.Add(new PromotionRequirementDto("전투 실적", recordValue, record.Required, recordValue >= record.Required));
                    requirementRows.Add(new PromotionRequirementDto("길드 레벨", guild.Value<int>("level"), review.GuildLevel, guild.Value<string>("state") == "ACTIVE" && guild.Value<int>("level") >= review.GuildLevel));
                    JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!;
                    foreach (P11ProgressionCatalog.CostItem item in catalog.Items(mercenary.Value<string>("gradeId"), rank.Id, review.To))
                    {
                        long owned = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == item.Id)?.Value<long>("quantity") ?? 0;
                        requirementRows.Add(new PromotionRequirementDto("재료 " + item.Id, owned, item.Quantity, owned >= item.Quantity));
                    }
                }
                IReadOnlyList<PromotionRequirementDto> requirements = requirementRows;
                rows.Add(new PromotionMercenaryDto(mercenary.Value<string>("instanceId"), mercenary.Value<string>("displayName"), mercenary.Value<string>("gradeId"), rank.Id, rank.NextId,
                    mercenary.Value<int>("level"), rank.MaxLevel, mercenary.Value<long>("exp"), mercenary.Value<int>("level") < rank.MaxLevel ? catalog.ExperienceToNext(rank.Id, mercenary.Value<int>("level")) : 0,
                    mercenary.Value<long>("personalGold"), review == null ? 0 : ProgressionMath.ScaleCost(rank.PersonalGold, multiplier), review == null ? 0 : ProgressionMath.ScaleCost(review.KingdomGold, multiplier),
                    status, remaining, score, review?.RecommendedScore ?? 0, requirements));
            }
            string last = document["payload"]!["progression"]!["events"]!.Children<JObject>().OrderByDescending(value => value.Value<long>("sequence")).FirstOrDefault()?.Value<string>("resultCode") ?? "NONE";
            return new ProgressionOverviewDto(document.Value<long>("revision"), guild.Value<int>("level"), guild.Value<string>("state"), document["payload"]!["kingdom"]!.Value<long>("kingdomGold"), rows, last);
        }

        public PromotionOperationResult StartReview(StartPromotionReviewCommand command) => Execute(command, draft =>
        {
            JObject mercenary = RequireMercenary(draft, command.MercenaryInstanceId); MarkReady(draft, mercenary, true);
            Require(mercenary["promotion"]!.Value<string>("status") == "READY", "P11_PROMOTION_NOT_READY");
            P11ProgressionCatalog.RankRule rank = catalog.Rank(mercenary.Value<string>("rankId"));
            P11ProgressionCatalog.ReviewRule review = catalog.Review(rank.Id) ?? throw new ProgressionDomainException("P11_PROMOTION_NOT_READY");
            int multiplier = catalog.GradeMultiplier(mercenary.Value<string>("gradeId"));
            long personal = ProgressionMath.ScaleCost(rank.PersonalGold, multiplier), kingdom = ProgressionMath.ScaleCost(review.KingdomGold, multiplier);
            IReadOnlyList<P11ProgressionCatalog.CostItem> items = catalog.Items(mercenary.Value<string>("gradeId"), rank.Id, review.To);
            Require(mercenary.Value<long>("personalGold") >= personal, "P11_PERSONAL_GOLD_INSUFFICIENT");
            JObject kingdomState = (JObject)draft["payload"]!["kingdom"]!;
            Require(kingdomState.Value<long>("kingdomGold") >= kingdom, "P11_KINGDOM_GOLD_INSUFFICIENT");
            DebitItems(draft, items);
            mercenary["personalGold"] = mercenary.Value<long>("personalGold") - personal;
            kingdomState["kingdomGold"] = kingdomState.Value<long>("kingdomGold") - kingdom;
            string started = FormatUtc(clock.UtcNow), finishes = FormatUtc(clock.UtcNow.AddSeconds(review.Seconds));
            mercenary["promotion"] = new JObject
            {
                ["status"] = "IN_REVIEW", ["targetRankId"] = review.To, ["operationId"] = command.OperationId.ToString("D"),
                ["startedAtUtc"] = started, ["finishesAtUtc"] = finishes,
                ["costSnapshot"] = new JObject { ["contentVersion"] = game.CurrentDocument.Value<string>("contentVersion"),
                    ["fromRankId"] = rank.Id, ["toRankId"] = review.To, ["personalGold"] = personal, ["kingdomGold"] = kingdom,
                    ["contributionRequired"] = rank.Contribution, ["reviewSeconds"] = review.Seconds,
                    ["items"] = new JArray(items.Select(value => new JObject { ["itemId"] = value.Id, ["quantity"] = value.Quantity })) }
            };
            JObject autonomy = (JObject)mercenary["autonomy"]!; autonomy["state"] = "PROMOTION_PROCESS"; autonomy["reasonCode"] = "PROMOTION_AVAILABLE";
            AppendEvent(draft, "PromotionReviewStarted", mercenary, rank.Id, review.To, null, "P11_PROMOTION_REVIEW_STARTED", clock.UtcNow);
            return Mutation.Started(mercenary.Value<string>("instanceId"), rank.Id, review.To, personal, kingdom, items);
        });

        public PromotionOperationResult ApplyPromotion(ApplyPromotionCommand command) => Execute(command, draft =>
        {
            JObject mercenary = RequireMercenary(draft, command.MercenaryInstanceId); JObject promotion = (JObject)mercenary["promotion"]!;
            Require(promotion.Value<string>("status") == "COMPLETED_PENDING_APPLY", "P11_PROMOTION_NOT_COMPLETE");
            JObject snapshot = (JObject)promotion["costSnapshot"]!; string from = mercenary.Value<string>("rankId"), to = promotion.Value<string>("targetRankId");
            Require(snapshot.Value<string>("fromRankId") == from && snapshot.Value<string>("toRankId") == to, "P11_PROMOTION_SNAPSHOT_INVALID");
            int before = mercenary.Value<int>("level"); mercenary["rankId"] = to; mercenary["level"] = 1; mercenary["exp"] = 0;
            mercenary["promotion"] = new JObject { ["status"] = "NONE", ["targetRankId"] = null, ["operationId"] = null, ["startedAtUtc"] = null, ["finishesAtUtc"] = null, ["costSnapshot"] = null };
            JObject autonomy = (JObject)mercenary["autonomy"]!; autonomy["state"] = "IDLE_TOWN"; autonomy["reasonCode"] = "NONE"; autonomy["stateStartedAtUtc"] = FormatUtc(clock.UtcNow); autonomy["nextDecisionAtUtc"] = FormatUtc(clock.UtcNow);
            AppendEvent(draft, "PromotionApplied", mercenary, from, to, null, "P11_PROMOTION_APPLIED", clock.UtcNow, before, 1);
            return Mutation.Applied(mercenary.Value<string>("instanceId"), from, to);
        });

        public bool NormalizeExpiredReviews()
        {
            if (!IsBootstrapped) return false;
            JObject draft = game.Snapshot(); bool changed = false;
            foreach (JObject mercenary in draft["payload"]!["mercenaries"]!.Children<JObject>())
            {
                JObject promotion = (JObject)mercenary["promotion"]!;
                if (promotion.Value<string>("status") != "IN_REVIEW" || ParseUtc(promotion.Value<string>("finishesAtUtc")) > clock.UtcNow) continue;
                promotion["status"] = "COMPLETED_PENDING_APPLY"; changed = true;
                AppendEvent(draft, "PromotionReviewCompleted", mercenary, mercenary.Value<string>("rankId"), promotion.Value<string>("targetRankId"), null, "P11_PROMOTION_REVIEW_COMPLETED", clock.UtcNow);
            }
            if (!changed) return false;
            SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow);
            if (!written.Success) throw new ProgressionDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P11_SAVE_REVISION_CONFLICT" : "P11_SAVE_WRITE_FAILED");
            game.SynchronizeCommittedDocument(written.Document); return true;
        }

        public void ApplyCombatSettlement(JObject draft, IEnumerable<string> partyIds, long totalExperience, string regionId, int regionBattles, int eliteKills, int bossContributions, DateTimeOffset now)
        {
            EnsureReady(); IReadOnlyDictionary<string, long> shares = ProgressionMath.AllocateExperience(totalExperience, partyIds);
            foreach (KeyValuePair<string, long> share in shares)
            {
                JObject mercenary = RequireMercenary(draft, share.Key); JObject records = (JObject)mercenary["records"]!;
                if (regionId == "REGION_R02") records["region2BattleCount"] = checked(records.Value<long>("region2BattleCount") + regionBattles);
                records["eliteKillCount"] = checked(records.Value<long>("eliteKillCount") + eliteKills);
                records["bossContributionCount"] = checked(records.Value<long>("bossContributionCount") + bossContributions);
                string rankId = mercenary.Value<string>("rankId"); int before = mercenary.Value<int>("level");
                LevelProgress progress = ProgressionMath.ApplyExperience(rankId, before, mercenary.Value<long>("exp"), share.Value, id => catalog.Rank(id).MaxLevel, catalog.ExperienceToNext);
                mercenary["level"] = progress.LevelAfter; mercenary["exp"] = progress.ExperienceAfter;
                AppendEvent(draft, "ExperienceAwarded", mercenary, rankId, catalog.Rank(rankId).NextId, share.Value, "P11_EXPERIENCE_AWARDED", now, before, progress.LevelAfter);
                GrantSupport(draft, mercenary, now); MarkReady(draft, mercenary, false, now);
            }
        }

        public void Shutdown() { Changed = null; inventoryCatalog = null; catalog = null; game = null; content = null; save = null; IsBootstrapped = false; }

        private PromotionOperationResult Execute(ProgressionCommand command, Func<JObject, Mutation> mutate)
        {
            EnsureReady(); NormalizeExpiredReviews(); if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new ProgressionRequestHasher().Compute(command)); JObject current = game.Snapshot();
            JObject replay = current["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
            if (replay != null)
            {
                VerifyHash(command.RequestHash, replay.Value<string>("requestHash")); JObject payload = replay["resultPayload"] as JObject;
                return new PromotionOperationResult(command.OperationId, payload?.Value<long>("revisionBefore") ?? game.Revision, payload?.Value<long>("revisionAfter") ?? game.Revision, true,
                    payload?.Value<string>("resultCode") ?? "P11_REPLAYED", replay.Value<string>("resultDigest"), payload == null ? new JObject() : (JObject)payload.DeepClone());
            }
            Require(command.ExpectedRevision == game.Revision, "P11_SAVE_REVISION_CONFLICT"); long before = game.Revision;
            JObject draft = (JObject)current.DeepClone(); Mutation change = mutate(draft); JObject result = change.Result(command.OperationId, before, before + 1);
            string digest = Rfc8785Canonicalizer.ComputeSha256(result); result["resultDigest"] = digest;
            if (change.IsStart) AppendLedger(draft, command, change, digest);
            AppendJournal(draft, command, result, digest); SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow);
            if (!written.Success) throw new ProgressionDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P11_SAVE_REVISION_CONFLICT" : "P11_SAVE_WRITE_FAILED");
            game.SynchronizeCommittedDocument(written.Document); ProgressionOverviewDto overview = GetOverview(); Changed?.Invoke(this, overview);
            return new PromotionOperationResult(command.OperationId, before, game.Revision, false, change.Code, digest, result);
        }

        private bool MarkReady(JObject document, JObject mercenary, bool strict, DateTimeOffset? at = null)
        {
            P11ProgressionCatalog.RankRule rank = catalog.Rank(mercenary.Value<string>("rankId"));
            P11ProgressionCatalog.ReviewRule review = catalog.Review(rank.Id); if (review == null) return false;
            P11ProgressionCatalog.RecordRule record = catalog.Record(rank.Id); JObject guild = Guild(document); JObject autonomy = (JObject)mercenary["autonomy"]!;
            bool ready = mercenary.Value<int>("level") >= rank.MaxLevel && mercenary.Value<long>("contribution") >= rank.Contribution && catalog.RecordValue(mercenary, record) >= record.Required &&
                guild.Value<string>("state") == "ACTIVE" && guild.Value<int>("level") >= review.GuildLevel && autonomy["currentRegionId"]!.Type == JTokenType.Null;
            JObject promotion = (JObject)mercenary["promotion"]!; string current = promotion.Value<string>("status");
            if (ready && current == "NONE")
            {
                promotion["status"] = "READY"; promotion["targetRankId"] = rank.NextId; autonomy["state"] = "PROMOTION_READY"; autonomy["reasonCode"] = "PROMOTION_AVAILABLE";
                AppendEvent(document, "PromotionReady", mercenary, rank.Id, rank.NextId, null, "P11_PROMOTION_READY", at ?? clock.UtcNow); return true;
            }
            if (strict && !ready) throw new ProgressionDomainException(!guild.Value<string>("state").Equals("ACTIVE", StringComparison.Ordinal) ? "P11_GUILD_INACTIVE" : "P11_PROMOTION_NOT_READY");
            return current == "READY";
        }

        private void GrantSupport(JObject document, JObject mercenary, DateTimeOffset now)
        {
            string rankId = mercenary.Value<string>("rankId"); P11ProgressionCatalog.RecordRule record = catalog.Record(rankId); P11ProgressionCatalog.SupplyRule supply = catalog.Supply(rankId);
            if (record == null || supply == null || catalog.RecordValue(mercenary, record) < record.Required) return;
            JObject progression = (JObject)document["payload"]!["progression"]!; string key = supply.Id + ":" + mercenary.Value<string>("instanceId"); JArray keys = (JArray)progression["issuedSupplyKeys"]!;
            if (keys.Values<string>().Contains(key, StringComparer.Ordinal)) return;
            if (keys.Count >= 120) throw new ProgressionDomainException("P11_INVENTORY_CAPACITY");
            keys.Add(key); progression["issuedSupplyKeys"] = new JArray(keys.Values<string>().OrderBy(value => value, StringComparer.Ordinal));
            JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!; JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == supply.ItemId);
            if (stack == null) stacks.Add(new JObject { ["itemId"] = supply.ItemId, ["quantity"] = supply.Quantity }); else stack["quantity"] = checked(stack.Value<long>("quantity") + supply.Quantity);
            document["payload"]!["inventory"]!["itemStacks"] = new JArray(stacks.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal));
            AppendEvent(document, "PromotionSupportIssued", mercenary, rankId, catalog.Rank(rankId).NextId, null, "P11_PROMOTION_SUPPORT_ISSUED", now);
        }

        private long EquipmentScore(JObject document, JObject mercenary)
        {
            Dictionary<string, JObject> equipment = document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            long score = 0; foreach (JProperty slot in ((JObject)mercenary["equipmentSlots"]!).Properties())
            {
                if (slot.Value.Type == JTokenType.Null || !equipment.TryGetValue(slot.Value.Value<string>(), out JObject stored)) continue;
                EquipmentInstance item = inventoryCatalog.Instance(stored); score = checked(score + EquipmentMath.Score(mercenary.Value<string>("jobId"), item, inventoryCatalog.Weights(mercenary.Value<string>("jobId"))));
            }
            return score;
        }

        private static void DebitItems(JObject document, IEnumerable<P11ProgressionCatalog.CostItem> items)
        {
            JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!;
            foreach (P11ProgressionCatalog.CostItem item in items)
            {
                JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == item.Id);
                Require(stack != null && stack.Value<long>("quantity") >= item.Quantity, "P11_PROMOTION_MATERIAL_INSUFFICIENT");
            }
            foreach (P11ProgressionCatalog.CostItem item in items)
            {
                JObject stack = stacks.Children<JObject>().Single(value => value.Value<string>("itemId") == item.Id); long remaining = stack.Value<long>("quantity") - item.Quantity;
                if (remaining == 0) stack.Remove(); else stack["quantity"] = remaining;
            }
        }

        private void AppendLedger(JObject document, ProgressionCommand command, Mutation change, string digest)
        {
            JObject store = (JObject)document["payload"]!["economy"]!["store"]!; JObject ledger = (JObject)store["ledger"]!; JArray entries = (JArray)ledger["entries"]!;
            if (entries.Count >= 200)
            {
                JObject removed = (JObject)entries[0];
                string previous = ledger["prunedDigest"]!.Type == JTokenType.Null ? new string('0', 64) : ledger.Value<string>("prunedDigest");
                ledger["prunedDigest"] = Sha256(Encoding.UTF8.GetBytes(previous).Concat(Rfc8785Canonicalizer.Canonicalize(removed)).ToArray());
                ledger["prunedThroughSequence"] = removed.Value<long>("sequence");
                entries.RemoveAt(0);
            }
            long sequence = ledger.Value<long>("nextSequence");
            entries.Add(new JObject { ["sequence"] = sequence, ["operationId"] = command.OperationId.ToString("D"), ["transactionType"] = "PROMOTION_START",
                ["actorMercenaryInstanceId"] = change.MercenaryId, ["reasonCode"] = change.Code, ["personalGoldDelta"] = -change.PersonalGold,
                ["kingdomGoldDelta"] = -change.KingdomGold, ["stockVersionAfter"] = store.Value<long>("stockVersion"), ["committedAtUtc"] = FormatUtc(clock.UtcNow),
                ["requestHash"] = command.RequestHash, ["resultDigest"] = digest,
                ["lines"] = new JArray(new JObject { ["lineNo"] = 1, ["productKind"] = "SERVICE", ["productId"] = "PROMOTION_" + change.ToRank,
                    ["quantity"] = 1, ["unitPrice"] = Math.Max(1, checked(change.PersonalGold + change.KingdomGold)), ["lineTotal"] = Math.Max(1, checked(change.PersonalGold + change.KingdomGold)), ["stockDelta"] = 0 }) });
            ledger["nextSequence"] = checked(sequence + 1);
        }

        private void AppendJournal(JObject document, ProgressionCommand command, JObject result, string digest)
        {
            string now = FormatUtc(clock.UtcNow); ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "PROMOTION", ["facilityJobType"] = null,
                ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = now, ["updatedAtUtc"] = now, ["completedAtUtc"] = now,
                ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest, ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = result.DeepClone()
            });
        }

        private static void AppendEvent(JObject document, string type, JObject mercenary, string from, string to, long? xp, string code, DateTimeOffset now, int? before = null, int? after = null)
        {
            JObject progression = (JObject)document["payload"]!["progression"]!; JArray events = (JArray)progression["events"]!; if (events.Count >= 200) events.RemoveAt(0);
            long sequence = progression.Value<long>("nextEventSequence"); events.Add(new JObject
            {
                ["sequence"] = sequence, ["eventType"] = type, ["mercenaryInstanceId"] = mercenary.Value<string>("instanceId"),
                ["fromRankId"] = from == null ? JValue.CreateNull() : from, ["toRankId"] = to == null ? JValue.CreateNull() : to,
                ["levelBefore"] = before.HasValue ? before.Value : JValue.CreateNull(), ["levelAfter"] = after.HasValue ? after.Value : JValue.CreateNull(),
                ["experienceDelta"] = xp.HasValue ? xp.Value : JValue.CreateNull(), ["resultCode"] = code, ["createdAtUtc"] = FormatUtc(now)
            }); progression["nextEventSequence"] = checked(sequence + 1);
        }

        private static JObject Guild(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_GUILD");
        private static JObject RequireMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new ProgressionDomainException("P11_MERCENARY_NOT_FOUND");
        private static void VerifyHash(string expected, string actual) => Require(FixedTimeEquals(expected, actual), "P11_OPERATION_HASH_MISMATCH");
        private static bool FixedTimeEquals(string left, string right) => left != null && right != null && left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
        private static void Require(bool condition, string code) { if (!condition) throw new ProgressionDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped || catalog == null || game == null) throw new ProgressionDomainException("P11_CONTENT_VERSION_UNSUPPORTED"); }
        private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static string Sha256(byte[] value) { using SHA256 sha = SHA256.Create(); return string.Concat(sha.ComputeHash(value).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }

        private sealed class Mutation
        {
            public string Code; public string MercenaryId; public string FromRank; public string ToRank; public bool IsStart; public long PersonalGold; public long KingdomGold; public IReadOnlyList<P11ProgressionCatalog.CostItem> Items;
            public static Mutation Started(string id, string from, string to, long personal, long kingdom, IReadOnlyList<P11ProgressionCatalog.CostItem> items) => new() { Code = "P11_PROMOTION_REVIEW_STARTED", MercenaryId = id, FromRank = from, ToRank = to, IsStart = true, PersonalGold = personal, KingdomGold = kingdom, Items = items };
            public static Mutation Applied(string id, string from, string to) => new() { Code = "P11_PROMOTION_APPLIED", MercenaryId = id, FromRank = from, ToRank = to, Items = Array.Empty<P11ProgressionCatalog.CostItem>() };
            public JObject Result(Guid operationId, long before, long after) => new()
            {
                ["operationId"] = operationId.ToString("D"), ["revisionBefore"] = before, ["revisionAfter"] = after, ["resultCode"] = Code,
                ["mercenaryInstanceId"] = MercenaryId, ["fromRankId"] = FromRank, ["toRankId"] = ToRank,
                ["personalGoldDelta"] = -PersonalGold, ["kingdomGoldDelta"] = -KingdomGold,
                ["items"] = new JArray((Items ?? Array.Empty<P11ProgressionCatalog.CostItem>()).Select(value => new JObject { ["itemId"] = value.Id, ["quantity"] = value.Quantity }))
            };
        }
    }
}
