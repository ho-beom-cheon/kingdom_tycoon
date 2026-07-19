using System;
using System.Linq;
using KingdomTycoon.Application.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Combat
{
    public sealed class RegionCombatView : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button recallButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stateActionButton;
        [SerializeField] private TMP_Text encounterLabel;
        [SerializeField] private TMP_Text autonomyLabel;
        [SerializeField] private TMP_Text partyLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private GameObject contentRoot;
        [SerializeField] private GameObject stateOverlay;
        [SerializeField] private GameObject offlineBadge;

        public event Action StartRequested;
        public event Action RecallRequested;
        public event Action PauseRequested;
        public event Action RetryRequested;
        public void TriggerStartForFixture() => StartRequested?.Invoke();
        public void TriggerRecallForFixture() => RecallRequested?.Invoke();

        public void Configure(Button start, Button recall, Button pause, Button stateAction, TMP_Text encounter, TMP_Text autonomy, TMP_Text party, TMP_Text state, GameObject content, GameObject overlay, GameObject offline)
        {
            startButton = start;
            recallButton = recall;
            pauseButton = pause;
            stateActionButton = stateAction;
            encounterLabel = encounter;
            autonomyLabel = autonomy;
            partyLabel = party;
            stateLabel = state;
            contentRoot = content;
            stateOverlay = overlay;
            offlineBadge = offline;
        }

        private void Awake()
        {
            startButton?.onClick.AddListener(() => StartRequested?.Invoke());
            recallButton?.onClick.AddListener(() => RecallRequested?.Invoke());
            pauseButton?.onClick.AddListener(() => PauseRequested?.Invoke());
            stateActionButton?.onClick.AddListener(() => RetryRequested?.Invoke());
        }

        public void Render(HuntSnapshotDto snapshot)
        {
            bool active = snapshot?.Active == true;
            encounterLabel.text = active
                ? $"왕국 외곽 초원 · 전투 {snapshot.EncounterIndex + 1} · 적 {snapshot.HostileCount}"
                : "평온한 들판 초원 · 사냥 준비";
            autonomyLabel.text = active
                ? $"진행 {snapshot.Tick} · {State(snapshot.State)} · {Reason(snapshot.ReasonCode)}"
                : "활동 용병 1~4명을 선택해 사냥을 시작하세요.";
            partyLabel.text = active
                ? string.Join("\n", snapshot.Members.Select((value, index) => $"용병 {index + 1}  체력 {value.CurrentHp}/{value.MaxHp}  기여 {value.DamageDealt}"))
                : "파티 미편성";
            startButton.gameObject.SetActive(!active);
            recallButton.gameObject.SetActive(active);
            pauseButton.interactable = active;
        }

        public void SetState(RegionUiState state, string message = null)
        {
            bool content = state is RegionUiState.Content or RegionUiState.Offline;
            contentRoot.SetActive(content);
            stateOverlay.SetActive(!content);
            offlineBadge.SetActive(state == RegionUiState.Offline);
            stateLabel.text = message ?? state switch
            {
                RegionUiState.Loading => "지역 정보를 불러오는 중입니다.",
                RegionUiState.Empty => "사냥에 참여할 활동 용병이 없습니다.",
                RegionUiState.Error => "전투 정보를 표시할 수 없습니다.",
                RegionUiState.Locked => "평온한 들판 초원은 아직 잠겨 있습니다.",
                _ => string.Empty
            };
            stateActionButton.gameObject.SetActive(state is RegionUiState.Error or RegionUiState.Empty);
        }

        public void SetPaused(bool paused)
        {
            pauseButton.GetComponentInChildren<TMP_Text>().text = paused ? "계속" : "일시정지";
        }

        private static string State(string value) => value switch
        {
            "PREPARE" => "출정 준비", "TRAVEL_TO_REGION" => "지역 이동", "FIND_TARGET" => "적 탐색", "COMBAT" => "전투 중",
            "LOOT" => "전리품 수습", "CONTINUE_DECISION" => "다음 행동 결정", "RETURN_TOWN" => "마을 귀환", _ => "자동 행동"
        };

        private static string Reason(string value) => value switch
        {
            "TARGET_FOUND" => "적 발견", "POLICY" => "파견 정책", "NONE" => "정상", "BAG_FULL" => "가방 가득 참",
            "POTION_LOW" => "물약 부족", "HP_LOW" => "체력 부족", _ => "상황 판단"
        };
    }
}
