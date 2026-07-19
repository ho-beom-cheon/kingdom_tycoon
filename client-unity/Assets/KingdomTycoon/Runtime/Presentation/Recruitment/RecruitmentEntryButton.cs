using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Recruitment
{
    public sealed class RecruitmentEntryButton : MonoBehaviour
    {
        [SerializeField] private RecruitmentScreenPresenter presenter;
        [SerializeField] private Button button;
        public void Configure(RecruitmentScreenPresenter value, Button source) { presenter = value; button = source; }
        public void OpenRecruitment()
        {
            presenter ??= FindFirstObjectByType<RecruitmentScreenPresenter>(FindObjectsInactive.Include);
            presenter?.Open();
        }
    }
}
