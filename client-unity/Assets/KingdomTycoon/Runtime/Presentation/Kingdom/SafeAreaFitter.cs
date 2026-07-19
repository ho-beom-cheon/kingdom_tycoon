using UnityEngine;

namespace KingdomTycoon.Presentation.Kingdom
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect lastSafeArea;
        private void OnEnable() => Apply();
        private void Update() { if (Screen.safeArea != lastSafeArea) Apply(); }
        private void Apply()
        {
            lastSafeArea = Screen.safeArea;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = lastSafeArea.position / new Vector2(Screen.width, Screen.height);
            rect.anchorMax = (lastSafeArea.position + lastSafeArea.size) / new Vector2(Screen.width, Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
