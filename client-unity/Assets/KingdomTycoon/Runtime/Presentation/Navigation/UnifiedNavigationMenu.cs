using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.EquipmentGrowth;
using KingdomTycoon.Presentation.Combat;
using KingdomTycoon.Presentation.Mercenaries;
using KingdomTycoon.Presentation.OfflineTutorial;
using KingdomTycoon.Presentation.Production;
using KingdomTycoon.Presentation.Progression;
using KingdomTycoon.Presentation.Raids;
using KingdomTycoon.Presentation.Recruitment;
using KingdomTycoon.Presentation.Regions;
using KingdomTycoon.Presentation.Store;
using KingdomTycoon.Services;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomTycoon.Presentation.Navigation
{
    [DisallowMultipleComponent]
    public sealed class UnifiedNavigationMenu : MonoBehaviour
    {
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject[] primaryNavigation;
        [SerializeField] private MercenaryRosterPresenter mercenaries;
        [SerializeField] private ProductionScreenPresenter production;
        [SerializeField] private EquipmentGrowthScreenPresenter equipmentGrowth;
        [SerializeField] private ProgressionScreenPresenter progression;
        [SerializeField] private RegionMapScreenPresenter regions;
        [SerializeField] private RecruitmentScreenPresenter recruitment;
        [SerializeField] private RaidScreenPresenter raids;
        [SerializeField] private OfflineTutorialHubPresenter report;
        private StoreScreenPresenter store;
        private ContinuousHuntScreenPresenter continuousHunt;

        public bool IsPrimaryNavigationVisible => (primaryNavigation ?? System.Array.Empty<GameObject>()).Any(value => value != null && value.activeSelf);

        private GameObject[] Screens => new[]
        {
            production?.gameObject,
            equipmentGrowth?.gameObject,
            progression?.gameObject,
            regions?.gameObject,
            recruitment?.gameObject,
            raids?.gameObject,
            report?.gameObject,
            store?.gameObject
        };

        public void Configure(
            GameObject panel,
            GameObject[] primaryItems,
            MercenaryRosterPresenter mercenaryScreen,
            ProductionScreenPresenter productionScreen,
            EquipmentGrowthScreenPresenter equipmentGrowthScreen,
            ProgressionScreenPresenter progressionScreen,
            RegionMapScreenPresenter regionScreen,
            RecruitmentScreenPresenter recruitmentScreen,
            RaidScreenPresenter raidScreen,
            OfflineTutorialHubPresenter reportScreen)
        {
            menuPanel = panel;
            primaryNavigation = primaryItems;
            mercenaries = mercenaryScreen;
            production = productionScreen;
            equipmentGrowth = equipmentGrowthScreen;
            progression = progressionScreen;
            regions = regionScreen;
            recruitment = recruitmentScreen;
            raids = raidScreen;
            report = reportScreen;
            CloseMenu();
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            SetNavigationVisible(SceneManager.GetActiveScene().name != "Inventory");
        }

        private void OnDisable() => SceneManager.activeSceneChanged -= ActiveSceneChanged;

        private void LateUpdate()
        {
            if (SceneManager.GetActiveScene().name != "Kingdom" || AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) return;
            if (menuPanel != null && menuPanel.activeSelf) return;
            if (mercenaries != null && mercenaries.IsOpen) return;
            if (Screens.Any(value => value != null && value.activeInHierarchy)) return;
            continuousHunt ??= ContinuousHuntScreenPresenter.Install();
            if (!continuousHunt.gameObject.activeSelf) continuousHunt.Open();
        }

        public void ToggleMenu()
        {
            continuousHunt?.Close();
            CloseScreensExcept(null);
            if (menuPanel != null) menuPanel.SetActive(!menuPanel.activeSelf);
        }

        public void CloseMenu()
        {
            if (menuPanel != null) menuPanel.SetActive(false);
        }

        public void CloseAll()
        {
            CloseMenu();
            continuousHunt?.Close();
            CloseMercenaries();
            CloseScreensExcept(null);
        }

        public void PrepareMercenaries()
        {
            CloseMenu();
            CloseScreensExcept(null);
        }

        public void PrepareProduction() => PrepareExternalOpen(production?.gameObject);
        public void PrepareRegions() => PrepareExternalOpen(regions?.gameObject);
        public void PrepareRecruitment() => PrepareExternalOpen(recruitment?.gameObject);

        public void OpenProduction()
        {
            if (production != null)
                OpenExclusive(production.gameObject, production.Open);
        }

        public void OpenMercenaries()
        {
            if (mercenaries == null) return;
            CloseMenu();
            continuousHunt?.Close();
            CloseScreensExcept(null);
            mercenaries.OpenRoster();
        }

        public void OpenRegions()
        {
            CloseMenu();
            CloseMercenaries();
            CloseScreensExcept(null);
            if (regions != null) regions.gameObject.SetActive(false);
            continuousHunt = ContinuousHuntScreenPresenter.Install();
            continuousHunt.Open();
        }

        public void OpenRecruitment()
        {
            if (recruitment != null)
                OpenExclusive(recruitment.gameObject, recruitment.Open);
        }

        public void OpenEquipmentGrowth()
        {
            if (equipmentGrowth != null)
                OpenExclusive(equipmentGrowth.gameObject, equipmentGrowth.Open);
        }

        public void OpenProgression()
        {
            if (progression != null)
                OpenExclusive(progression.gameObject, progression.Open);
        }

        public void OpenRaids()
        {
            if (raids != null)
                OpenExclusive(raids.gameObject, raids.Open);
        }

        public void OpenReport()
        {
            if (report != null)
                OpenExclusive(report.gameObject, report.Open);
        }

        public void OpenInventory()
        {
            CloseAll();
            AppRoot.Instance?.Services.Get<SceneFlowService>().LoadSceneAsync("Inventory");
        }

        public void OpenStore()
        {
            CloseAll();
            store = FindFirstObjectByType<StoreScreenPresenter>(FindObjectsInactive.Include);
            if (store != null)
            {
                store.Open();
                return;
            }
            StartCoroutine(OpenStoreAfterKingdomLoad());
        }

        public void SetNavigationVisible(bool visible)
        {
            foreach (GameObject item in primaryNavigation ?? System.Array.Empty<GameObject>())
                if (item != null)
                {
                    item.SetActive(visible);
                    TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
                    if (label != null && label.text == "지역") label.text = "사냥터";
                }
            if (!visible) CloseMenu();
        }

        private void PrepareExternalOpen(GameObject target)
        {
            CloseMenu();
            CloseScreensExcept(target);
        }

        private void OpenExclusive(GameObject target, System.Action open)
        {
            if (target == null || open == null) return;
            CloseMenu();
            continuousHunt?.Close();
            CloseMercenaries();
            CloseScreensExcept(target);
            open();
        }

        private void CloseScreensExcept(GameObject target)
        {
            foreach (GameObject screen in Screens)
            {
                if (screen != null && screen != target) screen.SetActive(false);
            }
        }

        private void CloseMercenaries()
        {
            if (mercenaries != null && mercenaries.IsOpen) mercenaries.CloseRoster();
        }

        private IEnumerator OpenStoreAfterKingdomLoad()
        {
            AsyncOperation operation = AppRoot.Instance?.Services.Get<SceneFlowService>().LoadSceneAsync("Kingdom");
            if (operation == null) yield break;
            yield return operation;
            store = FindFirstObjectByType<StoreScreenPresenter>(FindObjectsInactive.Include);
            store?.Open();
        }

        private void ActiveSceneChanged(Scene previous, Scene current)
        {
            store = null;
            SetNavigationVisible(current.name != "Inventory");
        }
    }
}
