using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.TwoDimension;

namespace SOTOR.Items
{

    [GameStateScreen(typeof(SotorEnchantingState))]
    public class SotorEnchantingScreen : ScreenBase, IGameStateListener
    {
        private GauntletLayer _gauntletLayer;
        private SotorEnchantingVM _dataSource;
        private readonly SotorEnchantingState _state;
        private SpriteCategory _craftingSpriteCategory;

        public SotorEnchantingScreen(SotorEnchantingState state)
        {
            _state = state;
            _state.RegisterListener(this);
        }

        public static void Open(Hero enchanter)
        {
            var state = Game.Current.GameStateManager.CreateState<SotorEnchantingState>();
            state.Enchanter = enchanter;
            Game.Current.GameStateManager.PushState(state);
        }

        protected override void OnFrameTick(float dt)
        {
            base.OnFrameTick(dt);
            LoadingWindow.DisableGlobalLoadingWindow();
            if (_gauntletLayer == null) return;
            if (_gauntletLayer.Input.IsHotKeyReleased("Exit") || Input.IsKeyReleased(InputKey.Escape))
            {
                CloseScreen();
            }
        }

        void IGameStateListener.OnActivate()
        {
            OnActivate();
            var spriteData = UIResourceManager.SpriteData;
            if (spriteData?.SpriteCategories != null && spriteData.SpriteCategories.TryGetValue("ui_crafting", out var crafting))
            {
                _craftingSpriteCategory = crafting;
                if (!crafting.IsLoaded)
                {
                    crafting.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
                }
            }
            EnsureCategoryLoaded("ui_sotor");

            EnsureCategoryLoaded("ui_inventory");

            _dataSource = new SotorEnchantingVM(CloseScreen, _state.Enchanter);
            _gauntletLayer = new GauntletLayer("GauntletLayer", 1, shouldClear: true);
            _gauntletLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _gauntletLayer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
            _gauntletLayer.LoadMovie("SotorEnchanting", _dataSource);
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
            _craftingSpriteCategory?.Unload();
            _craftingSpriteCategory = null;
        }

        void IGameStateListener.OnInitialize()
        {
            OnInitialize();
        }

        private void CloseScreen()
        {
            Game.Current?.GameStateManager?.PopState(0);
        }

        private static void EnsureCategoryLoaded(string name)
        {
            var spriteData = UIResourceManager.SpriteData;
            if (spriteData?.SpriteCategories == null || !spriteData.SpriteCategories.TryGetValue(name, out var category)) return;
            if (!category.IsLoaded)
            {
                category.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
            }
        }
    }
}
