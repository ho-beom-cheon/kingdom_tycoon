using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Combat;
using KingdomTycoon.Application.Economy;
using KingdomTycoon.Domain.Economy;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Economy
{
    public sealed class P08EconomyCatalog
    {
        public sealed class LevelRule
        {
            public int Level { get; set; }
            public int MaxTier { get; set; }
            public int MaxQualityOrder { get; set; }
            public bool BossAllowed { get; set; }
            public bool PolicyChangeAllowed { get; set; }
            public int MaxStackLines { get; set; }
            public int MaxEquipmentLines { get; set; }
        }

        public sealed class PriceRule
        {
            public string Kind { get; set; }
            public string ProductId { get; set; }
            public int AcquireBps { get; set; }
            public long CustomerBase { get; set; }
            public int MarkupBps { get; set; }
            public long Minimum { get; set; }
        }

        public sealed class SupplyRule
        {
            public string Kind { get; set; }
            public string ProductId { get; set; }
            public int TargetQuantity { get; set; }
            public int MinimumLevel { get; set; }
            public int RefreshBuyCount { get; set; }
        }

        public sealed class PurchaseRule
        {
            public string PersonalityId { get; set; }
            public int ThresholdBps { get; set; }
            public long ReserveGold { get; set; }
            public int PotionTarget { get; set; }
            public int PotionMax { get; set; }
            public int EquipmentMax { get; set; }
            public string Priority { get; set; }
        }

        private readonly Dictionary<string, PricePolicy> policies;
        private readonly Dictionary<int, LevelRule> levels;
        private readonly Dictionary<string, PriceRule> prices;
        private readonly Dictionary<string, SupplyRule> supplies;
        private readonly Dictionary<string, PurchaseRule> purchaseRules;
        private readonly Dictionary<string, EquipmentDefinition> equipment;
        private readonly Dictionary<string, int> qualityBps;
        private readonly Dictionary<string, int> qualityOrder;
        private readonly Dictionary<string, long> itemPrices;
        private readonly Dictionary<string, string> names;
        private readonly HashSet<string> equipmentEligibility;
        private readonly Dictionary<int, int> inventoryEquipmentCapacity;

        public P08EconomyCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion or CompileTimeActiveContentVersionProvider.P15ContentVersion))
                throw new EconomyDomainException("P08_CONTENT_MISSING");
            policies = catalog.GetTable("pricing_policies.csv").Rows.Where(Enabled).Select(row => new PricePolicy(row["policy_id"], Int(row, "multiplier_bps"), Int(row, "min_store_level"))).ToDictionary(value => value.Id, StringComparer.Ordinal);
            levels = catalog.GetTable("store_level_rules.csv").Rows.Where(Enabled).Select(row => new LevelRule
            {
                Level = Int(row, "store_level"), MaxTier = Int(row, "max_tier"), MaxQualityOrder = Int(row, "max_quality_order"),
                BossAllowed = Bool(row, "boss_allowed"), PolicyChangeAllowed = Bool(row, "policy_change_allowed"),
                MaxStackLines = Int(row, "max_stack_lines"), MaxEquipmentLines = Int(row, "max_equipment_lines")
            }).ToDictionary(value => value.Level);
            prices = catalog.GetTable("store_price_rules.csv").Rows.Where(Enabled).Select(row => new PriceRule
            {
                Kind = row["product_kind"], ProductId = row["product_id"], AcquireBps = Int(row, "acquire_rate_bps"),
                CustomerBase = Long(row, "customer_base_price"), MarkupBps = Int(row, "customer_markup_bps"), Minimum = Long(row, "min_price")
            }).ToDictionary(value => value.Kind + "\0" + value.ProductId, StringComparer.Ordinal);
            supplies = catalog.GetTable("store_supply_rules.csv").Rows.Where(Enabled).Select(row => new SupplyRule
            {
                Kind = row["product_kind"], ProductId = row["product_id"], TargetQuantity = Int(row, "target_quantity"),
                MinimumLevel = Int(row, "min_store_level"), RefreshBuyCount = Int(row, "refresh_buy_count")
            }).ToDictionary(value => value.Kind + "\0" + value.ProductId, StringComparer.Ordinal);
            purchaseRules = catalog.GetTable("store_purchase_ai_rules.csv").Rows.Where(Enabled).Select(row => new PurchaseRule
            {
                PersonalityId = row["personality_id"], ThresholdBps = Int(row, "threshold_bps"), ReserveGold = Long(row, "reserve_gold"),
                PotionTarget = Int(row, "potion_target"), PotionMax = Int(row, "potion_max_per_cycle"), EquipmentMax = Int(row, "equipment_max_per_cycle"), Priority = row["equipment_priority"]
            }).ToDictionary(value => value.PersonalityId, StringComparer.Ordinal);
            equipment = catalog.GetTable("equipment_templates.csv").Rows.Where(Enabled).Select(row => new EquipmentDefinition(
                row["equipment_template_id"], Int(row, "tier"), row["slot"], row["profile"], Int(row, "base_power"), row["source"]))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            qualityBps = catalog.GetTable("equipment_qualities.csv").Rows.Where(Enabled).ToDictionary(
                row => row["quality_id"], row => checked((int)(decimal.Parse(row["stat_multiplier"], CultureInfo.InvariantCulture) * 10_000m)), StringComparer.Ordinal);
            qualityOrder = catalog.GetTable("equipment_qualities.csv").Rows.Where(Enabled).OrderBy(row => decimal.Parse(row["stat_multiplier"], CultureInfo.InvariantCulture))
                .Select((row, index) => (row["quality_id"], Order: index + 1)).ToDictionary(value => value.Item1, value => value.Order, StringComparer.Ordinal);
            itemPrices = catalog.GetTable("items.csv").Rows.Where(Enabled).ToDictionary(row => row["item_id"], row => Long(row, "sell_price"), StringComparer.Ordinal);
            names = catalog.GetTable("localizations.csv").Rows.Where(row => Enabled(row) && row["locale"] == "ko-KR").ToDictionary(row => row["text_key"], row => row["text_value"], StringComparer.Ordinal);
            foreach (IReadOnlyDictionary<string, string> row in catalog.GetTable("items.csv").Rows.Where(Enabled)) names[row["item_id"]] = names.GetValueOrDefault(row["name_text_key"], row["item_id"]);
            foreach (IReadOnlyDictionary<string, string> row in catalog.GetTable("potions.csv").Rows.Where(Enabled)) names[row["potion_id"]] = names.GetValueOrDefault(row["name_text_key"], row["potion_id"]);
            foreach (IReadOnlyDictionary<string, string> row in catalog.GetTable("equipment_templates.csv").Rows.Where(Enabled)) names[row["equipment_template_id"]] = names.GetValueOrDefault(row["name_text_key"], row["equipment_template_id"]);
            equipmentEligibility = catalog.GetTable("equipment_job_eligibility.csv").Rows.Where(Enabled).Select(row => row["equipment_template_id"] + "\0" + row["job_id"]).ToHashSet(StringComparer.Ordinal);
            inventoryEquipmentCapacity = catalog.GetTable("inventory_capacity_rules.csv").Rows.Where(Enabled).ToDictionary(row => Int(row, "warehouse_level"), row => Int(row, "equipment_slots"));
            if (policies.Count != 3 || levels.Count != 4 || supplies.Count != 6 || purchaseRules.Count != 6)
                throw new EconomyDomainException("P08_CONTENT_MISSING");
        }

        public PricePolicy Policy(string id) => policies.TryGetValue(id, out PricePolicy value) ? value : throw new EconomyDomainException("P08_POLICY_INVALID");
        public LevelRule Level(int level) => levels.TryGetValue(level, out LevelRule value) ? value : throw new EconomyDomainException("P08_STORE_LEVEL_REQUIRED");
        public PurchaseRule Purchase(string personalityId) => purchaseRules.TryGetValue(personalityId, out PurchaseRule value) ? value : throw new EconomyDomainException("P08_CONTENT_MISSING");
        public IEnumerable<SupplyRule> SupplyRules => supplies.Values.OrderBy(value => value.Kind, StringComparer.Ordinal).ThenBy(value => value.ProductId, StringComparer.Ordinal);
        public int SupplyRefreshBuyCount => supplies.Values.Min(value => value.RefreshBuyCount);
        public EquipmentDefinition Equipment(string id) => equipment.TryGetValue(id, out EquipmentDefinition value) ? value : throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        public int QualityBps(string id) => qualityBps.TryGetValue(id, out int value) ? value : throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        public int QualityOrder(string id) => qualityOrder.TryGetValue(id, out int value) ? value : throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        public bool IsEligible(string equipmentId, string jobId) => equipmentEligibility.Contains(equipmentId + "\0" + jobId);
        public long ItemPrice(string id) => itemPrices.TryGetValue(id, out long value) ? value : throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        public string Name(string id) => names.GetValueOrDefault(id, id);
        public int InventoryEquipmentCapacity(int level) => inventoryEquipmentCapacity.GetValueOrDefault(level, 0);
        public long PotionBase(string id) => Price("POTION", id).CustomerBase;
        public PriceRule Price(string kind, string id) => prices.GetValueOrDefault(kind + "\0" + id) ?? prices.GetValueOrDefault(kind + "\0*") ?? throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        public EquipmentInstance Instance(JObject value)
        {
            JToken token = value["refineOption"];
            string optionId = token == null || token.Type == JTokenType.Null ? null : token.Type == JTokenType.Object ? token.Value<string>("optionId") : token.Value<string>();
            int optionValue = token == null || token.Type == JTokenType.Null ? 0 : token.Type == JTokenType.Object ? token.Value<int>("value") : 1000;
            return new EquipmentInstance(value.Value<string>("instanceId"), Equipment(value.Value<string>("equipmentTemplateId")), value.Value<string>("qualityId"), QualityBps(value.Value<string>("qualityId")), value.Value<int>("enhancementLevel"), optionId, optionValue, value.Value<bool>("locked"));
        }
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static bool Bool(IReadOnlyDictionary<string, string> row, string key) => row[key] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public sealed class EconomyRequestHasher
    {
        public string Compute(EconomyCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class EconomyGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private readonly SemaphoreSlim commitGate = new(1, 1);
        private readonly StorePricingService pricing = new();
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private CombatGameService combat;
        private P08EconomyCatalog catalog;

        public EconomyGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 67;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<StorefrontDto> Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>();
            try { combat = services.Get<CombatGameService>(); combat.HuntSettled += OnHuntSettled; }
            catch (KeyNotFoundException) { combat = null; }
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion or CompileTimeActiveContentVersionProvider.P15ContentVersion))
                throw new EconomyDomainException("P08_CONTENT_MISSING");
            catalog = new P08EconomyCatalog(content.Catalog);
            IsBootstrapped = true;
        }

        public StorefrontDto GetStorefront(string mercenaryId = null)
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject mercenary = SelectMercenary(document, mercenaryId);
            JObject store = Store(document); StoreAvailability availability = Availability(document);
            PricePolicy policy = catalog.Policy(document["payload"]!["kingdom"]!.Value<string>("pricingPolicy"));
            var products = new List<StoreProductDto>();
            foreach (IGrouping<(string Kind, string Id), JObject> group in store["stackLines"].Children<JObject>().GroupBy(value => (value.Value<string>("productKind"), value.Value<string>("productId"))))
            {
                long quantity = group.Sum(value => value.Value<long>("quantity"));
                long unitPrice = CustomerPrice(group.Key.Kind, group.Key.Id, null, policy.MultiplierBps);
                products.Add(new StoreProductDto(group.Key.Kind, group.Key.Id, catalog.Name(group.Key.Id), quantity, unitPrice, string.Join("+", group.Select(value => value.Value<string>("sourceType")).Distinct()), 0, null));
            }
            foreach (JObject equipment in store["equipment"].Children<JObject>())
            {
                EquipmentInstance instance = catalog.Instance(equipment);
                products.Add(new StoreProductDto("EQUIPMENT", instance.InstanceId, catalog.Name(instance.Definition.Id), 1,
                    CustomerPrice("EQUIPMENT", instance.InstanceId, equipment, policy.MultiplierBps), equipment.Value<string>("sourceType"), instance.Definition.Tier, instance.QualityId));
            }
            return new StorefrontDto(document.Value<long>("revision"), store.Value<long>("stockVersion"), availability.State.ToString().ToUpperInvariant(), availability.Reason,
                availability.Level, policy.Id, document["payload"]!["kingdom"]!.Value<long>("kingdomGold"), mercenary?.Value<string>("instanceId"), mercenary?.Value<long>("personalGold") ?? 0,
                products.OrderBy(value => value.Kind, StringComparer.Ordinal).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray(), store["ledger"]!["entries"]!.Count());
        }

        public string GetQuoteContextHash(string mercenaryId, IEnumerable<StoreCommandLine> lines)
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject store = Store(document); string policyId = document["payload"]!["kingdom"]!.Value<string>("pricingPolicy");
            return Rfc8785Canonicalizer.ComputeSha256(new JObject
            {
                ["contentVersion"] = document.Value<string>("contentVersion"), ["saveRevision"] = game.Revision,
                ["stockVersion"] = store.Value<long>("stockVersion"), ["policyId"] = policyId,
                ["facilityStateHash"] = FacilityStateHash(document), ["mercenaryInstanceId"] = mercenaryId,
                ["lines"] = new JArray((lines ?? Array.Empty<StoreCommandLine>()).Select(value => value.ToJson(true)))
            });
        }

        public EconomyOperationResult SetPricingPolicy(SetPricingPolicyCommand command) => Execute(command, draft =>
        {
            StoreAvailability availability = RequireOpen(draft); P08EconomyCatalog.LevelRule level = catalog.Level(availability.Level);
            PricePolicy policy = catalog.Policy(command.PolicyId);
            if (!level.PolicyChangeAllowed || policy.MinimumStoreLevel > availability.Level) throw new EconomyDomainException("P08_STORE_LEVEL_REQUIRED");
            string old = draft["payload"]!["kingdom"]!.Value<string>("pricingPolicy");
            draft["payload"]!["kingdom"]!["pricingPolicy"] = policy.Id;
            return TxMutation.Policy(old, policy.Id);
        });

        public EconomyOperationResult SellToStore(SellToStoreCommand command) => Execute(command, draft =>
        {
            StoreAvailability availability = RequireOpen(draft); JObject mercenary = FindMercenary(draft, command.MercenaryId); RequireTown(mercenary);
            ValidateLines(command.Lines, 32, false); JObject store = Store(draft); P08EconomyCatalog.LevelRule limits = catalog.Level(availability.Level);
            var resultLines = new JArray(); long total = 0;
            foreach (StoreCommandLine line in command.Lines)
            {
                if (line.Kind == "ITEM")
                {
                    JObject inventoryLine = draft["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == line.Id) ?? throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
                    if (inventoryLine.Value<long>("quantity") < line.Quantity) throw new EconomyDomainException("P08_QUANTITY_INVALID");
                    long unit = catalog.ItemPrice(line.Id); long lineTotal = StorePricingService.MultiplyChecked(unit, line.Quantity);
                    inventoryLine["quantity"] = inventoryLine.Value<long>("quantity") - line.Quantity; if (inventoryLine.Value<long>("quantity") == 0) inventoryLine.Remove();
                    AddStack(store, "ITEM", line.Id, line.Quantity, "MERCENARY_SALE", command.OperationId, limits.MaxStackLines);
                    total = checked(total + lineTotal); resultLines.Add(ResultLine(line, unit, lineTotal));
                }
                else if (line.Kind == "EQUIPMENT")
                {
                    if (line.Quantity != 1) throw new EconomyDomainException("P08_QUANTITY_INVALID");
                    JObject stored = draft["payload"]!["inventory"]!["equipment"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == line.Id) ?? throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
                    EquipmentInstance item = catalog.Instance(stored); RequireSellableEquipment(draft, stored, item, availability.Level);
                    if (store["equipment"]!.Count() >= limits.MaxEquipmentLines) throw new EconomyDomainException("P08_DESTINATION_CAPACITY_EXCEEDED");
                    long unit = pricing.EquipmentAcquire(EquipmentMath.EffectivePower(item));
                    stored.Remove(); JObject stock = (JObject)stored.DeepClone(); stock["locked"] = false; stock["equippedByMercenaryInstanceId"] = null;
                    stock["stockAcquiredAtUtc"] = FormatUtc(clock.UtcNow); stock["stockAcquiredOperationId"] = command.OperationId.ToString("D"); stock["sourceType"] = "MERCENARY_SALE";
                    ((JArray)store["equipment"]!).Add(stock); total = checked(total + unit); resultLines.Add(ResultLine(line, unit, unit));
                }
                else throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
            }
            mercenary["personalGold"] = new Money(mercenary.Value<long>("personalGold")).Add(total).Value;
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1);
            return TxMutation.Sale(command.MercenaryId, total, store.Value<long>("stockVersion"), resultLines);
        });

        public EconomyOperationResult BuyFromStore(BuyFromStoreCommand command) => Execute(command, draft =>
        {
            StoreAvailability availability = RequireOpen(draft); JObject mercenary = FindMercenary(draft, command.MercenaryId); RequireTown(mercenary);
            ValidateLines(command.Lines, 16, true);
            string expectedContext = QuoteContextHash(draft, command.MercenaryId, command.Lines);
            if (!FixedTimeEquals(command.QuoteContextHash, expectedContext)) throw new EconomyDomainException("P08_QUOTE_STALE");
            JObject store = Store(draft); PricePolicy policy = catalog.Policy(draft["payload"]!["kingdom"]!.Value<string>("pricingPolicy"));
            var resultLines = new JArray(); long total = 0;
            foreach (StoreCommandLine line in command.Lines)
            {
                if (line.Kind == "POTION")
                {
                    long unit = CustomerPrice("POTION", line.Id, null, policy.MultiplierBps); RequireQuoted(line, unit);
                    RemoveStack(store, "POTION", line.Id, line.Quantity); AddPotion(mercenary, line.Id, line.Quantity);
                    long lineTotal = StorePricingService.MultiplyChecked(unit, line.Quantity); total = checked(total + lineTotal); resultLines.Add(ResultLine(line, unit, lineTotal));
                }
                else if (line.Kind == "EQUIPMENT")
                {
                    if (line.Quantity != 1) throw new EconomyDomainException("P08_QUANTITY_INVALID");
                    JObject stock = store["equipment"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == line.Id) ?? throw new EconomyDomainException("P08_STOCK_EMPTY");
                    EquipmentInstance item = catalog.Instance(stock); RequireEquipmentLevel(item, availability.Level);
                    if (!catalog.IsEligible(item.Definition.Id, mercenary.Value<string>("jobId"))) throw new EconomyDomainException("P08_EQUIPMENT_INELIGIBLE");
                    long unit = CustomerPrice("EQUIPMENT", line.Id, stock, policy.MultiplierBps); RequireQuoted(line, unit);
                    stock.Remove(); JObject owned = (JObject)stock.DeepClone(); owned.Remove("stockAcquiredAtUtc"); owned.Remove("stockAcquiredOperationId"); owned.Remove("sourceType");
                    ((JArray)draft["payload"]!["inventory"]!["equipment"]!).Add(owned); MaybeEquip(draft, mercenary, owned, item, command.EquipIfUpgrade);
                    total = checked(total + unit); resultLines.Add(ResultLine(line, unit, unit));
                }
                else throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
            }
            Money personal = new(mercenary.Value<long>("personalGold")); mercenary["personalGold"] = personal.Subtract(total).Value;
            JObject kingdom = (JObject)draft["payload"]!["kingdom"]!; kingdom["kingdomGold"] = new Money(kingdom.Value<long>("kingdomGold")).Add(total).Value;
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1);
            JObject supply = (JObject)store["supplyState"]!; supply["buyCountSinceRefresh"] = Math.Min(8, supply.Value<int>("buyCountSinceRefresh") + 1);
            RequireInventoryCapacity(draft);
            return TxMutation.Purchase(command.MercenaryId, -total, total, store.Value<long>("stockVersion"), resultLines);
        });

        public EconomyOperationResult RefreshSystemStoreSupply(RefreshSystemStoreSupplyCommand command) => Execute(command, draft =>
        {
            StoreAvailability availability = RequireOpen(draft); JObject store = Store(draft); JObject state = (JObject)store["supplyState"]!;
            if (state.Value<string>("mode") != "SYSTEM_SUPPLY") throw new EconomyDomainException("P08_SUPPLY_ALREADY_APPLIED");
            if (command.TargetEpoch != state.Value<long>("epoch")) throw new EconomyDomainException("P08_SUPPLY_ALREADY_APPLIED");
            P08EconomyCatalog.LevelRule limits = catalog.Level(availability.Level); var added = new JArray();
            foreach (P08EconomyCatalog.SupplyRule rule in catalog.SupplyRules.Where(value => value.MinimumLevel <= availability.Level))
            {
                if (rule.Kind == "POTION")
                {
                    long current = store["stackLines"]!.Children<JObject>().Where(value => value.Value<string>("productKind") == "POTION" && value.Value<string>("productId") == rule.ProductId).Sum(value => value.Value<long>("quantity"));
                    long missing = Math.Max(0, rule.TargetQuantity - current);
                    if (missing > 0) { AddStack(store, "POTION", rule.ProductId, missing, "SYSTEM_SUPPLY", command.OperationId, limits.MaxStackLines); added.Add(AddedLine("POTION", rule.ProductId, missing)); }
                }
                else
                {
                    int current = store["equipment"]!.Children<JObject>().Count(value => value.Value<string>("equipmentTemplateId") == rule.ProductId && value.Value<string>("sourceType") == "SYSTEM_SUPPLY");
                    for (int index = current; index < rule.TargetQuantity; index++)
                    {
                        if (store["equipment"]!.Count() >= limits.MaxEquipmentLines) throw new EconomyDomainException("P08_DESTINATION_CAPACITY_EXCEEDED");
                        EquipmentDefinition definition = catalog.Equipment(rule.ProductId); string instanceId = DeterministicEquipmentId(command.OperationId, rule.ProductId, index);
                        ((JArray)store["equipment"]!).Add(new JObject
                        {
                            ["instanceId"] = instanceId, ["equipmentTemplateId"] = definition.Id, ["tier"] = definition.Tier,
                            ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0, ["refineOption"] = null,
                            ["locked"] = false, ["equippedByMercenaryInstanceId"] = null,
                            ["sourceContentVersion"] = draft.Value<string>("contentVersion"),
                            ["generationOperationId"] = command.OperationId.ToString("D"), ["stockAcquiredAtUtc"] = FormatUtc(clock.UtcNow),
                            ["stockAcquiredOperationId"] = command.OperationId.ToString("D"), ["sourceType"] = "SYSTEM_SUPPLY"
                        });
                    }
                    if (rule.TargetQuantity > current) added.Add(AddedLine("EQUIPMENT", rule.ProductId, rule.TargetQuantity - current));
                }
            }
            store["equipment"] = new JArray(store["equipment"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal));
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1); state["epoch"] = checked(state.Value<long>("epoch") + 1); state["buyCountSinceRefresh"] = 0; state["lastRefreshOperationId"] = command.OperationId.ToString("D");
            return TxMutation.Supply(state.Value<long>("epoch"), store.Value<long>("stockVersion"), added);
        });

        public EconomyOperationResult EnsureSystemSupply()
        {
            EnsureReady(); JObject document = game.Snapshot(); RequireOpen(document); JObject state = (JObject)Store(document)["supplyState"]!;
            if (state.Value<string>("mode") == "PRODUCTION_OWNED") return null;
            bool initial = state["lastRefreshOperationId"]!.Type == JTokenType.Null;
            bool refreshDue = state.Value<int>("buyCountSinceRefresh") >= catalog.SupplyRefreshBuyCount;
            if (!initial && !refreshDue) return null;
            long epoch = state.Value<long>("epoch");
            Guid operationId = Guid.Parse(DeterministicUuid($"SYSTEM_SUPPLY:{game.ActiveProfileId}:{epoch}"));
            var draft = new RefreshSystemStoreSupplyCommand(operationId, game.Revision, null, epoch);
            return RefreshSystemStoreSupply(new RefreshSystemStoreSupplyCommand(operationId, game.Revision, new EconomyRequestHasher().Compute(draft), epoch));
        }

        public int RunStoreAutonomyCycle(Guid cycleOperationId) => RunStoreAutonomyCycle(cycleOperationId, null);

        public int RunStoreAutonomyCycle(Guid cycleOperationId, IEnumerable<string> preferredMercenaryIds)
        {
            EnsureReady(); JObject cycleStart = game.Snapshot(); int replayed = ExistingCycleCommandCount(cycleStart, cycleOperationId);
            if (replayed > 0) return replayed;
            EnsureSystemSupply();
            cycleStart = game.Snapshot();
            int commands = 0;
            Dictionary<string, int> preference = (preferredMercenaryIds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)
                .Select((value, index) => (value, index)).ToDictionary(value => value.value, value => value.index, StringComparer.Ordinal);
            string[] mercenaryIds = cycleStart["payload"]!["mercenaries"]!.Children<JObject>()
                .Where(value => value["autonomy"]!.Value<string>("state") is "IDLE_TOWN" or "RETURN_TOWN" or "SELL_LOOT" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT")
                .OrderBy(value => preference.TryGetValue(value.Value<string>("instanceId"), out int order) ? order : int.MaxValue)
                .ThenBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal)
                .Select(value => value.Value<string>("instanceId")).ToArray();
            foreach (string mercenaryId in mercenaryIds)
            {
                if (commands >= 48) break;
                TryAutonomySale(cycleOperationId, mercenaryId, ref commands);
                if (commands >= 48) break;
                TryAutonomyPotionPurchase(cycleOperationId, mercenaryId, ref commands);
                if (commands >= 48) break;
                TryAutonomyEquipmentPurchase(cycleOperationId, mercenaryId, ref commands);
                if (commands < 48) EnsureSystemSupply();
            }
            return commands;
        }

        private static int ExistingCycleCommandCount(JObject document, Guid cycleOperationId)
        {
            HashSet<string> operationIds = document["payload"]!["operationJournal"]!.Children<JObject>().Select(value => value.Value<string>("operationId")).ToHashSet(StringComparer.Ordinal);
            int count = 0; while (count < 48 && operationIds.Contains(DeterministicOperationId(cycleOperationId, count).ToString("D"))) count++;
            return count;
        }

        private void TryAutonomySale(Guid cycleOperationId, string mercenaryId, ref int commands)
        {
            JObject document = game.Snapshot(); JObject policy = (JObject)document["payload"]!["kingdom"]!["inventoryPolicies"]!;
            if (!policy.Value<bool>("autoSellEnabled")) return;
            var lines = new List<StoreCommandLine>();
            foreach (JObject stack in document["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal))
                lines.Add(new StoreCommandLine("ITEM", stack.Value<string>("itemId"), stack.Value<long>("quantity")));
            int maxTier = policy.Value<int>("autoSellMaxTier"); HashSet<string> protectedQualities = policy["protectQualityIds"]!.Values<string>().ToHashSet(StringComparer.Ordinal);
            HashSet<string> discovered = policy["discoveredEquipmentTemplateIds"]!.Values<string>().ToHashSet(StringComparer.Ordinal);
            foreach (JObject stored in document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal))
            {
                EquipmentInstance item = catalog.Instance(stored);
                bool firstDiscoveryProtected = policy.Value<bool>("protectFirstDiscovery") && !discovered.Contains(item.Definition.Id);
                bool bossProtected = policy.Value<bool>("protectBossEquipment") && item.Definition.Source == "BOSS";
                if (!item.Locked && stored["equippedByMercenaryInstanceId"]!.Type == JTokenType.Null && item.Definition.Tier <= maxTier &&
                    !protectedQualities.Contains(item.QualityId) && !firstDiscoveryProtected && !bossProtected)
                    lines.Add(new StoreCommandLine("EQUIPMENT", item.InstanceId, 1));
            }
            if (lines.Count == 0) return;
            lines = lines.Take(32).ToList(); Guid operation = DeterministicOperationId(cycleOperationId, commands++); long revision = game.Revision;
            var draft = new SellToStoreCommand(operation, revision, null, mercenaryId, lines);
            SellToStore(new SellToStoreCommand(operation, revision, new EconomyRequestHasher().Compute(draft), mercenaryId, lines));
        }

        private void TryAutonomyPotionPurchase(Guid cycleOperationId, string mercenaryId, ref int commands)
        {
            JObject document = game.Snapshot(); JObject mercenary = FindMercenary(document, mercenaryId);
            P08EconomyCatalog.PurchaseRule rule = catalog.Purchase(mercenary.Value<string>("personalityId")); StorefrontDto storefront = GetStorefront(mercenaryId);
            long current = mercenary["potions"]!.Children<JObject>().Where(value => value.Value<string>("potionId") == "POT_HEAL_SMALL").Sum(value => value.Value<long>("quantity"));
            StoreProductDto potion = storefront.Products.FirstOrDefault(value => value.Kind == "POTION" && value.Id == "POT_HEAL_SMALL");
            if (potion == null || current >= rule.PotionTarget || !new StorePurchaseDecisionService().ShouldBuy(storefront.PersonalGold, potion.UnitPrice, rule.ThresholdBps, rule.ReserveGold)) return;
            long quantity = Math.Min(Math.Min(rule.PotionMax, rule.PotionTarget - current), potion.Quantity); if (quantity <= 0) return;
            Guid operation = DeterministicOperationId(cycleOperationId, commands++); long revision = game.Revision;
            var line = new StoreCommandLine("POTION", potion.Id, quantity, potion.UnitPrice); string quote = GetQuoteContextHash(mercenaryId, new[] { line });
            var draft = new BuyFromStoreCommand(operation, revision, null, quote, mercenaryId, false, new[] { line });
            BuyFromStore(new BuyFromStoreCommand(operation, revision, new EconomyRequestHasher().Compute(draft), quote, mercenaryId, false, new[] { line }));
        }

        private void TryAutonomyEquipmentPurchase(Guid cycleOperationId, string mercenaryId, ref int commands)
        {
            JObject document = game.Snapshot(); JObject mercenary = FindMercenary(document, mercenaryId); JObject policy = (JObject)document["payload"]!["kingdom"]!["inventoryPolicies"]!;
            P08EconomyCatalog.PurchaseRule rule = catalog.Purchase(mercenary.Value<string>("personalityId")); if (rule.EquipmentMax <= 0) return;
            StorefrontDto storefront = GetStorefront(mercenaryId); var decision = new StorePurchaseDecisionService();
            var candidates = new List<(string Id, long UpgradeScore, long Price, int QualityOrder, string Slot)>();
            foreach (JObject stock in Store(document)["equipment"]!.Children<JObject>())
            {
                EquipmentInstance item = catalog.Instance(stock); StoreProductDto product = storefront.Products.Single(value => value.Kind == "EQUIPMENT" && value.Id == item.InstanceId);
                if (!catalog.IsEligible(item.Definition.Id, mercenary.Value<string>("jobId")) || !decision.ShouldBuy(storefront.PersonalGold, product.UnitPrice, rule.ThresholdBps, rule.ReserveGold)) continue;
                string currentId = mercenary["equipmentSlots"]![item.Definition.Slot]!.Type == JTokenType.Null ? null : mercenary["equipmentSlots"]!.Value<string>(item.Definition.Slot);
                long currentPower = currentId == null ? 0 : EquipmentMath.EffectivePower(catalog.Instance(document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == currentId)));
                long newPower = EquipmentMath.EffectivePower(item); int threshold = policy.Value<int>("upgradeThresholdBps");
                if (currentPower > 0 && StorePricingService.MultiplyChecked(newPower, 10_000) < StorePricingService.MultiplyChecked(currentPower, 10_000 + threshold)) continue;
                candidates.Add((item.InstanceId, newPower - currentPower, product.UnitPrice, catalog.QualityOrder(item.QualityId), item.Definition.Slot));
            }
            if (candidates.Count == 0) return;
            IEnumerable<(string Id, long UpgradeScore, long Price, int QualityOrder, string Slot)> prioritized = candidates;
            if (rule.Priority == "WEAPON" && candidates.Any(value => value.Slot == "WEAPON")) prioritized = candidates.Where(value => value.Slot == "WEAPON");
            else if (rule.Priority == "DEFENSE" && candidates.Any(value => value.Slot != "WEAPON")) prioritized = candidates.Where(value => value.Slot != "WEAPON");
            string selected = decision.SelectEquipment(prioritized.Select(value => (value.Id, value.UpgradeScore, value.Price, value.QualityOrder)), rule.Priority);
            StoreProductDto productToBuy = storefront.Products.Single(value => value.Id == selected); Guid operation = DeterministicOperationId(cycleOperationId, commands++); long revision = game.Revision;
            var line = new StoreCommandLine("EQUIPMENT", selected, 1, productToBuy.UnitPrice); string quote = GetQuoteContextHash(mercenaryId, new[] { line });
            var draft = new BuyFromStoreCommand(operation, revision, null, quote, mercenaryId, true, new[] { line });
            BuyFromStore(new BuyFromStoreCommand(operation, revision, new EconomyRequestHasher().Compute(draft), quote, mercenaryId, true, new[] { line }));
        }

        public void Shutdown()
        {
            if (combat != null) combat.HuntSettled -= OnHuntSettled;
            Changed = null; combat = null; catalog = null; game = null; content = null; save = null; IsBootstrapped = false; commitGate.Dispose();
        }

        private EconomyOperationResult Execute(EconomyCommand command, Func<JObject, TxMutation> mutation)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new EconomyRequestHasher().Compute(command));
            commitGate.Wait();
            try
            {
                JObject current = game.Snapshot(); JObject replay = current["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
                if (replay != null)
                {
                    VerifyHash(command.RequestHash, replay.Value<string>("requestHash"));
                    JObject payload = replay["resultPayload"] as JObject;
                    return new EconomyOperationResult(command.OperationId, payload?.Value<long>("revisionBefore") ?? game.Revision, payload?.Value<long>("revisionAfter") ?? game.Revision,
                        replay.Value<string>("resultDigest"), true, payload?.Value<long?>("totalPersonalGoldDelta") ?? 0, payload?.Value<long?>("totalKingdomGoldDelta") ?? 0, payload?.Value<long?>("stockVersionAfter") ?? Store(current).Value<long>("stockVersion"));
                }
                if (command.ExpectedRevision != game.Revision) throw new EconomyDomainException("P08_SAVE_REVISION_CONFLICT");
                long before = game.Revision; JObject draft = (JObject)current.DeepClone(); TxMutation change = mutation(draft);
                if (!change.Changed) return new EconomyOperationResult(command.OperationId, before, before, change.ResultDigest, false, 0, 0, change.StockVersion);
                JObject result = change.BuildResult(command.OperationId, before); string digest = Rfc8785Canonicalizer.ComputeSha256(result); result["resultDigest"] = digest;
                AppendLedger(draft, command, change, digest); AppendJournal(draft, command, result, digest);
                SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow);
                if (!written.Success) throw new EconomyDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P08_SAVE_REVISION_CONFLICT" : "P08_TRANSACTION_INVARIANT_FAILED");
                game.SynchronizeCommittedDocument(written.Document); Changed?.Invoke(this, GetStorefront(change.MercenaryId));
                return new EconomyOperationResult(command.OperationId, before, game.Revision, digest, false, change.PersonalDelta, change.KingdomDelta, change.StockVersion);
            }
            finally { commitGate.Release(); }
        }

        private void AppendLedger(JObject document, EconomyCommand command, TxMutation change, string digest)
        {
            JObject ledger = (JObject)Store(document)["ledger"]!; JArray entries = (JArray)ledger["entries"]!;
            if (entries.Count >= 200)
            {
                JObject removed = (JObject)entries[0]; string previous = ledger["prunedDigest"]!.Type == JTokenType.Null ? new string('0', 64) : ledger.Value<string>("prunedDigest");
                ledger["prunedDigest"] = Sha256(Encoding.UTF8.GetBytes(previous).Concat(Rfc8785Canonicalizer.Canonicalize(removed)).ToArray());
                ledger["prunedThroughSequence"] = removed.Value<long>("sequence"); entries.RemoveAt(0);
            }
            long sequence = ledger.Value<long>("nextSequence"); entries.Add(new JObject
            {
                ["sequence"] = sequence, ["operationId"] = command.OperationId.ToString("D"), ["transactionType"] = change.TransactionType,
                ["actorMercenaryInstanceId"] = change.MercenaryId == null ? JValue.CreateNull() : change.MercenaryId, ["reasonCode"] = change.ReasonCode,
                ["personalGoldDelta"] = change.PersonalDelta, ["kingdomGoldDelta"] = change.KingdomDelta,
                ["stockVersionAfter"] = change.StockVersion, ["committedAtUtc"] = FormatUtc(clock.UtcNow),
                ["requestHash"] = command.RequestHash, ["resultDigest"] = digest, ["lines"] = (JArray)change.LedgerLines.DeepClone()
            }); ledger["nextSequence"] = checked(sequence + 1);
        }

        private void AppendJournal(JObject document, EconomyCommand command, JObject result, string digest)
        {
            string now = FormatUtc(clock.UtcNow); ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "STORE_TRANSACTION", ["facilityJobType"] = null,
                ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = now, ["updatedAtUtc"] = now,
                ["completedAtUtc"] = now, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest,
                ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = result.DeepClone()
            });
        }

        private StoreAvailability Availability(JObject document)
        {
            JObject facility = StoreFacility(document); string assigned = facility["assignedNpcInstanceId"]!.Type == JTokenType.Null ? null : facility.Value<string>("assignedNpcInstanceId");
            bool merchant = assigned != null && document["payload"]!["managementNpcs"]!.Children<JObject>().Any(value => value.Value<string>("instanceId") == assigned && value.Value<string>("professionId") == "NPC_MERCHANT" && value.Value<string>("assignedFacilityId") == "FAC_STORE" && value.Value<bool>("working"));
            return StoreAvailabilityPolicy.Evaluate(facility.Value<string>("state"), facility.Value<int>("level"), merchant);
        }
        private StoreAvailability RequireOpen(JObject document) { StoreAvailability value = Availability(document); if (!value.CanTrade) throw new EconomyDomainException(value.Reason); return value; }
        private static JObject Store(JObject document) => (JObject)document["payload"]!["economy"]!["store"]!;
        private static JObject StoreFacility(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_STORE");
        private static JObject FindMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND");
        private static JObject SelectMercenary(JObject document, string id) => id == null ? document["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).FirstOrDefault() : FindMercenary(document, id);
        private static void RequireTown(JObject mercenary) { if (mercenary["autonomy"]!.Value<string>("state") is not ("IDLE_TOWN" or "SELL_LOOT" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "PROMOTION_READY" or "INJURED")) throw new EconomyDomainException("P08_STORE_NOT_ACTIVE"); }

        private void RequireSellableEquipment(JObject document, JObject stored, EquipmentInstance item, int storeLevel)
        {
            JObject policy = (JObject)document["payload"]!["kingdom"]!["inventoryPolicies"]!; P08EconomyCatalog.LevelRule level = catalog.Level(storeLevel);
            bool discoveredFirst = policy.Value<bool>("protectFirstDiscovery") && !policy["discoveredEquipmentTemplateIds"]!.Values<string>().Contains(item.Definition.Id, StringComparer.Ordinal);
            bool protectedItem = item.Locked || stored["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null || policy["protectQualityIds"]!.Values<string>().Contains(item.QualityId, StringComparer.Ordinal) ||
                                 (policy.Value<bool>("protectBossEquipment") && item.Definition.Source == "BOSS") || discoveredFirst;
            if (protectedItem) throw new EconomyDomainException("P08_ITEM_PROTECTED"); RequireEquipmentLevel(item, storeLevel);
            if (item.Definition.Source == "BOSS" && !level.BossAllowed) throw new EconomyDomainException("P08_EQUIPMENT_INELIGIBLE");
        }

        private void RequireEquipmentLevel(EquipmentInstance item, int storeLevel)
        {
            P08EconomyCatalog.LevelRule rule = catalog.Level(storeLevel);
            if (item.Definition.Tier > rule.MaxTier || catalog.QualityOrder(item.QualityId) > rule.MaxQualityOrder) throw new EconomyDomainException("P08_STORE_LEVEL_REQUIRED");
        }

        private long CustomerPrice(string kind, string id, JObject equipment, int policyBps) => kind switch
        {
            "ITEM" => pricing.ItemCustomer(catalog.ItemPrice(id), policyBps),
            "POTION" => pricing.PotionCustomer(catalog.PotionBase(id), policyBps),
            "EQUIPMENT" => pricing.EquipmentCustomer(EquipmentMath.EffectivePower(catalog.Instance(equipment ?? throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND"))), policyBps),
            _ => throw new EconomyDomainException("P08_PRODUCT_NOT_FOUND")
        };

        private static void AddStack(JObject store, string kind, string id, long quantity, string source, Guid operationId, int capacity)
        {
            JArray lines = (JArray)store["stackLines"]!; string stockLineId = $"STACK:{kind}:{id}:{source}";
            JObject line = lines.Children<JObject>().SingleOrDefault(value => value.Value<string>("stockLineId") == stockLineId);
            if (line == null)
            {
                if (lines.Count >= capacity) throw new EconomyDomainException("P08_DESTINATION_CAPACITY_EXCEEDED");
                line = new JObject { ["stockLineId"] = stockLineId, ["productKind"] = kind, ["productId"] = id, ["quantity"] = quantity, ["sourceType"] = source, ["firstAcquiredOperationId"] = operationId.ToString("D"), ["lastAcquiredOperationId"] = operationId.ToString("D") }; lines.Add(line);
            }
            else { line["quantity"] = checked(line.Value<long>("quantity") + quantity); line["lastAcquiredOperationId"] = operationId.ToString("D"); }
            store["stackLines"] = new JArray(lines.Children<JObject>().OrderBy(value => value.Value<string>("productKind"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("productId"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("sourceType"), StringComparer.Ordinal));
        }

        private static void RemoveStack(JObject store, string kind, string id, long quantity)
        {
            JArray lines = (JArray)store["stackLines"]!; long remaining = quantity;
            string[] order = { "MERCENARY_SALE", "PRODUCTION", "SYSTEM_SUPPLY" };
            foreach (string source in order)
            {
                foreach (JObject line in lines.Children<JObject>().Where(value => value.Value<string>("productKind") == kind && value.Value<string>("productId") == id && value.Value<string>("sourceType") == source).OrderBy(value => value.Value<string>("firstAcquiredOperationId"), StringComparer.Ordinal).ToArray())
                {
                    long taken = Math.Min(remaining, line.Value<long>("quantity")); line["quantity"] = line.Value<long>("quantity") - taken; remaining -= taken; if (line.Value<long>("quantity") == 0) line.Remove(); if (remaining == 0) return;
                }
            }
            throw new EconomyDomainException("P08_STOCK_INSUFFICIENT");
        }

        private static void AddPotion(JObject mercenary, string potionId, long quantity)
        {
            JArray potions = (JArray)mercenary["potions"]!; JObject line = potions.Children<JObject>().SingleOrDefault(value => value.Value<string>("potionId") == potionId);
            if (line == null) { if (potions.Count >= 4) throw new EconomyDomainException("P08_DESTINATION_CAPACITY_EXCEEDED"); potions.Add(new JObject { ["potionId"] = potionId, ["quantity"] = quantity }); }
            else line["quantity"] = checked(line.Value<long>("quantity") + quantity);
        }

        private void MaybeEquip(JObject document, JObject mercenary, JObject owned, EquipmentInstance item, bool equip)
        {
            if (!equip) return; JObject slots = (JObject)mercenary["equipmentSlots"]!; string currentId = slots[item.Definition.Slot]!.Type == JTokenType.Null ? null : slots.Value<string>(item.Definition.Slot);
            if (currentId != null)
            {
                JObject current = document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == currentId);
                if (EquipmentMath.EffectivePower(catalog.Instance(current)) >= EquipmentMath.EffectivePower(item)) return;
                current["equippedByMercenaryInstanceId"] = null;
            }
            slots[item.Definition.Slot] = item.InstanceId; owned["equippedByMercenaryInstanceId"] = mercenary.Value<string>("instanceId");
        }

        private void RequireInventoryCapacity(JObject document)
        {
            int level = document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_WAREHOUSE").Value<int>("level");
            if (document["payload"]!["inventory"]!["equipment"]!.Count() > catalog.InventoryEquipmentCapacity(level)) throw new EconomyDomainException("P08_DESTINATION_CAPACITY_EXCEEDED");
        }

        private string QuoteContextHash(JObject document, string mercenaryId, IEnumerable<StoreCommandLine> lines) => Rfc8785Canonicalizer.ComputeSha256(new JObject
        {
            ["contentVersion"] = document.Value<string>("contentVersion"), ["saveRevision"] = document.Value<long>("revision"),
            ["stockVersion"] = Store(document).Value<long>("stockVersion"), ["policyId"] = document["payload"]!["kingdom"]!.Value<string>("pricingPolicy"),
            ["facilityStateHash"] = FacilityStateHash(document), ["mercenaryInstanceId"] = mercenaryId,
            ["lines"] = new JArray(lines.Select(value => value.ToJson(true)))
        });
        private static string FacilityStateHash(JObject document) => Rfc8785Canonicalizer.ComputeSha256(new JObject { ["facilityId"] = "FAC_STORE", ["level"] = StoreFacility(document).Value<int>("level"), ["state"] = StoreFacility(document).Value<string>("state"), ["assignedNpcInstanceId"] = StoreFacility(document)["assignedNpcInstanceId"]!.DeepClone() });
        private static void RequireQuoted(StoreCommandLine line, long unit) { if (line.QuotedUnitPrice != unit) throw new EconomyDomainException("P08_QUOTE_STALE"); }
        private static void ValidateLines(IReadOnlyList<StoreCommandLine> lines, int max, bool quote) { if (lines == null || lines.Count is < 1 || lines.Count > max || lines.Any(value => value.Quantity is < 1 or > 9999 || quote && value.QuotedUnitPrice < 1) || lines.Select(value => value.Kind + "\0" + value.Id).Distinct(StringComparer.Ordinal).Count() != lines.Count) throw new EconomyDomainException("P08_QUANTITY_INVALID"); }
        private static JObject ResultLine(StoreCommandLine line, long unit, long total) => new() { ["kind"] = line.Kind, ["id"] = line.Id, ["quantity"] = line.Quantity, ["unitPrice"] = unit, ["lineTotal"] = total };
        private static JObject AddedLine(string kind, string id, long quantity) => new() { ["kind"] = kind, ["id"] = id, ["quantity"] = quantity };
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static string Sha256(byte[] bytes) { using SHA256 hash = SHA256.Create(); return string.Concat(hash.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        private static string DeterministicEquipmentId(Guid operationId, string templateId, int index) => DeterministicUuid(operationId.ToString("D") + ":" + templateId + ":" + index);
        private static Guid DeterministicOperationId(Guid cycle, int index) => Guid.Parse(DeterministicUuid(cycle.ToString("D") + ":" + index));
        private static string DeterministicUuid(string seed)
        {
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(seed)).Take(16).ToArray();
            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70); bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            var hex = new StringBuilder(36);
            for (int index = 0; index < bytes.Length; index++) { if (index is 4 or 6 or 8 or 10) hex.Append('-'); hex.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture)); }
            return hex.ToString();
        }
        private static bool FixedTimeEquals(string left, string right) { if (left == null || right == null || left.Length != right.Length) return false; byte[] a = Encoding.ASCII.GetBytes(left), b = Encoding.ASCII.GetBytes(right); return CryptographicOperations.FixedTimeEquals(a, b); }
        private static void VerifyHash(string expected, string actual) { if (!FixedTimeEquals(expected, actual)) throw new EconomyDomainException("P08_OPERATION_REPLAY_MISMATCH"); }
        private void EnsureReady() { if (!IsBootstrapped || catalog == null) throw new EconomyDomainException("P08_CONTENT_MISSING"); }
        private void OnHuntSettled(object sender, RecallHuntResult result) { if (IsBootstrapped && Availability(game.Snapshot()).CanTrade) RunStoreAutonomyCycle(result.OperationId); }

        private sealed class TxMutation
        {
            public bool Changed { get; private set; } = true;
            public string TransactionType { get; private set; }
            public string ReasonCode { get; private set; }
            public string MercenaryId { get; private set; }
            public long PersonalDelta { get; private set; }
            public long KingdomDelta { get; private set; }
            public long StockVersion { get; private set; }
            public JArray ResultLines { get; private set; } = new();
            public JArray LedgerLines { get; private set; } = new();
            public string PolicyId { get; private set; }
            public long Epoch { get; private set; }
            public string ResultDigest { get; private set; }
            public static TxMutation Policy(string oldId, string newId) => new() { TransactionType = "SET_PRICING_POLICY", ReasonCode = "STORE_POLICY_CHANGE", PolicyId = newId };
            public static TxMutation Sale(string mercenaryId, long personal, long stock, JArray lines) => FromLines("SELL_TO_STORE", "MERCENARY_STORE_SALE", mercenaryId, personal, 0, stock, lines, 1);
            public static TxMutation Purchase(string mercenaryId, long personal, long kingdom, long stock, JArray lines) => FromLines("BUY_FROM_STORE", "MERCENARY_STORE_PURCHASE", mercenaryId, personal, kingdom, stock, lines, -1);
            public static TxMutation Supply(long epoch, long stock, JArray lines) => new() { TransactionType = "SYSTEM_SUPPLY", ReasonCode = "SYSTEM_STORE_SUPPLY", StockVersion = stock, ResultLines = lines, LedgerLines = LedgerFrom(lines, 1), Epoch = epoch };
            public static TxMutation NoChange(Guid operationId, long revision, long stock) => new() { Changed = false, StockVersion = stock, ResultDigest = Rfc8785Canonicalizer.ComputeSha256(new JObject { ["operationId"] = operationId.ToString("D"), ["revisionBefore"] = revision, ["revisionAfter"] = revision, ["replayed"] = false }) };
            private static TxMutation FromLines(string type, string reason, string mercenary, long personal, long kingdom, long stock, JArray lines, int stockDelta) => new() { TransactionType = type, ReasonCode = reason, MercenaryId = mercenary, PersonalDelta = personal, KingdomDelta = kingdom, StockVersion = stock, ResultLines = lines, LedgerLines = LedgerFrom(lines, stockDelta) };
            private static JArray LedgerFrom(JArray lines, int stockDelta) => new(lines.Children<JObject>().Select((line, index) => new JObject { ["lineNo"] = index + 1, ["productKind"] = line.Value<string>("kind"), ["productId"] = line.Value<string>("id"), ["quantity"] = line.Value<long>("quantity"), ["unitPrice"] = line.Value<long?>("unitPrice") ?? 1, ["lineTotal"] = line.Value<long?>("lineTotal") ?? line.Value<long>("quantity"), ["stockDelta"] = stockDelta * line.Value<long>("quantity") }));
            public JObject BuildResult(Guid operationId, long revisionBefore)
            {
                var result = new JObject { ["operationId"] = operationId.ToString("D"), ["revisionBefore"] = revisionBefore, ["revisionAfter"] = revisionBefore + 1 };
                if (TransactionType == "SET_PRICING_POLICY") result["policyId"] = PolicyId;
                else if (TransactionType == "SYSTEM_SUPPLY") { result["epoch"] = Epoch; result["addedLines"] = ResultLines.DeepClone(); result["stockVersionAfter"] = StockVersion; }
                else { result["mercenaryInstanceId"] = MercenaryId; result["totalPersonalGoldDelta"] = PersonalDelta; if (TransactionType == "BUY_FROM_STORE") result["totalKingdomGoldDelta"] = KingdomDelta; result["stockVersionAfter"] = StockVersion; result["lines"] = ResultLines.DeepClone(); }
                result["replayed"] = false; return result;
            }
        }
    }
}
