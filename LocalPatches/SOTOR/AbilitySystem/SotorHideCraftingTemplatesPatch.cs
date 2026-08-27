

using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;

namespace SOTOR.AbilitySystem
{
    [HarmonyPatch(typeof(WeaponClassSelectionPopupVM), MethodType.Constructor,
        new Type[] { typeof(ICraftingCampaignBehavior), typeof(List<CraftingTemplate>), typeof(Action<int>), typeof(Func<CraftingTemplate, int>) })]
    public static class SotorHideCraftingTemplatesPatch
    {
        public static void Prefix(List<CraftingTemplate> templatesList)
        {
            try
            {
                if (templatesList == null) return;
                templatesList.RemoveAll(t => t != null && t.StringId != null && t.StringId.StartsWith("sotor_", StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorHideCraftingTemplatesPatch failed: {ex.Message}");
            }
        }
    }
}
