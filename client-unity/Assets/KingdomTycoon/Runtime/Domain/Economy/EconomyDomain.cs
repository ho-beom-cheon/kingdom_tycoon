using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomTycoon.Domain.Economy
{
    public sealed class EconomyDomainException : InvalidOperationException
    {
        public EconomyDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public readonly struct Money : IEquatable<Money>
    {
        public const long MaxSafeInteger = 9_007_199_254_740_991L;
        public Money(long value)
        {
            if (value < 0 || value > MaxSafeInteger) throw new EconomyDomainException("P08_WALLET_RANGE_INVALID");
            Value = value;
        }
        public long Value { get; }
        public Money Add(long delta)
        {
            try { return new Money(checked(Value + delta)); }
            catch (OverflowException) { throw new EconomyDomainException("P08_PRICE_OVERFLOW"); }
        }
        public Money Subtract(long amount)
        {
            if (amount < 0 || amount > Value) throw new EconomyDomainException("P08_PERSONAL_GOLD_INSUFFICIENT");
            return new Money(Value - amount);
        }
        public bool Equals(Money other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct PricePolicy
    {
        public PricePolicy(string id, int multiplierBps, int minimumStoreLevel)
        {
            if (string.IsNullOrWhiteSpace(id) || multiplierBps is < 1 or > 20_000 || minimumStoreLevel is < 1 or > 4)
                throw new EconomyDomainException("P08_POLICY_INVALID");
            Id = id; MultiplierBps = multiplierBps; MinimumStoreLevel = minimumStoreLevel;
        }
        public string Id { get; }
        public int MultiplierBps { get; }
        public int MinimumStoreLevel { get; }
    }

    public sealed class StorePricingService
    {
        public long ItemAcquire(long sellPrice, int acquireBps) => Math.Max(1, MultiplyFloor(sellPrice, acquireBps, 10_000));
        public long ItemCustomer(long sellPrice, int policyBps) => MultiplyCeiling(MultiplyChecked(sellPrice, 2), policyBps, 10_000);
        public long PotionAcquire(long customerBasePrice) => Math.Max(1, MultiplyFloor(customerBasePrice, 2_500, 10_000));
        public long PotionCustomer(long customerBasePrice, int policyBps) => Math.Max(1, MultiplyCeiling(customerBasePrice, policyBps, 10_000));
        public long EquipmentAcquire(long effectivePower) => Math.Max(1, MultiplyFloor(effectivePower, 2_500, 10_000));
        public long EquipmentCustomer(long effectivePower, int policyBps) => Math.Max(1, MultiplyCeiling(effectivePower, policyBps, 10_000));

        public static long MultiplyFloor(long left, long right, long denominator)
        {
            if (left < 0 || right < 0 || denominator <= 0) throw new EconomyDomainException("P08_PRICE_OVERFLOW");
            try { return checked(left * right) / denominator; }
            catch (OverflowException) { throw new EconomyDomainException("P08_PRICE_OVERFLOW"); }
        }

        public static long MultiplyCeiling(long left, long right, long denominator)
        {
            if (left < 0 || right < 0 || denominator <= 0) throw new EconomyDomainException("P08_PRICE_OVERFLOW");
            try { return checked(checked(left * right) + denominator - 1) / denominator; }
            catch (OverflowException) { throw new EconomyDomainException("P08_PRICE_OVERFLOW"); }
        }

        public static long MultiplyChecked(long left, long right)
        {
            try { return checked(left * right); }
            catch (OverflowException) { throw new EconomyDomainException("P08_PRICE_OVERFLOW"); }
        }
    }

    public enum StoreAvailabilityState { Locked, Content, Open }

    public readonly struct StoreAvailability
    {
        public StoreAvailability(StoreAvailabilityState state, int level, string reason)
        { State = state; Level = level; Reason = reason; }
        public StoreAvailabilityState State { get; }
        public int Level { get; }
        public string Reason { get; }
        public bool CanTrade => State == StoreAvailabilityState.Open;
    }

    public static class StoreAvailabilityPolicy
    {
        public static StoreAvailability Evaluate(string facilityState, int level, bool hasWorkingMerchant)
        {
            if (facilityState is "LOCKED" or "BUILDABLE" or "BUILDING")
                return new StoreAvailability(StoreAvailabilityState.Locked, level, "P08_STORE_LOCKED");
            if (facilityState != "ACTIVE")
                return new StoreAvailability(StoreAvailabilityState.Content, level, "P08_STORE_NOT_ACTIVE");
            if (!hasWorkingMerchant)
                return new StoreAvailability(StoreAvailabilityState.Content, level, "P08_MERCHANT_REQUIRED");
            return new StoreAvailability(StoreAvailabilityState.Open, level, null);
        }
    }

    public sealed class StorePurchaseDecisionService
    {
        public bool ShouldBuy(long personalGold, long unitPrice, int thresholdBps, long reserveGold)
        {
            if (personalGold < reserveGold || unitPrice <= 0) return false;
            long spendable = personalGold - reserveGold;
            return StorePricingService.MultiplyFloor(unitPrice, thresholdBps, 10_000) <= spendable;
        }

        public string SelectEquipment(IEnumerable<(string Id, long UpgradeScore, long Price, int QualityOrder)> candidates, string priority) =>
            (priority switch
            {
                "PRICE" => candidates.OrderBy(value => value.Price).ThenByDescending(value => value.UpgradeScore),
                "QUALITY" => candidates.OrderByDescending(value => value.QualityOrder).ThenByDescending(value => value.UpgradeScore),
                _ => candidates.OrderByDescending(value => value.UpgradeScore).ThenBy(value => value.Price)
            }).ThenBy(value => value.Id, StringComparer.Ordinal).Select(value => value.Id).FirstOrDefault();
    }
}
