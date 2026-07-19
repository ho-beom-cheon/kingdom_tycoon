using UnityEngine;

namespace KingdomTycoon.Presentation.OfflineTutorial
{
    public sealed class OfflineTutorialEntryButton : MonoBehaviour
    {
        [SerializeField] private OfflineTutorialHubPresenter presenter;
        public void Configure(OfflineTutorialHubPresenter value) => presenter = value;
        public void OpenHub() { presenter ??= FindFirstObjectByType<OfflineTutorialHubPresenter>(FindObjectsInactive.Include); presenter?.Open(); }
    }
}
