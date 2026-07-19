using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Application.Recruitment;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Domain.Recruitment;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Recruitment
{
    public sealed class RecruitmentCatalog
    {
        private readonly Dictionary<int, TavernRule> tavernRules;
        private readonly Dictionary<string, Pool> pools;
        private readonly Dictionary<string, IReadOnlyList<PoolEntry>> entries;
        private readonly Dictionary<string, IReadOnlyList<PityRule>> pityRules;
        private readonly Dictionary<string, int> gradeOrder;
        private readonly Dictionary<string, int> gradeTraits;
        private readonly Dictionary<string, int> gradeCost;
        private readonly string[] jobs;
        private readonly string[] names;
        private readonly string[] personalities;
        private readonly Dictionary<string, string[]> traitsByJob;
        private readonly Dictionary<string, RateUpRule> rateUps;

        public RecruitmentCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
                throw new RecruitmentDomainException("P13_CONTENT_VERSION_UNSUPPORTED");
            ContentVersion = catalog.ContentVersion;
            tavernRules = catalog.GetTable("recruitment_tavern_rules.csv").Rows.Where(Enabled).ToDictionary(
                row => Int(row, "tavern_level"), row => new TavernRule(Int(row, "tavern_level"), row["pool_id"], Int(row, "candidate_count"), Int(row, "max_locked"), Long(row, "free_refresh_seconds"), Long(row, "paid_refresh_cost")));
            pools = catalog.GetTable("recruitment_pools.csv").Rows.Where(Enabled).ToDictionary(row => row["pool_id"], row =>
                new Pool(row["pool_id"], row["pool_type"], row["pity_group_id"], row["rate_up_group_id"], row["cost_type"], Long(row, "cost_amount")), StringComparer.Ordinal);
            entries = catalog.GetTable("recruitment_pool_entries.csv").Rows.Where(Enabled).Select(row => new PoolEntry(
                row["pool_id"], Int(row, "entry_no"), row["result_id"], row["grade_id"], row["job_selection_type"], row["job_selection_id"], Int(row, "weight")))
                .GroupBy(value => value.PoolId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<PoolEntry>)group.OrderBy(value => value.EntryNo).ToArray(), StringComparer.Ordinal);
            pityRules = catalog.GetTable("recruitment_pity_rules.csv").Rows.Where(Enabled).Select(row => new PityRule(
                row["pity_group_id"], row["pity_rule_id"], Int(row, "trigger_count"), row["guaranteed_grade_id"], row["reset_on_grade_or_higher_id"]))
                .GroupBy(value => value.GroupId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<PityRule>)group.OrderBy(value => value.TriggerCount).ToArray(), StringComparer.Ordinal);
            var grades = catalog.GetTable("mercenary_grades.csv").Rows.Where(Enabled).ToArray();
            gradeOrder = grades.ToDictionary(row => row["grade_id"], row => Int(row, "order"), StringComparer.Ordinal);
            gradeTraits = grades.ToDictionary(row => row["grade_id"], row => Int(row, "initial_trait_count"), StringComparer.Ordinal);
            gradeCost = catalog.GetTable("recruitment_grade_cost_rules.csv").Rows.Where(Enabled).ToDictionary(row => row["grade_id"], row => Int(row, "hire_cost_multiplier_bps"), StringComparer.Ordinal);
            jobs = catalog.GetTable("jobs.csv").Rows.Where(Enabled).Select(row => row["job_id"]).ToArray();
            names = catalog.GetTable("mercenary_name_pool_entries.csv").Rows.Where(row => Enabled(row) && row["locale"] == "ko-KR").Select(row => row["display_name"]).ToArray();
            personalities = catalog.GetTable("personalities.csv").Rows.Where(Enabled).Select(row => row["personality_id"]).ToArray();
            traitsByJob = catalog.GetTable("trait_job_eligibility.csv").Rows.Where(Enabled)
                .GroupBy(row => row["job_id"], StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Select(row => row["trait_id"]).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            var featuredEntries = catalog.GetTable("recruitment_rate_up_entries.csv").Rows.Where(Enabled)
                .GroupBy(row => row["rate_up_group_id"], StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.OrderBy(row => row["job_id"], StringComparer.Ordinal).Select(row => row["job_id"]).ToArray(), StringComparer.Ordinal);
            rateUps = catalog.GetTable("recruitment_rate_up_groups.csv").Rows.Where(Enabled).ToDictionary(row => row["rate_up_group_id"], row =>
                new RateUpRule(row["rate_up_group_id"], Int(row, "featured_share"), row["failure_guarantee_mode"], featuredEntries.TryGetValue(row["rate_up_group_id"], out string[] values) ? values : Array.Empty<string>()), StringComparer.Ordinal);
            Validate();
        }

        public TavernRule Tavern(int level) => tavernRules.TryGetValue(Math.Clamp(level, 1, 4), out TavernRule value) ? value : throw new RecruitmentDomainException("P13_TAVERN_RULE_NOT_FOUND");
        public string ContentVersion { get; }
        public Pool GetPool(string id) => pools.TryGetValue(id ?? string.Empty, out Pool value) ? value : throw new RecruitmentDomainException("P13_POOL_NOT_FOUND");
        public IReadOnlyList<PoolEntry> Entries(string poolId) => entries.TryGetValue(poolId ?? string.Empty, out IReadOnlyList<PoolEntry> value) ? value : throw new RecruitmentDomainException("P13_POOL_NOT_FOUND");
        public IReadOnlyList<PityRule> Pity(string group) => string.IsNullOrEmpty(group) ? Array.Empty<PityRule>() : pityRules.TryGetValue(group, out IReadOnlyList<PityRule> value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
        public RateUpRule RateUp(string id) => string.IsNullOrEmpty(id) ? null : rateUps.TryGetValue(id, out RateUpRule value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
        public int GradeOrder(string id) => gradeOrder.TryGetValue(id ?? string.Empty, out int value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
        public int TraitCount(string grade) => gradeTraits.TryGetValue(grade ?? string.Empty, out int value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
        public int GradeCostMultiplier(string grade) => gradeCost.TryGetValue(grade ?? string.Empty, out int value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
        public string[] Jobs => jobs;
        public string[] Names => names;
        public string[] Personalities => personalities;
        public string[] Traits(string job) => traitsByJob.TryGetValue(job ?? string.Empty, out string[] value) ? value : throw new RecruitmentDomainException("P13_CONTENT_INVALID");

        private void Validate()
        {
            if (tavernRules.Count != 4 || jobs.Length != 5 || names.Length == 0 || personalities.Length == 0) throw new RecruitmentDomainException("P13_CONTENT_INVALID");
            foreach (Pool pool in pools.Values)
            {
                IReadOnlyList<PoolEntry> values = Entries(pool.Id);
                if (values.Sum(value => value.Weight) != 100) throw new RecruitmentDomainException("P13_POOL_WEIGHT_INVALID");
                string[] allowed = pool.Type == "TAVERN" ? new[] { "GRADE_C", "GRADE_B", "GRADE_A" } : new[] { "GRADE_A", "GRADE_S", "GRADE_SS" };
                if (values.Any(value => !allowed.Contains(value.GradeId, StringComparer.Ordinal))) throw new RecruitmentDomainException("P13_POOL_GRADE_INVALID");
            }
        }

        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string field) => int.Parse(row[field], CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string field) => long.Parse(row[field], CultureInfo.InvariantCulture);

        public sealed class TavernRule
        {
            public TavernRule(int level, string poolId, int count, int maxLocked, long freeSeconds, long paidCost) { Level = level; PoolId = poolId; CandidateCount = count; MaxLocked = maxLocked; FreeSeconds = freeSeconds; PaidCost = paidCost; }
            public int Level { get; } public string PoolId { get; } public int CandidateCount { get; } public int MaxLocked { get; } public long FreeSeconds { get; } public long PaidCost { get; }
        }
        public sealed class Pool
        {
            public Pool(string id, string type, string pityGroupId, string rateUpGroupId, string costType, long cost) { Id = id; Type = type; PityGroupId = pityGroupId; RateUpGroupId = rateUpGroupId; CostType = costType; Cost = cost; }
            public string Id { get; } public string Type { get; } public string PityGroupId { get; } public string RateUpGroupId { get; } public string CostType { get; } public long Cost { get; }
        }
        public sealed class PoolEntry
        {
            public PoolEntry(string poolId, int entryNo, string profile, string grade, string jobType, string jobId, int weight) { PoolId = poolId; EntryNo = entryNo; GenerationProfileId = profile; GradeId = grade; JobSelectionType = jobType; JobSelectionId = jobId; Weight = weight; }
            public string PoolId { get; } public int EntryNo { get; } public string GenerationProfileId { get; } public string GradeId { get; } public string JobSelectionType { get; } public string JobSelectionId { get; } public int Weight { get; }
        }
        public sealed class PityRule
        {
            public PityRule(string group, string id, int trigger, string guaranteed, string reset) { GroupId = group; Id = id; TriggerCount = trigger; GuaranteedGradeId = guaranteed; ResetGradeId = reset; }
            public string GroupId { get; } public string Id { get; } public int TriggerCount { get; } public string GuaranteedGradeId { get; } public string ResetGradeId { get; }
        }
        public sealed class RateUpRule
        {
            public RateUpRule(string id, int share, string mode, string[] jobs) { Id = id; FeaturedShare = share; FailureMode = mode; FeaturedJobs = jobs; }
            public string Id { get; } public int FeaturedShare { get; } public string FailureMode { get; } public string[] FeaturedJobs { get; }
        }
    }

    internal sealed class RecruitmentRandom
    {
        private ulong state;
        public RecruitmentRandom(Guid operationId, string contentVersion)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(operationId.ToString("D") + "|" + contentVersion));
            state = BitConverter.ToUInt64(hash, 0); if (state == 0) state = 0x9E3779B97F4A7C15UL;
        }
        public ulong Next()
        {
            ulong value = state; value ^= value >> 12; value ^= value << 25; value ^= value >> 27; state = value; return value * 2685821657736338717UL;
        }
        public int Index(int count) => count > 0 ? (int)(Next() % (ulong)count) : throw new RecruitmentDomainException("P13_CONTENT_INVALID");
    }

    internal sealed class RecruitmentMercenaryGenerator
    {
        private readonly RecruitmentCatalog catalog;
        private readonly IUuidV7Provider ids;
        public RecruitmentMercenaryGenerator(RecruitmentCatalog catalog, IUuidV7Provider ids) { this.catalog = catalog; this.ids = ids; }

        public JObject Candidate(RecruitmentCatalog.Pool pool, Guid operationId, RecruitmentRandom random, DateTimeOffset now)
        {
            RecruitmentCatalog.PoolEntry entry = RecruitmentWeightSelector.Select(catalog.Entries(pool.Id), value => value.Weight, random.Next());
            string job = entry.JobSelectionType == "FIXED" ? entry.JobSelectionId : catalog.Jobs[random.Index(catalog.Jobs.Length)];
            return CandidateFrom(entry, pool, job, operationId, random, now);
        }

        public JObject Special(RecruitmentCatalog.PoolEntry entry, RecruitmentCatalog.Pool pool, string forcedJob, IEnumerable<string> excludedJobs, Guid operationId, RecruitmentRandom random, DateTimeOffset now)
        {
            string[] excluded = (excludedJobs ?? Array.Empty<string>()).ToArray();
            string[] available = catalog.Jobs.Where(value => !excluded.Contains(value, StringComparer.Ordinal)).ToArray();
            string job = !string.IsNullOrEmpty(forcedJob) ? forcedJob : entry.JobSelectionType == "FIXED" ? entry.JobSelectionId : available[random.Index(available.Length)];
            JObject candidate = CandidateFrom(entry, pool, job, operationId, random, now);
            return Snapshot(candidate, ids.NewId().ToString("D"), now);
        }

        public JObject Snapshot(JObject candidate, string instanceId, DateTimeOffset now)
        {
            string timestamp = Format(now);
            return new JObject
            {
                ["instanceId"] = instanceId, ["generationProfileId"] = candidate.Value<string>("generationProfileId"),
                ["nameSeed"] = candidate.Value<string>("nameSeed"), ["appearanceSeed"] = candidate.Value<string>("appearanceSeed"), ["growthSeed"] = candidate.Value<string>("growthSeed"),
                ["displayName"] = candidate.Value<string>("displayName"), ["jobId"] = candidate.Value<string>("jobId"), ["gradeId"] = candidate.Value<string>("gradeId"),
                ["rankId"] = "RANK_APPRENTICE", ["level"] = 1, ["exp"] = 0, ["personalityId"] = candidate.Value<string>("personalityId"), ["traitIds"] = candidate["traitIds"]!.DeepClone(),
                ["personalGold"] = 0, ["contribution"] = 0, ["active"] = false,
                ["autonomy"] = new JObject { ["state"] = "IDLE_TOWN", ["currentRegionId"] = null, ["targetInstanceId"] = null, ["reasonCode"] = "NONE", ["stateStartedAtUtc"] = timestamp, ["nextDecisionAtUtc"] = timestamp },
                ["potions"] = new JArray(), ["equipmentSlots"] = new JObject { ["WEAPON"] = null, ["ARMOR"] = null, ["HELMET"] = null, ["ACCESSORY"] = null },
                ["promotion"] = new JObject { ["status"] = "NONE", ["targetRankId"] = null, ["operationId"] = null, ["startedAtUtc"] = null, ["finishesAtUtc"] = null, ["costSnapshot"] = null },
                ["records"] = new JObject { ["huntCount"] = 0, ["killCount"] = 0, ["raidClearCount"] = 0, ["itemsCollected"] = 0, ["region2BattleCount"] = 0, ["eliteKillCount"] = 0, ["bossContributionCount"] = 0 }
            };
        }

        private JObject CandidateFrom(RecruitmentCatalog.PoolEntry entry, RecruitmentCatalog.Pool pool, string job, Guid operationId, RecruitmentRandom random, DateTimeOffset now)
        {
            string[] eligible = catalog.Traits(job); int traitCount = catalog.TraitCount(entry.GradeId); var traits = new List<string>();
            while (traits.Count < traitCount)
            {
                string trait = eligible[random.Index(eligible.Length)]; if (!traits.Contains(trait, StringComparer.Ordinal)) traits.Add(trait);
            }
            traits.Sort(StringComparer.Ordinal);
            long hireCost = pool.Type == "TAVERN" ? TavernHireCost.Calculate(pool.Cost, catalog.GradeCostMultiplier(entry.GradeId)) : 0;
            return new JObject
            {
                ["candidateId"] = ids.NewId().ToString("D"), ["poolId"] = pool.Id, ["generationProfileId"] = entry.GenerationProfileId,
                ["nameSeed"] = random.Next().ToString(CultureInfo.InvariantCulture), ["appearanceSeed"] = random.Next().ToString(CultureInfo.InvariantCulture), ["growthSeed"] = random.Next().ToString(CultureInfo.InvariantCulture),
                ["displayName"] = catalog.Names[random.Index(catalog.Names.Length)], ["jobId"] = job, ["gradeId"] = entry.GradeId,
                ["personalityId"] = catalog.Personalities[random.Index(catalog.Personalities.Length)], ["traitIds"] = new JArray(traits),
                ["hireCost"] = hireCost, ["locked"] = false, ["generatedAtUtc"] = Format(now)
            };
        }

        private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    public sealed class DevelopmentRecruitmentGateway : IRecruitmentGateway
    {
        private readonly RecruitmentCatalog catalog;
        private readonly RecruitmentMercenaryGenerator generator;
        public DevelopmentRecruitmentGateway(RecruitmentCatalog catalog, IUuidV7Provider ids) { this.catalog = catalog; generator = new RecruitmentMercenaryGenerator(catalog, ids); }
        public string Authority => "MOCK_ONLY";
        public bool IsOnline => true;

        public SpecialRecruitmentReceipt Recruit(SpecialRecruitmentCommand command, JObject state, DateTimeOffset now)
        {
            RecruitmentCatalog.Pool pool = catalog.GetPool(command.PoolId);
            if (pool.Type != "SPECIAL" || command.Count != 1) throw new RecruitmentDomainException("P13_SPECIAL_COUNT_UNSUPPORTED");
            if (pool.CostType != command.PaymentType) throw new RecruitmentDomainException("P13_PAYMENT_NOT_ALLOWED");
            JObject wallet = (JObject)state["premiumWalletCache"]!.DeepClone(); string walletField = command.PaymentType switch
            {
                "TICKET" => "specialRecruitTickets", "FREE_PREMIUM" => "freePremium", "PAID_PREMIUM" => "paidPremium",
                _ => throw new RecruitmentDomainException("P13_PAYMENT_NOT_ALLOWED")
            };
            long balance = wallet.Value<long>(walletField); if (balance < pool.Cost) throw new RecruitmentDomainException("P13_PREMIUM_BALANCE_INSUFFICIENT"); wallet[walletField] = balance - pool.Cost;
            var random = new RecruitmentRandom(command.OperationId, catalog.ContentVersion);
            JArray pity = (JArray)state["pityCounters"]!.DeepClone(); var reasons = new JArray(); int minimumOrder = 0;
            foreach (RecruitmentCatalog.PityRule rule in catalog.Pity(pool.PityGroupId))
            {
                JObject counter = pity.Children<JObject>().SingleOrDefault(value => value.Value<string>("pityGroupId") == rule.GroupId && value.Value<string>("pityRuleId") == rule.Id);
                if (counter == null) { counter = new JObject { ["pityGroupId"] = rule.GroupId, ["pityRuleId"] = rule.Id, ["pullCount"] = 0, ["lastUpdatedContentVersion"] = catalog.ContentVersion }; pity.Add(counter); }
                if (counter.Value<long>("pullCount") + 1 >= rule.TriggerCount && catalog.GradeOrder(rule.GuaranteedGradeId) > minimumOrder)
                { minimumOrder = catalog.GradeOrder(rule.GuaranteedGradeId); reasons.Clear(); reasons.Add(rule.Id); }
            }
            IReadOnlyList<RecruitmentCatalog.PoolEntry> eligible = catalog.Entries(pool.Id).Where(value => catalog.GradeOrder(value.GradeId) >= minimumOrder).ToArray();
            RecruitmentCatalog.PoolEntry selected = RecruitmentWeightSelector.Select(eligible, value => value.Weight, random.Next());
            foreach (RecruitmentCatalog.PityRule rule in catalog.Pity(pool.PityGroupId))
            {
                JObject counter = pity.Children<JObject>().Single(value => value.Value<string>("pityGroupId") == rule.GroupId && value.Value<string>("pityRuleId") == rule.Id);
                counter["pullCount"] = catalog.GradeOrder(selected.GradeId) >= catalog.GradeOrder(rule.ResetGradeId) ? 0 : counter.Value<long>("pullCount") + 1;
                counter["lastUpdatedContentVersion"] = catalog.ContentVersion;
            }
            JArray featured = (JArray)state["featuredGuarantees"]!.DeepClone(); RecruitmentCatalog.RateUpRule rate = catalog.RateUp(pool.RateUpGroupId); string forced = null; string[] excluded = Array.Empty<string>();
            if (rate != null && catalog.GradeOrder(selected.GradeId) >= catalog.GradeOrder("GRADE_S"))
            {
                JObject guarantee = featured.Children<JObject>().SingleOrDefault(value => value.Value<string>("pityGroupId") == pool.PityGroupId && value.Value<string>("rateUpGroupId") == rate.Id);
                if (guarantee == null) { guarantee = new JObject { ["pityGroupId"] = pool.PityGroupId, ["rateUpGroupId"] = rate.Id, ["state"] = "NONE", ["lastUpdatedContentVersion"] = catalog.ContentVersion }; featured.Add(guarantee); }
                bool guaranteedFeatured = guarantee.Value<string>("state") == "NEXT_S_OR_SS_FEATURED";
                bool featuredHit = guaranteedFeatured || random.Next() % 10000UL < (ulong)rate.FeaturedShare;
                if (featuredHit) { forced = rate.FeaturedJobs[random.Index(rate.FeaturedJobs.Length)]; guarantee["state"] = "NONE"; if (guaranteedFeatured) reasons.Add("RATE_UP_GUARANTEED"); }
                else { excluded = rate.FeaturedJobs; guarantee["state"] = rate.FailureMode; }
                guarantee["lastUpdatedContentVersion"] = catalog.ContentVersion;
            }
            JObject mercenary = generator.Special(selected, pool, forced, excluded, command.OperationId, random, now);
            pity = new JArray(pity.Children<JObject>().OrderBy(value => value.Value<string>("pityGroupId"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("pityRuleId"), StringComparer.Ordinal));
            featured = new JArray(featured.Children<JObject>().OrderBy(value => value.Value<string>("pityGroupId"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("rateUpGroupId"), StringComparer.Ordinal));
            JObject digestSource = new JObject { ["operationId"] = command.OperationId.ToString("D"), ["walletAfter"] = wallet.DeepClone(), ["pityAfter"] = pity.DeepClone(), ["featuredAfter"] = featured.DeepClone(), ["mercenary"] = mercenary.DeepClone(), ["guarantees"] = reasons.DeepClone() };
            return new SpecialRecruitmentReceipt("DEV-" + command.OperationId.ToString("N"), (state.Value<long?>("serverRevision") ?? 0) + 1, wallet, pity, featured, mercenary, reasons, Rfc8785Canonicalizer.ComputeSha256(digestSource));
        }
    }

    public sealed class RecruitmentGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private readonly IUuidV7Provider ids;
        private readonly SemaphoreSlim gate = new(1, 1);
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private MercenaryRosterService roster;
        private RecruitmentCatalog catalog;
        private RecruitmentMercenaryGenerator generator;
        private IRecruitmentGateway gateway;

        public RecruitmentGameService(ITrustedUtcClock clock, IUuidV7Provider ids = null) { this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); this.ids = ids ?? new SystemUuidV7Provider(); }
        public int InitializationOrder => 125;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>(); roster = services.Get<MercenaryRosterService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion) || !roster.IsBootstrapped) throw new RecruitmentDomainException("P13_CONTENT_VERSION_UNSUPPORTED");
            catalog = new RecruitmentCatalog(content.Catalog); generator = new RecruitmentMercenaryGenerator(catalog, ids); gateway = new DevelopmentRecruitmentGateway(catalog, ids); IsBootstrapped = true;
            if (!State(game.Snapshot())["tavern"]!["candidates"]!.Any()) Refresh(CreateRefreshCommand(true));
        }

        public RecruitmentOverviewDto GetOverview()
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject state = State(document); JObject wallet = (JObject)state["premiumWalletCache"]!;
            string raw = state["tavern"]!["nextFreeRefreshAtUtc"]!.Type == JTokenType.Null ? null : state["tavern"]!.Value<string>("nextFreeRefreshAtUtc");
            var pity = state["pityCounters"]!.Children<JObject>().ToDictionary(value => value.Value<string>("pityRuleId"), value => value.Value<long>("pullCount"), StringComparer.Ordinal);
            return new RecruitmentOverviewDto(game.Revision, state.Value<string>("authority"), document["payload"]!["kingdom"]!.Value<long>("kingdomGold"), wallet.Value<long>("specialRecruitTickets"), wallet.Value<long>("freePremium"), document["payload"]!["mercenaries"]!.Count(), document["payload"]!["kingdom"]!.Value<int>("ownedMercenaryLimit"), raw == null ? null : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal), state["tavern"]!["candidates"]!.Children<JObject>().Select(value => new RecruitmentCandidateDto(value)).ToArray(), pity, state["history"]!.Count());
        }

        public IReadOnlyList<string> GetHistoryLines()
        {
            EnsureReady();
            return State(game.Snapshot())["history"]!.Children<JObject>().Reverse().Take(12).Select(value =>
                $"{value.Value<string>("poolType")}|{value["resultGradeIds"]!.Values<string>().FirstOrDefault()}|{value.Value<long>("costAmount")}|{value.Value<string>("createdAtUtc")}").ToArray();
        }

        public RefreshTavernCommand CreateRefreshCommand(bool free) { Guid id = ids.NewId(); var draft = new RefreshTavernCommand(id, game.Revision, null, free); return new RefreshTavernCommand(id, game.Revision, new RecruitmentRequestHasher().Compute(draft), free); }
        public SetCandidateLockCommand CreateLockCommand(string candidateId, bool locked) { Guid id = ids.NewId(); var draft = new SetCandidateLockCommand(id, game.Revision, null, candidateId, locked); return new SetCandidateLockCommand(id, game.Revision, new RecruitmentRequestHasher().Compute(draft), candidateId, locked); }
        public HireCandidateCommand CreateHireCommand(string candidateId) { Guid id = ids.NewId(); var draft = new HireCandidateCommand(id, game.Revision, null, candidateId); return new HireCandidateCommand(id, game.Revision, new RecruitmentRequestHasher().Compute(draft), candidateId); }
        public SpecialRecruitmentCommand CreateSpecialCommand(string poolId, string paymentType) { Guid id = ids.NewId(); var draft = new SpecialRecruitmentCommand(id, game.Revision, null, poolId, paymentType); return new SpecialRecruitmentCommand(id, game.Revision, new RecruitmentRequestHasher().Compute(draft), poolId, paymentType); }

        public RecruitmentOperationResult Refresh(RefreshTavernCommand command) => Execute(command, (draft, now) =>
        {
            JObject state = State(draft); JObject tavern = (JObject)state["tavern"]!; int level = FacilityLevel(draft); RecruitmentCatalog.TavernRule rule = catalog.Tavern(level);
            if (command.Free && tavern["nextFreeRefreshAtUtc"]!.Type != JTokenType.Null && now < DateTimeOffset.Parse(tavern.Value<string>("nextFreeRefreshAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)) throw new RecruitmentDomainException("P13_TAVERN_REFRESH_NOT_READY");
            if (!command.Free)
            {
                long gold = draft["payload"]!["kingdom"]!.Value<long>("kingdomGold"); if (gold < rule.PaidCost) throw new RecruitmentDomainException("P13_KINGDOM_GOLD_INSUFFICIENT");
                draft["payload"]!["kingdom"]!["kingdomGold"] = gold - rule.PaidCost; AppendEconomy(draft, command, "TAVERN_REFRESH", -rule.PaidCost, rule.PoolId, now);
            }
            var random = new RecruitmentRandom(command.OperationId, catalog.ContentVersion); RecruitmentCatalog.Pool pool = catalog.GetPool(rule.PoolId);
            var values = tavern["candidates"]!.Children<JObject>().Where(value => value.Value<bool>("locked")).Select(value => (JObject)value.DeepClone()).Take(rule.MaxLocked).ToList();
            while (values.Count < rule.CandidateCount) values.Add(generator.Candidate(pool, command.OperationId, random, now));
            tavern["candidates"] = new JArray(values.OrderBy(value => value.Value<string>("candidateId"), StringComparer.Ordinal)); tavern["refreshSequence"] = tavern.Value<long>("refreshSequence") + 1; tavern["lastRefreshAtUtc"] = Format(now); tavern["nextFreeRefreshAtUtc"] = Format(now.AddSeconds(rule.FreeSeconds));
            AppendEvent(state, command, "TavernRefreshed", rule.PoolId, "P13_TAVERN_REFRESHED", now); return Result(command, "P13_TAVERN_REFRESHED", null);
        });

        public RecruitmentOperationResult SetCandidateLock(SetCandidateLockCommand command) => Execute(command, (draft, now) =>
        {
            JObject state = State(draft); JArray candidates = (JArray)state["tavern"]!["candidates"]!; JObject candidate = candidates.Children<JObject>().SingleOrDefault(value => value.Value<string>("candidateId") == command.CandidateId) ?? throw new RecruitmentDomainException("P13_CANDIDATE_NOT_FOUND");
            if (command.Locked && !candidate.Value<bool>("locked") && candidates.Children<JObject>().Count(value => value.Value<bool>("locked")) >= catalog.Tavern(FacilityLevel(draft)).MaxLocked) throw new RecruitmentDomainException("P13_CANDIDATE_LOCK_LIMIT");
            candidate["locked"] = command.Locked; AppendEvent(state, command, "CandidateLockChanged", command.CandidateId, command.Locked ? "P13_CANDIDATE_LOCKED" : "P13_CANDIDATE_UNLOCKED", now); return Result(command, command.Locked ? "P13_CANDIDATE_LOCKED" : "P13_CANDIDATE_UNLOCKED", null);
        });

        public RecruitmentOperationResult Hire(HireCandidateCommand command) => Execute(command, (draft, now) =>
        {
            JObject state = State(draft); JArray candidates = (JArray)state["tavern"]!["candidates"]!; JObject candidate = candidates.Children<JObject>().SingleOrDefault(value => value.Value<string>("candidateId") == command.CandidateId) ?? throw new RecruitmentDomainException("P13_CANDIDATE_NOT_FOUND");
            RequireCapacity(draft); long cost = candidate.Value<long>("hireCost"); long gold = draft["payload"]!["kingdom"]!.Value<long>("kingdomGold"); if (gold < cost) throw new RecruitmentDomainException("P13_KINGDOM_GOLD_INSUFFICIENT");
            JObject snapshot = generator.Snapshot(candidate, ids.NewId().ToString("D"), now); JObject withMercenary = new CreateMercenaryFromSnapshot(new MercenaryInvariantValidator(), roster.Catalog).ExecuteInternal(draft, snapshot); draft.RemoveAll(); foreach (JProperty property in withMercenary.Properties().ToArray()) { property.Remove(); draft.Add(property); }
            draft["payload"]!["kingdom"]!["kingdomGold"] = gold - cost; State(draft)["tavern"]!["candidates"]!.Children<JObject>().Single(value => value.Value<string>("candidateId") == command.CandidateId).Remove();
            AppendEconomy(draft, command, "TAVERN_HIRE", -cost, candidate.Value<string>("poolId"), now); AppendHistory(State(draft), command, candidate.Value<string>("poolId"), "TAVERN", "KINGDOM_GOLD", cost, snapshot, new JArray(), null, now); AppendEvent(State(draft), command, "CandidateHired", snapshot.Value<string>("instanceId"), "P13_CANDIDATE_HIRED", now); return Result(command, "P13_CANDIDATE_HIRED", snapshot.Value<string>("instanceId"));
        });

        public RecruitmentOperationResult RecruitSpecial(SpecialRecruitmentCommand command) => Execute(command, (draft, now) =>
        {
            RequireCapacity(draft); JObject state = State(draft); if (state.Value<string>("authority") != gateway.Authority || !gateway.IsOnline) throw new RecruitmentDomainException("P13_GATEWAY_OFFLINE");
            SpecialRecruitmentReceipt receipt = gateway.Recruit(command, state, now); if (string.IsNullOrEmpty(receipt.ResultDigest)) throw new RecruitmentDomainException("P13_RECEIPT_INVALID");
            JObject withMercenary = new CreateMercenaryFromSnapshot(new MercenaryInvariantValidator(), roster.Catalog).ExecuteInternal(draft, receipt.MercenarySnapshot); draft.RemoveAll(); foreach (JProperty property in withMercenary.Properties().ToArray()) { property.Remove(); draft.Add(property); }
            state = State(draft); state["serverRevision"] = receipt.ServerRevision; state["premiumWalletCache"] = receipt.WalletAfter.DeepClone(); state["pityCounters"] = receipt.PityAfter.DeepClone(); state["featuredGuarantees"] = receipt.FeaturedAfter.DeepClone();
            RecruitmentCatalog.Pool pool = catalog.GetPool(command.PoolId); AppendHistory(state, command, pool.Id, "SPECIAL", command.PaymentType, pool.Cost, receipt.MercenarySnapshot, receipt.GuaranteeReasons, receipt.ReceiptId, now); AppendEvent(state, command, "SpecialRecruitmentCompleted", receipt.MercenarySnapshot.Value<string>("instanceId"), "P13_SPECIAL_COMPLETED", now); return Result(command, "P13_SPECIAL_COMPLETED", receipt.MercenarySnapshot.Value<string>("instanceId"));
        });

        private RecruitmentOperationResult Execute(RecruitmentCommand command, Func<JObject, DateTimeOffset, JObject> mutation)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command)); gate.Wait();
            try
            {
                if (!FixedTimeEquals(new RecruitmentRequestHasher().Compute(command), command.RequestHash)) throw new RecruitmentDomainException("P13_OPERATION_HASH_MISMATCH");
                JObject current = game.Snapshot(); JObject replay = current["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
                if (replay != null)
                {
                    if (!FixedTimeEquals(replay.Value<string>("requestHash"), command.RequestHash)) throw new RecruitmentDomainException("P13_OPERATION_HASH_MISMATCH"); JObject payload = (JObject)replay["resultPayload"]!;
                    return new RecruitmentOperationResult(command.OperationId, game.Revision, payload.Value<string>("resultCode"), payload["instanceId"]!.Type == JTokenType.Null ? null : payload.Value<string>("instanceId"), true);
                }
                if (command.ExpectedRevision != game.Revision) throw new RecruitmentDomainException("P13_SAVE_REVISION_CONFLICT");
                JObject draft = (JObject)current.DeepClone(); JObject result = mutation(draft, clock.UtcNow); string digest = Rfc8785Canonicalizer.ComputeSha256(result); ReconcileEconomyDigest(draft, command, digest); AppendJournal(draft, command, result, digest, clock.UtcNow);
                SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, command.ExpectedRevision, clock.UtcNow);
                if (!written.Success)
                {
                    string code = written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P13_SAVE_REVISION_CONFLICT" : "P13_SAVE_WRITE_FAILED";
                    string detail = written.ErrorCode + (written.Report.Issues.Count == 0 ? string.Empty : " | " + string.Join(" | ", written.Report.Issues));
                    throw new RecruitmentDomainException(code, detail);
                }
                game.SynchronizeCommittedDocument(written.Document); Changed?.Invoke(this, EventArgs.Empty); return new RecruitmentOperationResult(command.OperationId, game.Revision, result.Value<string>("resultCode"), result["instanceId"]!.Type == JTokenType.Null ? null : result.Value<string>("instanceId"), false);
            }
            finally { gate.Release(); }
        }

        private static JObject Result(RecruitmentCommand command, string code, string instanceId) => new JObject { ["operationId"] = command.OperationId.ToString("D"), ["resultCode"] = code, ["instanceId"] = instanceId == null ? JValue.CreateNull() : instanceId };
        private static JObject State(JObject document) => (JObject)document["payload"]!["recruitmentMockState"]!;
        private static int FacilityLevel(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_TAVERN").Value<int>("level");
        private static void RequireCapacity(JObject document) { if (document["payload"]!["mercenaries"]!.Count() >= document["payload"]!["kingdom"]!.Value<int>("ownedMercenaryLimit")) throw new RecruitmentDomainException("P13_OWNED_LIMIT_REACHED"); }

        private static void AppendHistory(JObject state, RecruitmentCommand command, string poolId, string poolType, string payment, long cost, JObject mercenary, JArray reasons, string receipt, DateTimeOffset now)
        {
            JArray values = (JArray)state["history"]!; if (values.Count >= 100) values.RemoveAt(0); long sequence = state.Value<long>("nextHistorySequence");
            values.Add(new JObject { ["sequence"] = sequence, ["operationId"] = command.OperationId.ToString("D"), ["poolId"] = poolId, ["poolType"] = poolType, ["paymentType"] = payment, ["count"] = 1, ["costAmount"] = cost, ["resultInstanceIds"] = new JArray(mercenary.Value<string>("instanceId")), ["resultGradeIds"] = new JArray(mercenary.Value<string>("gradeId")), ["guaranteeReasonIds"] = reasons.DeepClone(), ["serverReceiptId"] = receipt == null ? JValue.CreateNull() : receipt, ["createdAtUtc"] = Format(now) }); state["nextHistorySequence"] = sequence + 1;
        }
        private static void AppendEvent(JObject state, RecruitmentCommand command, string type, string subject, string code, DateTimeOffset now)
        {
            JArray values = (JArray)state["events"]!; if (values.Count >= 200) values.RemoveAt(0); long sequence = state.Value<long>("nextEventSequence"); values.Add(new JObject { ["sequence"] = sequence, ["eventType"] = type, ["operationId"] = command.OperationId.ToString("D"), ["subjectId"] = subject, ["resultCode"] = code, ["createdAtUtc"] = Format(now) }); state["nextEventSequence"] = sequence + 1;
        }
        private static void AppendJournal(JObject document, RecruitmentCommand command, JObject result, string digest, DateTimeOffset now)
        {
            string timestamp = Format(now); ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject { ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = command.OperationType, ["facilityJobType"] = null, ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp, ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest, ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = result.DeepClone() });
        }
        private static void AppendEconomy(JObject document, RecruitmentCommand command, string transaction, long delta, string productId, DateTimeOffset now)
        {
            JObject ledger = (JObject)document["payload"]!["economy"]!["store"]!["ledger"]!; JArray entries = (JArray)ledger["entries"]!; if (entries.Count >= 200) entries.RemoveAt(0); long sequence = ledger.Value<long>("nextSequence");
            var lines = new JArray(new JObject { ["lineNo"] = 1, ["productKind"] = "SERVICE", ["productId"] = productId, ["quantity"] = 1, ["unitPrice"] = Math.Abs(delta), ["lineTotal"] = Math.Abs(delta), ["stockDelta"] = 0 });
            entries.Add(new JObject { ["sequence"] = sequence, ["operationId"] = command.OperationId.ToString("D"), ["transactionType"] = transaction, ["actorMercenaryInstanceId"] = null, ["reasonCode"] = transaction, ["personalGoldDelta"] = 0, ["kingdomGoldDelta"] = delta, ["stockVersionAfter"] = document["payload"]!["economy"]!["store"]!.Value<long>("stockVersion"), ["committedAtUtc"] = Format(now), ["requestHash"] = command.RequestHash, ["resultDigest"] = new string('0', 64), ["lines"] = lines }); ledger["nextSequence"] = sequence + 1;
        }
        private static void ReconcileEconomyDigest(JObject document, RecruitmentCommand command, string digest)
        {
            JObject entry = document["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Children<JObject>()
                .SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
            if (entry != null) entry["resultDigest"] = digest;
        }
        private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static bool FixedTimeEquals(string left, string right) { if (left == null || right == null || left.Length != right.Length) return false; int difference = 0; for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0; }
        private void EnsureReady() { if (!IsBootstrapped) throw new RecruitmentDomainException("P13_NOT_BOOTSTRAPPED"); }
        public void Shutdown() { Changed = null; IsBootstrapped = false; gateway = null; generator = null; catalog = null; roster = null; game = null; content = null; save = null; gate.Dispose(); }
    }
}
