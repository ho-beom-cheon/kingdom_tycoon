using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Economy
{
    public sealed class StoreCommandLine
    {
        public StoreCommandLine(string kind, string id, long quantity, long quotedUnitPrice = 0)
        { Kind = kind; Id = id; Quantity = quantity; QuotedUnitPrice = quotedUnitPrice; }
        public string Kind { get; }
        public string Id { get; }
        public long Quantity { get; }
        public long QuotedUnitPrice { get; }
        public JObject ToJson(bool includeQuote) => new()
        {
            ["kind"] = Kind, ["id"] = Id, ["quantity"] = Quantity,
            ["quotedUnitPrice"] = includeQuote ? QuotedUnitPrice : null
        };
    }

    public abstract class EconomyCommand
    {
        protected EconomyCommand(Guid operationId, long expectedRevision, string requestHash)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public abstract string CommandType { get; }
        public abstract JObject ToHashJson();
    }

    public sealed class SetPricingPolicyCommand : EconomyCommand
    {
        public SetPricingPolicyCommand(Guid operationId, long expectedRevision, string requestHash, string policyId) : base(operationId, expectedRevision, requestHash) => PolicyId = policyId;
        public override string CommandType => "SET_PRICING_POLICY";
        public string PolicyId { get; }
        public override JObject ToHashJson() => new() { ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision, ["policyId"] = PolicyId };
    }

    public sealed class SellToStoreCommand : EconomyCommand
    {
        public SellToStoreCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryId, IEnumerable<StoreCommandLine> lines) : base(operationId, expectedRevision, requestHash)
        { MercenaryId = mercenaryId; Lines = (lines ?? throw new ArgumentNullException(nameof(lines))).ToArray(); }
        public override string CommandType => "SELL_TO_STORE";
        public string MercenaryId { get; }
        public IReadOnlyList<StoreCommandLine> Lines { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryId, ["lines"] = new JArray(Lines.Select(value => { JObject json = value.ToJson(false); json.Remove("quotedUnitPrice"); return json; }))
        };
    }

    public sealed class BuyFromStoreCommand : EconomyCommand
    {
        public BuyFromStoreCommand(Guid operationId, long expectedRevision, string requestHash, string quoteContextHash, string mercenaryId, bool equipIfUpgrade, IEnumerable<StoreCommandLine> lines) : base(operationId, expectedRevision, requestHash)
        { QuoteContextHash = quoteContextHash; MercenaryId = mercenaryId; EquipIfUpgrade = equipIfUpgrade; Lines = (lines ?? throw new ArgumentNullException(nameof(lines))).ToArray(); }
        public override string CommandType => "BUY_FROM_STORE";
        public string QuoteContextHash { get; }
        public string MercenaryId { get; }
        public bool EquipIfUpgrade { get; }
        public IReadOnlyList<StoreCommandLine> Lines { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["quoteContextHash"] = QuoteContextHash, ["mercenaryInstanceId"] = MercenaryId, ["equipIfUpgrade"] = EquipIfUpgrade,
            ["lines"] = new JArray(Lines.Select(value => value.ToJson(true)))
        };
    }

    public sealed class RefreshSystemStoreSupplyCommand : EconomyCommand
    {
        public RefreshSystemStoreSupplyCommand(Guid operationId, long expectedRevision, string requestHash, long targetEpoch) : base(operationId, expectedRevision, requestHash) => TargetEpoch = targetEpoch;
        public override string CommandType => "REFRESH_SYSTEM_STORE_SUPPLY";
        public long TargetEpoch { get; }
        public override JObject ToHashJson() => new() { ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision, ["targetEpoch"] = TargetEpoch };
    }

    public sealed class EconomyOperationResult
    {
        public EconomyOperationResult(Guid operationId, long revisionBefore, long revisionAfter, string resultDigest, bool replayed, long personalGoldDelta, long kingdomGoldDelta, long stockVersion)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; ResultDigest = resultDigest; Replayed = replayed; PersonalGoldDelta = personalGoldDelta; KingdomGoldDelta = kingdomGoldDelta; StockVersion = stockVersion; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public string ResultDigest { get; }
        public bool Replayed { get; }
        public long PersonalGoldDelta { get; }
        public long KingdomGoldDelta { get; }
        public long StockVersion { get; }
    }

    public sealed class StoreProductDto
    {
        public StoreProductDto(string kind, string id, string name, long quantity, long unitPrice, string sourceType, int tier, string qualityId)
        { Kind = kind; Id = id; Name = name; Quantity = quantity; UnitPrice = unitPrice; SourceType = sourceType; Tier = tier; QualityId = qualityId; }
        public string Kind { get; }
        public string Id { get; }
        public string Name { get; }
        public long Quantity { get; }
        public long UnitPrice { get; }
        public string SourceType { get; }
        public int Tier { get; }
        public string QualityId { get; }
    }

    public sealed class StorefrontDto
    {
        public StorefrontDto(long revision, long stockVersion, string availability, string disabledReason, int level, string policyId, long kingdomGold, string mercenaryId, long personalGold, IReadOnlyList<StoreProductDto> products, int ledgerCount)
        { Revision = revision; StockVersion = stockVersion; Availability = availability; DisabledReason = disabledReason; Level = level; PolicyId = policyId; KingdomGold = kingdomGold; MercenaryId = mercenaryId; PersonalGold = personalGold; Products = products ?? Array.Empty<StoreProductDto>(); LedgerCount = ledgerCount; }
        public long Revision { get; }
        public long StockVersion { get; }
        public string Availability { get; }
        public string DisabledReason { get; }
        public int Level { get; }
        public string PolicyId { get; }
        public long KingdomGold { get; }
        public string MercenaryId { get; }
        public long PersonalGold { get; }
        public IReadOnlyList<StoreProductDto> Products { get; }
        public int LedgerCount { get; }
    }
}
