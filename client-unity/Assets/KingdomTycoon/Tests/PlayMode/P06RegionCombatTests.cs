using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P06RegionCombatTests
    {
        [UnitySetUp]
        public IEnumerator ResetPersistentAppRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator DestroyPersistentAppRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegionSceneBindsPresenterViewAndStableIds()
        {
            yield return LoadRegion();
            GameObject root = GameObject.Find("P06RegionCombatRoot");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<RegionCombatPresenter>(), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<RegionCombatView>(true), Is.Not.Null);
            string[] required = { "P06_UI_PARTY_MODAL", "P06_UI_MEMBER_CARD", "P06_UI_ENCOUNTER", "P06_UI_AUTONOMY", "P06_UI_RECALL", "P06_UI_RECALL_MODAL", "P06_UI_PAUSE", "P06_UI_RESULT", "P06_UI_STATE_OVERLAY" };
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (string id in required) Assert.That(children.Count(value => value.name == id), Is.EqualTo(1), id);
        }

        [UnityTest]
        public IEnumerator StartAndRecallCommitExactlyTwoRevisions()
        {
            yield return LoadRegion();
            CombatGameService combat = AppRoot.Instance.Services.Get<CombatGameService>();
            RegionCombatPresenter presenter = Object.FindAnyObjectByType<RegionCombatPresenter>();
            RegionCombatView view = Object.FindAnyObjectByType<RegionCombatView>();
            Assert.That(presenter.IsStarted, Is.True);
            long before = combat.Revision;
            view.TriggerStartForFixture();
            for (int frame = 0; frame < 10 && !combat.GetSnapshot().Active; frame++) yield return null;
            Assert.That(combat.GetSnapshot().Active, Is.True, presenter.LastError);
            Assert.That(combat.Revision, Is.EqualTo(before + 1));
            Assert.That(combat.GetSnapshot().Members.Count, Is.InRange(1, 4));
            view.TriggerRecallForFixture();
            for (int frame = 0; frame < 10 && combat.GetSnapshot().Active; frame++) yield return null;
            Assert.That(combat.GetSnapshot().Active, Is.False, presenter.LastError);
            Assert.That(combat.Revision, Is.EqualTo(before + 2));
            Assert.That(combat.GetSnapshot().ReasonCode, Is.EqualTo("IDLE_TOWN"));
        }

        [UnityTest]
        public IEnumerator TouchTargetsAndSixUiStatesRemainUsableAtTwentyByNine()
        {
            yield return LoadRegion();
            RegionCombatView view = Object.FindAnyObjectByType<RegionCombatView>();
            foreach (RegionUiState state in System.Enum.GetValues(typeof(RegionUiState)))
            {
                view.SetState(state);
                yield return null;
            }
            view.SetState(RegionUiState.Content);
            foreach ((int width, int height) in new[] { (1920, 1080), (2160, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false);
                yield return null;
                Canvas.ForceUpdateCanvases();
                RectTransform[] regionTargets = view.transform.root.GetComponentsInChildren<RectTransform>(true);
                foreach (string id in new[] { "P06_UI_START", "P06_UI_RECALL", "P06_UI_PAUSE", "P06_UI_STATE_ACTION" })
                {
                    RectTransform target = regionTargets.Single(value => value.name == id);
                    Assert.That(target.rect.width, Is.GreaterThanOrEqualTo(64f), id);
                    Assert.That(target.rect.height, Is.GreaterThanOrEqualTo(64f), id);
                }
            }
        }

        private static IEnumerator LoadRegion()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized);
            yield return AppRoot.Instance.Services.Get<KingdomTycoon.Services.SceneFlowService>().LoadSceneAsync("Region");
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Region" && Object.FindAnyObjectByType<RegionCombatView>() != null);
            yield return null;
        }
    }
}
