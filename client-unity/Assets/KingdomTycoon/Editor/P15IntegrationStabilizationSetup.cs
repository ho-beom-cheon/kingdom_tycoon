using UnityEditor;
using UnityEngine;

namespace KingdomTycoon.Editor
{
    public static class P15IntegrationStabilizationSetup
    {
        [MenuItem("Kingdom Tycoon/P15/Run Integration Stabilization")]
        public static void Run()
        {
            P05MercenaryRosterSetup.Run();
            P06CombatSetup.Run();
            P07InventorySetup.Run();
            P08StoreSetup.Run();
            P09ProductionSetup.Run();
            P10EquipmentGrowthSetup.Run();
            P11ProgressionSetup.Run();
            P12RegionMapSetup.Run();
            P13RecruitmentSetup.Run();
            P14RaidSetup.Run();
            P15OfflineTutorialSetup.Run();
            Debug.Log("P15_INTEGRATION_STABILIZATION_COMPLETED");
        }
    }
}
