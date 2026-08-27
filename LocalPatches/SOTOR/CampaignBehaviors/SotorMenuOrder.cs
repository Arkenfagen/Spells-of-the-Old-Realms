using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;

namespace SOTOR.CampaignBehaviors
{

    public static class SotorMenuOrder
    {

        public const string TownSmithy = "town_smithy";
        public const string TownTavernDistrict = "town_backstreet";
        public const string TownRecruitTroops = "recruit_volunteers";

        public static int After(string menuId, string anchorOptionId)
        {
            try
            {
                var menu = Campaign.Current?.GameMenuManager?.GetGameMenu(menuId);
                if (menu == null) return -1;

                int i = 0;
                var ids = new System.Text.StringBuilder();
                foreach (var option in menu.MenuOptions)
                {
                    if (option != null && option.IdString == anchorOptionId)
                    {
                        SotorLog.Info($"SotorMenuOrder: '{anchorOptionId}' at {i} in '{menuId}' -> inserting at {i + 1}.");
                        return i + 1;
                    }
                    if (ids.Length > 0) ids.Append(", ");
                    ids.Append(i).Append(':').Append(option?.IdString ?? "<null>");
                    i++;
                }

                SotorLog.Info($"SotorMenuOrder: anchor '{anchorOptionId}' NOT in '{menuId}' "
                              + $"({i} options: {ids}); appending.");
                return -1;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorMenuOrder.After('{menuId}','{anchorOptionId}') failed: {ex.Message}");
                return -1;
            }
        }
    }
}
