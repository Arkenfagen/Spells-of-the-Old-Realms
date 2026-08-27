using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia;

namespace SOTOR.AbilitySystem.Rivals
{

    [HarmonyPatch(typeof(EncyclopediaNavigatorVM), nameof(EncyclopediaNavigatorVM.ExecuteBarLink))]
    public static class SotorEncyclopediaBreadcrumbPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(string targetID)
        {
            if (string.IsNullOrEmpty(targetID)) return true;

            if (targetID != "ListPage-" + SotorTraditionEncyclopediaPage.PageIdentifier) return true;

            try
            {

                Campaign.Current?.EncyclopediaManager?.GoToLink(
                    "ListPage", SotorTraditionEncyclopediaPage.PageIdentifier);
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"Encyclopedia: breadcrumb link failed: {ex.GetType().Name}: {ex.Message}");
            }

            return false;
        }
    }
}
