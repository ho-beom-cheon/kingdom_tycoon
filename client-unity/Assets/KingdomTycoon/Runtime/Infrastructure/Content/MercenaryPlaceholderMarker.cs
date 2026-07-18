using UnityEngine;

namespace KingdomTycoon.Infrastructure.Content
{
    public sealed class MercenaryPlaceholderMarker : MonoBehaviour
    {
        [SerializeField] private string jobId;
        [SerializeField] private string assetId;
        [SerializeField] private string fingerprint;

        public string JobId => jobId;

        public string AssetId => assetId;

        public string Fingerprint => fingerprint;

        public Sprite PortraitSprite
        {
            get
            {
                SpriteRenderer renderer = transform.Find("Body")?.GetComponent<SpriteRenderer>();
                return renderer != null ? renderer.sprite : null;
            }
        }

        public void Configure(string configuredJobId, string configuredAssetId, string configuredFingerprint)
        {
            jobId = configuredJobId;
            assetId = configuredAssetId;
            fingerprint = configuredFingerprint;
        }
    }
}
