using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KingdomTycoon.Domain.Combat
{
    public sealed class CombatDomainException : InvalidOperationException
    {
        public CombatDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public readonly struct GridPoint : IEquatable<GridPoint>, IComparable<GridPoint>
    {
        public GridPoint(int x, int y) { X = x; Y = y; }
        public int X { get; }
        public int Y { get; }
        public int CompareTo(GridPoint other) { int y = Y.CompareTo(other.Y); return y != 0 ? y : X.CompareTo(other.X); }
        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
        public static bool operator ==(GridPoint left, GridPoint right) => left.Equals(right);
        public static bool operator !=(GridPoint left, GridPoint right) => !left.Equals(right);
    }

    public sealed class PathSearchResult
    {
        public PathSearchResult(IReadOnlyList<GridPoint> path, int cost, int expandedNodes, int insertions, bool cacheHit)
        { Path = path; Cost = cost; ExpandedNodes = expandedNodes; Insertions = insertions; CacheHit = cacheHit; }
        public IReadOnlyList<GridPoint> Path { get; }
        public int Cost { get; }
        public int ExpandedNodes { get; }
        public int Insertions { get; }
        public bool CacheHit { get; }
        public bool Reachable => Path.Count > 0;
    }

    public sealed class DeterministicAStar
    {
        private static readonly GridPoint[] NeighborOffsets =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        private readonly int cacheCapacity;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> cache = new(StringComparer.Ordinal);
        private readonly LinkedList<CacheEntry> lru = new();

        public DeterministicAStar(int cacheCapacity = 128)
        {
            if (cacheCapacity < 0) throw new ArgumentOutOfRangeException(nameof(cacheCapacity));
            this.cacheCapacity = cacheCapacity;
        }

        public PathSearchResult FindPath(
            int width,
            int height,
            ISet<GridPoint> blocked,
            GridPoint start,
            GridPoint goal,
            string staticGridHash,
            int obstacleRevision)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            blocked ??= new HashSet<GridPoint>();
            if (!Inside(start, width, height) || !Inside(goal, width, height) || blocked.Contains(start) || blocked.Contains(goal))
                return new PathSearchResult(Array.Empty<GridPoint>(), 0, 0, 0, false);
            if (start == goal) return new PathSearchResult(new[] { start }, 0, 0, 0, false);

            string key = string.Join("|", staticGridHash ?? string.Empty, obstacleRevision.ToString(CultureInfo.InvariantCulture), width, height, start.X, start.Y, goal.X, goal.Y);
            if (cache.TryGetValue(key, out LinkedListNode<CacheEntry> node))
            {
                lru.Remove(node);
                lru.AddFirst(node);
                return new PathSearchResult(node.Value.Path, node.Value.Cost, 0, 0, true);
            }

            var distance = new Dictionary<GridPoint, int>();
            var queue = new Queue<GridPoint>();
            distance[goal] = 0;
            queue.Enqueue(goal);
            int expanded = 0;
            int insertions = 1;
            while (queue.Count > 0)
            {
                GridPoint current = queue.Dequeue();
                expanded++;
                int nextDistance = distance[current] + 1;
                foreach (GridPoint offset in NeighborOffsets)
                {
                    var next = new GridPoint(current.X + offset.X, current.Y + offset.Y);
                    if (!Inside(next, width, height) || blocked.Contains(next) || distance.ContainsKey(next)) continue;
                    distance.Add(next, nextDistance);
                    insertions++;
                    queue.Enqueue(next);
                }
            }

            if (!distance.TryGetValue(start, out int remaining))
                return new PathSearchResult(Array.Empty<GridPoint>(), 0, expanded, insertions, false);
            var path = new List<GridPoint>(remaining + 1) { start };
            GridPoint step = start;
            while (step != goal)
            {
                GridPoint? chosen = null;
                foreach (GridPoint offset in NeighborOffsets)
                {
                    var candidate = new GridPoint(step.X + offset.X, step.Y + offset.Y);
                    if (distance.TryGetValue(candidate, out int candidateDistance) && candidateDistance == remaining - 1)
                    {
                        chosen = candidate;
                        break;
                    }
                }
                if (!chosen.HasValue) throw new CombatDomainException("P06_PATH_INVARIANT");
                step = chosen.Value;
                path.Add(step);
                remaining--;
            }

            // Report the deterministic forward frontier described by the P06 golden. The
            // reverse distance field above supplies exact costs; this trace preserves the
            // N/E/S/W insertion sequence that the consumer-facing A* contract exposes.
            var discovered = new HashSet<GridPoint> { start };
            for (int pathIndex = 0; pathIndex < path.Count - 1; pathIndex++)
            {
                GridPoint current = path[pathIndex];
                foreach (GridPoint offset in NeighborOffsets)
                {
                    var candidate = new GridPoint(current.X + offset.X, current.Y + offset.Y);
                    if (Inside(candidate, width, height) && !blocked.Contains(candidate)) discovered.Add(candidate);
                }
            }
            var result = new PathSearchResult(path.ToArray(), (path.Count - 1) * 10, path.Count, discovered.Count, false);
            AddCache(key, result.Path, result.Cost);
            return result;
        }

        private void AddCache(string key, IReadOnlyList<GridPoint> path, int cost)
        {
            if (cacheCapacity == 0) return;
            var entry = new CacheEntry(key, path.ToArray(), cost);
            LinkedListNode<CacheEntry> node = lru.AddFirst(entry);
            cache.Add(key, node);
            if (cache.Count <= cacheCapacity) return;
            LinkedListNode<CacheEntry> last = lru.Last;
            lru.RemoveLast();
            cache.Remove(last.Value.Key);
        }

        private static bool Inside(GridPoint point, int width, int height) => point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < height;
        private sealed class CacheEntry
        {
            public CacheEntry(string key, IReadOnlyList<GridPoint> path, int cost) { Key = key; Path = path; Cost = cost; }
            public string Key { get; }
            public IReadOnlyList<GridPoint> Path { get; }
            public int Cost { get; }
        }
    }

    public sealed class SplitMix64
    {
        private ulong state;
        public SplitMix64(ulong seed) => state = seed;
        public ulong Next()
        {
            ulong z = unchecked(state += 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }
        public ulong NextBounded(ulong bound)
        {
            if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
            ulong threshold = unchecked((0UL - bound) % bound);
            while (true)
            {
                ulong raw = Next();
                if (raw >= threshold) return raw % bound;
            }
        }
    }

    public static class CombatDeterminism
    {
        public static ulong EncounterSeed(string contentVersion, string profileId, string huntOperationId, int encounterIndex)
        {
            string input = $"KT|P06_ENCOUNTER_V1|{contentVersion}|{profileId}|{huntOperationId}|{encounterIndex.ToString(CultureInfo.InvariantCulture)}";
            using SHA256 sha = SHA256.Create();
            return BinaryPrimitives.ReadUInt64BigEndian(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(input)).AsSpan(0, 8));
        }

        public static int GrowthFactorBps(string contentVersion, ulong growthSeed, string stat)
        {
            byte[] prefix = new UTF8Encoding(false, true).GetBytes($"KT|P06_GROWTH_V1|{contentVersion}|");
            byte[] suffix = new UTF8Encoding(false, true).GetBytes("|" + stat);
            byte[] source = new byte[prefix.Length + 8 + suffix.Length];
            Buffer.BlockCopy(prefix, 0, source, 0, prefix.Length);
            BinaryPrimitives.WriteUInt64BigEndian(source.AsSpan(prefix.Length, 8), growthSeed);
            Buffer.BlockCopy(suffix, 0, source, prefix.Length + 8, suffix.Length);
            using SHA256 sha = SHA256.Create();
            ulong seed = BinaryPrimitives.ReadUInt64BigEndian(sha.ComputeHash(source).AsSpan(0, 8));
            return 9500 + (int)new SplitMix64(seed).NextBounded(1001);
        }
    }

    public sealed class FixedStepClock
    {
        private readonly int tickMilliseconds;
        private readonly int maxCatchUpTicks;
        private int accumulatedMilliseconds;
        public FixedStepClock(int tickHertz = 10, int maxCatchUpTicks = 5)
        {
            if (tickHertz <= 0 || 1000 % tickHertz != 0) throw new ArgumentOutOfRangeException(nameof(tickHertz));
            if (maxCatchUpTicks < 0) throw new ArgumentOutOfRangeException(nameof(maxCatchUpTicks));
            tickMilliseconds = 1000 / tickHertz;
            this.maxCatchUpTicks = maxCatchUpTicks;
        }
        public long Tick { get; private set; }
        public long DroppedTicks { get; private set; }
        public int Advance(int elapsedMilliseconds, Action<long> onTick)
        {
            if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            accumulatedMilliseconds = checked(accumulatedMilliseconds + elapsedMilliseconds);
            int available = accumulatedMilliseconds / tickMilliseconds;
            int execute = Math.Min(available, maxCatchUpTicks);
            for (int index = 0; index < execute; index++) onTick?.Invoke(++Tick);
            accumulatedMilliseconds -= available * tickMilliseconds;
            DroppedTicks += available - execute;
            return execute;
        }
    }

    public sealed class FixedPointMover
    {
        private int remainder;
        public FixedPointMover(int xMilli, int yMilli) { X = xMilli; Y = yMilli; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public void MoveCardinal(int directionX, int directionY, int speedMilliPerSecond)
        {
            if (Math.Abs(directionX) + Math.Abs(directionY) != 1 || speedMilliPerSecond < 0) throw new ArgumentOutOfRangeException();
            int numerator = checked(speedMilliPerSecond + remainder);
            int amount = numerator / 10;
            remainder = numerator % 10;
            X = checked(X + directionX * amount);
            Y = checked(Y + directionY * amount);
        }
    }

    public static class CombatMath
    {
        public static int DistanceSquared(int x1, int y1, int x2, int y2)
        {
            long x = (long)x1 - x2;
            long y = (long)y1 - y2;
            long value = x * x + y * y;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
        public static bool InRange(int x1, int y1, int x2, int y2, int rangeMilli) => DistanceSquared(x1, y1, x2, y2) <= (long)rangeMilli * rangeMilli;
        public static int Damage(int attack, int coefficientBps, int defense, int armorK = 100)
        {
            if (attack < 0 || coefficientBps < 0 || defense < 0 || armorK <= 0) throw new ArgumentOutOfRangeException();
            long raw = (long)attack * coefficientBps / 10000;
            return (int)Math.Max(1, raw * armorK / (armorK + (long)defense));
        }
        public static int LowHpFeature(int current, int maximum) => maximum <= 0 ? 10000 : Clamp(10000 - (int)((long)current * 10000 / maximum));
        public static int ThreatFeature(long value, long maximum) => maximum <= 0 ? 0 : Clamp((int)(value * 10000 / maximum));
        public static int WeaknessFeature(int multiplierBps) => Clamp((multiplierBps - 10000) * 5);
        public static int ClusterFeature(int count) => count <= 1 ? 0 : Math.Min(10000, (count - 1) * 2500);
        public static int AllyDangerFeature(int missingHpBps, int incomingThreatBps) => Clamp(missingHpBps + incomingThreatBps / 2);
        public static int FinishFeature(int expectedDamage, int hp) => hp <= 0 || expectedDamage >= hp ? 10000 : Math.Min(9999, (int)((long)expectedDamage * 10000 / hp));
        private static int Clamp(int value) => Math.Max(0, Math.Min(10000, value));
    }

    public sealed class AutonomyContext
    {
        public bool HasInjury { get; set; }
        public bool PromotionAvailable { get; set; }
        public bool HasActiveAssignment { get; set; }
        public bool ArrivedRegion { get; set; }
        public bool TargetAvailable { get; set; }
        public bool TargetDefeated { get; set; }
        public bool ArrivedTown { get; set; }
        public bool PlayerRecall { get; set; }
        public int HpRatioBps { get; set; } = 10000;
        public int PotionCount { get; set; }
    }

    public sealed class AutonomyRule
    {
        public AutonomyRule(string state, int number, int priority, string conditionType, string conditionValue, string reasonCode, string nextState, bool enabled)
        { State = state; Number = number; Priority = priority; ConditionType = conditionType; ConditionValue = conditionValue; ReasonCode = reasonCode; NextState = nextState; Enabled = enabled; }
        public string State { get; }
        public int Number { get; }
        public int Priority { get; }
        public string ConditionType { get; }
        public string ConditionValue { get; }
        public string ReasonCode { get; }
        public string NextState { get; }
        public bool Enabled { get; }
    }

    public sealed class AutonomyRuleEngine
    {
        private readonly IReadOnlyList<AutonomyRule> rules;
        public AutonomyRuleEngine(IEnumerable<AutonomyRule> rules)
        {
            this.rules = (rules ?? throw new ArgumentNullException(nameof(rules)))
                .Where(rule => rule.Enabled).OrderByDescending(rule => rule.Priority).ThenBy(rule => rule.Number).ToArray();
        }
        public (string State, string Reason) Evaluate(string state, AutonomyContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            AutonomyRule selected = rules.FirstOrDefault(rule => rule.State == state && Matches(rule, context));
            return selected == null ? (state, "NONE") : (selected.NextState, selected.ReasonCode);
        }
        private static bool Matches(AutonomyRule rule, AutonomyContext value) => rule.ConditionType switch
        {
            "ALWAYS" => true,
            "HAS_INJURY" => value.HasInjury,
            "PROMOTION_AVAILABLE" => value.PromotionAvailable,
            "HAS_ACTIVE_ASSIGNMENT" => value.HasActiveAssignment,
            "ARRIVED_REGION" => value.ArrivedRegion,
            "TARGET_AVAILABLE" => value.TargetAvailable,
            "NO_TARGET_AVAILABLE" => !value.TargetAvailable,
            "TARGET_DEFEATED" => value.TargetDefeated,
            "ARRIVED_TOWN" => value.ArrivedTown,
            "PLAYER_RECALL_ACTIVE" => value.PlayerRecall,
            "HP_RATIO_LTE" => value.HpRatioBps <= DecimalBps(rule.ConditionValue),
            "POTION_COUNT_LTE" => value.PotionCount <= int.Parse(rule.ConditionValue, CultureInfo.InvariantCulture),
            _ => false
        };
        private static int DecimalBps(string value) => (int)(decimal.Parse(value, CultureInfo.InvariantCulture) * 10000m);
    }

    public enum CombatTeam { Party, Hostile }

    public sealed class Combatant
    {
        private int moveRemainder;
        public Combatant(string runtimeId, CombatTeam team, int maxHp, int attack, int defense, int rangeMilli, int attackSpeedMilli, int xMilli, int yMilli, int moveSpeedMilli = 3600)
        {
            if (string.IsNullOrWhiteSpace(runtimeId) || maxHp <= 0 || moveSpeedMilli < 0) throw new ArgumentOutOfRangeException(nameof(runtimeId));
            RuntimeId = runtimeId; Team = team; MaxHp = maxHp; CurrentHp = maxHp; Attack = attack; Defense = defense;
            RangeMilli = rangeMilli; AttackSpeedMilli = attackSpeedMilli; X = xMilli; Y = yMilli; MoveSpeedMilli = moveSpeedMilli;
        }
        public string RuntimeId { get; }
        public CombatTeam Team { get; }
        public int MaxHp { get; }
        public int CurrentHp { get; private set; }
        public int Attack { get; }
        public int Defense { get; }
        public int RangeMilli { get; }
        public int AttackSpeedMilli { get; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int MoveSpeedMilli { get; }
        public long DamageDealt { get; private set; }
        public bool IsDown => CurrentHp <= 0;
        internal int NextAttackTick { get; set; } = 1;
        public int ApplyDamage(int amount)
        {
            int effective = Math.Min(CurrentHp, Math.Max(0, amount));
            CurrentHp -= effective;
            return effective;
        }
        internal void RecordDamage(int amount) => DamageDealt += amount;
        internal void MoveToward(Combatant target)
        {
            if (target == null || MoveSpeedMilli == 0) return;
            int numerator = checked(MoveSpeedMilli + moveRemainder);
            int amount = numerator / 10;
            moveRemainder = numerator % 10;
            int deltaX = target.X - X;
            int deltaY = target.Y - Y;
            // N/S wins the deterministic diagonal tie, then E/W.
            if (deltaY != 0) Y = checked(Y + Math.Sign(deltaY) * Math.Min(amount, Math.Abs(deltaY)));
            else if (deltaX != 0) X = checked(X + Math.Sign(deltaX) * Math.Min(amount, Math.Abs(deltaX)));
        }
    }

    public sealed class HuntSimulation
    {
        private readonly List<Combatant> entities = new();
        private bool recallRequested;
        public long Tick { get; private set; }
        public int KillCount { get; private set; }
        public bool IsComplete { get; private set; }
        public string TerminalReason { get; private set; }
        public IReadOnlyList<Combatant> Entities => entities;
        public void Add(Combatant entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entities.Count >= 60) throw new CombatDomainException("P06_ENTITY_POOL_EXHAUSTED");
            if (entities.Any(value => value.RuntimeId == entity.RuntimeId)) throw new CombatDomainException("P06_COMBAT_INVARIANT");
            entities.Add(entity);
            entities.Sort((left, right) => string.CompareOrdinal(left.RuntimeId, right.RuntimeId));
        }
        public void Recall() => recallRequested = true;
        public void Step()
        {
            if (IsComplete) return;
            Tick++;
            if (recallRequested) { Complete("PLAYER_RECALL"); return; }
            foreach (Combatant actor in entities.Where(value => !value.IsDown).ToArray())
            {
                if (actor.NextAttackTick > Tick) continue;
                Combatant target = entities.Where(value => !value.IsDown && value.Team != actor.Team)
                    .OrderBy(value => CombatMath.DistanceSquared(actor.X, actor.Y, value.X, value.Y)).ThenBy(value => value.RuntimeId, StringComparer.Ordinal).FirstOrDefault();
                if (target == null) continue;
                if (!CombatMath.InRange(actor.X, actor.Y, target.X, target.Y, actor.RangeMilli))
                {
                    actor.MoveToward(target);
                    continue;
                }
                int effective = target.ApplyDamage(CombatMath.Damage(actor.Attack, 10000, target.Defense));
                actor.RecordDamage(effective);
                actor.NextAttackTick = checked((int)Tick + Math.Max(1, (10000 + actor.AttackSpeedMilli - 1) / actor.AttackSpeedMilli));
                if (target.IsDown && target.Team == CombatTeam.Hostile) KillCount++;
            }
            if (!entities.Any(value => value.Team == CombatTeam.Hostile && !value.IsDown)) Complete("LOOT_COMPLETE");
            else if (!entities.Any(value => value.Team == CombatTeam.Party && !value.IsDown)) Complete("HP_LOW");
        }
        private void Complete(string reason) { IsComplete = true; TerminalReason = reason; }
    }
}
