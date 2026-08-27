using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace SOTOR.GameManagers
{

    public static class SotorSpellbookHotkey
    {
        private static GameKey _key;
        private static bool _lookupDone;

        public static void Tick()
        {
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return;

                var map = SandBox.View.Map.MapScreen.Instance;
                if (map == null) return;
                if (!ReferenceEquals(TaleWorlds.ScreenSystem.ScreenManager.TopScreen, map)) return;
                if (map.EncyclopediaScreenManager?.IsEncyclopediaOpen == true) return;
                if (map.IsEscapeMenuOpened) return;

                var key = ResolveKey();
                if (key == null) return;
                bool pressed =
                    (key.KeyboardKey != null && key.KeyboardKey.InputKey != InputKey.Invalid
                        && Input.IsKeyPressed(key.KeyboardKey.InputKey))
                    || (key.ControllerKey != null && key.ControllerKey.InputKey != InputKey.Invalid
                        && Input.IsKeyPressed(key.ControllerKey.InputKey));
                if (!pressed) return;

                var state = Game.Current.GameStateManager.CreateState<SotorSpellBookState>();
                Game.Current.GameStateManager.PushState(state, 0);
                SotorLog.Info("Spellbook opened via hotkey.");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorSpellbookHotkey.Tick failed: {ex.Message}");
                _lookupDone = true;
                _key = null;
            }
        }

        private static GameKey ResolveKey()
        {
            if (_lookupDone) return _key;
            foreach (var category in HotKeyManager.GetAllCategories())
            {
                if (category is SotorGameKeyContext)
                {
                    _key = category.GetGameKey(SotorGameKeyContext.OpenSpellbook);
                    break;
                }
            }
            _lookupDone = true;
            return _key;
        }
    }
}
