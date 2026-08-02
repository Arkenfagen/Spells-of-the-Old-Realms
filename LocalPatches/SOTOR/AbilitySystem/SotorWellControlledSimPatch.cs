using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatch(typeof(DefaultCombatSimulationModel), nameof(DefaultCombatSimulationModel.GetBattleAdvantage))]
    public static class SotorWellControlledSimPatch
    {
        private const float WellControlledSimBonus = 0.05f;

        public static void Postfix(MapEvent mapEvent, ref ExplainedNumber defenderAdvantage, ref ExplainedNumber attackerAdvantage)
        {
            try
            {
                var perk = SotorPerks.WellControlled;
                if (perk == null || mapEvent == null)
                {
                    return;
                }

                Hero defenderLeader = mapEvent.DefenderSide?.LeaderParty?.LeaderHero;
                if (defenderLeader != null && defenderLeader.GetPerkValue(perk))
                {
                    defenderAdvantage.Add(WellControlledSimBonus);
                }

                Hero attackerLeader = mapEvent.AttackerSide?.LeaderParty?.LeaderHero;
                if (attackerLeader != null && attackerLeader.GetPerkValue(perk))
                {
                    attackerAdvantage.Add(WellControlledSimBonus);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorWellControlledSimPatch failed: {ex.Message}");
            }
        }
    }
}
