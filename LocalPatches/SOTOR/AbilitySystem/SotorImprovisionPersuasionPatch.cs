using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatch(typeof(DefaultPersuasionModel), nameof(DefaultPersuasionModel.GetChances))]
    public static class SotorImprovisionPersuasionPatch
    {
        private const float ImprovisionPersuasionBonus = 0.1f;

        public static void Postfix(ref float successChance)
        {
            try
            {
                var perk = SotorPerks.Improvision;
                if (perk == null || Hero.MainHero == null)
                {
                    return;
                }

                if (Hero.MainHero.GetPerkValue(perk))
                {
                    successChance = MBMath.ClampFloat(successChance * (1f + ImprovisionPersuasionBonus), 0f, 1f);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorImprovisionPersuasionPatch failed: {ex.Message}");
            }
        }
    }
}
