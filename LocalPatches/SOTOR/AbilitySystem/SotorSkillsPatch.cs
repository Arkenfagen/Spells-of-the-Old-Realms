using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatch(typeof(Game), nameof(Game.InitializeDefaultGameObjects))]
    public static class SotorSkillsPatch
    {
        public static void Postfix()
        {
            try
            {
                new SotorSkills();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"SotorSkillsPatch failed to register skills: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Campaign), "InitializeDefaultCampaignObjects")]
    public static class SotorPerksPatch
    {
        public static void Postfix()
        {
            try
            {
                new SotorPerks();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"SotorPerksPatch failed to register perks: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
