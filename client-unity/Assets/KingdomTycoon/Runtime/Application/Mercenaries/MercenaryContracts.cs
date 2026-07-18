using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Domain.Mercenaries;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Mercenaries
{
    public sealed class CreateMercenaryFromSnapshot
    {
        private readonly MercenaryInvariantValidator validator;
        private readonly IMercenaryCatalogRules catalog;
        public CreateMercenaryFromSnapshot(MercenaryInvariantValidator validator, IMercenaryCatalogRules catalog) { this.validator = validator; this.catalog = catalog; }

        public JObject ExecuteInternal(JObject document, JObject snapshot)
        {
            if (document == null || snapshot == null) throw new ArgumentNullException(document == null ? nameof(document) : nameof(snapshot));
            var draft = (JObject)document.DeepClone();
            JArray mercenaries = (JArray)draft["payload"]!["mercenaries"]!;
            int limit = draft["payload"]!["kingdom"]!.Value<int>("ownedMercenaryLimit");
            if (mercenaries.Count >= limit) throw new MercenaryDomainException("MERCENARY_OWNED_LIMIT_REACHED");
            string id = snapshot.Value<string>("instanceId");
            if (mercenaries.Children<JObject>().Any(value => value.Value<string>("instanceId") == id)) throw new MercenaryDomainException("SAVE_MERCENARY_ID_INVALID");
            mercenaries.Add(snapshot.DeepClone());
            draft["payload"]!["mercenaries"] = new JArray(mercenaries.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal));
            validator.Validate(draft, catalog);
            return draft;
        }
    }

    public sealed class MercenaryRosterQueryDto
    {
        public MercenaryRosterQueryDto(
            string search = "",
            IEnumerable<string> jobIds = null,
            IEnumerable<string> gradeIds = null,
            IEnumerable<string> rankIds = null,
            IEnumerable<string> stateIds = null,
            string activeFilter = "ALL",
            bool promotionReadyOnly = false,
            bool injuredOnly = false,
            string sortId = "DEFAULT")
        {
            Search = search ?? string.Empty;
            JobIds = (jobIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            GradeIds = (gradeIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            RankIds = (rankIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            StateIds = (stateIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            ActiveFilter = activeFilter ?? "ALL";
            PromotionReadyOnly = promotionReadyOnly;
            InjuredOnly = injuredOnly;
            SortId = sortId ?? "DEFAULT";
        }

        public string Search { get; }
        public IReadOnlyList<string> JobIds { get; }
        public IReadOnlyList<string> GradeIds { get; }
        public IReadOnlyList<string> RankIds { get; }
        public IReadOnlyList<string> StateIds { get; }
        public string ActiveFilter { get; }
        public bool PromotionReadyOnly { get; }
        public bool InjuredOnly { get; }
        public string SortId { get; }
    }

    public sealed class MercenaryCardDto
    {
        public MercenaryCardDto(Mercenary value, IMercenaryCatalogRules catalog)
        {
            InstanceId = value.InstanceId;
            DisplayName = value.DisplayName;
            JobId = value.JobId;
            JobName = catalog.Localize(value.JobId);
            GradeId = value.GradeId;
            GradeName = catalog.Localize(value.GradeId);
            RankId = value.RankId;
            RankName = catalog.Localize(value.RankId);
            Level = value.Level;
            Active = value.Active;
            AutonomyState = value.AutonomyState;
            ReasonCode = value.ReasonCode;
            PromotionReady = value.PromotionStatus == "READY" || value.AutonomyState == "PROMOTION_READY";
            Injured = value.AutonomyState == "INJURED";
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string JobName { get; }
        public string GradeId { get; }
        public string GradeName { get; }
        public string RankId { get; }
        public string RankName { get; }
        public int Level { get; }
        public bool Active { get; }
        public string AutonomyState { get; }
        public string ReasonCode { get; }
        public bool PromotionReady { get; }
        public bool Injured { get; }
    }

    public sealed class MercenaryRosterResultDto
    {
        public MercenaryRosterResultDto(int totalOwned, int totalActive, int ownedLimit, int activeLimit, IReadOnlyList<MercenaryCardDto> cards)
        {
            TotalOwned = totalOwned;
            TotalActive = totalActive;
            OwnedLimit = ownedLimit;
            ActiveLimit = activeLimit;
            Cards = cards ?? Array.Empty<MercenaryCardDto>();
        }

        public int TotalOwned { get; }
        public int TotalActive { get; }
        public int OwnedLimit { get; }
        public int ActiveLimit { get; }
        public int FilteredCount => Cards.Count;
        public IReadOnlyList<MercenaryCardDto> Cards { get; }
    }

    public sealed class MercenaryDetailDto
    {
        public MercenaryDetailDto(Mercenary value, IMercenaryCatalogRules catalog, int rankMaxLevel)
        {
            InstanceId = value.InstanceId;
            DisplayName = value.DisplayName;
            JobId = value.JobId;
            JobName = catalog.Localize(value.JobId);
            GradeId = value.GradeId;
            GradeName = catalog.Localize(value.GradeId);
            RankId = value.RankId;
            RankName = catalog.Localize(value.RankId);
            RankMaxLevel = rankMaxLevel;
            Level = value.Level;
            Exp = value.Exp;
            PersonalityName = catalog.Localize(value.PersonalityId);
            TraitNames = value.TraitIds.Select(catalog.Localize).ToArray();
            PersonalGold = value.PersonalGold;
            Contribution = value.Contribution;
            Active = value.Active;
            AutonomyState = value.AutonomyState;
            ReasonCode = value.ReasonCode;
            CurrentRegionId = value.CurrentRegionId;
            PromotionStatus = value.PromotionStatus;
            EquipmentSlots = new Dictionary<string, string>(value.EquipmentSlots, StringComparer.Ordinal);
            PotionStacks = new Dictionary<string, long>(value.PotionStacks, StringComparer.Ordinal);
            Records = new Dictionary<string, long>(value.Records, StringComparer.Ordinal);
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string JobName { get; }
        public string GradeId { get; }
        public string GradeName { get; }
        public string RankId { get; }
        public string RankName { get; }
        public int RankMaxLevel { get; }
        public int Level { get; }
        public long Exp { get; }
        public string PersonalityName { get; }
        public IReadOnlyList<string> TraitNames { get; }
        public long PersonalGold { get; }
        public long Contribution { get; }
        public bool Active { get; }
        public string AutonomyState { get; }
        public string ReasonCode { get; }
        public string CurrentRegionId { get; }
        public string PromotionStatus { get; }
        public IReadOnlyDictionary<string, string> EquipmentSlots { get; }
        public IReadOnlyDictionary<string, long> PotionStacks { get; }
        public IReadOnlyDictionary<string, long> Records { get; }
    }

    public sealed class GetMercenaryRosterQuery
    {
        public MercenaryRosterResultDto Execute(IReadOnlyList<Mercenary> source, MercenaryRosterQueryDto input, IMercenaryCatalogRules catalog, int ownedLimit, int activeLimit)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            input ??= new MercenaryRosterQueryDto();
            var jobs = new HashSet<string>(input.JobIds, StringComparer.Ordinal);
            var grades = new HashSet<string>(input.GradeIds, StringComparer.Ordinal);
            var ranks = new HashSet<string>(input.RankIds, StringComparer.Ordinal);
            var states = new HashSet<string>(input.StateIds, StringComparer.Ordinal);
            Mercenary[] filtered = source.Where(value => MercenaryRosterFilter.Matches(value, input.Search, jobs, grades, ranks, states, input.ActiveFilter, input.PromotionReadyOnly, input.InjuredOnly)).ToArray();
            Array.Sort(filtered, new MercenaryRosterComparer(input.SortId, catalog));
            return new MercenaryRosterResultDto(source.Count, source.Count(value => value.Active), ownedLimit, activeLimit, filtered.Select(value => new MercenaryCardDto(value, catalog)).ToArray());
        }
    }

    public sealed class GetMercenaryDetailQuery
    {
        public MercenaryDetailDto Execute(IReadOnlyList<Mercenary> source, string instanceId, IMercenaryCatalogRules catalog)
        {
            Mercenary value = source?.SingleOrDefault(item => item.InstanceId == instanceId) ?? throw new MercenaryDomainException("MERCENARY_NOT_FOUND");
            return new MercenaryDetailDto(value, catalog, catalog.RankMaxLevel(value.RankId));
        }
    }
}

namespace KingdomTycoon.Application.Mercenaries.Commands
{
    public interface IMercenaryUnitOfWork
    {
        long Revision { get; }
        MercenaryOperationResult SetActive(SetMercenaryActiveCommand command);
    }

    public interface IMercenaryRequestHasher
    {
        string ComputeHash(SetMercenaryActiveCommand command);
    }

    public sealed class SetMercenaryActiveCommand
    {
        public SetMercenaryActiveCommand(Guid operationId, string requestHash, long expectedRevision, Guid mercenaryInstanceId, bool desiredActive)
        {
            OperationId = operationId;
            RequestHash = requestHash;
            ExpectedRevision = expectedRevision;
            MercenaryInstanceId = mercenaryInstanceId;
            DesiredActive = desiredActive;
        }
        public Guid OperationId { get; }
        public string RequestHash { get; }
        public long ExpectedRevision { get; }
        public Guid MercenaryInstanceId { get; }
        public bool DesiredActive { get; }
        public JObject ToJson() => new()
        {
            ["commandType"] = "SET_MERCENARY_ACTIVE",
            ["operationId"] = OperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryInstanceId.ToString("D"),
            ["desiredActive"] = DesiredActive
        };
    }

    public sealed class MercenaryOperationRequestFactory
    {
        private readonly IUuidV7Provider ids;
        private readonly IMercenaryRequestHasher hasher;
        public MercenaryOperationRequestFactory(IUuidV7Provider ids, IMercenaryRequestHasher hasher) { this.ids = ids; this.hasher = hasher; }
        public SetMercenaryActiveCommand CreateSetActive(long revision, Guid id, bool desired)
        {
            Guid operationId = ids.NewId();
            var draft = new SetMercenaryActiveCommand(operationId, null, revision, id, desired);
            return new SetMercenaryActiveCommand(operationId, hasher.ComputeHash(draft), revision, id, desired);
        }
    }

    public sealed class MercenaryOperationResult
    {
        public MercenaryOperationResult(Guid operationId, long revision, string instanceId, bool active, bool replayed)
        { OperationId = operationId; Revision = revision; InstanceId = instanceId; Active = active; Replayed = replayed; }
        public Guid OperationId { get; }
        public long Revision { get; }
        public string InstanceId { get; }
        public bool Active { get; }
        public bool Replayed { get; }
    }

    public sealed class SetMercenaryActiveHandler
    {
        private readonly IMercenaryUnitOfWork unitOfWork;
        public SetMercenaryActiveHandler(IMercenaryUnitOfWork unitOfWork) => this.unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        public MercenaryOperationResult Handle(SetMercenaryActiveCommand command) => unitOfWork.SetActive(command);
    }
}
