using System;
using TaleWorlds.Library;

namespace SOTOR
{
    public class SotorLoreObjectVM : ViewModel
    {
        private readonly SotorSpellBookVM _parent;
        private readonly Action<SotorLoreObjectVM> _onSelected;
        private readonly string _loreId;
        private string _name;
        private string _spriteName;
        private bool _isSelected;
        private bool _isVisible = true;
        private bool _isLocked;
        private bool _isUnlockable = true;
        private string _unlockText;
        private bool _isRightSide;
        private MBBindingList<SotorSpellItemVM> _spellList;

        public SotorLoreObjectVM(
            SotorSpellBookVM parent,
            Action<SotorLoreObjectVM> onSelected,
            string name,
            string spriteName,
            MBBindingList<SotorSpellItemVM> spellList,
            string loreId)
        {
            _parent = parent;
            _onSelected = onSelected;
            _name = name;
            _loreId = loreId;
            _isRightSide = AbilitySystem.SotorLores.IsRightSideLore(loreId);

            _spriteName = _isRightSide ? spriteName + "_r" : spriteName;
            _spellList = spellList;
            RefreshFromState();
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
        public bool IsRightSide
        {
            get => _isRightSide;
            set { if (value != _isRightSide) { _isRightSide = value; OnPropertyChangedWithValue(value, nameof(IsRightSide)); } }
        }

        [DataSourceProperty]
        public bool IsUnlockable
        {
            get => _isUnlockable;
            set { if (value != _isUnlockable) { _isUnlockable = value; OnPropertyChangedWithValue(value, nameof(IsUnlockable)); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == _isSelected)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
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
        public MBBindingList<SotorSpellItemVM> SpellList
        {
            get => _spellList;
            set
            {
                if (value == _spellList)
                {
                    return;
                }

                _spellList = value;
                OnPropertyChangedWithValue(value, nameof(SpellList));
            }
        }

        [DataSourceProperty]
        public bool IsLocked
        {
            get => _isLocked;
            set { if (value != _isLocked) { _isLocked = value; OnPropertyChangedWithValue(value, nameof(IsLocked)); } }
        }

        [DataSourceProperty]
        public string UnlockText
        {
            get => _unlockText;
            set { if (value != _unlockText) { _unlockText = value; OnPropertyChangedWithValue(value, nameof(UnlockText)); } }
        }

        public void ExecuteUnlockLore()
        {
            if (_parent == null || _loreId == null) return;
            if (!_parent.IsLoreLocked(_loreId)) return;

            if (!_parent.MeetsCasterLevelForLore(_loreId))
            {
                SotorLog.Info($"Spellbook: unlock '{_loreId}' blocked — caster level too low.");
                return;
            }
            if (!_parent.CanAffordLore(_loreId))
            {
                SotorLog.Info($"Spellbook: unlock '{_loreId}' blocked — not enough gold (need {_parent.LorePrice(_loreId)}, have {_parent.HeroGold}).");
                return;
            }
            _parent.StageUnlockLore(_loreId);
        }

        public void RefreshFromState()
        {
            if (_parent == null || _loreId == null)
            {
                IsLocked = false;
                return;
            }

            IsLocked = _parent.IsLoreLocked(_loreId);

            bool casterOk = _parent.MeetsCasterLevelForLore(_loreId);

            bool masterGated = _parent.IsMasterGated(_loreId);
            IsUnlockable = casterOk && !masterGated;
            if (!IsUnlockable)
            {
                UnlockText = $"{_name}\n{_parent.LoreUnlockBlockReason(_loreId)}";
            }
            else
            {
                int price = _parent.LorePrice(_loreId);
                UnlockText = $"Unlock {_name}\n{price:N0} Gold";
            }
        }

        public void ExecuteSelectLoreObject()
        {
            _onSelected?.Invoke(this);
        }
    }
}
