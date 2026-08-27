using SOTOR;
using SOTOR.Extensions;
using TaleWorlds.DotNet;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.ScreenSystem;

namespace SOTOR.AbilitySystem
{
    [DefaultView]
    public class AbilityHUDMissionView : MissionView
    {
        private int _countOfAbilities;
        private bool _isInitialized;
        private AbilityHUD_VM _abilityHudVm;
        private AbilityRadialSelection_VM _abilityRadialSelectionVm;
        private GauntletLayer _abilityLayer;
        private GauntletLayer _radialMenuLayer;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Mission.Current.OnMainAgentChanged += CheckMainAgent;
            _abilityHudVm = new AbilityHUD_VM();
            _abilityLayer = new GauntletLayer("GauntletLayer", 100, false);
            _abilityLayer.LoadMovie("AbilityHUD", _abilityHudVm);
            MissionScreen.AddLayer(_abilityLayer);

            _abilityRadialSelectionVm = new AbilityRadialSelection_VM();
            _radialMenuLayer = new GauntletLayer("GauntletLayer", 98, false);
            _radialMenuLayer.LoadMovie("AbilityRadialSelection", _abilityRadialSelectionVm);
            MissionScreen.AddLayer(_radialMenuLayer);
            _isInitialized = true;
        }

        private void CheckMainAgent(Agent oldAgent)
        {
            if (Agent.Main == null)
            {
                return;
            }

            var component = Agent.Main.GetComponent<AbilityComponent>();
            if (component == null)
            {
                return;
            }

            _countOfAbilities = component.KnownAbilitySystem.Count;
            _abilityRadialSelectionVm?.FillAbilities(Agent.Main);
        }

        public void DisplayErrorMessage(string message)
        {
            _abilityRadialSelectionVm?.DisplayErrorMessage(message);
        }

        public void OnQuickMenuOpened()
        {
            if (!_isInitialized || Agent.Main == null)
            {
                return;
            }

            EnsureMainAgentAbilitiesCached();
            _abilityRadialSelectionVm?.FillAbilities(Agent.Main);
            _abilityRadialSelectionVm?.RefreshValues();
            SotorLog.Debug($"Quick menu UI refresh. abilities={_countOfAbilities}");
        }

        public override void OnMissionTick(float dt)
        {
            if (!_isInitialized)
            {
                return;
            }

            EnsureMainAgentAbilitiesCached();

            var mission = Mission.Current;
            var abilityLogic = mission?.GetMissionBehavior<AbilityManagerMissionLogic>();
            var quickMenuOpen = abilityLogic?.CurrentState == AbilityModeState.QuickMenuSelection;

            if (quickMenuOpen)
            {
                _abilityRadialSelectionVm?.RefreshValues();
            }

            if (IsAgentControlContext())
            {
                if (_countOfAbilities > 0)
                {
                    _abilityHudVm.RefreshValues();
                    if (!quickMenuOpen)
                    {
                        _abilityRadialSelectionVm.RefreshValues();
                    }
                }
            }
            else if (!quickMenuOpen)
            {
                _abilityHudVm.IsVisible = false;
                _abilityRadialSelectionVm.IsVisible = false;
            }
            else
            {
                _abilityHudVm.IsVisible = false;

                var main = Agent.Main;
                if (main == null || !main.IsActive())
                {
                    _abilityRadialSelectionVm.IsVisible = false;
                }
            }
        }

        private bool IsAgentControlContext()
        {
            var main = Agent.Main;
            var mission = Mission.Current;
            return main != null
                && (int)main.State == 1
                && AbilityMissionModeHelper.IsAbilityHudMissionMode(mission)
                && (NativeObject)MissionScreen.CustomCamera == null
                && !MissionScreen.IsViewingCharacter()
                && !MissionScreen.IsPhotoModeEnabled
                && !mission.IsOrderMenuOpen

                && !ScreenManager.GetMouseVisibility();
        }

        private void EnsureMainAgentAbilitiesCached()
        {
            if (Agent.Main == null)
            {
                return;
            }

            var component = Agent.Main.GetComponent<AbilityComponent>();
            if (component == null)
            {
                return;
            }

            var count = component.KnownAbilitySystem.Count;
            if (count == _countOfAbilities)
            {
                return;
            }

            _countOfAbilities = count;
            _abilityRadialSelectionVm?.FillAbilities(Agent.Main);
            SotorLog.Info($"HUD ability count updated: {_countOfAbilities}");
        }
    }
}
