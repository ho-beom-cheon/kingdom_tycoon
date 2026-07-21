using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Combat;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class WorldHuntFeedbackTests
    {
        [Test]
        public void FirstObservationAndSameRevisionNeverCreateDuplicateFeedback()
        {
            var tracker = new WorldHuntFeedbackTracker(); ContinuousHuntOverviewDto baseline = Overview(1, Member(), Monster());
            Assert.That(tracker.Observe(baseline), Is.Empty);
            Assert.That(tracker.Observe(baseline), Is.Empty);
            Assert.That(tracker.Observe(Overview(2, Member(), Monster())), Is.Empty);
        }

        [Test]
        public void SameSaveRevisionStillEmitsOneLiveCombatDifference()
        {
            var tracker = new WorldHuntFeedbackTracker();
            string monsterId = "019f9000-0000-7000-8000-000000000201";
            tracker.Observe(Overview(3, Member(target: monsterId), Monster(monsterId, 100, "ACTIVE")));
            ContinuousHuntOverviewDto damaged = Overview(3, Member(target: monsterId, damage: 25), Monster(monsterId, 75, "ACTIVE"));

            Assert.That(tracker.Observe(damaged).Single().Amount, Is.EqualTo(25));
            Assert.That(tracker.Observe(damaged), Is.Empty, "같은 실시간 상태를 다시 발행해도 피드백은 중복되면 안 됩니다.");
        }

        [Test]
        public void DamageSkillDefeatAndRespawnUseTheCorrectMonsterAndAttacker()
        {
            var tracker = new WorldHuntFeedbackTracker(); string monsterId = "019f9000-0000-7000-8000-000000000201";
            tracker.Observe(Overview(1, Member(target: monsterId), Monster(monsterId, 100, "ACTIVE")));
            IReadOnlyList<WorldHuntFeedbackEvent> hit = tracker.Observe(Overview(2, Member(target: monsterId, damage: 30, skill: "SKILL_POWER"), Monster(monsterId, 70, "ACTIVE")));
            WorldHuntFeedbackEvent skill = hit.Single();
            Assert.That(skill.Type, Is.EqualTo(WorldHuntFeedbackType.SkillHit)); Assert.That(skill.Amount, Is.EqualTo(30)); Assert.That(skill.MercenaryInstanceId, Is.EqualTo("MERC_1"));

            IReadOnlyList<WorldHuntFeedbackEvent> death = tracker.Observe(Overview(3, Member(target: null, damage: 70), Monster(monsterId, 0, "RESPAWNING")));
            Assert.That(death.Any(value => value.Type == WorldHuntFeedbackType.MonsterDefeated && value.MonsterInstanceId == monsterId), Is.True);

            string replacement = "019f9000-0000-7000-8000-000000000202";
            IReadOnlyList<WorldHuntFeedbackEvent> respawn = tracker.Observe(Overview(4, Member(), Monster(replacement, 120, "ACTIVE")));
            Assert.That(respawn.Single().Type, Is.EqualTo(WorldHuntFeedbackType.MonsterRespawned)); Assert.That(respawn.Single().MonsterInstanceId, Is.EqualTo(replacement));
        }

        [Test]
        public void LootReturnSettlementSkillAndEnhancementBecomeKoreanFeedback()
        {
            var tracker = new WorldHuntFeedbackTracker(); tracker.Observe(Overview(10, Member(), Monster()));
            ContinuousHuntMemberDto changed = Member(state: "RETURN_TOWN", bag: 2, earned: 25, bounty: 14, totalSkill: 2, enhancement: 1);
            IReadOnlyList<WorldHuntFeedbackEvent> events = tracker.Observe(Overview(11, changed, Monster()));
            foreach (WorldHuntFeedbackType type in new[] { WorldHuntFeedbackType.LootCollected, WorldHuntFeedbackType.BountyEarned, WorldHuntFeedbackType.Returning, WorldHuntFeedbackType.Settled, WorldHuntFeedbackType.SkillTrained, WorldHuntFeedbackType.EquipmentEnhanced })
                Assert.That(events.Any(value => value.Type == type), Is.True, type.ToString());
            string messages = string.Join(" ", events.Select(value => value.Message));
            Assert.That(messages, Does.Contain("전리품")); Assert.That(messages, Does.Contain("현상금")); Assert.That(messages, Does.Contain("귀환")); Assert.That(messages, Does.Contain("장비 최고"));
        }

        private static ContinuousHuntOverviewDto Overview(long revision, ContinuousHuntMemberDto member, WorldHuntMonsterDto monster)
            => new(revision, new[] { member }, new[] { new WorldHuntRegionDto("REGION_R01", "왕국 외곽 초원", 1, 1, 100, 8, 760, "MEADOW", true, 1, "안정", "부드러운 목재 · 장비") },
                new[] { monster }, new WorldHuntFeedbackConfigDto(.16f, .36f, .85f, 4f, 3, 2200, 180, 480, 720, 920));

        private static ContinuousHuntMemberDto Member(string state = "COMBAT", string target = null, int bag = 0, long earned = 0, long bounty = 0,
            int totalSkill = 0, int enhancement = 0, int damage = 0, string skill = null)
            => new("MERC_1", "아론", "JOB_WARRIOR", state, "REGION_R01", target, 10000, bag, 12, 0, earned, bounty,
                totalSkill, enhancement, damage, skill, "NONE", "GROWTH_NONE");

        private static WorldHuntMonsterDto Monster(string id = "019f9000-0000-7000-8000-000000000201", int hp = 100, string state = "ACTIVE")
            => new(id, "MON_R01_SLIME", "초원 슬라임", "REGION_R01", "NORMAL", 1, hp, 100, state, 0, hp > 0 ? "MERC_1" : null);
    }
}
