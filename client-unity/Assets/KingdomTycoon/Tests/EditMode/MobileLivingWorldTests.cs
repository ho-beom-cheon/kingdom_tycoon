using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class MobileLivingWorldTests
    {
        [Test]
        public void FiveRegionsOccupyAllFourDirectionsWithoutCoveringKingdom()
        {
            string[] ids = { "REGION_R01", "REGION_R02", "REGION_R03", "REGION_R04", "REGION_R05" };
            Vector2[] positions = ids.Select(MobileLivingWorldLayout.RegionPosition).ToArray();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(5));
            Assert.That(positions.All(value => value.magnitude >= 850f), Is.True);
            CollectionAssert.AreEquivalent(new[] { "북쪽", "동쪽", "남쪽", "서쪽" }, ids.Select(MobileLivingWorldLayout.DirectionOf).Distinct());
        }

        [Test]
        public void EveryTownStateRoutesToAnExplicitFacility()
        {
            var expected = new Dictionary<string, string>
            {
                ["IDLE_TOWN"] = "FAC_TAVERN",
                ["SELL_LOOT"] = "FAC_STORE",
                ["HEAL"] = "FAC_INFIRMARY",
                ["BUY_CONSUMABLES"] = "FAC_STORE",
                ["EVALUATE_EQUIPMENT"] = "FAC_STORE",
                ["BUY_EQUIPMENT"] = "FAC_STORE",
                ["TRAIN_SKILLS"] = "FAC_GUILD",
                ["ENHANCE_EQUIPMENT"] = "FAC_BLACKSMITH"
            };
            foreach ((string state, string facility) in expected)
            {
                Assert.That(MobileLivingWorldLayout.FacilityForState(state), Is.EqualTo(facility), state);
                Assert.That(MobileLivingWorldLayout.FacilityPosition(facility), Is.Not.EqualTo(new Vector2(float.NaN, float.NaN)), facility);
            }
        }

        [Test]
        public void StableOffsetsAndTravelTargetsAreDeterministic()
        {
            Vector2 first = MobileLivingWorldLayout.StableOffset("018f0000-0000-7000-8000-000000000001", 60f);
            Vector2 second = MobileLivingWorldLayout.StableOffset("018f0000-0000-7000-8000-000000000001", 60f);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first.magnitude, Is.InRange(27f, 61f));

            var member = new ContinuousHuntMemberDto("MERC_A", "레온", "JOB_WARRIOR", "TRAVEL_TO_REGION", "REGION_R02",
                null, 10000, 0, 12, 0, 0, 0, 0, 0, 0, null, "NONE", "NONE");
            Vector2 target = MobileLivingWorldLayout.TargetFor(member);
            Assert.That(target.x, Is.GreaterThan(300f));
            Assert.That(target, Is.Not.EqualTo(MobileLivingWorldLayout.KingdomCenter));
        }

        [Test]
        public void Cc0EnvironmentLoadsAndUsesPointFiltering()
        {
            using var library = new MobileWorldAssetLibrary();
            Assert.That(library.HasExternalEnvironment, Is.True);
            Assert.That(library.LoadedSpriteCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(library.Ground("MEADOW"), Is.Not.Null);
            Assert.That(library.Ground("MEADOW").texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(library.Facility(true), Is.Not.Null);
            Assert.That(library.Decoration(0), Is.Not.Null);
        }
    }
}
