using UnityEngine;

namespace KingdomTycoon.UI
{
    public sealed class CommonUiRoot : MonoBehaviour
    {
        public static readonly string[] RequiredLayers =
        {
            "WorldCanvas",
            "HudCanvas",
            "ScreenCanvas",
            "DrawerCanvas",
            "ModalCanvas",
            "ToastCanvas",
            "TutorialCanvas",
            "DebugCanvas",
        };

        public bool HasAllRequiredLayers()
        {
            foreach (string layerName in RequiredLayers)
            {
                if (transform.Find(layerName) == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
