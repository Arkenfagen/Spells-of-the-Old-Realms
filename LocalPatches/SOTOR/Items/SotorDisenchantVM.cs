using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace SOTOR.Items
{

    public class SotorDisenchantVM : ViewModel
    {
        private readonly Action _closeScreen;
        private readonly Action<List<EquipmentElement>> _onAccept;
        private MBBindingList<SotorEnchantingIngredientVM> _ingredients;
        private MBBindingList<SotorDisenchantItemVM> _items;
        private string _titleText;
        private string _descriptionText;
        private string _acceptText;
        private string _cancelText;
        private bool _isAcceptEnabled;

        [DataSourceProperty]
        public MBBindingList<SotorEnchantingIngredientVM> Ingredients
        {
            get => _ingredients;
            set { if (_ingredients != value) { _ingredients = value; OnPropertyChangedWithValue(value, "Ingredients"); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorDisenchantItemVM> Items
        {
            get => _items;
            set { if (_items != value) { _items = value; OnPropertyChangedWithValue(value, "Items"); } }
        }

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set { if (_titleText != value) { _titleText = value; OnPropertyChangedWithValue(value, "TitleText"); } }
        }

        [DataSourceProperty]
        public string DescriptionText
        {
            get => _descriptionText;
            set { if (_descriptionText != value) { _descriptionText = value; OnPropertyChangedWithValue(value, "DescriptionText"); } }
        }

        [DataSourceProperty]
        public string AcceptText
        {
            get => _acceptText;
            set { if (_acceptText != value) { _acceptText = value; OnPropertyChangedWithValue(value, "AcceptText"); } }
        }

        [DataSourceProperty]
        public string CancelText
        {
            get => _cancelText;
            set { if (_cancelText != value) { _cancelText = value; OnPropertyChangedWithValue(value, "CancelText"); } }
        }

        [DataSourceProperty]
        public bool IsAcceptEnabled
        {
            get => _isAcceptEnabled;
            set { if (_isAcceptEnabled != value) { _isAcceptEnabled = value; OnPropertyChangedWithValue(value, "IsAcceptEnabled"); } }
        }

        public SotorDisenchantVM(Action closeScreen, Action<List<EquipmentElement>> onAccept)
        {
            _closeScreen = closeScreen;
            _onAccept = onAccept;
            Ingredients = new MBBindingList<SotorEnchantingIngredientVM>();
            Items = new MBBindingList<SotorDisenchantItemVM>();
            TitleText = SotorText.Rendered("sotor_enchanter_disenchant_title");
            DescriptionText = SotorText.Rendered("sotor_enchanter_disenchant_text");
            AcceptText = SotorText.Rendered("sotor_str_accept");
            CancelText = SotorText.Rendered("sotor_str_cancel");

            var roster = MobileParty.MainParty.ItemRoster;
            foreach (var type in SotorEnchantingIngredients.AllTypes)
            {
                var item = SotorEnchantingIngredients.GetItem(type);
                if (item == null) continue;
                var vm = new SotorEnchantingIngredientVM(type, item, roster.GetItemNumber(item));
                vm.SetRecoveredAmount(0);
                Ingredients.Add(vm);
            }
            foreach (var entry in SotorEnchantingBehavior.GetDisenchantCandidates())
            {
                Items.Add(new SotorDisenchantItemVM(entry.EquipmentElement, OnItemToggled));
            }
            RefreshTotals();
        }

        private void OnItemToggled(SotorDisenchantItemVM item)
        {
            RefreshTotals();
        }

        private void RefreshTotals()
        {
            var totals = new Dictionary<SotorIngredientType, int>();
            var selected = 0;
            foreach (var item in Items)
            {
                if (!item.IsSelected) continue;
                selected++;
                foreach (var kv in SotorEnchantingBehavior.GetRefund(item.Item.Item).amounts)
                {
                    if (kv.Value <= 0) continue;
                    totals[kv.Key] = totals.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
                }
            }
            foreach (var ingredient in Ingredients)
            {
                totals.TryGetValue(ingredient.IngredientType, out var amount);
                ingredient.SetRecoveredAmount(amount);
            }
            IsAcceptEnabled = selected > 0;
        }

        public void ExecuteAccept()
        {
            if (!IsAcceptEnabled) return;
            var picked = Items.Where(x => x.IsSelected).Select(x => x.Item).ToList();
            _closeScreen?.Invoke();
            _onAccept?.Invoke(picked);
        }

        public void ExecuteCancel()
        {
            _closeScreen?.Invoke();
        }
    }

    public class SotorDisenchantItemVM : ViewModel
    {
        private readonly Action<SotorDisenchantItemVM> _onToggled;
        private readonly EquipmentElement _item;
        private string _itemName;
        private ImageIdentifierVM _imageIdentifier;
        private bool _isSelected;
        private BasicTooltipViewModel _itemTooltip;

        public EquipmentElement Item => _item;

        [DataSourceProperty]
        public string ItemName
        {
            get => _itemName;
            set { if (_itemName != value) { _itemName = value; OnPropertyChangedWithValue(value, "ItemName"); } }
        }

        [DataSourceProperty]
        public ImageIdentifierVM ImageIdentifier
        {
            get => _imageIdentifier;
            set { if (_imageIdentifier != value) { _imageIdentifier = value; OnPropertyChangedWithValue(value, "ImageIdentifier"); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel ItemTooltip
        {
            get => _itemTooltip;
            set { if (_itemTooltip != value) { _itemTooltip = value; OnPropertyChangedWithValue(value, "ItemTooltip"); } }
        }

        public SotorDisenchantItemVM(EquipmentElement item, Action<SotorDisenchantItemVM> onToggled)
        {
            _item = item;
            _onToggled = onToggled;
            ItemName = item.GetModifiedItemName().ToString();
            ImageIdentifier = new ItemImageIdentifierVM(item.Item, Clan.PlayerClan?.Banner?.Serialize());
            ImageIdentifier.RefreshValues();
            ItemTooltip = new BasicTooltipViewModel(GetHintText);
        }

        private string GetHintText()
        {
            var hint = SotorText.GetObject("sotor_enchanter_disenchant_row_hint");
            hint.SetTextVariable("ITEMS", SotorEnchantingBehavior.GetRefund(_item.Item).text);
            return hint.ToString();
        }

        private void ExecuteToggleSelection()
        {
            IsSelected = !IsSelected;
            _onToggled?.Invoke(this);
        }
    }
}
