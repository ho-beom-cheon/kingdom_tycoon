using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class ContentDeterminismTests
    {
        [Test]
        public void SplitMix64_MercenaryGoldenMatchesVerifiedDraws()
        {
            ulong[] expected =
            {
                2466975172287755897UL,
                8832083440362974766UL,
                3534771765162737125UL,
                9592110948284743397UL,
                1881757512419323243UL,
                12920672458450473694UL,
                15403818807231698370UL
            };
            var random = new SplitMix64(123456789UL);

            Assert.That(expected.Select(_ => random.NextUInt64()).ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void SplitMix64_EquipmentGoldenMatchesVerifiedDraws()
        {
            var random = new SplitMix64(987654321UL);

            Assert.That(random.NextUInt64(), Is.EqualTo(12744715263588028796UL));
            Assert.That(random.NextUInt64(), Is.EqualTo(16192141852193020578UL));
            Assert.That(random.NextUInt64(), Is.EqualTo(16161435109270938784UL));
        }

        [Test]
        public void SplitMix64_AffixSuccessGoldenUsesInclusiveBound()
        {
            var random = new SplitMix64(1UL);

            Assert.That(random.NextBounded(4), Is.EqualTo(1UL));
            Assert.That(random.NextBounded(100), Is.EqualTo(19UL));
            Assert.That(random.NextBounded(10_000), Is.EqualTo(590UL));
            Assert.That(random.NextBounded(11), Is.EqualTo(7UL));
            Assert.That(200UL + random.NextBounded(601), Is.EqualTo(426UL));
        }

        [Test]
        public void SeedDerivation_MultiDrawGoldenUsesSha256BigEndianPrefix()
        {
            const string prefix = "KT|SUMMON_SEED_V1|1.0.0-content.1|00000000-0000-7000-8000-000000000001|00000000-0000-7000-8000-000000000002|SPECIAL_STANDARD_TICKET|";

            Assert.That(ContentSeedDerivation.DeriveUInt64(prefix + "0"), Is.EqualTo(4778811787865032672UL));
            Assert.That(ContentSeedDerivation.DeriveUInt64(prefix + "9"), Is.EqualTo(5649623414433526989UL));
        }

        [Test]
        public void GrowthFactors_MatchAllSixVerifiedStats()
        {
            const ulong growthSeed = 1881757512419323243UL;

            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "STR"), Is.EqualTo(10111));
            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "VIT"), Is.EqualTo(10336));
            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "DEX"), Is.EqualTo(9667));
            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "LUK"), Is.EqualTo(9748));
            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "INT"), Is.EqualTo(10122));
            Assert.That(ContentSeedDerivation.GrowthFactorBps("1.0.0-content.1", growthSeed, "WIS"), Is.EqualTo(9660));
        }

        [Test]
        public void IdempotencyKeys_MatchOfflineAndTutorialGoldens()
        {
            const string offline = "00000000-0000-7000-8000-000000000201|1|HUNT|MERCENARY|00000000-0000-7000-8000-000000000301";
            const string tutorial = "00000000-0000-7000-8000-000000000401|TUTORIAL|GRANT_TUTORIAL_BASIC_WEAPON_MATERIALS";

            Assert.That(ContentSeedDerivation.Sha256Hex(offline), Is.EqualTo("a356710098eaba2e8a1e231fdbf220ae3811cd7dd01590028c1ecd97deda910d"));
            Assert.That(ContentSeedDerivation.Sha256Hex(tutorial), Is.EqualTo("e990504b90ddab922d3cb0f6167b31be329327f4688109ccf27aa69bc1266017"));
        }
    }
}
