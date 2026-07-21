using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure.Combat;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    public enum WorldHuntFeedbackType
    {
        BasicHit,
        SkillHit,
        MonsterDefeated,
        MonsterRespawned,
        LootCollected,
        BountyEarned,
        Returning,
        Settled,
        SkillTrained,
        EquipmentEnhanced
    }

    public sealed class WorldHuntFeedbackEvent
    {
        public WorldHuntFeedbackEvent(long revision, WorldHuntFeedbackType type, string regionId, string mercenaryInstanceId,
            string monsterInstanceId, long amount, string message)
        {
            Revision = revision; Type = type; RegionId = regionId; MercenaryInstanceId = mercenaryInstanceId;
            MonsterInstanceId = monsterInstanceId; Amount = amount; Message = message;
        }

        public long Revision { get; }
        public WorldHuntFeedbackType Type { get; }
        public string RegionId { get; }
        public string MercenaryInstanceId { get; }
        public string MonsterInstanceId { get; }
        public long Amount { get; }
        public string Message { get; }
        public bool IsCombat => Type is WorldHuntFeedbackType.BasicHit or WorldHuntFeedbackType.SkillHit or WorldHuntFeedbackType.MonsterDefeated or WorldHuntFeedbackType.MonsterRespawned;
    }

    /// <summary>Turns live read-model differences into disposable presentation events.</summary>
    public sealed class WorldHuntFeedbackTracker
    {
        private ContinuousHuntOverviewDto previous;

        public void Reset() => previous = null;

        public IReadOnlyList<WorldHuntFeedbackEvent> Observe(ContinuousHuntOverviewDto current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (previous == null || current.Revision < previous.Revision)
            {
                previous = current;
                return Array.Empty<WorldHuntFeedbackEvent>();
            }
            var events = new List<WorldHuntFeedbackEvent>();
            Dictionary<string, WorldHuntMonsterDto> oldMonsters = previous.Monsters.ToDictionary(value => value.InstanceId, StringComparer.Ordinal);
            Dictionary<string, WorldHuntMonsterDto> oldSlots = previous.Monsters.ToDictionary(SlotKey, StringComparer.Ordinal);
            Dictionary<string, ContinuousHuntMemberDto> oldMembers = previous.Members.ToDictionary(value => value.InstanceId, StringComparer.Ordinal);

            foreach (WorldHuntMonsterDto monster in current.Monsters)
            {
                if (oldMonsters.TryGetValue(monster.InstanceId, out WorldHuntMonsterDto oldMonster))
                {
                    ContinuousHuntMemberDto attacker = FindAttacker(current, previous, monster.InstanceId);
                    long damage = Math.Max(0, oldMonster.CurrentHp - monster.CurrentHp);
                    if (damage > 0)
                    {
                        bool skill = attacker != null && !string.IsNullOrEmpty(attacker.LastSkillId);
                        events.Add(new WorldHuntFeedbackEvent(current.Revision, skill ? WorldHuntFeedbackType.SkillHit : WorldHuntFeedbackType.BasicHit,
                            monster.RegionId, attacker?.InstanceId, monster.InstanceId, damage,
                            skill ? $"{attacker.DisplayName} · 스킬 피해 {damage:N0}" : $"{attacker?.DisplayName ?? "용병"} · 피해 {damage:N0}"));
                    }
                    if (oldMonster.State == "ACTIVE" && monster.State == "RESPAWNING")
                    {
                        events.Add(new WorldHuntFeedbackEvent(current.Revision, WorldHuntFeedbackType.MonsterDefeated, monster.RegionId,
                            attacker?.InstanceId, monster.InstanceId, damage, $"{monster.DisplayName} 처치"));
                    }
                }
                else if (oldSlots.TryGetValue(SlotKey(monster), out WorldHuntMonsterDto oldSlot) && oldSlot.InstanceId != monster.InstanceId && monster.State == "ACTIVE")
                {
                    events.Add(new WorldHuntFeedbackEvent(current.Revision, WorldHuntFeedbackType.MonsterRespawned, monster.RegionId,
                        null, monster.InstanceId, 0, $"{monster.DisplayName} 재등장"));
                }
            }

            foreach (ContinuousHuntMemberDto member in current.Members)
            {
                if (!oldMembers.TryGetValue(member.InstanceId, out ContinuousHuntMemberDto old)) continue;
                long bagIncrease = Math.Max(0, member.BagFill - old.BagFill);
                if (bagIncrease > 0)
                    events.Add(Event(current, member, WorldHuntFeedbackType.LootCollected, bagIncrease, $"{member.DisplayName} · 전리품 +{bagIncrease:N0}"));
                long bountyIncrease = Math.Max(0, member.PendingBountyGold - old.PendingBountyGold);
                if (bountyIncrease > 0)
                    events.Add(Event(current, member, WorldHuntFeedbackType.BountyEarned, bountyIncrease, $"{member.DisplayName} · 현상금 +{bountyIncrease:N0}골드"));
                if (old.State != "RETURN_TOWN" && member.State == "RETURN_TOWN")
                    events.Add(Event(current, member, WorldHuntFeedbackType.Returning, 0, $"{member.DisplayName} · 체력/가방 점검 후 귀환"));
                long settled = Math.Max(0, member.EarnedGold - old.EarnedGold);
                if (settled > 0 || old.BagFill > 0 && member.BagFill == 0)
                    events.Add(Event(current, member, WorldHuntFeedbackType.Settled, settled,
                        settled > 0 ? $"{member.DisplayName} · 정산 +{settled:N0}골드" : $"{member.DisplayName} · 전리품 정산 완료"));
                if (member.TotalSkillLevels > old.TotalSkillLevels)
                    events.Add(Event(current, member, WorldHuntFeedbackType.SkillTrained, member.TotalSkillLevels,
                        $"{member.DisplayName} · 스킬 총 레벨 {member.TotalSkillLevels}"));
                if (member.BestEnhancementLevel > old.BestEnhancementLevel)
                    events.Add(Event(current, member, WorldHuntFeedbackType.EquipmentEnhanced, member.BestEnhancementLevel,
                        $"{member.DisplayName} · 장비 최고 +{member.BestEnhancementLevel}"));
            }

            previous = current;
            return events;
        }

        private static WorldHuntFeedbackEvent Event(ContinuousHuntOverviewDto overview, ContinuousHuntMemberDto member,
            WorldHuntFeedbackType type, long amount, string message) => new(overview.Revision, type, member.AssignedRegionId,
            member.InstanceId, member.TargetMonsterInstanceId, amount, message);
        private static string SlotKey(WorldHuntMonsterDto value) => value.RegionId + ":" + value.SpawnSlot;
        private static ContinuousHuntMemberDto FindAttacker(ContinuousHuntOverviewDto current, ContinuousHuntOverviewDto old, string monsterId)
            => current.Members.FirstOrDefault(value => value.TargetMonsterInstanceId == monsterId)
               ?? old.Members.FirstOrDefault(value => value.TargetMonsterInstanceId == monsterId);
    }

    public static class WorldHuntFeedbackPreferences
    {
        private const string SoundKey = "KINGDOM_TYCOON_HUNT_SOUND";
        private const string ReducedMotionKey = "KINGDOM_TYCOON_HUNT_REDUCED_MOTION";
        public static bool SoundEnabled { get => PlayerPrefs.GetInt(SoundKey, 1) == 1; set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static bool ReducedMotion { get => PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1; set { PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0); PlayerPrefs.Save(); } }
        public static void ResetForTests() { PlayerPrefs.DeleteKey(SoundKey); PlayerPrefs.DeleteKey(ReducedMotionKey); }
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class WorldHuntFeedbackAudio : MonoBehaviour
    {
        private readonly Dictionary<WorldHuntFeedbackType, int> lastPlayedFrame = new();
        private AudioSource source;
        private AudioClip hit;
        private AudioClip skill;
        private AudioClip reward;
        private AudioClip growth;
        private WorldHuntFeedbackConfigDto config;

        public bool SoundEnabled => WorldHuntFeedbackPreferences.SoundEnabled;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0f; source.loop = false;
        }

        public void Configure(WorldHuntFeedbackConfigDto value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            config = value;
            if (source == null) throw new InvalidOperationException("WORLD_HUNT_AUDIO_SOURCE_MISSING");
            if (hit != null) return;
            hit = CreateTone("사냥_일반타격", value.HitFrequencyHz, .07f, false);
            skill = CreateTone("사냥_스킬", value.SkillFrequencyHz, .12f, true);
            reward = CreateTone("사냥_보상", value.RewardFrequencyHz, .11f, false);
            growth = CreateTone("사냥_성장", value.GrowthFrequencyHz, .16f, true);
        }

        public void SetSoundEnabled(bool enabled)
        {
            WorldHuntFeedbackPreferences.SoundEnabled = enabled;
            if (!enabled && source != null) source.Stop();
        }

        public void Play(WorldHuntFeedbackType type)
        {
            if (!SoundEnabled || config == null || source == null || lastPlayedFrame.GetValueOrDefault(type, -1) == Time.frameCount) return;
            lastPlayedFrame[type] = Time.frameCount;
            AudioClip clip = type switch
            {
                WorldHuntFeedbackType.BasicHit => hit,
                WorldHuntFeedbackType.SkillHit or WorldHuntFeedbackType.MonsterDefeated or WorldHuntFeedbackType.MonsterRespawned => skill,
                WorldHuntFeedbackType.SkillTrained or WorldHuntFeedbackType.EquipmentEnhanced => growth,
                _ => reward
            };
            float categoryVolume = type == WorldHuntFeedbackType.BasicHit ? .45f : type is WorldHuntFeedbackType.SkillTrained or WorldHuntFeedbackType.EquipmentEnhanced ? .9f : .7f;
            source.PlayOneShot(clip, config.MasterVolumeBps / 10000f * categoryVolume);
        }

        private void OnDisable() { if (source != null) source.Stop(); }
        private void OnDestroy()
        {
            foreach (AudioClip clip in new[] { hit, skill, reward, growth }) if (clip != null) Destroy(clip);
        }

        private static AudioClip CreateTone(string name, int frequency, float seconds, bool rising)
        {
            const int sampleRate = 22050; int sampleCount = Mathf.CeilToInt(sampleRate * seconds); var samples = new float[sampleCount];
            for (int index = 0; index < sampleCount; index++)
            {
                float progress = index / (float)sampleCount; float envelope = Mathf.Sin(progress * Mathf.PI) * (1f - progress * .35f);
                float activeFrequency = rising && progress > .5f ? frequency * 1.25f : frequency;
                samples[index] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * activeFrequency * index / sampleRate)) * envelope * .12f;
            }
            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false); clip.hideFlags = HideFlags.DontSave; clip.SetData(samples, 0); return clip;
        }
    }
}
