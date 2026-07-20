using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class WorldHuntPixelArtTests
    {
        private static readonly string[] Jobs = { "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC" };
        private static readonly string[] Themes = { "MEADOW", "FOREST", "MINE", "SWAMP", "FROST_RUIN" };

        [Test]
        public void LibraryCreatesTwentyOnePointFilteredCachedSpritesWithinBudget()
        {
            using var library = new WorldHuntPixelArtLibrary();
            Assert.That(library.SpriteCount, Is.EqualTo(21));
            Assert.That(library.AllSprites.Sum(value => value.texture.width * value.texture.height), Is.EqualTo(22784));
            Assert.That(library.AllSprites.All(value => value.texture.filterMode == FilterMode.Point && value.texture.wrapMode == TextureWrapMode.Clamp), Is.True);
            Assert.That(ReferenceEquals(library.JobSprite("JOB_WARRIOR"), library.JobSprite("JOB_WARRIOR")), Is.True);
            Assert.That(ReferenceEquals(library.MonsterSprite("MEADOW", false), library.MonsterSprite("MEADOW", false)), Is.True);
        }

        [Test]
        public void JobsThemesAndEliteShapesHaveDistinctPixelFingerprints()
        {
            using var library = new WorldHuntPixelArtLibrary();
            Assert.That(Jobs.Select(value => Fingerprint(library.JobSprite(value))).Distinct().Count(), Is.EqualTo(5));
            Assert.That(Themes.Select(value => Fingerprint(library.DecorationSprite(value))).Distinct().Count(), Is.EqualTo(5));
            Assert.That(Themes.Select(value => Fingerprint(library.MonsterSprite(value, false))).Distinct().Count(), Is.EqualTo(5));
            foreach (string theme in Themes) Assert.That(Fingerprint(library.MonsterSprite(theme, false)), Is.Not.EqualTo(Fingerprint(library.MonsterSprite(theme, true))), theme);
        }

        [Test]
        public void EverySpriteHasTransparentCornersOpaqueSilhouetteAndEightColorsOrFewer()
        {
            using var library = new WorldHuntPixelArtLibrary();
            foreach (Sprite sprite in library.AllSprites)
            {
                Color32[] pixels = sprite.texture.GetPixels32();
                Assert.That(pixels[0].a, Is.Zero, sprite.name); Assert.That(pixels[pixels.Length - 1].a, Is.Zero, sprite.name);
                Assert.That(pixels.Count(value => value.a == 255), Is.GreaterThan(24), sprite.name);
                Assert.That(pixels.Select(ColorKey).Distinct().Count(), Is.LessThanOrEqualTo(8), sprite.name);
            }
        }

        [Test]
        public void DisposeDestroysOwnedAssetsAndRejectsFurtherAccess()
        {
            var library = new WorldHuntPixelArtLibrary(); Sprite owned = library.KingdomSprite;
            library.Dispose();
            Assert.That(library.IsDisposed, Is.True); Assert.That(library.SpriteCount, Is.Zero); Assert.That(owned == null, Is.True);
            Assert.Throws<ObjectDisposedException>(() => library.JobSprite("JOB_WARRIOR")); library.Dispose();
        }

        private static ulong Fingerprint(Sprite sprite)
        {
            ulong hash = 1469598103934665603UL;
            foreach (Color32 value in sprite.texture.GetPixels32())
            {
                hash = (hash ^ value.r) * 1099511628211UL; hash = (hash ^ value.g) * 1099511628211UL;
                hash = (hash ^ value.b) * 1099511628211UL; hash = (hash ^ value.a) * 1099511628211UL;
            }
            return hash;
        }

        private static uint ColorKey(Color32 value) => (uint)(value.r << 24 | value.g << 16 | value.b << 8 | value.a);
    }
}
