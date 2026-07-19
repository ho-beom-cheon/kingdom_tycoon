using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomTycoon.Domain.Raids
{
    public sealed class RaidDomainException : Exception
    {
        public RaidDomainException(string code) : base(code) => Code = code;
        public RaidDomainException(string code, string detail) : base(string.IsNullOrEmpty(detail) ? code : $"{code}: {detail}") => Code = code;
        public string Code { get; }
    }

    public sealed class RaidCombatantSpec
    {
        public string Id; public string JobId; public int MaxHp; public int Attack; public int Defense; public int HealPower; public int AvailablePotions;
    }

    public sealed class RaidPartSpec
    {
        public string Id; public int MaxHp; public int BossDamageShareBps; public int TargetOrder; public string PrerequisitePartId;
    }

    public sealed class RaidPhaseSpec
    {
        public int PhaseNo; public int TriggerBossHpBps; public int ActionIntervalTicks; public int AttackMultiplierBps; public string AbilityTag;
    }

    public sealed class RaidSimulationSpec
    {
        public string RaidId; public string TargetPartId; public int TimeLimitTicks; public int BossMaxHp; public int BossAttack; public int BossDefense;
        public int DifficultyScaleBps; public IReadOnlyList<RaidCombatantSpec> Party; public IReadOnlyList<RaidPartSpec> Parts; public IReadOnlyList<RaidPhaseSpec> Phases;
    }

    public sealed class RaidMemberOutcome
    {
        public string Id; public long Damage; public long Healing; public long PreventedDamage; public bool Injured; public int PotionsUsed;
        public long Total => checked(Damage + Healing + PreventedDamage);
    }

    public sealed class RaidTracePoint
    {
        public int Tick; public int BossHp; public string Phase; public int ActiveParty; public string EventCode;
    }

    public sealed class RaidSimulationOutcome
    {
        public bool Success; public int DurationTicks; public IReadOnlyList<string> BrokenPartIds; public IReadOnlyList<RaidMemberOutcome> Members; public IReadOnlyList<RaidTracePoint> Trace;
    }

    public sealed class DeterministicRaidSimulation
    {
        public RaidSimulationOutcome Run(RaidSimulationSpec spec)
        {
            if (spec == null || spec.Party == null || spec.Party.Count < 1 || spec.Parts == null || spec.Phases == null)
                throw new RaidDomainException("P14_SIMULATION_SPEC_INVALID");
            RaidPartSpec target = spec.Parts.SingleOrDefault(value => value.Id == spec.TargetPartId)
                ?? throw new RaidDomainException("P14_TARGET_PART_INVALID");
            int bossHp = checked(spec.BossMaxHp * spec.DifficultyScaleBps / 10000);
            int bossMaxHp = bossHp;
            int partHp = Math.Max(1, checked(target.MaxHp * spec.DifficultyScaleBps / 10000));
            var health = spec.Party.ToDictionary(value => value.Id, value => value.MaxHp, StringComparer.Ordinal);
            var outcomes = spec.Party.ToDictionary(value => value.Id, value => new RaidMemberOutcome { Id = value.Id }, StringComparer.Ordinal);
            var broken = new List<string>();
            var trace = new List<RaidTracePoint>();
            int tick;
            for (tick = 1; tick <= spec.TimeLimitTicks && bossHp > 0 && health.Values.Any(value => value > 0); tick++)
            {
                RaidPhaseSpec phase = spec.Phases.Where(value => bossHp * 10000L <= bossMaxHp * (long)value.TriggerBossHpBps)
                    .OrderByDescending(value => value.PhaseNo).FirstOrDefault() ?? spec.Phases.OrderBy(value => value.PhaseNo).First();
                if (tick % 10 == 0)
                {
                    foreach (RaidCombatantSpec member in spec.Party.Where(value => health[value.Id] > 0))
                    {
                        int raw = Math.Max(1, member.Attack - spec.BossDefense * spec.DifficultyScaleBps / 100000);
                        int damage = checked(raw * 10);
                        if (partHp > 0)
                        {
                            int partDamage = Math.Min(partHp, damage); partHp -= partDamage;
                            int shared = checked(partDamage * target.BossDamageShareBps / 10000); bossHp = Math.Max(0, bossHp - shared); outcomes[member.Id].Damage += shared;
                            if (partHp == 0 && !broken.Contains(target.Id, StringComparer.Ordinal)) broken.Add(target.Id);
                        }
                        else { bossHp = Math.Max(0, bossHp - damage); outcomes[member.Id].Damage += damage; }
                    }
                    int totalHeal = spec.Party.Where(value => health[value.Id] > 0).Sum(value => value.HealPower * 4);
                    if (totalHeal > 0)
                    {
                        RaidCombatantSpec wounded = spec.Party.Where(value => health[value.Id] > 0).OrderBy(value => health[value.Id] * 10000L / value.MaxHp).First();
                        int healed = Math.Min(totalHeal, wounded.MaxHp - health[wounded.Id]); health[wounded.Id] += healed;
                        RaidCombatantSpec healer = spec.Party.OrderByDescending(value => value.HealPower).First(); outcomes[healer.Id].Healing += healed;
                    }
                }
                if (tick % Math.Max(1, phase.ActionIntervalTicks) == 0 && bossHp > 0)
                {
                    RaidCombatantSpec victim = spec.Party.Where(value => health[value.Id] > 0).OrderBy(value => health[value.Id]).ThenBy(value => value.Id, StringComparer.Ordinal).First();
                    int raw = Math.Max(1, checked(spec.BossAttack * spec.DifficultyScaleBps / 40000 * phase.AttackMultiplierBps / 10000) - victim.Defense);
                    int prevented = Math.Min(victim.Defense, raw); outcomes[victim.Id].PreventedDamage += prevented; health[victim.Id] = Math.Max(0, health[victim.Id] - raw);
                    if (health[victim.Id] > 0 && health[victim.Id] * 100 <= victim.MaxHp * 35 && outcomes[victim.Id].PotionsUsed < victim.AvailablePotions)
                    { health[victim.Id] = Math.Min(victim.MaxHp, health[victim.Id] + 360); outcomes[victim.Id].PotionsUsed = 1; }
                }
                if (tick == 1 || tick % 100 == 0 || bossHp == 0)
                    trace.Add(new RaidTracePoint { Tick = tick, BossHp = bossHp, Phase = phase.AbilityTag, ActiveParty = health.Count(value => value.Value > 0), EventCode = bossHp == 0 ? "BOSS_DEFEATED" : broken.Contains(target.Id, StringComparer.Ordinal) ? "PART_BROKEN" : "COMBAT" });
            }
            foreach (RaidCombatantSpec member in spec.Party) outcomes[member.Id].Injured = health[member.Id] == 0;
            return new RaidSimulationOutcome { Success = bossHp == 0, DurationTicks = Math.Min(Math.Max(0, tick - 1), spec.TimeLimitTicks), BrokenPartIds = broken, Members = outcomes.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray(), Trace = trace.TakeLast(64).ToArray() };
        }
    }
}
