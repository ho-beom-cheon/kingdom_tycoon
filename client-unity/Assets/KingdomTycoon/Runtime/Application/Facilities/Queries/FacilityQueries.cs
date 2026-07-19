using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure.Facilities;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Facilities.Queries
{
    public sealed class KingdomScreenDto
    {
        public KingdomScreenDto(string contentVersion, long saveRevision, string stageId, long kingdomGold, long freePremium, long paidPremium, bool recovered, bool offline, IReadOnlyList<FacilityWorldDto> facilities)
        { ContentVersion = contentVersion; SaveRevision = saveRevision; StageId = stageId; KingdomGold = kingdomGold; FreePremium = freePremium; PaidPremium = paidPremium; Recovered = recovered; Offline = offline; Facilities = facilities; }
        public string ContentVersion { get; }
        public long SaveRevision { get; }
        public string StageId { get; }
        public long KingdomGold { get; }
        public long FreePremium { get; }
        public long PaidPremium { get; }
        public bool Recovered { get; }
        public bool Offline { get; }
        public IReadOnlyList<FacilityWorldDto> Facilities { get; }
    }

    public sealed class FacilityWorldDto
    {
        public FacilityWorldDto(string facilityId, string name, string state, int level, string stopReason, int remainingSeconds, string assetAddress, Guid? jobOperationId)
        { FacilityId = facilityId; Name = name; State = state; Level = level; StopReason = stopReason; RemainingSeconds = remainingSeconds; AssetAddress = assetAddress; JobOperationId = jobOperationId; }
        public string FacilityId { get; }
        public string Name { get; }
        public string State { get; }
        public int Level { get; }
        public string StopReason { get; }
        public int RemainingSeconds { get; }
        public string AssetAddress { get; }
        public Guid? JobOperationId { get; }
    }

    public sealed class CostItemDto
    {
        public CostItemDto(string itemId, string name, long required, long owned) { ItemId = itemId; Name = name; Required = required; Owned = owned; }
        public string ItemId { get; }
        public string Name { get; }
        public long Required { get; }
        public long Owned { get; }
    }

    public sealed class NpcCandidateDto
    {
        public NpcCandidateDto(Guid instanceId, string professionId, bool assigned, bool working) { InstanceId = instanceId; ProfessionId = professionId; Assigned = assigned; Working = working; }
        public Guid InstanceId { get; }
        public string ProfessionId { get; }
        public bool Assigned { get; }
        public bool Working { get; }
    }

    public sealed class FacilityDetailDto
    {
        public FacilityDetailDto(FacilityWorldDto world, string operationMode, string requiredProfession, string effectText, int maxLevel, int nextLevel, long costGold, IReadOnlyList<CostItemDto> costItems, IReadOnlyList<NpcCandidateDto> npcCandidates, string primaryAction, string disabledReason)
        { World = world; OperationMode = operationMode; RequiredProfession = requiredProfession; EffectText = effectText; MaxLevel = maxLevel; NextLevel = nextLevel; CostGold = costGold; CostItems = costItems; NpcCandidates = npcCandidates; PrimaryAction = primaryAction; DisabledReason = disabledReason; }
        public FacilityWorldDto World { get; }
        public string OperationMode { get; }
        public string RequiredProfession { get; }
        public string EffectText { get; }
        public int MaxLevel { get; }
        public int NextLevel { get; }
        public long CostGold { get; }
        public IReadOnlyList<CostItemDto> CostItems { get; }
        public IReadOnlyList<NpcCandidateDto> NpcCandidates { get; }
        public string PrimaryAction { get; }
        public string DisabledReason { get; }
    }

    public sealed class GetKingdomScreenQuery
    {
        private static readonly string[] Order = { "FAC_TAVERN", "FAC_LODGE", "FAC_GUILD", "FAC_STORE", "FAC_BLACKSMITH", "FAC_ALCHEMY", "FAC_WAREHOUSE", "FAC_INFIRMARY" };
        private readonly FacilityGameService service;
        public GetKingdomScreenQuery(FacilityGameService service) => this.service = service ?? throw new ArgumentNullException(nameof(service));

        public KingdomScreenDto Execute()
        {
            service.NormalizeExpiredJobs();
            JObject document = service.Snapshot();
            JToken payload = document["payload"];
            JObject kingdom = (JObject)payload["kingdom"];
            JObject wallet = (JObject)payload["recruitmentMockState"]["premiumWalletCache"];
            Dictionary<string, JObject> facilities = payload["facilities"].Children<JObject>().ToDictionary(value => value.Value<string>("facilityId"), StringComparer.Ordinal);
            IReadOnlyList<FacilityWorldDto> worlds = Order.Select(id => Map(facilities[id])).ToArray();
            return new KingdomScreenDto(document.Value<string>("contentVersion"), document.Value<long>("revision"), kingdom.Value<string>("kingdomStageId"), kingdom.Value<long>("kingdomGold"), wallet.Value<long>("freePremium"), wallet.Value<long>("paidPremium"), service.WasRecovered, false, worlds);
        }

        private FacilityWorldDto Map(JObject facility)
        {
            string id = facility.Value<string>("facilityId");
            JObject job = facility["job"].Type == JTokenType.Null ? null : (JObject)facility["job"];
            int remaining = job == null || job.Value<string>("status") != "RUNNING" ? 0 : Math.Max(0, (int)Math.Ceiling((DateTimeOffset.Parse(job.Value<string>("finishesAtUtc")) - DateTimeOffset.UtcNow).TotalSeconds));
            Guid? operationId = job == null ? null : Guid.Parse(job.Value<string>("operationId"));
            string reason = facility.Value<string>("state") == "STOPPED" ? "NPC_REQUIRED" : "NONE";
            return new FacilityWorldDto(id, service.Catalog.Name(id), facility.Value<string>("state"), facility.Value<int>("level"), reason, remaining, service.Catalog.Address(id), operationId);
        }
    }

    public sealed class GetFacilityDetailQuery
    {
        private readonly FacilityGameService service;
        public GetFacilityDetailQuery(FacilityGameService service) => this.service = service ?? throw new ArgumentNullException(nameof(service));

        public FacilityDetailDto Execute(string facilityId)
        {
            KingdomScreenDto screen = new GetKingdomScreenQuery(service).Execute();
            FacilityWorldDto world = screen.Facilities.Single(value => value.FacilityId == facilityId);
            JObject document = service.Snapshot();
            JObject facility = document["payload"]["facilities"].Children<JObject>().Single(value => value.Value<string>("facilityId") == facilityId);
            FacilityDefinition definition = service.Catalog.GetFacility(facilityId);
            int next = world.State == "BUILDABLE" ? 1 : Math.Min(4, world.Level + 1);
            FacilityLevelDefinition nextDefinition = service.Catalog.GetLevel(facilityId, next);
            Dictionary<string, long> owned = document["payload"]["inventory"]["itemStacks"].Children<JObject>().ToDictionary(value => value.Value<string>("itemId"), value => value.Value<long>("quantity"), StringComparer.Ordinal);
            IReadOnlyList<CostItemDto> costs = nextDefinition.Materials.OrderBy(value => value.Key, StringComparer.Ordinal).Select(value => new CostItemDto(value.Key, service.Catalog.ItemName(value.Key), value.Value, owned.TryGetValue(value.Key, out long amount) ? amount : 0)).ToArray();
            IReadOnlyList<NpcCandidateDto> candidates = document["payload"]["managementNpcs"].Children<JObject>()
                .Where(value => !definition.IsManaged || value.Value<string>("professionId") == definition.RequiredProfessionId)
                .Select(value => new NpcCandidateDto(Guid.Parse(value.Value<string>("instanceId")), value.Value<string>("professionId"), value["assignedFacilityId"].Type != JTokenType.Null, value.Value<bool>("working"))).ToArray();
            string action = world.State switch { "BUILDABLE" => "BUILD", "BUILDING" or "UPGRADING" => facility["job"].Value<string>("status") == "READY" ? "CLAIM" : "WAIT", "ACTIVE" when definition.IsManaged => "UNASSIGN", "ACTIVE" => world.Level == 4 ? "MAX" : "UPGRADE", "STOPPED" => facility["assignedNpcInstanceId"].Type == JTokenType.Null ? "ASSIGN" : world.Level == 4 ? "MAX" : "UPGRADE", _ => "LOCKED" };
            string disabled = action switch { "WAIT" => "FACILITY_JOB_NOT_READY", "MAX" => "FACILITY_LEVEL_MAX", "LOCKED" => "FACILITY_LOCKED", _ => null };
            FacilityLevelDefinition current = service.Catalog.GetLevel(facilityId, world.Level);
            return new FacilityDetailDto(world, definition.OperationMode, definition.RequiredProfessionId, service.Catalog.Text(current.EffectTextKey), 4, next, nextDefinition.Gold, costs, candidates, action, disabled);
        }
    }
}
