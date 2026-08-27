using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encyclopedia;

namespace SOTOR.AbilitySystem.Rivals
{

    [HarmonyPatch(typeof(EncyclopediaPage), MethodType.Constructor)]
    public static class SotorEncyclopediaPageCtorPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(EncyclopediaPage __instance,
                                  ref Type[] ____identifierTypes,
                                  ref Dictionary<Type, string> ____identifiers,
                                  ref IEnumerable<EncyclopediaFilterGroup> ____filters,
                                  ref IEnumerable<EncyclopediaListItem> ____items,
                                  ref IEnumerable<EncyclopediaSortController> ____sortControllers)
        {
            var page = __instance as SotorTraditionEncyclopediaPage;
            if (page == null)
            {
                return true;
            }

            try
            {
                ____filters = page.BuildFilters();

                var items = new List<EncyclopediaListItem>(page.BuildItems());
                ____items = items;

                var sorters = new List<EncyclopediaSortController>
                {
                    new EncyclopediaSortController(
                        page.GetName(),
                        new SotorTraditionEncyclopediaPage.NameComparer()),
                };
                sorters.AddRange(page.BuildSortControllers());
                ____sortControllers = sorters;

                ____identifierTypes = SotorTraditionEncyclopediaPage.IdentifierTypes;

                ____identifiers = new Dictionary<Type, string>
                {
                    [typeof(SotorTraditionObject)] = SotorTraditionEncyclopediaPage.PageIdentifier,
                };

                SotorLog.Info("Encyclopedia: Arcane Traditions page initialized with "
                              + items.Count + " traditions.");
            }
            catch (Exception ex)
            {

                SotorLog.Error($"Encyclopedia: tradition page init failed: {ex.GetType().Name}: {ex.Message}");
                ____filters = new List<EncyclopediaFilterGroup>();
                ____items = new List<EncyclopediaListItem>();
                ____sortControllers = new List<EncyclopediaSortController>();
                ____identifierTypes = SotorTraditionEncyclopediaPage.IdentifierTypes;
                ____identifiers = new Dictionary<Type, string>
                {
                    [typeof(SotorTraditionObject)] = SotorTraditionEncyclopediaPage.PageIdentifier,
                };
            }

            return false;
        }
    }
}
