using System;
using System.Collections.Generic;
using KingdomTycoon.Infrastructure.Combat;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Presentation-only canonical nodes for the continuous mobile world.</summary>
    public static class MobileLivingWorldLayout
    {
        public static readonly Vector2 WorldSize = new(3000f, 4200f);
        public static readonly Vector2 KingdomCenter = Vector2.zero;

        private static readonly IReadOnlyDictionary<string, Vector2> Regions = new Dictionary<string, Vector2>(StringComparer.Ordinal)
        {
            ["REGION_R01"] = new(0f, 1250f),
            ["REGION_R02"] = new(900f, 350f),
            ["REGION_R03"] = new(0f, -1300f),
            ["REGION_R04"] = new(-900f, 350f),
            ["REGION_R05"] = new(780f, 1500f)
        };

        private static readonly IReadOnlyDictionary<string, Vector2> Facilities = new Dictionary<string, Vector2>(StringComparer.Ordinal)
        {
            ["FAC_TAVERN"] = new(-330f, 260f),
            ["FAC_STORE"] = new(330f, 240f),
            ["FAC_BLACKSMITH"] = new(-330f, -230f),
            ["FAC_INFIRMARY"] = new(330f, -230f),
            ["FAC_GUILD"] = new(0f, 30f)
        };

        public static Vector2 RegionPosition(string regionId) =>
            regionId != null && Regions.TryGetValue(regionId, out Vector2 value) ? value : KingdomCenter;

        public static Vector2 FacilityPosition(string facilityId) =>
            facilityId != null && Facilities.TryGetValue(facilityId, out Vector2 value) ? value : KingdomCenter;

        public static string FacilityForState(string state) => state switch
        {
            "IDLE_TOWN" => "FAC_TAVERN",
            "SELL_LOOT" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" => "FAC_STORE",
            "HEAL" => "FAC_INFIRMARY",
            "TRAIN_SKILLS" => "FAC_GUILD",
            "ENHANCE_EQUIPMENT" => "FAC_BLACKSMITH",
            _ => null
        };

        public static Vector2 TargetFor(ContinuousHuntMemberDto member)
        {
            if (member == null) return KingdomCenter;
            if (member.State == "IDLE_TOWN") return FacilityPosition("FAC_TAVERN") + StableOffset(member.InstanceId, 130f);
            string facilityId = FacilityForState(member.State);
            if (facilityId != null) return FacilityPosition(facilityId) + StableOffset(member.InstanceId, 78f);

            Vector2 region = RegionPosition(member.AssignedRegionId);
            return member.State switch
            {
                "TRAVEL_TO_REGION" => Vector2.Lerp(KingdomGate(region), region, .72f) + StableOffset(member.InstanceId, 52f),
                "RETURN_TOWN" => Vector2.Lerp(region, KingdomGate(region), .72f) + StableOffset(member.InstanceId, 52f),
                "FIND_TARGET" => region + new Vector2(-85f, 35f) + StableOffset(member.InstanceId, 70f),
                "COMBAT" => region + new Vector2(80f, -55f) + StableOffset(member.InstanceId, 62f),
                "LOOT" => region + new Vector2(120f, -90f) + StableOffset(member.InstanceId, 48f),
                "CONTINUE_DECISION" => region + new Vector2(-20f, -140f) + StableOffset(member.InstanceId, 52f),
                _ => FacilityPosition("FAC_TAVERN") + StableOffset(member.InstanceId, 44f)
            };
        }

        public static Vector2 KingdomGate(Vector2 region)
        {
            Vector2 direction = region.sqrMagnitude <= .01f ? Vector2.up : region.normalized;
            return direction * 500f;
        }

        public static Vector2 StableOffset(string id, float radius)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in id ?? string.Empty) hash = (hash ^ value) * 16777619;
                float angle = (hash % 360u) * Mathf.Deg2Rad;
                float distance = radius * (.45f + ((hash >> 9) % 56u) / 100f);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            }
        }

        public static string DirectionOf(string regionId)
        {
            Vector2 value = RegionPosition(regionId);
            if (Mathf.Abs(value.y) >= Mathf.Abs(value.x)) return value.y >= 0f ? "북쪽" : "남쪽";
            return value.x >= 0f ? "동쪽" : "서쪽";
        }
    }
}
