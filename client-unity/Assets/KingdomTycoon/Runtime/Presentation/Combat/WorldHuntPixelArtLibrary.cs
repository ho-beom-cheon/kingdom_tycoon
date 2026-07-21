using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Owns the deterministic, zero-cost pixel sprites used by the continuous hunting world.</summary>
    public sealed class WorldHuntPixelArtLibrary : IDisposable
    {
        public const int CharacterSize = 32;
        public const int KingdomSize = 48;
        public const int FacilitySize = 64;
        private static readonly string[] JobIds = { "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC" };
        private static readonly string[] Themes = { "MEADOW", "FOREST", "MINE", "SWAMP", "FROST_RUIN" };
        private static readonly string[] FacilityIds = { "FAC_TAVERN", "FAC_STORE", "FAC_BLACKSMITH", "FAC_INFIRMARY", "FAC_GUILD" };
        private static readonly Color32 Transparent = new(0, 0, 0, 0);
        private static readonly Color32 Outline = new(24, 20, 27, 255);
        private static readonly Color32 Skin = new(202, 145, 101, 255);
        private static readonly Color32 SkinLight = new(238, 193, 139, 255);

        private readonly Dictionary<string, Sprite> jobs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> monsters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> decorations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> facilities = new(StringComparer.Ordinal);
        private readonly List<Sprite> sprites = new();
        private readonly List<Texture2D> textures = new();
        private Sprite kingdom;

        public WorldHuntPixelArtLibrary()
        {
            foreach (string jobId in JobIds) jobs.Add(jobId, Create($"헌트_직업_{jobId}", CharacterSize, canvas => DrawJob(canvas, jobId)));
            foreach (string theme in Themes)
            {
                monsters.Add(MonsterKey(theme, false), Create($"헌트_몬스터_{theme}_일반", CharacterSize, canvas => DrawMonster(canvas, theme, false)));
                monsters.Add(MonsterKey(theme, true), Create($"헌트_몬스터_{theme}_정예", CharacterSize, canvas => DrawMonster(canvas, theme, true)));
                decorations.Add(theme, Create($"헌트_환경_{theme}", CharacterSize, canvas => DrawDecoration(canvas, theme)));
            }
            kingdom = Create("헌트_왕국_랜드마크", KingdomSize, DrawKingdom);
            foreach (string facilityId in FacilityIds)
                facilities.Add(facilityId, Create($"헌트_시설_{facilityId}", FacilitySize, canvas => DrawFacility(canvas, facilityId)));
        }

        public int SpriteCount => sprites.Count;
        public bool IsDisposed { get; private set; }
        public IReadOnlyList<Sprite> AllSprites => sprites;
        public Sprite KingdomSprite => EnsureAlive(kingdom);

        public Sprite JobSprite(string jobId)
        {
            EnsureAlive();
            return jobs.TryGetValue(jobId ?? string.Empty, out Sprite value) ? value : jobs["JOB_WARRIOR"];
        }

        public Sprite MonsterSprite(string theme, bool elite)
        {
            EnsureAlive();
            string knownTheme = Array.IndexOf(Themes, theme) >= 0 ? theme : "MEADOW";
            return monsters[MonsterKey(knownTheme, elite)];
        }

        public Sprite DecorationSprite(string theme)
        {
            EnsureAlive();
            return decorations.TryGetValue(theme ?? string.Empty, out Sprite value) ? value : decorations["MEADOW"];
        }

        public Sprite FacilitySprite(string facilityId)
        {
            EnsureAlive();
            return facilities.TryGetValue(facilityId ?? string.Empty, out Sprite value) ? value : facilities["FAC_TAVERN"];
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            foreach (Sprite sprite in sprites) DestroyOwned(sprite);
            foreach (Texture2D texture in textures) DestroyOwned(texture);
            sprites.Clear(); textures.Clear(); jobs.Clear(); monsters.Clear(); decorations.Clear(); facilities.Clear(); kingdom = null;
        }

        private Sprite Create(string name, int size, Action<PixelCanvas> draw)
        {
            var pixels = new Color32[size * size];
            for (int index = 0; index < pixels.Length; index++) pixels[index] = Transparent;
            draw(new PixelCanvas(size, pixels));
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name + "_텍스처",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels); texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .1f), size, 0, SpriteMeshType.FullRect);
            sprite.name = name; sprite.hideFlags = HideFlags.DontSave; textures.Add(texture); sprites.Add(sprite); return sprite;
        }

        private static void DrawJob(PixelCanvas c, string jobId)
        {
            (Color32 dark, Color32 main, Color32 light, Color32 accent) = JobPalette(jobId);
            c.Ellipse(16, 3, 9, 2, Outline, true);
            c.Rect(11, 4, 14, 9, Outline); c.Rect(18, 4, 21, 9, Outline);
            c.Rect(12, 5, 14, 9, dark); c.Rect(18, 5, 20, 9, dark);
            c.Rect(9, 9, 22, 20, Outline); c.Rect(10, 10, 21, 19, main);
            c.Rect(11, 17, 20, 20, light); c.Rect(6, 11, 10, 18, Outline); c.Rect(7, 12, 9, 17, dark);
            c.Rect(21, 11, 25, 18, Outline); c.Rect(22, 12, 24, 17, dark);
            c.Ellipse(16, 23, 6, 6, Outline, true); c.Ellipse(16, 23, 5, 5, Skin, true); c.Rect(13, 24, 17, 27, SkinLight);
            c.Set(14, 23, Outline); c.Set(18, 23, Outline);

            switch (jobId)
            {
                case "JOB_WARRIOR":
                    c.Line(23, 8, 29, 27, Outline, 3); c.Line(24, 9, 28, 26, accent, 1); c.Line(21, 13, 27, 11, Outline, 2);
                    c.Rect(8, 11, 10, 19, dark); c.Set(9, 20, light); break;
                case "JOB_GUARDIAN":
                    c.Ellipse(7, 14, 6, 8, Outline, true); c.Ellipse(7, 15, 5, 7, accent, true); c.Rect(6, 8, 8, 20, light); c.Set(7, 7, Outline);
                    c.Rect(10, 18, 22, 21, dark); break;
                case "JOB_ARCHER":
                    c.Arc(25, 16, 6, 10, Outline); c.Arc(25, 16, 5, 9, accent); c.Line(25, 7, 25, 25, light); c.Line(21, 16, 30, 16, Outline, 2); c.Set(30, 16, accent);
                    c.Rect(9, 17, 11, 25, dark); break;
                case "JOB_MAGE":
                    c.Triangle(10, 27, 22, 27, 18, 31, Outline); c.Triangle(11, 27, 21, 27, 18, 30, accent); c.Rect(10, 25, 22, 28, Outline); c.Rect(11, 26, 21, 27, dark);
                    c.Line(6, 6, 7, 26, Outline, 3); c.Line(7, 7, 7, 25, light); c.Ellipse(7, 27, 3, 3, accent, true); break;
                case "JOB_CLERIC":
                    c.Arc(16, 23, 7, 7, Outline); c.Arc(16, 23, 6, 6, light); c.Rect(10, 18, 22, 21, light);
                    c.Line(26, 11, 26, 23, Outline, 3); c.Line(22, 18, 30, 18, Outline, 3); c.Line(26, 12, 26, 22, accent); c.Line(23, 18, 29, 18, accent); break;
            }
        }

        private static void DrawMonster(PixelCanvas c, string theme, bool elite)
        {
            (Color32 dark, Color32 main, Color32 light, Color32 accent) = ThemePalette(theme, elite);
            c.Ellipse(16, 3, 10, 2, Outline, true);
            switch (theme)
            {
                case "FOREST": DrawForestMonster(c, dark, main, light, accent, elite); break;
                case "MINE": DrawMineMonster(c, dark, main, light, accent, elite); break;
                case "SWAMP": DrawSwampMonster(c, dark, main, light, accent, elite); break;
                case "FROST_RUIN": DrawFrostMonster(c, dark, main, light, accent, elite); break;
                default: DrawMeadowMonster(c, dark, main, light, accent, elite); break;
            }
        }

        private static void DrawMeadowMonster(PixelCanvas c, Color32 dark, Color32 main, Color32 light, Color32 accent, bool elite)
        {
            c.Ellipse(15, 14, 10, 7, Outline, true); c.Ellipse(15, 15, 9, 6, main, true); c.Ellipse(24, 16, 5, 5, Outline, true); c.Ellipse(24, 16, 4, 4, dark, true);
            c.Rect(8, 5, 11, 10, Outline); c.Rect(19, 5, 22, 10, Outline); c.Rect(9, 6, 10, 10, dark); c.Rect(20, 6, 21, 10, dark);
            c.Rect(26, 14, 30, 17, Outline); c.Rect(27, 15, 30, 16, SkinLight); c.Set(25, 18, SkinLight); c.Set(23, 18, Outline); c.Rect(8, 18, 16, 20, light);
            if (elite) { c.Line(22, 20, 19, 29, Outline, 3); c.Line(26, 20, 30, 28, Outline, 3); c.Line(22, 21, 20, 28, accent); c.Line(26, 21, 29, 27, accent); }
            else { c.Triangle(22, 20, 24, 20, 23, 24, Outline); c.Triangle(25, 20, 27, 20, 26, 23, Outline); }
        }

        private static void DrawForestMonster(PixelCanvas c, Color32 dark, Color32 main, Color32 light, Color32 accent, bool elite)
        {
            c.Ellipse(15, 14, 9, 6, Outline, true); c.Ellipse(15, 15, 8, 5, main, true); c.Ellipse(24, 17, 5, 5, Outline, true); c.Ellipse(24, 17, 4, 4, dark, true);
            c.Triangle(21, 21, 24, 21, 22, 28, Outline); c.Triangle(25, 21, 28, 21, 28, 28, Outline); c.Set(25, 18, SkinLight); c.Set(23, 18, Outline);
            c.Rect(9, 5, 11, 11, Outline); c.Rect(19, 5, 21, 11, Outline); c.Rect(10, 6, 10, 11, dark); c.Rect(20, 6, 20, 11, dark);
            c.Line(7, 16, 2, 22, Outline, 3); c.Line(6, 17, 2, 21, light); c.Rect(9, 18, 18, 20, light);
            if (elite) for (int x = 9; x <= 18; x += 3) { c.Triangle(x, 20, x + 2, 20, x + 1, 25, accent); c.Set(x + 1, 24, Outline); }
        }

        private static void DrawMineMonster(PixelCanvas c, Color32 dark, Color32 main, Color32 light, Color32 accent, bool elite)
        {
            c.Rect(9, 9, 22, 21, Outline); c.Rect(10, 10, 21, 20, main); c.Rect(12, 20, 20, 26, Outline); c.Rect(13, 21, 19, 25, dark);
            c.Rect(4, 10, 9, 18, Outline); c.Rect(5, 11, 8, 17, dark); c.Rect(22, 10, 27, 18, Outline); c.Rect(23, 11, 26, 17, dark);
            c.Rect(11, 4, 15, 10, Outline); c.Rect(18, 4, 22, 10, Outline); c.Rect(12, 5, 14, 9, dark); c.Rect(19, 5, 21, 9, dark);
            c.Set(14, 23, accent); c.Set(18, 23, accent); c.Rect(11, 17, 20, 20, light);
            if (elite) { c.Triangle(5, 18, 10, 18, 7, 28, Outline); c.Triangle(22, 18, 27, 18, 25, 29, Outline); c.Triangle(6, 19, 9, 19, 7, 27, accent); c.Triangle(23, 19, 26, 19, 25, 28, accent); }
        }

        private static void DrawSwampMonster(PixelCanvas c, Color32 dark, Color32 main, Color32 light, Color32 accent, bool elite)
        {
            c.Ellipse(16, 12, 12, 9, Outline, true); c.Ellipse(16, 13, 11, 8, main, true); c.Ellipse(11, 20, 4, 5, Outline, true); c.Ellipse(21, 20, 4, 5, Outline, true);
            c.Ellipse(11, 20, 3, 4, light, true); c.Ellipse(21, 20, 3, 4, light, true); c.Set(11, 21, Outline); c.Set(21, 21, Outline); c.Rect(11, 10, 21, 12, dark); c.Rect(13, 9, 19, 10, accent);
            if (elite) { c.Triangle(9, 23, 13, 23, 11, 30, Outline); c.Triangle(15, 23, 19, 23, 17, 31, Outline); c.Triangle(20, 23, 24, 23, 22, 30, Outline); c.Set(11, 28, accent); c.Set(17, 29, accent); c.Set(22, 28, accent); }
        }

        private static void DrawFrostMonster(PixelCanvas c, Color32 dark, Color32 main, Color32 light, Color32 accent, bool elite)
        {
            c.Ellipse(16, 23, 6, 6, Outline, true); c.Ellipse(16, 23, 5, 5, light, true); c.Set(14, 24, Outline); c.Set(18, 24, Outline); c.Rect(14, 20, 18, 22, dark);
            c.Line(16, 8, 16, 18, Outline, 5); c.Line(16, 9, 16, 18, main, 3); c.Line(10, 17, 22, 17, Outline, 3); c.Line(8, 11, 24, 11, Outline, 2);
            c.Line(13, 8, 10, 3, Outline, 3); c.Line(19, 8, 22, 3, Outline, 3); c.Set(14, 24, accent); c.Set(18, 24, accent);
            if (elite) { c.Line(12, 27, 8, 31, Outline, 3); c.Line(20, 27, 24, 31, Outline, 3); c.Line(12, 28, 9, 31, accent); c.Line(20, 28, 23, 31, accent); }
        }

        private static void DrawDecoration(PixelCanvas c, string theme)
        {
            (Color32 dark, Color32 main, Color32 light, Color32 accent) = DecorationPalette(theme);
            c.Ellipse(16, 3, 11, 2, Outline, true);
            switch (theme)
            {
                case "FOREST":
                    c.Rect(14, 4, 18, 15, Outline); c.Rect(15, 5, 17, 15, dark); c.Triangle(5, 12, 27, 12, 16, 29, Outline); c.Triangle(7, 13, 25, 13, 16, 28, main); c.Triangle(10, 18, 22, 18, 16, 27, light); break;
                case "MINE":
                    c.Triangle(4, 5, 20, 5, 11, 20, Outline); c.Triangle(6, 6, 18, 6, 11, 18, main); c.Triangle(16, 5, 29, 5, 24, 15, Outline); c.Triangle(18, 6, 28, 6, 24, 14, dark); c.Triangle(12, 8, 18, 8, 16, 25, accent); c.Line(16, 10, 16, 23, light, 2); break;
                case "SWAMP":
                    c.Ellipse(16, 6, 12, 3, dark, true); c.Ellipse(16, 7, 10, 2, main, true); for (int x = 7; x <= 25; x += 6) { c.Line(x, 7, x + 1, 24, Outline, 3); c.Line(x + 1, 8, x + 1, 23, light); c.Line(x + 1, 18, x + 5, 21, accent, 2); } break;
                case "FROST_RUIN":
                    c.Triangle(5, 5, 15, 5, 11, 26, Outline); c.Triangle(7, 6, 14, 6, 11, 24, main); c.Triangle(15, 5, 28, 5, 22, 31, Outline); c.Triangle(17, 6, 26, 6, 22, 29, accent); c.Line(22, 8, 22, 27, light, 2); break;
                default:
                    for (int x = 6; x <= 26; x += 5) { c.Line(x, 5, x + (x % 2 == 0 ? -3 : 3), 20 + x % 6, Outline, 3); c.Line(x, 6, x + (x % 2 == 0 ? -2 : 2), 19 + x % 6, main); } c.Set(10, 19, accent); c.Set(22, 21, light); break;
            }
        }

        private static void DrawKingdom(PixelCanvas c)
        {
            Color32 stoneDark = new(72, 67, 67, 255), stone = new(111, 105, 96, 255), stoneLight = new(154, 143, 122, 255);
            Color32 roof = new(112, 57, 48, 255), warm = new(243, 181, 76, 255), smoke = new(151, 151, 143, 255);
            c.Ellipse(24, 3, 20, 3, Outline, true); c.Rect(11, 8, 37, 31, Outline); c.Rect(12, 9, 36, 30, stone); c.Rect(16, 25, 32, 39, Outline); c.Rect(17, 26, 31, 38, stoneLight);
            c.Triangle(13, 39, 35, 39, 24, 47, Outline); c.Triangle(15, 39, 33, 39, 24, 45, roof); c.Rect(7, 14, 14, 32, Outline); c.Rect(8, 15, 13, 31, stoneDark); c.Rect(34, 14, 41, 32, Outline); c.Rect(35, 15, 40, 31, stoneDark);
            c.Triangle(6, 32, 15, 32, 10, 39, Outline); c.Triangle(8, 32, 13, 32, 10, 37, roof); c.Triangle(33, 32, 42, 32, 38, 39, Outline); c.Triangle(35, 32, 40, 32, 38, 37, roof);
            c.Rect(21, 9, 27, 23, Outline); c.Rect(22, 10, 26, 22, stoneDark); c.Rect(17, 29, 21, 34, Outline); c.Rect(18, 30, 20, 33, warm); c.Rect(27, 29, 31, 34, Outline); c.Rect(28, 30, 30, 33, warm);
            c.Rect(4, 8, 13, 15, Outline); c.Rect(5, 9, 12, 14, roof); c.Rect(35, 8, 45, 15, Outline); c.Rect(36, 9, 44, 14, roof); c.Line(41, 15, 41, 24, Outline, 3); c.Set(42, 25, smoke); c.Set(43, 27, smoke); c.Set(42, 29, smoke);
        }

        private static void DrawFacility(PixelCanvas c, string facilityId)
        {
            Color32 shadow = new(48, 45, 45, 255);
            Color32 wall = new(151, 124, 88, 255);
            Color32 wallLight = new(196, 166, 112, 255);
            (Color32 roof, Color32 accent) = facilityId switch
            {
                "FAC_STORE" => (new Color32(188, 123, 49, 255), new Color32(246, 205, 96, 255)),
                "FAC_BLACKSMITH" => (new Color32(115, 61, 51, 255), new Color32(235, 112, 59, 255)),
                "FAC_INFIRMARY" => (new Color32(55, 125, 116, 255), new Color32(157, 226, 191, 255)),
                "FAC_GUILD" => (new Color32(65, 80, 126, 255), new Color32(200, 180, 94, 255)),
                _ => (new Color32(139, 71, 48, 255), new Color32(231, 170, 77, 255))
            };

            c.Ellipse(32, 5, 27, 4, shadow, true);
            c.Rect(12, 10, 52, 39, Outline); c.Rect(14, 12, 50, 38, wall);
            c.Triangle(7, 39, 57, 39, 32, 61, Outline); c.Triangle(10, 40, 54, 40, 32, 58, roof);
            c.Rect(27, 10, 38, 29, Outline); c.Rect(29, 11, 36, 27, shadow);
            c.Rect(17, 22, 25, 31, Outline); c.Rect(19, 24, 23, 29, accent);
            c.Rect(41, 22, 49, 31, Outline); c.Rect(43, 24, 47, 29, accent);
            c.Line(14, 37, 50, 37, wallLight, 2);

            switch (facilityId)
            {
                case "FAC_TAVERN":
                    c.Rect(8, 24, 16, 37, Outline); c.Rect(10, 26, 14, 35, accent); c.Line(7, 20, 17, 20, Outline, 3); c.Line(9, 21, 15, 21, wallLight); break;
                case "FAC_STORE":
                    for (int x = 14; x <= 46; x += 8) c.Rect(x, 32, x + 5, 39, x % 16 == 14 ? accent : wallLight);
                    c.Rect(45, 7, 56, 17, Outline); c.Rect(47, 9, 54, 15, roof); break;
                case "FAC_BLACKSMITH":
                    c.Rect(45, 40, 53, 57, Outline); c.Rect(47, 42, 51, 55, shadow); c.Triangle(5, 8, 20, 8, 13, 17, Outline); c.Rect(9, 8, 17, 11, accent); break;
                case "FAC_INFIRMARY":
                    c.Rect(27, 44, 37, 57, Outline); c.Rect(21, 48, 43, 54, Outline); c.Rect(29, 45, 35, 56, accent); c.Rect(23, 50, 41, 52, accent); break;
                case "FAC_GUILD":
                    c.Rect(5, 16, 14, 47, Outline); c.Rect(7, 18, 12, 45, roof); c.Triangle(4, 47, 15, 47, 10, 58, Outline); c.Triangle(6, 47, 13, 47, 10, 56, accent); c.Line(10, 18, 10, 42, wallLight, 2); break;
            }
        }

        private static (Color32 dark, Color32 main, Color32 light, Color32 accent) JobPalette(string id) => id switch
        {
            "JOB_GUARDIAN" => (new(47, 66, 86, 255), new(78, 109, 140, 255), new(119, 151, 178, 255), new(198, 216, 226, 255)),
            "JOB_ARCHER" => (new(45, 77, 49, 255), new(68, 121, 73, 255), new(105, 158, 93, 255), new(218, 184, 98, 255)),
            "JOB_MAGE" => (new(55, 49, 103, 255), new(80, 76, 152, 255), new(121, 119, 197, 255), new(107, 221, 222, 255)),
            "JOB_CLERIC" => (new(104, 83, 45, 255), new(188, 154, 75, 255), new(237, 214, 151, 255), new(255, 241, 178, 255)),
            _ => (new(91, 42, 37, 255), new(163, 70, 54, 255), new(211, 105, 75, 255), new(232, 190, 105, 255))
        };

        private static (Color32 dark, Color32 main, Color32 light, Color32 accent) ThemePalette(string theme, bool elite)
        {
            if (elite) return (new(69, 40, 82, 255), new(119, 68, 139, 255), new(180, 116, 195, 255), new(231, 179, 247, 255));
            return theme switch
            {
                "FOREST" => (new(50, 66, 39, 255), new(83, 111, 56, 255), new(132, 151, 77, 255), new(217, 187, 97, 255)),
                "MINE" => (new(61, 61, 68, 255), new(101, 96, 91, 255), new(149, 137, 119, 255), new(88, 202, 203, 255)),
                "SWAMP" => (new(49, 66, 42, 255), new(78, 113, 56, 255), new(129, 157, 79, 255), new(207, 193, 80, 255)),
                "FROST_RUIN" => (new(61, 78, 91, 255), new(92, 131, 147, 255), new(157, 198, 207, 255), new(190, 239, 247, 255)),
                _ => (new(80, 48, 35, 255), new(139, 78, 52, 255), new(190, 122, 77, 255), new(232, 183, 99, 255))
            };
        }

        private static (Color32 dark, Color32 main, Color32 light, Color32 accent) DecorationPalette(string theme) => theme switch
        {
            "FOREST" => (new(35, 61, 45, 255), new(49, 94, 58, 255), new(77, 128, 73, 255), new(177, 147, 72, 255)),
            "MINE" => (new(61, 57, 57, 255), new(94, 84, 74, 255), new(137, 123, 102, 255), new(86, 192, 192, 255)),
            "SWAMP" => (new(48, 65, 47, 255), new(72, 99, 55, 255), new(110, 139, 72, 255), new(158, 132, 61, 255)),
            "FROST_RUIN" => (new(62, 81, 92, 255), new(91, 135, 151, 255), new(157, 201, 213, 255), new(204, 241, 247, 255)),
            _ => (new(42, 73, 43, 255), new(65, 112, 57, 255), new(103, 153, 76, 255), new(227, 184, 78, 255))
        };

        private void EnsureAlive()
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(WorldHuntPixelArtLibrary));
        }

        private Sprite EnsureAlive(Sprite value) { EnsureAlive(); return value; }
        private static string MonsterKey(string theme, bool elite) => theme + ":" + (elite ? "ELITE" : "NORMAL");
        private static void DestroyOwned(UnityEngine.Object value) { if (value == null) return; if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }

        private sealed class PixelCanvas
        {
            private readonly int size;
            private readonly Color32[] pixels;
            public PixelCanvas(int size, Color32[] pixels) { this.size = size; this.pixels = pixels; }
            public void Set(int x, int y, Color32 color) { if (x >= 0 && x < size && y >= 0 && y < size) pixels[y * size + x] = color; }
            public void Rect(int minX, int minY, int maxX, int maxY, Color32 color) { for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++) Set(x, y, color); }
            public void Ellipse(int cx, int cy, int rx, int ry, Color32 color, bool filled)
            {
                for (int y = cy - ry; y <= cy + ry; y++) for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float d = (x - cx) * (x - cx) / (float)(rx * rx) + (y - cy) * (y - cy) / (float)(ry * ry);
                    if (filled ? d <= 1f : d <= 1f && d >= .62f) Set(x, y, color);
                }
            }
            public void Arc(int cx, int cy, int rx, int ry, Color32 color) => Ellipse(cx, cy, rx, ry, color, false);
            public void Triangle(int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
            {
                int minX = Math.Min(x1, Math.Min(x2, x3)), maxX = Math.Max(x1, Math.Max(x2, x3)); int minY = Math.Min(y1, Math.Min(y2, y3)), maxY = Math.Max(y1, Math.Max(y2, y3));
                int area = Edge(x1, y1, x2, y2, x3, y3);
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    int a = Edge(x1, y1, x2, y2, x, y), b = Edge(x2, y2, x3, y3, x, y), d = Edge(x3, y3, x1, y1, x, y);
                    if (area >= 0 ? a >= 0 && b >= 0 && d >= 0 : a <= 0 && b <= 0 && d <= 0) Set(x, y, color);
                }
            }
            public void Line(int x0, int y0, int x1, int y1, Color32 color, int thickness = 1)
            {
                int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, error = dx + dy;
                while (true)
                {
                    int radius = Math.Max(0, thickness - 1) / 2; for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++) Set(x0 + x, y0 + y, color);
                    if (x0 == x1 && y0 == y1) break; int twice = 2 * error; if (twice >= dy) { error += dy; x0 += sx; } if (twice <= dx) { error += dx; y0 += sy; }
                }
            }
            private static int Edge(int ax, int ay, int bx, int by, int px, int py) => (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        }
    }
}
