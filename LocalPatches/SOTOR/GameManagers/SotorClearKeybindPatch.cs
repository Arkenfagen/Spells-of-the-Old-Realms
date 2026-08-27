using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;

namespace SOTOR.GameManagers
{

    [HarmonyPatch(typeof(GauntletOptionsScreen), "SetHotKey")]
    public static class SotorClearKeybindPatch
    {
        public static bool Prefix(GauntletOptionsScreen __instance, Key key)
        {
            try
            {
                if (key == null || key.InputKey != InputKey.Delete)
                {
                    return true;
                }

                var currentKey = AccessTools.Field(typeof(GauntletOptionsScreen), "_currentKey")?.GetValue(__instance);
                var option = currentKey as GameKeyOptionVM;
                if (option?.CurrentGameKey == null || option.CurrentGameKey.GroupId != "SotorGameKeyContext")
                {
                    return true;
                }

                var defaultKey = option.CurrentGameKey.DefaultKeyboardKey;
                if (defaultKey != null && defaultKey.InputKey != InputKey.Invalid)
                {
                    return true;
                }

                var popup = AccessTools.Field(typeof(GauntletOptionsScreen), "_keybindingPopup")?.GetValue(__instance) as KeybindingPopup;
                var dataSource = AccessTools.Field(typeof(GauntletOptionsScreen), "_dataSource")?.GetValue(__instance) as OptionsVM;
                var pendingField = AccessTools.Field(typeof(GameKeyOptionCategoryVM), "_keysToChangeOnDone");
                if (popup == null || dataSource?.GameKeyOptionGroups == null || pendingField == null)
                {
                    return true;
                }

                option.CurrentGameKey.KeyboardKey?.ChangeKey(InputKey.Invalid);
                option.Update();
                if (pendingField.GetValue(dataSource.GameKeyOptionGroups) is Dictionary<GameKey, InputKey> pending)
                {
                    pending[option.CurrentGameKey] = InputKey.Invalid;
                }
                popup.OnToggle(isActive: false);
                SotorLog.Info($"Keybind cleared: {option.CurrentGameKey.StringId} unbound via Delete.");
                return false;
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorClearKeybindPatch failed: {ex.Message}");
                return true;
            }
        }
    }
}
