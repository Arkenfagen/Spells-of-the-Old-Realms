using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace SOTOR
{
    [GameStateScreen(typeof(SotorSpellBookState))]
    public class SotorSpellBookScreen : ScreenBase, IGameStateListener
    {
        private GauntletLayer _gauntletLayer;
        private SotorSpellBookVM _dataSource;
        private readonly SotorSpellBookState _state;

        private SpriteCategory _charDevCategory;
        private bool _charDevLoadedByUs;

        public SotorSpellBookScreen(SotorSpellBookState state)
        {
            _state = state;
            _state.RegisterListener(this);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            LoadingWindow.DisableGlobalLoadingWindow();

            if (_gauntletLayer == null)
            {
                return;
            }

            if (_gauntletLayer.Input.IsHotKeyReleased("Exit") || Input.IsKeyReleased(InputKey.Escape))
            {
                CloseScreen();
            }
        }

        void IGameStateListener.OnActivate()
        {
            OnActivate();
            LoadSotorSprites();
            EnsureCharacterDeveloperSprites();
            _dataSource = new SotorSpellBookVM(CloseScreen);
            _gauntletLayer = new GauntletLayer("GauntletLayer", 1, shouldClear: true);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
            _gauntletLayer.LoadMovie("SotorSpellBook", _dataSource);
            _gauntletLayer.IsFocusLayer = true;
            AddLayer(_gauntletLayer);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        void IGameStateListener.OnDeactivate()
        {
            OnDeactivate();
            if (_gauntletLayer != null)
            {
                RemoveLayer(_gauntletLayer);
                _gauntletLayer.IsFocusLayer = false;
                ScreenManager.TryLoseFocus(_gauntletLayer);
            }
        }

        void IGameStateListener.OnFinalize()
        {

            _dataSource?.OnFinalize();
            _gauntletLayer = null;
            _dataSource = null;
            if (_charDevLoadedByUs)
            {
                try { _charDevCategory?.Unload(); }
                catch (System.Exception ex) { SotorLog.Warn($"ui_characterdeveloper unload failed: {ex.Message}"); }
            }
            _charDevCategory = null;
            _charDevLoadedByUs = false;
        }

        void IGameStateListener.OnInitialize()
        {
            OnInitialize();
        }

        private void CloseScreen()
        {
            if (Game.Current?.GameStateManager != null)
            {
                Game.Current.GameStateManager.PopState(0);
            }
        }

        private void EnsureCharacterDeveloperSprites()
        {
            try
            {
                var spriteData = UIResourceManager.SpriteData;
                if (spriteData?.SpriteCategories == null
                    || !spriteData.SpriteCategories.TryGetValue("ui_characterdeveloper", out var category))
                {
                    return;
                }
                _charDevCategory = category;
                if (!category.IsLoaded)
                {
                    category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                    _charDevLoadedByUs = true;
                    SotorLog.Info("Spellbook: loaded ui_characterdeveloper (map-hotkey path).");
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"EnsureCharacterDeveloperSprites failed: {ex.Message}");
            }
        }

        private static void LoadSotorSprites()
        {
            EnsureCategoryLoaded("ui_sotor");

            EnsureCategoryLoaded("ui_sotor_perks");

        }

        private static void EnsureCategoryLoaded(string name)
        {
            var spriteData = UIResourceManager.SpriteData;
            bool has = spriteData?.SpriteCategories != null && spriteData.SpriteCategories.ContainsKey(name);

            SotorLog.Info($"[TABTEST] EnsureCategoryLoaded('{name}') registered={has}");
            if (!has)
            {
                return;
            }

            var category = spriteData.SpriteCategories[name];
            if (!category.IsLoaded)
            {
                category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
            }
            SotorLog.Info($"[TABTEST] '{name}' loaded={category.IsLoaded}");
        }
    }
}
