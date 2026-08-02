using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace SOTOR.CampaignBehaviors
{

    [HarmonyPatch(typeof(BattleInitializationModel), nameof(BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle))]
    public static class SotorGraveyardDeploymentPatch
    {

        public static bool SuppressOrderOfBattleDeployment;

        public static void Postfix(ref bool __result)
        {
            try
            {
                if (SuppressOrderOfBattleDeployment) __result = false;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorGraveyardDeploymentPatch.Postfix failed: {ex.Message}");
            }
        }
    }
}
