using System;
using System.Timers;
using SOTOR.Extensions;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    public class AbilityRadialSelection_VM : ViewModel
    {
        private bool _isVisible;
        private MBBindingList<AbilityRadialSelectionItem_VM> _abilities = new MBBindingList<AbilityRadialSelectionItem_VM>();
        private AbilityHUD_VM _abilityVm;
        private bool _errorMessageVisible;
        private string _errorMessageText = string.Empty;
        private readonly Timer _timer;
        private AbilityManagerMissionLogic _abilityLogic;

        [DataSourceProperty]
        public AbilityHUD_VM CurrentAbility
        {
            get => _abilityVm;
            set
            {
                if (value == _abilityVm)
                {
                    return;
                }

                _abilityVm = value;
                OnPropertyChangedWithValue(value, nameof(CurrentAbility));
            }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value == _isVisible)
                {
                    return;
                }

                _isVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsVisible));
            }
        }

        [DataSourceProperty]
        public bool ErrorMessageVisible
        {
            get => _errorMessageVisible;
            set
            {
                if (value == _errorMessageVisible)
                {
                    return;
                }

                _errorMessageVisible = value;
                OnPropertyChangedWithValue(value, nameof(ErrorMessageVisible));
            }
        }

        [DataSourceProperty]
        public string ErrorMessageText
        {
            get => _errorMessageText;
            set
            {
                if (value == _errorMessageText)
                {
                    return;
                }

                _errorMessageText = value;
                OnPropertyChangedWithValue(value, nameof(ErrorMessageText));
            }
        }

        [DataSourceProperty]
        public MBBindingList<AbilityRadialSelectionItem_VM> Abilities
        {
            get => _abilities;
            set
            {
                if (value == _abilities)
                {
                    return;
                }

                _abilities = value;
                OnPropertyChangedWithValue(value, nameof(Abilities));
            }
        }

        public AbilityRadialSelection_VM()
        {
            _timer = new Timer(2000.0) { AutoReset = false };
            _timer.Elapsed += (_, __) =>
            {
                _timer.Stop();
                ErrorMessageVisible = false;
            };
        }

        public override void RefreshValues()
        {
            _abilityLogic ??= Mission.Current?.GetMissionBehavior<AbilityManagerMissionLogic>();
            if (_abilityLogic == null)
            {
                return;
            }

            IsVisible = _abilityLogic.CurrentState == AbilityModeState.QuickMenuSelection;
            if (!IsVisible)
            {
                return;
            }

            CurrentAbility ??= new AbilityHUD_VM();
            CurrentAbility.RefreshValues();
        }

        public void FillAbilities(Agent agent)
        {
            _abilities.Clear();
            var component = agent.GetComponent<AbilityComponent>();
            if (component == null || component.KnownAbilitySystem.Count == 0)
            {
                return;
            }

            foreach (var ability in component.KnownAbilitySystem)
            {
                _abilities.Add(new AbilityRadialSelectionItem_VM(ability, OnItemSelected));
            }
        }

        public void DisplayErrorMessage(string message)
        {
            if (ErrorMessageVisible && _timer.Enabled)
            {
                return;
            }

            ErrorMessageVisible = true;
            ErrorMessageText = message;
            _timer.Start();
        }

        private void OnItemSelected(Ability ability)
        {
            Agent.Main?.SelectAbility(ability);
        }
    }
}
