using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatch(typeof(CharacterDeveloperHeroItemVM), "RefreshValues")]
    public static class SotorHideSpellcraftPatch
    {
        public static void Postfix(CharacterDeveloperHeroItemVM __instance)
        {
            try
            {
                var skills = __instance?.Skills;
                if (skills == null)
                {
                    return;
                }

                int removed = 0;
                for (int i = skills.Count - 1; i >= 0; i--)
                {
                    if (skills[i]?.Skill?.StringId == SotorSkills.SpellcraftId)
                    {
                        skills.RemoveAt(i);
                        removed++;
                    }
                }

                if (removed > 0)
                {
                    SotorLog.Info($"HIDEDIAG: removed {removed} Spellcraft entr(ies) from skill grid ({skills.Count} remain).");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorHideSpellcraftPatch failed: {ex.Message}");
            }
        }
    }
}
