using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatchCategory(SotorPatchCategories.MissionOnly)]
    [HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
    public static class SotorJavelinControlPatch
    {
        public static void Postfix()
        {
            try
            {
                SotorThrownJavelinMissionLogic.Instance?.DriveThrowFlagsFromControlTick();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorJavelinControlPatch.Postfix failed: {ex.Message}");
            }
        }
    }
}
