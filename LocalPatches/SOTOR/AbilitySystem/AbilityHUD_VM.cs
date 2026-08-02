using SOTOR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    public class AbilityHUD_VM : ViewModel
    {
        private string _name = string.Empty;
        private string _spriteName = string.Empty;
        private string _coolDownLeft = string.Empty;
        private string _windsOfMagicLeft = "-";
        private bool _isVisible;
        private bool _onCoolDown;
        private bool _isSpell;
        private string _windsCost = string.Empty;
        private bool _isDisabled;
        private string _disabledText = string.Empty;
        private string _abilityType = string.Empty;

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
        public string WindsOfMagicLeft
        {
            get => _windsOfMagicLeft;
            set
            {
                _windsOfMagicLeft = value;
                OnPropertyChangedWithValue(value, nameof(WindsOfMagicLeft));
            }
        }

        [DataSourceProperty]
        public string Name
        {
            get => _name;
            set
            {
                if (value == _name)
                {
                    return;
                }

                _name = value;
                OnPropertyChangedWithValue(value, nameof(Name));
            }
        }

        [DataSourceProperty]
        public string SpriteName
        {
            get => _spriteName;
            set
            {
                if (value == _spriteName)
                {
                    return;
                }

                _spriteName = value;
                OnPropertyChangedWithValue(value, nameof(SpriteName));
            }
        }

        [DataSourceProperty]
        public string CoolDownLeft
        {
            get => _coolDownLeft;
            set
            {
                if (value == _coolDownLeft)
                {
                    return;
                }

                _coolDownLeft = value;
                OnPropertyChangedWithValue(value, nameof(CoolDownLeft));
            }
        }

        [DataSourceProperty]
        public bool IsOnCoolDown
        {
            get => _onCoolDown;
            set
            {
                if (value == _onCoolDown)
                {
                    return;
                }

                _onCoolDown = value;
                OnPropertyChangedWithValue(value, nameof(IsOnCoolDown));
            }
        }

        [DataSourceProperty]
        public string WindsCost
        {
            get => _windsCost;
            set
            {
                if (value == _windsCost)
                {
                    return;
                }

                _windsCost = value;
                OnPropertyChangedWithValue(value, nameof(WindsCost));
            }
        }

        [DataSourceProperty]
        public bool IsSpell
        {
            get => _isSpell;
            set
            {
                if (value == _isSpell)
                {
                    return;
                }

                _isSpell = value;
                OnPropertyChangedWithValue(value, nameof(IsSpell));
            }
        }

        [DataSourceProperty]
        public string AbilityType
        {
            get => _abilityType;
            set
            {
                if (value == _abilityType)
                {
                    return;
                }

                _abilityType = value;
                OnPropertyChangedWithValue(value, nameof(AbilityType));
            }
        }

        [DataSourceProperty]
        public bool IsDisabled
        {
            get => _isDisabled;
            set
            {
                if (value == _isDisabled)
                {
                    return;
                }

                _isDisabled = value;
                OnPropertyChangedWithValue(value, nameof(IsDisabled));
            }
        }

        [DataSourceProperty]
        public string DisabledText
        {
            get => _disabledText;
            set
            {
                if (value == _disabledText)
                {
                    return;
                }

                _disabledText = value;
                OnPropertyChangedWithValue(value, nameof(DisabledText));
            }
        }

        public override void RefreshValues()
        {
            var ability = Agent.Main?.GetCurrentAbility();
            var abilityLogic = Mission.Current?.GetMissionBehavior<AbilityManagerMissionLogic>();
            var mission = Mission.Current;
            IsVisible = ability != null
                && abilityLogic != null
                && mission != null
                && AbilityMissionModeHelper.IsAbilityHudMissionMode(mission)
                && IsHudAllowedByMode(abilityLogic);

            if (!IsVisible)
            {
                return;
            }

            AbilityType = "(" + ability.Template.AbilityType + ")";
            IsSpell = ability.Template.AbilityType == SOTOR.AbilitySystem.AbilityType.Spell;
            SpriteName = ability.Template.SpriteName;
            Name = ability.Template.Name;
            WindsCost = ability.Template.WindsOfMagicCost.ToString();

            var hero = Agent.Main?.GetHero();
            if (hero != null && hero.GetExtendedInfo() != null)
            {
                WindsOfMagicLeft = ((int)hero.GetWindsOfMagic()).ToString();
            }
            else
            {
                WindsOfMagicLeft = "-";
            }

            CoolDownLeft = ability.GetCoolDownLeft().ToString();
            IsOnCoolDown = ability.IsOnCooldown();
            if (ability.IsDisabled(Agent.Main, out var disabledReason))
            {
                IsDisabled = true;
                DisabledText = disabledReason.ToString();
            }
            else
            {
                IsDisabled = false;
                DisabledText = string.Empty;
            }
        }

        private static bool IsHudAllowedByMode(AbilityManagerMissionLogic abilityLogic)
        {
            switch (SOTOR.SotorSettings.HudMode)
            {
                case 2:
                    return false;
                case 1:
                    return abilityLogic != null
                        && abilityLogic.CurrentState == AbilityModeState.QuickMenuSelection;
                default:
                    return true;
            }
        }
    }
}
