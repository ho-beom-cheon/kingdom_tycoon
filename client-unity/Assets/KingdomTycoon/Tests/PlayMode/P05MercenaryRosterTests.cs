using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Presentation.Mercenaries;
using KingdomTycoon.Presentation.Mercenaries.Views;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P05MercenaryRosterTests
    {
        private MercenaryRosterPresenter presenter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (AppRoot.Instance != null) UnityEngine.Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom");
            presenter = UnityEngine.Object.FindAnyObjectByType<MercenaryRosterPresenter>(FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            yield return new WaitUntil(() => presenter.IsReady);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (AppRoot.Instance != null) UnityEngine.Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator P05_P_001_NavigationOpensGoldenRosterWithExactCountsAndBoundedPool()
        {
            Button nav = GameObject.Find("NAV_MERCENARIES").GetComponent<Button>();
            Assert.That(nav.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64f));
            nav.onClick.Invoke();
            yield return new WaitUntil(() => presenter.IsBound);
            Assert.That(presenter.IsOpen, Is.True);
            Assert.That(presenter.CurrentState, Is.EqualTo(MercenaryRosterUiState.CONTENT));
            Assert.That(presenter.BoundCardCount, Is.EqualTo(4));
            Assert.That(presenter.PoolCount, Is.LessThanOrEqualTo(16));
            Assert.That(presenter.OwnedCountText, Is.EqualTo("보유 4/8"));
            Assert.That(presenter.ActiveCountText, Is.EqualTo("활동 4/4"));
            Canvas.ForceUpdateCanvases();
            MercenaryCardView firstCard = presenter.RosterView.PooledCards.First(value => value.gameObject.activeSelf);
            RectTransform cardRect = firstCard.GetComponent<RectTransform>();
            Assert.That(firstCard.gameObject.activeInHierarchy, Is.True);
            Assert.That(cardRect.rect.width, Is.GreaterThan(0f));
            Assert.That(cardRect.rect.height, Is.GreaterThan(0f));
            Assert.That(firstCard.GetComponent<Graphic>().canvasRenderer.cull, Is.False,
                $"card={cardRect.rect}, world={cardRect.position}, local={cardRect.anchoredPosition}");
        }

        [UnityTest]
        public IEnumerator P05_P_002_SelectionReselectionTabsScrimAndBackCloseDrawer()
        {
            OpenRoster();
            MercenaryCardView card = UnityEngine.Object.FindAnyObjectByType<MercenaryCardView>();
            card.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(presenter.IsDrawerOpen, Is.True);
            Assert.That(presenter.SelectedMercenaryId, Is.EqualTo(card.BoundInstanceId));
            Canvas.ForceUpdateCanvases();
            CanvasGroup drawerGroup = presenter.DrawerView.GetComponent<CanvasGroup>();
            RectTransform drawerPanel = presenter.DrawerView.transform.Find("Panel").GetComponent<RectTransform>();
            Assert.That(drawerGroup.alpha, Is.EqualTo(1f).Within(0.01f),
                $"panel={drawerPanel.rect}, world={drawerPanel.position}, local={drawerPanel.anchoredPosition}");
            Assert.That(drawerPanel.GetComponent<Graphic>().canvasRenderer.cull, Is.False);
            presenter.SelectForFixture(card.BoundInstanceId);
            yield return null;
            Assert.That(presenter.DrawerBoundInstanceId, Is.EqualTo(card.BoundInstanceId));
            for (int index = 0; index < 4; index++)
            {
                presenter.DrawerView.GetTabButton(index).onClick.Invoke();
                Assert.That(presenter.DrawerView.SelectedTabIndex, Is.EqualTo(index));
            }
            string differentId = presenter.RosterView.PooledCards.First(value => value.BoundInstanceId != card.BoundInstanceId).BoundInstanceId;
            presenter.SelectForFixture(differentId);
            Assert.That(presenter.DrawerView.SelectedTabIndex, Is.Zero);
            presenter.DrawerView.CloseButton.onClick.Invoke();
            yield return new WaitUntil(() => !presenter.IsDrawerOpen);
            Assert.That(presenter.SelectedMercenaryId, Is.Null);
        }

        [UnityTest]
        public IEnumerator P05_P_003_FullFilterSearchSortAndResetPreserveExactCounts()
        {
            InjectRoster(24, 16);
            OpenRoster();
            Assert.That(presenter.OwnedCountText, Is.EqualTo("보유 24/24"));
            presenter.FilterModal.Open(presenter.CurrentQuery);
            Canvas.ForceUpdateCanvases();
            Graphic firstFilterOption = presenter.FilterModal.transform.Find("Panel/FilterScroll/Viewport/Content/Filter_JOB_JOB_WARRIOR")
                .GetComponent<Graphic>();
            Assert.That(firstFilterOption.rectTransform.rect.height, Is.GreaterThanOrEqualTo(64f));
            Assert.That(firstFilterOption.canvasRenderer.cull, Is.False,
                $"filter={firstFilterOption.rectTransform.rect}, world={firstFilterOption.rectTransform.position}");
            presenter.FilterModal.SelectForFixture("JOB", "JOB_CLERIC");
            presenter.FilterModal.SelectForFixture("ACTIVE", "ACTIVE");
            presenter.FilterModal.ApplyForFixture();
            yield return null;
            Assert.That(presenter.CurrentQuery.JobIds, Is.EqualTo(new[] { "JOB_CLERIC" }));
            Assert.That(presenter.CurrentQuery.ActiveFilter, Is.EqualTo("ACTIVE"));
            Assert.That(presenter.RosterView.FilterSummaryText, Does.Contain("직업 1").And.Contain("활동"));
            presenter.RosterView.SearchInput.SetTextWithoutNotify("세라");
            presenter.RosterView.SearchInput.onValueChanged.Invoke("세라");
            yield return null;
            Assert.That(presenter.CurrentQuery.Search, Is.EqualTo("세라"));
            presenter.SortModal.SelectForFixture("NAME_DESC");
            yield return null;
            Assert.That(presenter.CurrentQuery.SortId, Is.EqualTo("NAME_DESC"));
            presenter.FilterModal.Open(presenter.CurrentQuery);
            presenter.FilterModal.ResetForFixture();
            presenter.FilterModal.ApplyForFixture();
            presenter.RosterView.SearchInput.onValueChanged.Invoke(string.Empty);
            yield return null;
            Assert.That(presenter.CurrentQuery.JobIds, Is.Empty);
            Assert.That(presenter.CurrentQuery.ActiveFilter, Is.EqualTo("ALL"));
            Assert.That(presenter.OwnedCountText, Is.EqualTo("보유 24/24"));
        }

        [UnityTest]
        public IEnumerator P05_P_004_ActivityTogglePersistsBadgeAndCountsAcrossReload()
        {
            OpenRoster();
            string id = presenter.RosterView.PooledCards.First(value => value.gameObject.activeSelf).BoundInstanceId;
            presenter.SelectForFixture(id);
            yield return new WaitForSecondsRealtime(0.25f);
            Button toggle = presenter.DrawerView.transform.Find("Panel/ACTIVE_TOGGLE").GetComponent<Button>();
            toggle.onClick.Invoke();
            yield return null;
            Assert.That(presenter.ActiveCountText, Is.EqualTo("활동 3/4"));
            AppRoot.Instance.Services.Get<MercenaryRosterService>().Reload();
            presenter.Refresh();
            Assert.That(presenter.ActiveCountText, Is.EqualTo("활동 3/4"));
            toggle.onClick.Invoke();
            yield return null;
            Assert.That(presenter.ActiveCountText, Is.EqualTo("활동 4/4"));
        }

        [UnityTest]
        public IEnumerator P05_P_005_ActiveLimitDisablesSeventeenthWithExactReason()
        {
            InjectRoster(24, 16);
            OpenRoster();
            string inactive = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster().Cards.First(value => !value.Active).InstanceId;
            presenter.SelectForFixture(inactive);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(presenter.DrawerView.IsToggleInteractable, Is.False);
            Assert.That(presenter.DrawerView.ErrorText, Is.EqualTo("활동 가능한 용병 수가 가득 찼습니다."));
        }

        [UnityTest]
        public IEnumerator P05_P_006_AllDetailTabsProjectStrictFieldsAndDeferredCopy()
        {
            OpenRoster();
            string id = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster().Cards.First().InstanceId;
            presenter.SelectForFixture(id);
            yield return new WaitForSecondsRealtime(0.25f);
            var expected = new[] { "전투 능력치는 P06", "장비 변경은 P07", "승급 심사는 P11", "사냥" };
            for (int index = 0; index < 4; index++)
            {
                presenter.DrawerView.GetTabButton(index).onClick.Invoke();
                Assert.That(presenter.DrawerView.BodyText, Does.Contain(expected[index]));
            }
        }

        [UnityTest]
        public IEnumerator P05_P_007_SixCommonStatesBindExactPanelsWithoutPoolGrowth()
        {
            OpenRoster();
            int pool = presenter.PoolCount;
            var cases = new Dictionary<MercenaryRosterUiState, string>
            {
                [MercenaryRosterUiState.LOADING] = "용병 정보를 불러오는 중…",
                [MercenaryRosterUiState.CONTENT] = string.Empty,
                [MercenaryRosterUiState.EMPTY] = "보유한 용병이 없습니다.",
                [MercenaryRosterUiState.ERROR] = "다시 시도해 주세요.",
                [MercenaryRosterUiState.LOCKED] = "아직 이용할 수 없습니다.",
                [MercenaryRosterUiState.OFFLINE] = "로컬 저장 사용 중"
            };
            foreach (KeyValuePair<MercenaryRosterUiState, string> item in cases)
            {
                presenter.BindStateForFixture(item.Key, item.Value);
                Assert.That(presenter.CurrentState, Is.EqualTo(item.Key));
                Assert.That(presenter.RosterView.StateMessageText, Is.EqualTo(item.Value));
            }
            Assert.That(presenter.PoolCount, Is.EqualTo(pool).And.LessThanOrEqualTo(16));
            yield return null;
        }

        [UnityTest]
        public IEnumerator P05_P_008_HundredDtoScrollAndRefilterNeverGrowPoolPastSixteen()
        {
            InjectRoster(100, 16);
            OpenRoster();
            MercenaryRosterResultDto dto = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster();
            Assert.That(dto.TotalOwned, Is.EqualTo(100));
            string last = dto.Cards.Last().InstanceId;
            presenter.RosterView.ScrollTo(last);
            yield return null;
            Assert.That(presenter.PoolCount, Is.EqualTo(16));
            Assert.That(presenter.RosterView.PooledCards.Where(value => value.gameObject.activeSelf).Select(value => value.BoundInstanceId), Does.Contain(last));
            presenter.RosterView.SearchInput.onValueChanged.Invoke("없는 이름");
            yield return null;
            Assert.That(presenter.PoolCount, Is.EqualTo(16));
        }

        [UnityTest]
        public IEnumerator P05_P_009_SafeAreaAndRequiredTouchTargetsCoverSupportedAspectRatios()
        {
            OpenRoster();
            foreach ((int width, int height) in new[] { (1920, 1080), (2160, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false);
                yield return null;
                foreach (string name in new[] { "NAV_MERCENARIES", "FILTER_BUTTON", "SORT_BUTTON" })
                {
                    Rect rect = GameObject.Find(name).GetComponent<RectTransform>().rect;
                    Assert.That(rect.width, Is.GreaterThanOrEqualTo(64f), name);
                    Assert.That(rect.height, Is.GreaterThanOrEqualTo(64f), name);
                }
            }
        }

        [UnityTest]
        public IEnumerator P05_P_010_KoreanLargeTextUsesTallCardsScrollableDrawerAndNoEllipsis()
        {
            OpenRoster();
            presenter.RosterView.SetLargeTextForFixture(true);
            string id = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster().Cards.First().InstanceId;
            presenter.SelectForFixture(id);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(presenter.DrawerView.transform.Find("Panel/DETAIL_SCROLL").GetComponent<ScrollRect>(), Is.Not.Null);
            foreach (TMP_Text text in presenter.RosterView.GetComponentsInChildren<TMP_Text>(true)
                         .Concat(presenter.DrawerView.GetComponentsInChildren<TMP_Text>(true)))
                Assert.That(text.overflowMode, Is.Not.EqualTo(TextOverflowModes.Ellipsis));
            Assert.That(presenter.RosterView.PooledCards.First().GetComponent<RectTransform>().rect.height, Is.EqualTo(280f).Within(0.1f));
        }

        [UnityTest]
        public IEnumerator P05_P_011_CaptureEightAcceptanceFixtures()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("P05 captures require a rendered non-batch Game view.");
            OpenRoster();
            yield return Capture("p05_01_roster_4.png", 1920, 1080);

            InjectRoster(24, 16); presenter.Refresh();
            yield return Capture("p05_02_roster_24.png", 1920, 1080);
            string warrior = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster().Cards.First(value => value.JobId == "JOB_WARRIOR").InstanceId;
            presenter.SelectForFixture(warrior); yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("p05_03_detail_overview.png", 1920, 1080);
            presenter.DrawerView.GetTabButton(1).onClick.Invoke();
            yield return Capture("p05_04_detail_equipment.png", 1920, 1080);
            presenter.FilterModal.Open(presenter.CurrentQuery);
            presenter.FilterModal.SelectForFixture("JOB", "JOB_WARRIOR");
            presenter.FilterModal.SelectForFixture("GRADE", "GRADE_C");
            yield return Capture("p05_05_filters.png", 1920, 1080);
            presenter.FilterModal.CloseModal();
            string inactive = AppRoot.Instance.Services.Get<MercenaryRosterService>().GetRoster().Cards.First(value => !value.Active).InstanceId;
            presenter.SelectForFixture(inactive); yield return new WaitForSecondsRealtime(0.25f);
            yield return Capture("p05_06_active_limit.png", 1920, 1080);
            presenter.DrawerView.CloseButton.onClick.Invoke(); yield return new WaitUntil(() => !presenter.IsDrawerOpen);
            yield return Capture("p05_07_20x9.png", 2400, 1080);
            presenter.ShowRecoveryForFixture();
            yield return Capture("p05_08_recovery.png", 1920, 1080);
        }

        private void OpenRoster()
        {
            presenter.OpenRoster();
            Assert.That(presenter.IsBound, Is.True);
        }

        private static void InjectRoster(int count, int activeCount)
        {
            FacilityGameService game = AppRoot.Instance.Services.Get<FacilityGameService>();
            JObject document = game.Snapshot();
            JObject[] starters = document["payload"]!["mercenaries"]!.Children<JObject>().ToArray();
            var mercenaries = new JArray();
            for (int index = 0; index < count; index++)
            {
                JObject value = (JObject)starters[index % starters.Length].DeepClone();
                value["instanceId"] = $"019f7cd2-8800-7002-8000-{index + 1:000000000000}";
                value["displayName"] = value.Value<string>("displayName") + (index + 1).ToString("00");
                value["active"] = index < activeCount;
                value["autonomy"]!["state"] = "IDLE_TOWN";
                value["autonomy"]!["reasonCode"] = "NONE";
                value["autonomy"]!["currentRegionId"] = null;
                value["autonomy"]!["targetInstanceId"] = null;
                mercenaries.Add(value);
            }
            document["payload"]!["mercenaries"] = mercenaries;
            document["payload"]!["kingdom"]!["activeMercenaryLimit"] = Math.Max(16, activeCount);
            document["payload"]!["kingdom"]!["ownedMercenaryLimit"] = Math.Max(24, count);
            JObject lodge = document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_LODGE");
            lodge["level"] = 4;
            game.SynchronizeCommittedDocument(document);
        }

        private static IEnumerator Capture(string filename, int width, int height)
        {
            Screen.SetResolution(width, height, false);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            string root = Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath)!.FullName)!.FullName;
            string output = Path.Combine(root, "docs", "reports", "captures", "P05", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            if (File.Exists(output)) File.Delete(output);
            Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>();
            var modes = canvases.Select(value => value.renderMode).ToArray();
            var cameras = canvases.Select(value => value.worldCamera).ToArray();
            var distances = canvases.Select(value => value.planeDistance).ToArray();
            var cameraObject = new GameObject("P05CaptureCamera", typeof(Camera));
            Camera captureCamera = cameraObject.GetComponent<Camera>();
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.orthographic = true;
            captureCamera.nearClipPlane = 0.1f;
            captureCamera.farClipPlane = 100f;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            captureCamera.targetTexture = target;
            for (int index = 0; index < canvases.Length; index++)
            {
                if (modes[index] != RenderMode.ScreenSpaceOverlay) continue;
                canvases[index].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[index].worldCamera = captureCamera;
                canvases[index].planeDistance = 1f + canvases[index].sortingOrder * 0.01f;
            }
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            captureCamera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture.Apply(false, false);
            File.WriteAllBytes(output, texture.EncodeToPNG());
            RenderTexture.active = previous;
            for (int index = 0; index < canvases.Length; index++)
            {
                canvases[index].renderMode = modes[index];
                canvases[index].worldCamera = cameras[index];
                canvases[index].planeDistance = distances[index];
            }
            captureCamera.targetTexture = null;
            target.Release();
            UnityEngine.Object.Destroy(texture);
            cameraObject.SetActive(false);
            UnityEngine.Object.Destroy(cameraObject);
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            Assert.That(File.Exists(output), Is.True, filename);
            Assert.That(new FileInfo(output).Length, Is.GreaterThan(1024), filename);
        }
    }
}
