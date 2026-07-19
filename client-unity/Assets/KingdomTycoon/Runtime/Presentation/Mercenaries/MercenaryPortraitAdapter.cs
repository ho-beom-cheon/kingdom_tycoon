using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KingdomTycoon.Infrastructure.Content;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KingdomTycoon.Presentation.Mercenaries
{
    public sealed class MercenaryPortraitAdapter : IDisposable
    {
        private static readonly IReadOnlyDictionary<string, string> Addresses = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["JOB_WARRIOR"] = "ASSET_MERC_PLACEHOLDER_WARRIOR_V1", ["JOB_GUARDIAN"] = "ASSET_MERC_PLACEHOLDER_GUARDIAN_V1",
            ["JOB_ARCHER"] = "ASSET_MERC_PLACEHOLDER_ARCHER_V1", ["JOB_MAGE"] = "ASSET_MERC_PLACEHOLDER_MAGE_V1", ["JOB_CLERIC"] = "ASSET_MERC_PLACEHOLDER_CLERIC_V1"
        };
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> handles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> sprites = new(StringComparer.Ordinal);

        public async Task LoadAsync()
        {
            foreach (KeyValuePair<string, string> pair in Addresses)
            {
                AsyncOperationHandle<GameObject> handle = Addressables.LoadAssetAsync<GameObject>(pair.Value);
                handles[pair.Key] = handle;
                GameObject prefab = await handle.Task;
                MercenaryPlaceholderMarker marker = prefab != null ? prefab.GetComponent<MercenaryPlaceholderMarker>() : null;
                if (handle.Status != AsyncOperationStatus.Succeeded || marker == null || marker.JobId != pair.Key || marker.PortraitSprite == null)
                    throw new InvalidOperationException("P05_PORTRAIT_ASSET_MISSING");
                sprites[pair.Key] = marker.PortraitSprite;
            }
        }
        public Sprite Resolve(string jobId) => sprites.TryGetValue(jobId ?? string.Empty, out Sprite value) ? value : null;
        public void Dispose() { foreach (AsyncOperationHandle<GameObject> handle in handles.Values) if (handle.IsValid()) Addressables.Release(handle); handles.Clear(); sprites.Clear(); }
    }
}
