using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;

namespace SOTOR.CampaignBehaviors
{

    public static class SotorMenuIcons
    {

        private const string Marker = "SotorIcon:";

        private static readonly Dictionary<string, string> IconByOptionId =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sotor_enchanter_quarter"] = "sotor_enchanter_quarter_icon",
                ["sotor_graveyard"] = "sotor_graveyard_icon",
            };

        public static string MarkerFor(string optionId)
        {
            return optionId != null && IconByOptionId.TryGetValue(optionId, out var sprite)
                ? Marker + sprite
                : null;
        }

        public static bool TryGetSpriteName(string leaveType, out string spriteName)
        {
            spriteName = null;
            if (leaveType == null || !leaveType.StartsWith(Marker, StringComparison.Ordinal)) return false;
            spriteName = leaveType.Substring(Marker.Length);
            return true;
        }

        private static bool _loaded;

        public static TaleWorlds.TwoDimension.Sprite GetSprite(string name)
        {
            try
            {
                var spriteData = UIResourceManager.SpriteData;
                if (spriteData == null) return null;
                if (!_loaded && spriteData.SpriteCategories != null
                    && spriteData.SpriteCategories.TryGetValue("ui_sotor_menu", out var category))
                {
                    if (!category.IsLoaded)
                    {
                        category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                    }
                    _loaded = true;
                }
                return spriteData.GetSprite(name);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorMenuIcons.GetSprite('{name}') failed: {ex.Message}");
                return null;
            }
        }
    }

    [HarmonyPatch(typeof(GameMenuItemVM), "Refresh")]
    public static class SotorMenuItemVMPatch
    {
        public static void Postfix(GameMenuItemVM __instance)
        {
            try
            {
                string marker = SotorMenuIcons.MarkerFor(__instance?.OptionID);
                if (marker != null && __instance.OptionLeaveType != marker)
                {
                    __instance.OptionLeaveType = marker;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorMenuItemVMPatch failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(GameMenuItemWidget), "UpdateLeaveTypeIcon")]
    public static class SotorMenuItemWidgetPatch
    {
        public static void Postfix(GameMenuItemWidget __instance)
        {
            try
            {
                if (__instance?.LeaveTypeIcon == null) return;
                if (!SotorMenuIcons.TryGetSpriteName(__instance.LeaveType, out string spriteName)) return;

                var sprite = SotorMenuIcons.GetSprite(spriteName);
                if (sprite == null) return;

                __instance.LeaveTypeIcon.IsVisible = true;
                __instance.LeaveTypeIcon.Brush.Sprite = sprite;
                __instance.LeaveTypeIcon.Brush.DefaultLayer.Sprite = sprite;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorMenuItemWidgetPatch failed: {ex.Message}");
            }
        }
    }
}
