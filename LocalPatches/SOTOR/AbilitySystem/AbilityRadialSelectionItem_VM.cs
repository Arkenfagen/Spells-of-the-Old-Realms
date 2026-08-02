using System;
using System.Timers;
using SOTOR.Extensions;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    public class AbilityRadialSelectionItem_VM : ViewModel
    {
        private readonly Ability _ability;
        private readonly Action<Ability> _onSelected;
        private string _spriteName;

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

        public AbilityRadialSelectionItem_VM(Ability ability, Action<Ability> onSelected)
        {
            _ability = ability;
            _onSelected = onSelected;
            _spriteName = ability.Template.SpriteName;
        }

        public void OnSelected()
        {
            _onSelected?.Invoke(_ability);
        }
    }
}
