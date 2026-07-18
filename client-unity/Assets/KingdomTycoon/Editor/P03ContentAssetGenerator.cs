using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KingdomTycoon.Infrastructure.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P03ContentAssetGenerator
    {
        public const string GroupName = "Content-Mercenary-Placeholders-v1";
        public const string ManagedRoot = "Assets/KingdomTycoon/ContentGenerated/Mercenaries/Placeholders";
        private const string Fingerprint = "P03_PLACEHOLDER_V1";

        private static readonly PlaceholderSpec[] Specs =
        {
            new("JOB_WARRIOR", "Warrior", "ASSET_MERC_PLACEHOLDER_WARRIOR_V1", new Color32(0xC9, 0x4F, 0x4F, 0xFF), RoleMark.Sword),
            new("JOB_GUARDIAN", "Guardian", "ASSET_MERC_PLACEHOLDER_GUARDIAN_V1", new Color32(0x4F, 0x78, 0xC9, 0xFF), RoleMark.Shield),
            new("JOB_ARCHER", "Archer", "ASSET_MERC_PLACEHOLDER_ARCHER_V1", new Color32(0x4F, 0xAE, 0x68, 0xFF), RoleMark.Bow),
            new("JOB_MAGE", "Mage", "ASSET_MERC_PLACEHOLDER_MAGE_V1", new Color32(0x8B, 0x5C, 0xC7, 0xFF), RoleMark.Star),
            new("JOB_CLERIC", "Cleric", "ASSET_MERC_PLACEHOLDER_CLERIC_V1", new Color32(0xE0, 0xC8, 0x5A, 0xFF), RoleMark.Cross),
        };

        [MenuItem("Kingdom Tycoon/P03/Generate Canonical Content Assets")]
        public static void GenerateAndVerify()
        {
            EnsureSortingLayer("Characters");
            EnsureFolders(ManagedRoot);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true)
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            AddressableAssetGroup group = EnsureGroup(settings);

            foreach (PlaceholderSpec spec in Specs)
            {
                Generate(spec, settings, group);
            }

            RemoveStaleManagedPrefabs(settings);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Verify();
            Debug.Log("P03 canonical content assets generated and verified.");
        }

        [MenuItem("Kingdom Tycoon/P03/Verify Canonical Content Assets")]
        public static void Verify()
        {
            VerifyPackage();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new BuildFailedException("CONTENT_PLACEHOLDER_ASSET_MISSING: Addressables settings are missing.");
            var addresses = settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
                .GroupBy(entry => entry.address, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

            HashSet<string> registryIds = LoadAssetRegistryIds();
            foreach (PlaceholderSpec spec in Specs)
            {
                string prefabPath = PrefabPath(spec);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    throw new BuildFailedException($"CONTENT_PLACEHOLDER_ASSET_MISSING: {prefabPath}");
                }

                MercenaryPlaceholderMarker marker = prefab.GetComponent<MercenaryPlaceholderMarker>();
                if (marker == null || marker.JobId != spec.JobId || marker.AssetId != spec.AssetId || marker.Fingerprint != Fingerprint || !registryIds.Contains(spec.AssetId))
                {
                    throw new BuildFailedException($"CONTENT_ASSET_REGISTRY_MISMATCH: {spec.AssetId}");
                }

                if (!addresses.TryGetValue(spec.AssetId, out AddressableAssetEntry[] entries))
                {
                    throw new BuildFailedException($"CONTENT_PLACEHOLDER_ASSET_MISSING: address {spec.AssetId}");
                }
                if (entries.Length != 1)
                {
                    throw new BuildFailedException($"CONTENT_ADDRESSABLE_DUPLICATE_ADDRESS: {spec.AssetId}");
                }
                if (entries[0].AssetPath != prefabPath)
                {
                    throw new BuildFailedException($"CONTENT_ASSET_REGISTRY_MISMATCH: address {spec.AssetId}");
                }
                if (prefab.GetComponentsInChildren<Animator>(true).Length != 0 || prefab.GetComponentsInChildren<Animation>(true).Length != 0)
                {
                    throw new BuildFailedException($"CONTENT_ASSET_REGISTRY_MISMATCH: animation component on {spec.AssetId}");
                }
            }
        }

        private static void VerifyPackage()
        {
            string root = Path.Combine(Application.dataPath, "StreamingAssets", "Content");
            string manifest = File.ReadAllText(Path.Combine(root, "content_manifest.json"), new UTF8Encoding(false, true));
            ContentImportResult result = new CsvContentImporter().Import(
                manifest,
                file => File.ReadAllText(Path.Combine(root, file), new UTF8Encoding(false, true)));
            if (!result.IsValid || result.Catalog.Tables.Count != 60)
            {
                throw new BuildFailedException("CONTENT_MANIFEST_HASH_MISMATCH: canonical package validation failed. " + string.Join("; ", result.Report.Issues));
            }
        }

        private static HashSet<string> LoadAssetRegistryIds()
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets", "Content", "asset_register.csv");
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            return lines.Skip(1).Where(line => line.Length > 0).Select(line => line.Split(',')[0]).ToHashSet(StringComparer.Ordinal);
        }

        private static void Generate(PlaceholderSpec spec, AddressableAssetSettings settings, AddressableAssetGroup group)
        {
            string bodyPath = $"{ManagedRoot}/{spec.Name}.Body.asset";
            string markPath = $"{ManagedRoot}/{spec.Name}.RoleMark.asset";
            string prefabPath = PrefabPath(spec);
            AssetDatabase.DeleteAsset(bodyPath);
            AssetDatabase.DeleteAsset(markPath);
            AssetDatabase.DeleteAsset(prefabPath);

            Sprite body = CreateSpriteAsset(bodyPath, 64, spec.Color, (_, _) => true, "BodySprite");
            Sprite mark = CreateSpriteAsset(markPath, 16, new Color32(0xFF, 0xFF, 0xFF, 0xFF), (x, y) => MarkPixel(spec.Mark, x, y), "RoleMarkSprite");

            var root = new GameObject(spec.AssetId, typeof(MercenaryPlaceholderMarker));
            root.GetComponent<MercenaryPlaceholderMarker>().Configure(spec.JobId, spec.AssetId, Fingerprint);
            CreateRenderer("Body", root.transform, body, 0);
            CreateRenderer("RoleMark", root.transform, mark, 1);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = spec.AssetId;
            entry.SetLabel("P03-Placeholder", true, true);
        }

        private static Sprite CreateSpriteAsset(string path, int size, Color32 color, Func<int, int, bool> filled, string spriteName)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = Path.GetFileNameWithoutExtension(path),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var transparent = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = filled(x, y) ? color : transparent;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
            sprite.name = spriteName;
            AssetDatabase.AddObjectToAsset(sprite, texture);
            return sprite;
        }

        private static void CreateRenderer(string name, Transform parent, Sprite sprite, int order)
        {
            var child = new GameObject(name, typeof(SpriteRenderer));
            child.transform.SetParent(parent, false);
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Characters";
            renderer.sortingOrder = order;
        }

        private static bool MarkPixel(RoleMark mark, int x, int y) => mark switch
        {
            RoleMark.Sword => Math.Abs(x - y) <= 1 || y <= 3 && x >= 10,
            RoleMark.Shield => x >= 3 && x <= 12 && y >= 3 && y <= 13 && (y >= 6 || x >= 5 && x <= 10),
            RoleMark.Bow => Math.Abs((x - 7) * (x - 7) + (y - 7) * (y - 7) - 36) <= 10 || x + y == 14,
            RoleMark.Star => x == 7 || y == 7 || x == y || x + y == 14,
            RoleMark.Cross => x is >= 6 and <= 9 || y is >= 6 and <= 9,
            _ => false
        };

        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = settings.FindGroup(GroupName);
            return group ?? settings.CreateGroup(
                GroupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        private static void RemoveStaleManagedPrefabs(AddressableAssetSettings settings)
        {
            HashSet<string> expected = Specs.Select(PrefabPath).ToHashSet(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ManagedRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!expected.Contains(path) && prefab != null && prefab.GetComponent<MercenaryPlaceholderMarker>() != null)
                {
                    settings.RemoveAssetEntry(guid);
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static string PrefabPath(PlaceholderSpec spec) => $"{ManagedRoot}/{spec.Name}.prefab";

        private static void EnsureFolders(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }

        private static void EnsureSortingLayer(string layerName)
        {
            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").First();
            var serialized = new SerializedObject(tagManager);
            SerializedProperty layers = serialized.FindProperty("m_SortingLayers");
            for (int index = 0; index < layers.arraySize; index++)
            {
                if (layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == layerName) return;
            }
            layers.InsertArrayElementAtIndex(layers.arraySize);
            SerializedProperty layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            layer.FindPropertyRelative("name").stringValue = layerName;
            layer.FindPropertyRelative("uniqueID").longValue = 193_484;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class PlaceholderSpec
        {
            public PlaceholderSpec(string jobId, string name, string assetId, Color32 color, RoleMark mark)
            {
                JobId = jobId;
                Name = name;
                AssetId = assetId;
                Color = color;
                Mark = mark;
            }

            public string JobId { get; }
            public string Name { get; }
            public string AssetId { get; }
            public Color32 Color { get; }
            public RoleMark Mark { get; }
        }
        private enum RoleMark { Sword, Shield, Bow, Star, Cross }
    }

    internal sealed class P03ContentBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => P03ContentAssetGenerator.Verify();
    }
}
