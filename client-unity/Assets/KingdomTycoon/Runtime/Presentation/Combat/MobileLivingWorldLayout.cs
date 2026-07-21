using System;
using System.Collections.Generic;
using KingdomTycoon.Infrastructure.Combat;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Presentation-only canonical nodes for the oblique continuous mobile world.</summary>
    public static class MobileLivingWorldLayout
    {
        public const float ProjectionShear = .16f;
        public const float ProjectionVerticalScale = .74f;
        public static readonly Vector2 WorldSize = new(3000f, 3600f);
        public static readonly Vector2 KingdomCenter = Vector2.zero;

        private static readonly IReadOnlyDictionary<string, Vector2> RegionLogicalPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal)
        {
            ["REGION_R01"] = new(-620f, 1050f),
            ["REGION_R02"] = new(760f, 820f),
            ["REGION_R03"] = new(650f, -1050f),
            ["REGION_R04"] = new(-760f, -850f),
            ["REGION_R05"] = new(0f, 1580f)
        };

        private static readonly IReadOnlyDictionary<string, Vector2> FacilityLogicalPositions = new Dictionary<string, Vector2>(StringComparer.Ordinal)
        {
            ["FAC_TAVERN"] = new(-330f, 260f),
            ["FAC_STORE"] = new(330f, 240f),
            ["FAC_BLACKSMITH"] = new(-330f, -230f),
            ["FAC_INFIRMARY"] = new(330f, -230f),
            ["FAC_GUILD"] = new(0f, 30f)
        };

        public static Vector2 Project(Vector2 logical) =>
            new(logical.x + logical.y * ProjectionShear, logical.y * ProjectionVerticalScale);

        public static Vector2 LogicalRegionPosition(string regionId) =>
            regionId != null && RegionLogicalPositions.TryGetValue(regionId, out Vector2 value) ? value : KingdomCenter;

        public static Vector2 LogicalFacilityPosition(string facilityId) =>
            facilityId != null && FacilityLogicalPositions.TryGetValue(facilityId, out Vector2 value) ? value : KingdomCenter;

        public static Vector2 RegionPosition(string regionId) => Project(LogicalRegionPosition(regionId));

        public static Vector2 FacilityPosition(string facilityId) => Project(LogicalFacilityPosition(facilityId));

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
            Vector2 gate = KingdomGateForRegion(member.AssignedRegionId);
            return member.State switch
            {
                "TRAVEL_TO_REGION" => Vector2.Lerp(gate, region, .72f) + StableOffset(member.InstanceId, 52f),
                "RETURN_TOWN" => Vector2.Lerp(region, gate, .72f) + StableOffset(member.InstanceId, 52f),
                "FIND_TARGET" => region + new Vector2(-85f, 35f) + StableOffset(member.InstanceId, 70f),
                "COMBAT" => region + new Vector2(80f, -55f) + StableOffset(member.InstanceId, 62f),
                "LOOT" => region + new Vector2(120f, -90f) + StableOffset(member.InstanceId, 48f),
                "CONTINUE_DECISION" => region + new Vector2(-20f, -140f) + StableOffset(member.InstanceId, 52f),
                _ => FacilityPosition("FAC_TAVERN") + StableOffset(member.InstanceId, 44f)
            };
        }

        public static Vector2 KingdomGateForRegion(string regionId)
        {
            Vector2 logical = LogicalRegionPosition(regionId);
            Vector2 direction = logical.sqrMagnitude <= .01f ? Vector2.up : logical.normalized;
            return Project(direction * 470f);
        }

        public static Vector2 KingdomGate(Vector2 region)
        {
            Vector2 direction = region.sqrMagnitude <= .01f ? Vector2.up : region.normalized;
            return direction * 470f;
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
            Vector2 value = LogicalRegionPosition(regionId);
            if (Mathf.Abs(value.x) < 120f) return value.y >= 0f ? "북쪽" : "남쪽";
            if (value.y >= 0f) return value.x >= 0f ? "북동쪽" : "북서쪽";
            return value.x >= 0f ? "남동쪽" : "남서쪽";
        }
    }
}
