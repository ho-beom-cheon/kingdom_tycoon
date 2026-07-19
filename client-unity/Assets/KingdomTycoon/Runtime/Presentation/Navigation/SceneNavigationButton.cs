using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Navigation
{
    [DisallowMultipleComponent]
    public sealed class SceneNavigationButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private string targetScene;
        [SerializeField] private bool blockWhileHuntActive;
        private bool loading;

        public string TargetScene => targetScene;

        public void Configure(Button value, string sceneName, bool blockDuringHunt)
        {
            button = value;
            targetScene = sceneName;
            blockWhileHuntActive = blockDuringHunt;
        }

        private void Awake()
        {
            button ??= GetComponent<Button>();
            button.onClick.RemoveListener(Navigate);
            button.onClick.AddListener(Navigate);
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Refresh();
        }

        private void OnDisable() => SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        private void Update() => Refresh();

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Navigate);
        }

        private void OnActiveSceneChanged(Scene previous, Scene current)
        {
            loading = false;
            Refresh();
        }

        public void Navigate()
        {
            if (!CanNavigate()) return;
            loading = true;
            button.interactable = false;
            AppRoot.Instance.Services.Get<SceneFlowService>().LoadSceneAsync(targetScene);
        }

        private void Refresh()
        {
            if (button != null) button.interactable = CanNavigate();
        }

        private bool CanNavigate()
        {
            if (loading || button == null || string.IsNullOrWhiteSpace(targetScene) || AppRoot.Instance == null || !AppRoot.Instance.IsInitialized)
                return false;
            if (SceneManager.GetActiveScene().name == targetScene) return false;
            if (!blockWhileHuntActive) return true;
            try { return !AppRoot.Instance.Services.Get<CombatGameService>().GetSnapshot().Active; }
            catch { return false; }
        }
    }
}
