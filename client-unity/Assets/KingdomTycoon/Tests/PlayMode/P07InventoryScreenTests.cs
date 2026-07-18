using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Inventory;
using KingdomTycoon.Presentation.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P07InventoryScreenTests
    {
        [UnitySetUp]
        public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown]
        public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator InventorySceneBootstrapsServiceAndStableHierarchy()
        {
            yield return LoadInventory();
            GameObject root = GameObject.Find("P07InventoryRoot");
            Assert.That(root, Is.Not.Null);
            Assert.That(root.GetComponent<InventoryScreenPresenter>(), Is.Not.Null);
            Assert.That(AppRoot.Instance.Services.Get<InventoryGameService>().IsBootstrapped, Is.True);
            string[] required = { "P07_UI_GRID", "P07_UI_CATEGORY", "P07_UI_SORT", "P07_UI_ITEM_DETAIL", "P07_UI_COMPARE", "P07_UI_EQUIP", "P07_UI_UNEQUIP", "P07_UI_SELL", "P07_UI_SELL_MODAL", "P07_UI_POLICY_MODAL", "P07_UI_POTION_PANEL", "P07_UI_TRANSFER", "P07_UI_RETURN", "P07_UI_OVERFLOW", "P07_UI_STATE" };
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            foreach (string id in required) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id);
        }

        [UnityTest]
        public IEnumerator TerminalLootMutatesOneDraftWithEquipmentLinks()
        {
            yield return LoadInventory();
            InventoryGameService inventory = AppRoot.Instance.Services.Get<InventoryGameService>();
            FacilityGameService game = AppRoot.Instance.Services.Get<FacilityGameService>();
            var draft = game.Snapshot();
            string[] party = draft["payload"]!["mercenaries"]!.Children<Newtonsoft.Json.Linq.JObject>().Take(4).Select(value => value.Value<string>("instanceId")).ToArray();
            InventorySettlementMutation result = inventory.ApplyTerminalLoot(draft, System.Guid.Parse("019f8320-1800-7000-8000-000000000002"), 4, party);
            Assert.That(result.RetainedItems, Is.EqualTo(1));
            Assert.That(result.RetainedEquipment, Is.EqualTo(1));
            Assert.That(draft["payload"]!["inventory"]!["itemStacks"]!.Count(), Is.EqualTo(1));
            Assert.That(draft["payload"]!["inventory"]!["equipment"]!.Count(), Is.EqualTo(1));
            Assert.That(result.EquipmentInstanceId, Is.Not.Null.And.Not.Empty);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainUsableAtTwentyByNine()
        {
            yield return LoadInventory();
            GameObject root = GameObject.Find("P07InventoryRoot");
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                foreach (Button button in root.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name);
                    Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name);
                }
            }
        }

        private static IEnumerator LoadInventory()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized);
            yield return AppRoot.Instance.Services.Get<KingdomTycoon.Services.SceneFlowService>().LoadSceneAsync("Inventory");
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Inventory" && Object.FindAnyObjectByType<InventoryScreenView>() != null);
            yield return null;
        }
    }
}
