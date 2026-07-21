using System;
using System.Collections.Generic;

namespace KingdomTycoon.Presentation.Combat
{
    public enum WorldFacilityAction
    {
        Recruitment,
        Mercenaries,
        Store,
        Inventory,
        EquipmentGrowth,
        Production,
        Progression
    }

    public sealed class WorldFacilityInteractionDefinition
    {
        public WorldFacilityInteractionDefinition(string id, string title, string description, string primaryLabel,
            WorldFacilityAction primaryAction, string secondaryLabel, WorldFacilityAction secondaryAction)
        {
            Id = id; Title = title; Description = description; PrimaryLabel = primaryLabel; PrimaryAction = primaryAction;
            SecondaryLabel = secondaryLabel; SecondaryAction = secondaryAction;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string PrimaryLabel { get; }
        public WorldFacilityAction PrimaryAction { get; }
        public string SecondaryLabel { get; }
        public WorldFacilityAction SecondaryAction { get; }
    }

    /// <summary>Single presentation contract for world-building labels and feature routes.</summary>
    public static class WorldFacilityInteractionCatalog
    {
        private static readonly Dictionary<string, WorldFacilityInteractionDefinition> Definitions =
            new Dictionary<string, WorldFacilityInteractionDefinition>(StringComparer.Ordinal)
            {
                ["FAC_TAVERN"] = new("FAC_TAVERN", "황금 사슴 주점", "새 용병을 만나고 왕국의 동료를 관리합니다.", "용병 모집", WorldFacilityAction.Recruitment, "용병 관리", WorldFacilityAction.Mercenaries),
                ["FAC_STORE"] = new("FAC_STORE", "왕국 잡화점", "사냥에서 얻은 물품을 정리하고 필요한 보급품을 확인합니다.", "상점 열기", WorldFacilityAction.Store, "가방 확인", WorldFacilityAction.Inventory),
                ["FAC_BLACKSMITH"] = new("FAC_BLACKSMITH", "불꽃 대장간", "용병 장비를 점검하고 왕국 생산 시설을 운영합니다.", "장비 공방", WorldFacilityAction.EquipmentGrowth, "제작 관리", WorldFacilityAction.Production),
                ["FAC_INFIRMARY"] = new("FAC_INFIRMARY", "푸른잎 치료소", "귀환한 용병의 상태와 회복에 필요한 생산품을 확인합니다.", "회복품 제작", WorldFacilityAction.Production, "용병 상태", WorldFacilityAction.Mercenaries),
                ["FAC_GUILD"] = new("FAC_GUILD", "왕국 모험가 길드", "용병의 승급 조건과 현재 활동 명단을 관리합니다.", "승급 관리", WorldFacilityAction.Progression, "용병 관리", WorldFacilityAction.Mercenaries)
            };

        public static IReadOnlyCollection<WorldFacilityInteractionDefinition> All => Definitions.Values;

        public static WorldFacilityInteractionDefinition Get(string facilityId) =>
            facilityId != null && Definitions.TryGetValue(facilityId, out WorldFacilityInteractionDefinition value)
                ? value
                : throw new ArgumentOutOfRangeException(nameof(facilityId), facilityId, "등록되지 않은 왕국 시설입니다.");
    }
}
