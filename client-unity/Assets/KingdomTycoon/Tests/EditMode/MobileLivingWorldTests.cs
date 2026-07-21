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
        public void FiveRegionsFormAnObliqueRingWithoutCoveringKingdom()
        {
            string[] ids = { "REGION_R01", "REGION_R02", "REGION_R03", "REGION_R04", "REGION_R05" };
            Vector2[] positions = ids.Select(MobileLivingWorldLayout.RegionPosition).ToArray();
            Assert.That(positions.Distinct().Count(), Is.EqualTo(5));
            Assert.That(positions.All(value => value.magnitude >= 850f), Is.True);
            CollectionAssert.AreEquivalent(new[] { "북서쪽", "북동쪽", "남동쪽", "남서쪽", "북쪽" }, ids.Select(MobileLivingWorldLayout.DirectionOf));
            Assert.That(MobileLivingWorldLayout.WorldSize, Is.EqualTo(new Vector2(3000f, 3600f)));
        }

        [Test]
        public void ProjectionCompressesDepthAndShearsTheWorldConsistently()
        {
            Vector2 projected = MobileLivingWorldLayout.Project(new Vector2(100f, 500f));
            Assert.That(projected, Is.EqualTo(new Vector2(180f, 370f)));
            Assert.That(MobileLivingWorldLayout.RegionPosition("REGION_R01"), Is.EqualTo(MobileLivingWorldLayout.Project(new Vector2(-620f, 1050f))));
            Assert.That(MobileLivingWorldLayout.KingdomGateForRegion("REGION_R03").y, Is.LessThan(0f));
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
        public void EveryVisibleFacilityHasTwoKoreanFeatureRoutes()
        {
            Assert.That(WorldFacilityInteractionCatalog.All.Count, Is.EqualTo(5));
            WorldFacilityInteractionDefinition tavern = WorldFacilityInteractionCatalog.Get("FAC_TAVERN");
            Assert.That(tavern.PrimaryLabel, Is.EqualTo("용병 모집"));
            Assert.That(tavern.PrimaryAction, Is.EqualTo(WorldFacilityAction.Recruitment));
            Assert.That(tavern.SecondaryAction, Is.EqualTo(WorldFacilityAction.Mercenaries));
            WorldFacilityInteractionDefinition blacksmith = WorldFacilityInteractionCatalog.Get("FAC_BLACKSMITH");
            Assert.That(blacksmith.PrimaryAction, Is.EqualTo(WorldFacilityAction.EquipmentGrowth));
            Assert.That(blacksmith.SecondaryAction, Is.EqualTo(WorldFacilityAction.Production));
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
