using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SotorPatchCategories
    {
        public const string MissionOnly = "SotorMissionOnlyPatches";
    }

    [HarmonyPatchCategory(SotorPatchCategories.MissionOnly)]
    [HarmonyPatch(typeof(Agent), nameof(Agent.CombatActionsEnabled), MethodType.Getter)]
    public static class SotorCombatActionsPatch
    {
        public static void Postfix(ref bool __result, Agent __instance)
        {
            try
            {
                if (!__instance.IsMainAgent || !__result)
                {
                    return;
                }

                var missionLogic = Mission.Current?.GetMissionBehavior<AbilityManagerMissionLogic>();
                if (missionLogic != null && missionLogic.ShouldSuppressCombatActions)
                {
                    __result = false;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorCombatActionsPatch.Postfix failed: {ex.Message}");
            }
        }
    }
}
