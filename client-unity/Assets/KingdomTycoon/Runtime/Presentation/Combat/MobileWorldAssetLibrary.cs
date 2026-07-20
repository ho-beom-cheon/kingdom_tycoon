using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Loads the selected CC0 environment subset and owns runtime crop sprites.</summary>
    public sealed class MobileWorldAssetLibrary : IDisposable
    {
        private const string Root = "ThirdParty/NinjaAdventure/";
        private readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);
        private readonly List<Sprite> owned = new();

        public int LoadedSpriteCount => sprites.Count;
        public bool HasExternalEnvironment => sprites.Count >= 7;

        public MobileWorldAssetLibrary()
        {
            Texture2D floor = Resources.Load<Texture2D>(Root + "tileset_floor");
            Texture2D village = Resources.Load<Texture2D>(Root + "tileset_village_abandoned");
            AddCrop("MEADOW", floor, new Rect(48f, 210f, 64f, 64f));
            AddCrop("FOREST", floor, new Rect(224f, 210f, 64f, 64f));
            AddCrop("MINE", floor, new Rect(224f, 130f, 64f, 64f));
            AddCrop("SWAMP", floor, new Rect(48f, 290f, 64f, 64f));
            AddCrop("FROST_RUIN", floor, new Rect(48f, 48f, 64f, 64f));
            AddCrop("FACILITY_LARGE", village, new Rect(145f, 0f, 82f, 80f));
            AddCrop("FACILITY_SMALL", village, new Rect(226f, 0f, 74f, 78f));
            AddWhole("GRASS", Resources.Load<Texture2D>(Root + "grass"));
            AddWhole("CRATE", Resources.Load<Texture2D>(Root + "crate"));
            AddWhole("POT", Resources.Load<Texture2D>(Root + "pot"));
        }

        public Sprite Ground(string theme) => Get(theme);
        public Sprite Facility(bool large) => Get(large ? "FACILITY_LARGE" : "FACILITY_SMALL");
        public Sprite Decoration(int index) => Get((index % 3) switch { 0 => "GRASS", 1 => "CRATE", _ => "POT" });

        public void Dispose()
        {
            foreach (Sprite sprite in owned)
                if (sprite != null)
                {
                    if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                    else UnityEngine.Object.DestroyImmediate(sprite);
                }
            owned.Clear();
            sprites.Clear();
        }

        private Sprite Get(string key) => sprites.TryGetValue(key, out Sprite value) ? value : null;

        private void AddWhole(string key, Texture2D texture)
        {
            if (texture == null) return;
            Add(key, texture, new Rect(0f, 0f, texture.width, texture.height));
        }

        private void AddCrop(string key, Texture2D texture, Rect rect)
        {
            if (texture == null || rect.xMin < 0f || rect.yMin < 0f || rect.xMax > texture.width || rect.yMax > texture.height) return;
            Add(key, texture, rect);
        }

        private void Add(string key, Texture2D texture, Rect rect)
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 16f, 0, SpriteMeshType.FullRect);
            sprite.name = "CC0_NinjaAdventure_" + key;
            sprite.hideFlags = HideFlags.DontSave;
            sprites[key] = sprite;
            owned.Add(sprite);
        }
    }
}
