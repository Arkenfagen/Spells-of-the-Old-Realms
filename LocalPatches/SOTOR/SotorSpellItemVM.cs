using System;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace SOTOR
{
    public class SotorSpellItemVM : ViewModel
    {
        private readonly SotorSpellBookVM _book;
        private readonly Hero _hero;
        private readonly string _abilityId;
        private readonly string _loreId;
        private readonly int _spellTier;
        private readonly string _description;
        private string _abilitySpriteName;
        private string _name;
        private bool _isKnown = true;
        private bool _isDisabled;
        private bool _isSelected;
        private bool _canLearn;
        private bool _isPurchased;
        private bool _isBuyable;
        private bool _showBuyOverlay;
        private string _buyText;
        private MBBindingList<SotorStatItemVM> _statItems;
        private BasicTooltipViewModel _abilityHint;

        public SotorSpellItemVM(SotorSpellBookVM book, Hero hero, AbilityTemplate template, string loreId)
        {
            _book = book;
            _hero = hero;
            _abilityId = template.StringID;
            _loreId = loreId;
            _spellTier = template.SpellTier;
            _name = template.Name;
            _description = template.TooltipDescription;
            _abilitySpriteName = template.SpriteName;

            _statItems = new MBBindingList<SotorStatItemVM>
            {
                new SotorStatItemVM(SotorText.Rendered("sotor_sb_lbl_cooldown"), template.CoolDown + " seconds"),
                new SotorStatItemVM(SotorText.Rendered("sotor_sb_lbl_spell_type"), template.AbilityEffectType.ToString()),
                new SotorStatItemVM(SotorText.Rendered("sotor_sb_lbl_spell_tier"), ((SpellCastingLevel)template.SpellTier).ToString()),
                new SotorStatItemVM(SotorText.Rendered("sotor_sb_lbl_winds_cost"), template.WindsOfMagicCost.ToString()),
                new SotorStatItemVM(SotorText.Rendered("sotor_sb_lbl_spell_name"), template.Name),
            };

            _abilityHint = new BasicTooltipViewModel(() => _description);

            RefreshFromState();
        }

        [DataSourceProperty]
        public string AbilitySpriteName
        {
            get => _abilitySpriteName;
            set
            {
                if (value == _abilitySpriteName)
                {
                    return;
                }

                _abilitySpriteName = value;
                OnPropertyChangedWithValue(value, nameof(AbilitySpriteName));
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
        public MBBindingList<SotorStatItemVM> AbilityStatItems
        {
            get => _statItems;
            set
            {
                if (value == _statItems)
                {
                    return;
                }

                _statItems = value;
                OnPropertyChangedWithValue(value, nameof(AbilityStatItems));
            }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel AbilityHint
        {
            get => _abilityHint;
            set
            {
                if (value == _abilityHint)
                {
                    return;
                }

                _abilityHint = value;
                OnPropertyChangedWithValue(value, nameof(AbilityHint));
            }
        }

        [DataSourceProperty]
        public bool IsKnown
        {
            get => _isKnown;
            set
            {
                if (value == _isKnown)
                {
                    return;
                }

                _isKnown = value;
                OnPropertyChangedWithValue(value, nameof(IsKnown));
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
        public bool CanLearn
        {
            get => _canLearn;
            set
            {
                if (value == _canLearn)
                {
                    return;
                }

                _canLearn = value;
                OnPropertyChangedWithValue(value, nameof(CanLearn));
            }
        }

        [DataSourceProperty]
        public bool IsPurchased
        {
            get => _isPurchased;
            set { if (value != _isPurchased) { _isPurchased = value; OnPropertyChangedWithValue(value, nameof(IsPurchased)); } }
        }

        [DataSourceProperty]
        public bool IsBuyable
        {
            get => _isBuyable;
            set { if (value != _isBuyable) { _isBuyable = value; OnPropertyChangedWithValue(value, nameof(IsBuyable)); } }
        }

        [DataSourceProperty]
        public bool ShowBuyOverlay
        {
            get => _showBuyOverlay;
            set { if (value != _showBuyOverlay) { _showBuyOverlay = value; OnPropertyChangedWithValue(value, nameof(ShowBuyOverlay)); } }
        }

        [DataSourceProperty]
        public string BuyText
        {
            get => _buyText;
            set { if (value != _buyText) { _buyText = value; OnPropertyChangedWithValue(value, nameof(BuyText)); } }
        }

        public void ExecuteSelectAbility()
        {
            if (_book == null) return;
            if (!_book.IsSpellPurchased(_abilityId))
            {
                _book.StageBuySpell(_abilityId, _loreId, _spellTier);
            }
            else
            {
                _book.ToggleSpellStaged(_abilityId, _loreId);
            }
        }

        public void ExecuteBuySpell()
        {
            _book?.StageBuySpell(_abilityId, _loreId, _spellTier);
        }

        public void RefreshFromState()
        {
            if (_book == null)
            {
                var info = _hero?.GetExtendedInfo();
                IsSelected = info != null && info.IsAbilitySelected(_abilityId);
                IsKnown = _hero == null || _hero.HasAbility(_abilityId);
                IsPurchased = IsKnown;
                IsBuyable = false;
                ShowBuyOverlay = false;
                return;
            }

            IsKnown = _book.IsLoreOwned(_loreId);
            IsPurchased = _book.IsSpellPurchased(_abilityId);
            IsSelected = _book.IsSpellSelectedStaged(_abilityId);

            bool canBuy = _book.CanBuySpell(_abilityId, _loreId, _spellTier);
            IsBuyable = canBuy && _book.CanAffordSpell(_abilityId);

            ShowBuyOverlay = IsKnown && !IsPurchased;

            if (!IsPurchased && _book.IsLoreOwned(_loreId))
            {

                if (_book.IsSpellMasterGated(_abilityId, _loreId))
                {
                    BuyText = "";
                }
                else
                {
                    string reason = _book.SpellBuyBlockReason(_abilityId, _loreId, _spellTier);
                    BuyText = string.IsNullOrEmpty(reason)
                        ? $"Learn\n{_book.SpellPriceById(_abilityId):N0} Gold"
                        : reason;
                }
            }
            else
            {
                BuyText = "";
            }
        }
    }
}
