using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.Items
{

    public class SotorEnchantingVM : ViewModel
    {
        private readonly Action _closeAction;
        private MBBindingList<SotorEnchantableItemVM> _items = new MBBindingList<SotorEnchantableItemVM>();
        private MBBindingList<SotorEnchantableTraitVM> _traits = new MBBindingList<SotorEnchantableTraitVM>();
        private MBBindingList<SotorEnchantableTraitVM> _selectedTraits = new MBBindingList<SotorEnchantableTraitVM>();
        private MBBindingList<SotorEnchantingIngredientVM> _ingredients = new MBBindingList<SotorEnchantingIngredientVM>();
        private SotorEnchantableItemVM _selectedItem;
        private SotorEnchantingTableauVM _itemTableau;
        private bool _isPreviewEnabled;
        private string _screenTitle;
        private string _itemsLabel;
        private string _traitsLabel;
        private string _enchantLabel;

        [DataSourceProperty]
        public MBBindingList<SotorEnchantableItemVM> Items
        {
            get => _items;
            set { if (_items != value) { _items = value; OnPropertyChangedWithValue(value, "Items"); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorEnchantableTraitVM> Traits
        {
            get => _traits;
            set { if (_traits != value) { _traits = value; OnPropertyChangedWithValue(value, "Traits"); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorEnchantableTraitVM> SelectedTraits
        {
            get => _selectedTraits;
            set { if (_selectedTraits != value) { _selectedTraits = value; OnPropertyChangedWithValue(value, "SelectedTraits"); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorEnchantingIngredientVM> Ingredients
        {
            get => _ingredients;
            set { if (_ingredients != value) { _ingredients = value; OnPropertyChangedWithValue(value, "Ingredients"); } }
        }

        [DataSourceProperty]
        public SotorEnchantableItemVM SelectedItem
        {
            get => _selectedItem;
            set { if (_selectedItem != value) { _selectedItem = value; OnPropertyChangedWithValue(value, "SelectedItem"); } }
        }

        [DataSourceProperty]
        public SotorEnchantingTableauVM ItemTableau
        {
            get => _itemTableau;
            set { if (_itemTableau != value) { _itemTableau = value; OnPropertyChangedWithValue(value, "ItemTableau"); } }
        }

        [DataSourceProperty]
        public bool IsPreviewEnabled
        {
            get => _isPreviewEnabled;
            set { if (_isPreviewEnabled != value) { _isPreviewEnabled = value; OnPropertyChangedWithValue(value, "IsPreviewEnabled"); } }
        }

        [DataSourceProperty]
        public string ScreenTitle
        {
            get => _screenTitle;
            set { if (_screenTitle != value) { _screenTitle = value; OnPropertyChangedWithValue(value, "ScreenTitle"); } }
        }

        [DataSourceProperty]
        public string ItemsLabel
        {
            get => _itemsLabel;
            set { if (_itemsLabel != value) { _itemsLabel = value; OnPropertyChangedWithValue(value, "ItemsLabel"); } }
        }

        [DataSourceProperty]
        public string TraitsLabel
        {
            get => _traitsLabel;
            set { if (_traitsLabel != value) { _traitsLabel = value; OnPropertyChangedWithValue(value, "TraitsLabel"); } }
        }

        [DataSourceProperty]
        public string EnchantLabel
        {
            get => _enchantLabel;
            set { if (_enchantLabel != value) { _enchantLabel = value; OnPropertyChangedWithValue(value, "EnchantLabel"); } }
        }

        private readonly Hero _enchanter;

        private TextObject EnchanterName => _enchanter?.Name ?? new TextObject("");

        public SotorEnchantingVM(Action closeScreen, Hero enchanter)
        {
            _closeAction = closeScreen;
            _enchanter = enchanter ?? Hero.MainHero;
            _sortController = new SotorEnchantingSortControllerVM(ApplySort);

            _traitSortController = new SotorEnchantingSortControllerVM(ApplyTraitSort, "sotor_enchanting_sort_type");
            RefreshValues();
        }

        private SotorEnchantingSortControllerVM _sortController;
        private SotorEnchantingSortControllerVM _traitSortController;

        [DataSourceProperty]
        public SotorEnchantingSortControllerVM SortController
        {
            get => _sortController;
            set { if (_sortController != value) { _sortController = value; OnPropertyChangedWithValue(value, "SortController"); } }
        }

        [DataSourceProperty]
        public SotorEnchantingSortControllerVM TraitSortController
        {
            get => _traitSortController;
            set { if (_traitSortController != value) { _traitSortController = value; OnPropertyChangedWithValue(value, "TraitSortController"); } }
        }

        private void ApplyTraitSort()
        {
            if (_traitSortController == null || Traits.Count <= 1) return;
            IEnumerable<SotorEnchantableTraitVM> ordered;
            switch (_traitSortController.ActiveColumn)
            {
                case SotorEnchantingSortControllerVM.ColumnType:

                    ordered = _traitSortController.Descending
                        ? Traits.OrderByDescending(TypeKey)
                            .ThenByDescending(LoreSubKey, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(x => x.TraitName, StringComparer.OrdinalIgnoreCase)
                        : Traits.OrderBy(TypeKey)
                            .ThenBy(LoreSubKey, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(x => x.TraitName, StringComparer.OrdinalIgnoreCase);
                    break;
                case SotorEnchantingSortControllerVM.ColumnName:
                    ordered = _traitSortController.Descending
                        ? Traits.OrderByDescending(x => x.TraitName, StringComparer.OrdinalIgnoreCase)
                        : Traits.OrderBy(x => x.TraitName, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    ordered = Traits.OrderBy(x => x.TraitName, StringComparer.OrdinalIgnoreCase);
                    break;
            }
            var list = ordered.ToList();
            Traits.Clear();
            foreach (var vm in list) Traits.Add(vm);
        }

        private static int TypeKey(SotorEnchantableTraitVM vm)
        {
            return vm?.ItemTrait?.TypeOrder ?? 9;
        }

        private static string LoreSubKey(SotorEnchantableTraitVM vm)
        {
            return vm?.ItemTrait?.RequiredLore ?? "";
        }

        private void ApplySort()
        {
            if (_sortController == null || Items.Count <= 1) return;
            IEnumerable<SotorEnchantableItemVM> ordered;
            switch (_sortController.ActiveColumn)
            {
                case SotorEnchantingSortControllerVM.ColumnType:
                    ordered = _sortController.Descending
                        ? Items.OrderByDescending(x => (int)x.Item.Item.ItemType)
                            .ThenBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
                        : Items.OrderBy(x => (int)x.Item.Item.ItemType)
                            .ThenBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase);
                    break;
                case SotorEnchantingSortControllerVM.ColumnName:
                    ordered = _sortController.Descending
                        ? Items.OrderByDescending(x => x.ItemName, StringComparer.OrdinalIgnoreCase)
                        : Items.OrderBy(x => x.ItemName, StringComparer.OrdinalIgnoreCase);
                    break;
                default:
                    ordered = Items.OrderBy(x => x.RosterIndex);
                    break;
            }
            var list = ordered.ToList();
            Items.Clear();
            foreach (var vm in list) Items.Add(vm);
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            var title = SotorText.GetObject("sotor_enchanting_title_hero");
            title.SetTextVariable("HERO", EnchanterName);
            ScreenTitle = title.ToString();
            ItemsLabel = SotorText.Rendered("sotor_enchanting_items_label");
            TraitsLabel = SotorText.Rendered("sotor_enchanting_traits_label");
            EnchantLabel = SotorText.Rendered("sotor_enchanting_enchant_label");
            Items.Clear();
            Traits.Clear();
            SelectedTraits.Clear();
            Ingredients.Clear();
            ItemTableau = new SotorEnchantingTableauVM();
            IsPreviewEnabled = false;

            int rosterIndex = 0;
            foreach (var element in MobileParty.MainParty.ItemRoster)
            {
                var item = element.EquipmentElement.Item;
                if (IsEnchantable(item))
                {
                    Items.Add(new SotorEnchantableItemVM(element.EquipmentElement, OnItemSelected)
                    {
                        RosterIndex = rosterIndex++
                    });
                }
            }
            ApplySort();
            foreach (var type in SotorEnchantingIngredients.AllTypes)
            {
                var item = SotorEnchantingIngredients.GetItem(type);
                if (item == null) continue;
                Ingredients.Add(new SotorEnchantingIngredientVM(type, item,
                    MobileParty.MainParty.ItemRoster.GetItemNumber(item)));
            }
        }

        private static bool IsEnchantable(ItemObject item)
        {
            return item != null
                && (item.HasArmorComponent || item.HasWeaponComponent)
                && !SotorExtendedItemManager.HasTraits(item);
        }

        private int MaxTraits()
        {
            return SotorPerks.Archmage != null && _enchanter != null
                && _enchanter.GetPerkValue(SotorPerks.Archmage) ? 2 : 1;
        }

        private bool CanWork(SotorItemTrait trait)
        {
            return CampaignBehaviors.SotorBlueprintBookBehavior.HeroMeetsGate(_enchanter, trait);
        }

        private void OnItemSelected(SotorEnchantableItemVM item)
        {
            Traits.Clear();
            SelectedTraits.Clear();
            SelectedItem?.DeselectItem();
            SelectedItem = item;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var itemType = item.Item.Item.ItemType;
            var info = _enchanter?.GetExtendedInfo();
            if (info != null)
            {
                foreach (var traitId in info.AllKnownBlueprints)
                {
                    if (!seen.Add(traitId)) continue;
                    var trait = SotorItemTraitManager.GetTrait(traitId);
                    if (trait != null && trait.IsCraftable && SotorItemTrait.IsValidFor(trait, itemType))
                    {
                        Traits.Add(new SotorEnchantableTraitVM(trait, OnTraitSelected,
                            CanWork(trait)));
                    }
                }
            }
            ApplyTraitSort();

            foreach (var ingredient in Ingredients) ingredient.ResetPendingAmount();
            ItemTableau.FillFrom(item.Item);
            IsPreviewEnabled = true;
        }

        private void OnTraitSelected(SotorEnchantableTraitVM traitVM, bool isSelected)
        {
            if (isSelected)
            {
                if (!traitVM.CanWork)
                {
                    var blocked = SotorText.GetObject("sotor_enchanting_trait_blocked");
                    blocked.SetTextVariable("HERO", EnchanterName);
                    blocked.SetTextVariable("NEEDS", SotorSkillGate.NeedsLine(traitVM.ItemTrait));
                    InformationManager.ShowInquiry(new InquiryData(
                        ScreenTitle, blocked.ToString(),
                        true, false, SotorText.Rendered("sotor_str_ok"), null, null, null));
                    traitVM.DeselectTrait();
                    return;
                }
                int max = MaxTraits();
                if (SelectedTraits.Count >= max)
                {
                    var text = SotorText.GetObject("sotor_enchanting_max_traits");
                    text.SetTextVariable("MAX_TRAITS", max);
                    InformationManager.ShowInquiry(new InquiryData(
                        SotorText.Rendered("sotor_enchanting_title"), text.ToString(),
                        true, false, SotorText.Rendered("sotor_str_ok"), null, null, null));
                    traitVM.DeselectTrait();
                    return;
                }
                if (!SelectedTraits.Contains(traitVM))
                {
                    SelectedTraits.Add(traitVM);
                    FindIngredient(traitVM.ItemTrait.IngredientItem)?.AddPendingAmount(-traitVM.ItemTrait.IngredientAmount);
                }
            }
            else if (SelectedTraits.Contains(traitVM))
            {
                SelectedTraits.Remove(traitVM);
                FindIngredient(traitVM.ItemTrait.IngredientItem)?.AddPendingAmount(traitVM.ItemTrait.IngredientAmount);
            }
            ItemTableau.FillFrom(SelectedItem.Item);
        }

        private SotorEnchantingIngredientVM FindIngredient(SotorIngredientType type)
        {
            return Ingredients.FirstOrDefault(x => x.IngredientType == type);
        }

        private void ExecuteEnchant()
        {
            if (Traits.Count <= 0)
            {
                var none = SotorText.GetObject("sotor_enchanting_no_blueprints_hero");
                none.SetTextVariable("HERO", EnchanterName);
                InformationManager.ShowInquiry(new InquiryData(
                    ScreenTitle, none.ToString(),
                    true, false, SotorText.Rendered("sotor_str_ok"), null, null, null));
                return;
            }
            if (SelectedTraits.Count == 0)
            {
                ShowMessage("sotor_enchanting_no_traits_selected");
                return;
            }

            if (SelectedTraits.Any(x => !x.CanWork))
            {
                ShowMessage("sotor_enchanting_no_traits_selected");
                return;
            }
            if (Ingredients.Any(x => Math.Abs(x.PendingAmount) > x.CurrentAmount))
            {
                ShowMessage("sotor_enchanting_missing_ingredients");
                return;
            }
            var element = _selectedItem.Item;
            InformationManager.ShowTextInquiry(new TextInquiryData(
                SotorText.Rendered("sotor_enchanting_title"),
                SotorText.Rendered("sotor_enchanting_enter_name"),
                true, true, SotorText.Rendered("sotor_str_ok"), SotorText.Rendered("sotor_str_cancel"),
                name => CreateNewItem(name, element.Item, element.ItemModifier),
                InformationManager.HideInquiry));
        }

        private void CreateNewItem(string newItemName, ItemObject original, ItemModifier modifier)
        {
            var traitIds = SelectedTraits.Select(x => x.ItemTrait.ItemTraitStringId).ToList();
            var created = SotorEnchantmentHelper.CreateEnchantedItem(original, traitIds, newItemName, playerCrafted: true);
            if (created == null)
            {
                ShowMessage("sotor_enchanting_failed");
                return;
            }
            var roster = MobileParty.MainParty.ItemRoster;
            roster.AddToCounts(new EquipmentElement(created, modifier), 1);
            roster.AddToCounts(new EquipmentElement(original, modifier), -1);
            foreach (var ingredient in Ingredients.Where(x => x.PendingAmount != 0))
            {
                roster.AddToCounts(ingredient.Item, ingredient.PendingAmount);
            }
            ShowMessage("sotor_enchanting_success");
            RefreshValues();
        }

        private static void ShowMessage(string stringId)
        {
            InformationManager.ShowInquiry(new InquiryData(
                SotorText.Rendered("sotor_enchanting_title"), SotorText.Rendered(stringId),
                true, false, SotorText.Rendered("sotor_str_ok"), null, null, null));
        }

        private void ExecuteClose()
        {
            _closeAction?.Invoke();
        }
    }

    public class SotorEnchantableItemVM : ViewModel
    {
        private EquipmentElement _item;
        private string _itemName;
        private ImageIdentifierVM _imageIdentifier;
        private bool _isSelected;
        private readonly Action<SotorEnchantableItemVM> _onItemSelected;

        public EquipmentElement Item => _item;

        public int RosterIndex { get; set; }

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

        public SotorEnchantableItemVM(EquipmentElement item, Action<SotorEnchantableItemVM> onItemSelected)
        {
            _item = item;
            _onItemSelected = onItemSelected;
            ItemName = item.GetModifiedItemName().ToString();
            ImageIdentifier = new ItemImageIdentifierVM(item.Item, Clan.PlayerClan?.Banner?.Serialize());
            ImageIdentifier.RefreshValues();
        }

        private void ExecuteSelectItem()
        {
            if (!IsSelected)
            {
                IsSelected = true;
                _onItemSelected?.Invoke(this);
            }
        }

        public void DeselectItem()
        {
            IsSelected = false;
        }
    }

    public class SotorEnchantableTraitVM : ViewModel
    {
        private bool _isSelected;
        private SotorItemTrait _trait;
        private string _traitName;
        private string _iconName;
        private string _itemTraitDescription;
        private BasicTooltipViewModel _itemTraitDescriptionHint;
        private Action<SotorEnchantableTraitVM, bool> _onSelected;

        public SotorItemTrait ItemTrait => _trait;

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChangedWithValue(value, "IsSelected"); } }
        }

        [DataSourceProperty]
        public string TraitName
        {
            get => _traitName;
            set { if (_traitName != value) { _traitName = value; OnPropertyChangedWithValue(value, "TraitName"); } }
        }

        [DataSourceProperty]
        public string IconName
        {
            get => _iconName;
            set { if (_iconName != value) { _iconName = value; OnPropertyChangedWithValue(value, "IconName"); } }
        }

        [DataSourceProperty]
        public string ItemTraitDescription
        {
            get => _itemTraitDescription;
            set { if (_itemTraitDescription != value) { _itemTraitDescription = value; OnPropertyChangedWithValue(value, "ItemTraitDescription"); } }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel ItemTraitDescriptionHint
        {
            get => _itemTraitDescriptionHint;
            set { if (_itemTraitDescriptionHint != value) { _itemTraitDescriptionHint = value; OnPropertyChangedWithValue(value, "ItemTraitDescriptionHint"); } }
        }

        public bool CanWork { get; }

        public SotorEnchantableTraitVM(SotorItemTrait trait, Action<SotorEnchantableTraitVM, bool> onSelected,
            bool canWork = true)
        {
            _trait = trait;
            _onSelected = onSelected;
            CanWork = canWork;
            IsSelected = false;
            TraitName = trait.ItemTraitName;
            IconName = trait.IconName;
            ItemTraitDescription = trait.ItemTraitDescription ?? "";
            ItemTraitDescriptionHint = new BasicTooltipViewModel(GetHintText);
        }

        private string GetHintText()
        {
            string description = string.IsNullOrEmpty(_trait.ItemTraitDescription)
                ? SotorText.Rendered("sotor_enchanting_no_description")
                : _trait.ItemTraitDescription;
            if (CanWork) return description;

            string needs = SotorSkillGate.NeedsLine(_trait);
            return string.IsNullOrEmpty(needs) ? description : description + "\n" + needs;
        }

        private void ExecuteSelectTrait()
        {
            IsSelected = !IsSelected;
            _onSelected?.Invoke(this, IsSelected);
        }

        public void DeselectTrait()
        {
            IsSelected = false;
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            _trait = null;
            _onSelected = null;
        }
    }

    public class SotorEnchantingIngredientVM : ViewModel
    {
        private readonly ItemObject _ingredient;
        private readonly SotorIngredientType _type;
        private int _currentAmount;
        private int _pendingAmount;
        private string _ingredientName;
        private string _ingredientId;
        private string _currentAmountText;
        private string _pendingAmountText;
        private string _textureProviderName = "ItemImageTextureProvider";
        private BasicTooltipViewModel _ingredientTooltip;
        private bool _isVisible = true;

        public ItemObject Item => _ingredient;
        public SotorIngredientType IngredientType => _type;
        public int CurrentAmount => _currentAmount;
        public int PendingAmount => _pendingAmount;

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set { if (_isVisible != value) { _isVisible = value; OnPropertyChangedWithValue(value, "IsVisible"); } }
        }

        [DataSourceProperty]
        public string TextureProviderName
        {
            get => _textureProviderName;
            set { if (_textureProviderName != value) { _textureProviderName = value; OnPropertyChangedWithValue(value, "TextureProviderName"); } }
        }

        [DataSourceProperty]
        public string IngredientName
        {
            get => _ingredientName;
            set { if (_ingredientName != value) { _ingredientName = value; OnPropertyChangedWithValue(value, "IngredientName"); } }
        }

        [DataSourceProperty]
        public string IngredientId
        {
            get => _ingredientId;
            set { if (_ingredientId != value) { _ingredientId = value; OnPropertyChangedWithValue(value, "IngredientId"); } }
        }

        [DataSourceProperty]
        public string CurrentAmountText
        {
            get => _currentAmountText;
            set { if (_currentAmountText != value) { _currentAmountText = value; OnPropertyChangedWithValue(value, "CurrentAmountText"); } }
        }

        [DataSourceProperty]
        public string PendingAmountText
        {
            get => _pendingAmountText;
            set { if (_pendingAmountText != value) { _pendingAmountText = value; OnPropertyChangedWithValue(value, "PendingAmountText"); } }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel IngredientTooltip
        {
            get => _ingredientTooltip;
            set { if (_ingredientTooltip != value) { _ingredientTooltip = value; OnPropertyChangedWithValue(value, "IngredientTooltip"); } }
        }

        public SotorEnchantingIngredientVM(SotorIngredientType type, ItemObject item, int amount)
        {
            _type = type;
            _ingredient = item;
            IngredientName = item.Name.ToString();
            IngredientId = item.StringId;
            _currentAmount = amount;
            _pendingAmount = 0;
            IngredientTooltip = new BasicTooltipViewModel(GetHintText);
            RefreshAmountTexts();
        }

        private string GetHintText()
        {
            return IngredientName;
        }

        public void AddPendingAmount(int amount)
        {
            _pendingAmount += amount;
            RefreshAmountTexts();
        }

        public void ResetPendingAmount()
        {
            _pendingAmount = 0;
            RefreshAmountTexts();
        }

        private void RefreshAmountTexts()
        {
            CurrentAmountText = _currentAmount > 0 ? _currentAmount.ToString() : " ";
            PendingAmountText = _pendingAmount < 0 ? _pendingAmount.ToString() : " ";
        }

        public void SetRecoveredAmount(int amount)
        {
            _pendingAmount = amount;
            IsVisible = amount > 0;
            CurrentAmountText = amount > 0 ? amount.ToString() : " ";
            PendingAmountText = " ";
        }
    }

    public class SotorEnchantingTableauVM : ViewModel
    {
        private string _stringId = string.Empty;
        private string _bannerCode = string.Empty;
        private string _itemModifierId = string.Empty;

        [DataSourceProperty]
        public string StringId
        {
            get => _stringId;
            set { _stringId = value; OnPropertyChangedWithValue(value, "StringId"); }
        }

        [DataSourceProperty]
        public string BannerCode
        {
            get => _bannerCode;
            set { if (_bannerCode != value) { _bannerCode = value; OnPropertyChangedWithValue(value, "BannerCode"); } }
        }

        [DataSourceProperty]
        public string ItemModifierId
        {
            get => _itemModifierId;
            set { if (_itemModifierId != value) { _itemModifierId = value; OnPropertyChangedWithValue(value, "ItemModifierId"); } }
        }

        public void FillFrom(EquipmentElement element)
        {
            StringId = element.Item != null ? element.Item.StringId : string.Empty;
            BannerCode = Clan.PlayerClan?.Banner?.Serialize() ?? string.Empty;
            ItemModifierId = element.ItemModifier != null ? element.ItemModifier.StringId : string.Empty;
        }
    }

    public class SotorEnchantingSortControllerVM : ViewModel
    {
        public const int ColumnNone = 0;
        public const int ColumnType = 1;
        public const int ColumnName = 2;

        private readonly Action _applySort;
        private string _typeText;
        private string _nameText;
        private int _typeState;
        private int _nameState;
        private bool _isTypeSelected;
        private bool _isNameSelected;

        public int ActiveColumn { get; private set; } = ColumnNone;
        public bool Descending { get; private set; }

        public SotorEnchantingSortControllerVM(Action applySort, string typeLabelId = "sotor_enchanting_sort_type")
        {
            _applySort = applySort;
            TypeText = SotorText.Rendered(typeLabelId);
            NameText = SotorText.Rendered("sotor_enchanting_sort_name");
        }

        [DataSourceProperty]
        public string TypeText
        {
            get => _typeText;
            set { if (_typeText != value) { _typeText = value; OnPropertyChangedWithValue(value, "TypeText"); } }
        }

        [DataSourceProperty]
        public string NameText
        {
            get => _nameText;
            set { if (_nameText != value) { _nameText = value; OnPropertyChangedWithValue(value, "NameText"); } }
        }

        [DataSourceProperty]
        public int TypeState
        {
            get => _typeState;
            set { if (_typeState != value) { _typeState = value; OnPropertyChangedWithValue(value, "TypeState"); } }
        }

        [DataSourceProperty]
        public int NameState
        {
            get => _nameState;
            set { if (_nameState != value) { _nameState = value; OnPropertyChangedWithValue(value, "NameState"); } }
        }

        [DataSourceProperty]
        public bool IsTypeSelected
        {
            get => _isTypeSelected;
            set { if (_isTypeSelected != value) { _isTypeSelected = value; OnPropertyChangedWithValue(value, "IsTypeSelected"); } }
        }

        [DataSourceProperty]
        public bool IsNameSelected
        {
            get => _isNameSelected;
            set { if (_isNameSelected != value) { _isNameSelected = value; OnPropertyChangedWithValue(value, "IsNameSelected"); } }
        }

        private void ExecuteSortByType() { Cycle(ColumnType); }
        private void ExecuteSortByName() { Cycle(ColumnName); }

        private void Cycle(int column)
        {
            if (ActiveColumn != column)
            {
                ActiveColumn = column;
                Descending = false;
            }
            else if (!Descending)
            {
                Descending = true;
            }
            else
            {
                ActiveColumn = ColumnNone;
                Descending = false;
            }

            TypeState = ActiveColumn == ColumnType ? (Descending ? 2 : 1) : 0;
            NameState = ActiveColumn == ColumnName ? (Descending ? 2 : 1) : 0;
            IsTypeSelected = ActiveColumn == ColumnType;
            IsNameSelected = ActiveColumn == ColumnName;
            _applySort?.Invoke();
        }
    }
}
