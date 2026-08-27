

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{
    public static class SotorHideCraftingOrderTemplatesPatch
    {
        private static readonly MethodInfo TemplateAllGetter =
            AccessTools.PropertyGetter(typeof(CraftingTemplate), nameof(CraftingTemplate.All));
        private static readonly MethodInfo FilteredGetter =
            AccessTools.Method(typeof(SotorHideCraftingOrderTemplatesPatch), nameof(NonSotorTemplates));

        public static MBReadOnlyList<CraftingTemplate> NonSotorTemplates()
        {
            MBReadOnlyList<CraftingTemplate> all = CraftingTemplate.All;
            var filtered = new MBReadOnlyList<CraftingTemplate>();
            foreach (CraftingTemplate template in all)
            {
                if (template == null || template.StringId == null ||
                    !template.StringId.StartsWith("sotor_", StringComparison.Ordinal))
                {
                    filtered.Add(template);
                }
            }

            return filtered.Count > 0 ? filtered : all;
        }

        internal static IEnumerable<CodeInstruction> SwapTemplateSource(
            IEnumerable<CodeInstruction> instructions, string targetName)
        {
            var list = new List<CodeInstruction>(instructions);
            bool found = false;
            foreach (CodeInstruction instruction in list)
            {
                if (instruction.Calls(TemplateAllGetter))
                {
                    instruction.operand = FilteredGetter;
                    found = true;
                }
            }
            if (!found)
            {
                SotorLog.Warn($"SotorHideCraftingOrderTemplatesPatch: CraftingTemplate.All call not found in {targetName}; sotor templates may leak into smithy orders.");
            }
            return list;
        }

        [HarmonyPatch(typeof(CraftingCampaignBehavior), "CreateTownOrder")]
        public static class TownOrderPatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return SwapTemplateSource(instructions, "CreateTownOrder");
            }
        }

        [HarmonyPatch(typeof(CraftingCampaignBehavior), "CreateCustomOrderForHero")]
        public static class CustomOrderPatch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return SwapTemplateSource(instructions, "CreateCustomOrderForHero");
            }
        }

        [HarmonyPatch(typeof(CraftingCampaignBehavior), "OnGameLoaded")]
        public static class ScrubSavedOrdersPatch
        {
            public static void Postfix(CraftingCampaignBehavior __instance)
            {
                try
                {
                    int removed = 0;
                    foreach (KeyValuePair<Town, CraftingCampaignBehavior.CraftingOrderSlots> pair in __instance.CraftingOrders)
                    {
                        CraftingOrder[] slots = pair.Value.Slots;
                        for (int i = 0; i < slots.Length; i++)
                        {
                            if (IsSotorOrder(slots[i]))
                            {
                                slots[i] = null;
                                removed++;
                            }
                        }

                        MBReadOnlyList<CraftingOrder> customOrders = pair.Value.CustomOrders;
                        if (customOrders == null)
                        {
                            continue;
                        }
                        List<CraftingOrder> stale = null;
                        foreach (CraftingOrder order in customOrders)
                        {
                            if (IsSotorOrder(order))
                            {
                                (stale ?? (stale = new List<CraftingOrder>())).Add(order);
                            }
                        }
                        if (stale != null)
                        {

                            MethodInfo removeCustom = AccessTools.Method(
                                typeof(CraftingCampaignBehavior.CraftingOrderSlots), "RemoveCustomOrder");
                            foreach (CraftingOrder order in stale)
                            {
                                removeCustom.Invoke(pair.Value, new object[] { order });
                                removed++;
                            }
                        }
                    }
                    if (removed > 0)
                    {
                        SotorLog.Info($"Scrubbed {removed} sotor crafting order(s) from the loaded save; emptied town slots refill on the daily tick.");
                    }
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"SotorHideCraftingOrderTemplatesPatch scrub failed: {ex.GetType().Name}: {ex.Message}");
                }
            }

            private static bool IsSotorOrder(CraftingOrder order)
            {
                string id = order?.WeaponDesignTemplate?.Template?.StringId;
                return id != null && id.StartsWith("sotor_", StringComparison.Ordinal);
            }
        }
    }
}
