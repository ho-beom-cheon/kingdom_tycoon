using System;
using System.Linq;
using KingdomTycoon.Application.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Inventory
{
    public enum InventoryUiState { Loading, Content, Empty, Error, Locked, Offline }

    public sealed class InventoryScreenView : MonoBehaviour
    {
        [SerializeField] private TMP_Text capacity;
        [SerializeField] private TMP_Text grid;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private TMP_Text stateMessage;
        [SerializeField] private GameObject content;
        [SerializeField] private GameObject stateOverlay;
        [SerializeField] private GameObject policyModal;
        [SerializeField] private GameObject saleModal;
        [SerializeField] private GameObject lootModal;
        [SerializeField] private Button policyButton;
        [SerializeField] private Button sellButton;

        public void Configure(TMP_Text capacityLabel, TMP_Text gridLabel, TMP_Text detailLabel, TMP_Text stateLabel,
            GameObject contentRoot, GameObject stateRoot, GameObject policyRoot, GameObject saleRoot, GameObject lootRoot,
            Button policies, Button sell)
        {
            capacity = capacityLabel; grid = gridLabel; detail = detailLabel; stateMessage = stateLabel;
            content = contentRoot; stateOverlay = stateRoot; policyModal = policyRoot; saleModal = saleRoot; lootModal = lootRoot;
            policyButton = policies; sellButton = sell;
        }

        public void Bind(Action showPolicy, Action showSale)
        {
            policyButton.onClick.RemoveAllListeners(); sellButton.onClick.RemoveAllListeners();
            policyButton.onClick.AddListener(() => showPolicy()); sellButton.onClick.AddListener(() => showSale());
        }

        public void Render(InventorySnapshotDto snapshot)
        {
            if (snapshot == null) { ShowState(InventoryUiState.Error, "P07_CONTENT_MISSING"); return; }
            content.SetActive(true); stateOverlay.SetActive(false);
            capacity.text = $"재료 {snapshot.Items.Count(value => value.Kind == "ITEM")}/{snapshot.ItemSlots}   장비 {snapshot.Items.Count(value => value.Kind == "EQUIPMENT")}/{snapshot.EquipmentSlots}   포션 {snapshot.Items.Count(value => value.Kind == "POTION")}/{snapshot.PotionSlots}";
            InventoryItemDto[] values = snapshot.Items.Take(30).ToArray();
            grid.text = values.Length == 0 ? "보관 중인 아이템이 없습니다." : string.Join("\n", values.Select((value, index) =>
                $"{index + 1:00}  [{Kind(value.Kind)}] {DisplayName(value.Name)}  {value.Quantity}개  {(value.Locked ? "잠금" : string.Empty)}"));
            InventoryItemDto first = values.FirstOrDefault();
            detail.text = first == null ? "아이템을 선택하면 상세 정보가 표시됩니다." :
                $"{DisplayName(first.Name)}\n품질  {Quality(first.Quality)}\n단계  {first.Tier}\n장비 점수  {Math.Max(0, first.Score)}\n\n공격력  +95\n치명타  +500\n이동 속도  +0";
        }

        public void ShowState(InventoryUiState state, string diagnostic = null)
        {
            content.SetActive(state is InventoryUiState.Content or InventoryUiState.Empty or InventoryUiState.Offline);
            stateOverlay.SetActive(state is InventoryUiState.Loading or InventoryUiState.Error or InventoryUiState.Locked);
            stateMessage.text = state switch
            {
                InventoryUiState.Loading => "인벤토리를 불러오는 중입니다.",
                InventoryUiState.Locked => "인벤토리가 아직 잠겨 있습니다.",
                InventoryUiState.Error => "인벤토리를 표시할 수 없습니다.\n잠시 후 다시 열어 주세요.",
                _ => string.Empty
            };
        }

        public void ShowPolicy(bool visible) => policyModal.SetActive(visible);
        public void ShowSale(bool visible) => saleModal.SetActive(visible);
        public void ShowLoot(bool visible) => lootModal.SetActive(visible);
        private static string Kind(string value) => value switch { "ITEM" => "재료", "EQUIPMENT" => "장비", "POTION" => "물약", _ => "물품" };
        private static string Quality(string value) => value switch { "QUALITY_COMMON" => "일반", "QUALITY_FINE" => "고급", "QUALITY_RARE" => "희귀", "QUALITY_LEGACY" => "영웅", "QUALITY_RELIC" => "유물", null => "-", _ => "표준" };
        private static string DisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "이름 없는 물품";
            if (!value.StartsWith("EQ_", StringComparison.Ordinal)) return value.Contains('_') ? ItemName(value) : value;
            string[] parts = value.Split('_');
            if (parts.Length < 4) return "왕국 장비";
            string tier = parts[1].StartsWith("T", StringComparison.Ordinal) ? parts[1].Substring(1) : parts[1];
            return $"{tier}단계 {JobName(parts[2])} {SlotName(parts[3])}";
        }

        private static string JobName(string value) => value switch { "WARRIOR" => "전사", "GUARDIAN" => "수호자", "ARCHER" => "궁수", "MAGE" => "마법사", "CLERIC" => "성직자", _ => "공용" };
        private static string SlotName(string value) => value switch { "WEAPON" => "무기", "ARMOR" => "갑옷", "HELMET" => "투구", "ACCESSORY" => "장신구", _ => "장비" };
        private static string ItemName(string value) => value switch
        {
            "MAT_PROMO_BRONZE_EMBLEM" => "청동 승급 문장", "MAT_BOSS_HYDRA_VENOM" => "히드라 맹독", "MAT_BOSS_HYDRA_SCALE" => "히드라 비늘",
            "MAT_BOSS_HYDRA_HEART" => "히드라 심장", "MAT_BOSS_DRAGON_HORN" => "고룡의 뿔", "MAT_BOSS_ASH_CORE" => "잿빛 핵",
            "MAT_BOSS_DRAGON_SCALE" => "고룡의 비늘", "MAT_BOSS_DRAGON_HEART" => "고룡의 심장", "POT_HEAL_SMALL" => "소형 회복 물약", _ => "왕국 재료"
        };
    }
}
