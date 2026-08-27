using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.Items
{

    public class SotorItemMenuVM : ItemMenuVM
    {
        private bool _isMagicItem;
        private MBBindingList<SotorItemTraitIconVM> _itemTraitList = new MBBindingList<SotorItemTraitIconVM>();
        private string _magicItemLabel;
        private string _itemEffectsLabel;

        [DataSourceProperty]
        public bool IsMagicItem
        {
            get => _isMagicItem;
            set { if (_isMagicItem != value) { _isMagicItem = value; OnPropertyChangedWithValue(value, "IsMagicItem"); } }
        }

        [DataSourceProperty]
        public MBBindingList<SotorItemTraitIconVM> ItemTraitList
        {
            get => _itemTraitList;
            set { if (_itemTraitList != value) { _itemTraitList = value; OnPropertyChangedWithValue(value, "ItemTraitList"); } }
        }

        [DataSourceProperty]
        public string MagicItemLabel
        {
            get => _magicItemLabel;
            set { if (_magicItemLabel != value) { _magicItemLabel = value; OnPropertyChangedWithValue(value, "MagicItemLabel"); } }
        }

        [DataSourceProperty]
        public string ItemEffectsLabel
        {
            get => _itemEffectsLabel;
            set { if (_itemEffectsLabel != value) { _itemEffectsLabel = value; OnPropertyChangedWithValue(value, "ItemEffectsLabel"); } }
        }

        public SotorItemMenuVM(Action<ItemVM, int> resetComparedItems, InventoryLogic inventoryLogic,
            Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags,
            Func<EquipmentIndex, SPItemVM> getEquipmentAtIndex)
            : base(resetComparedItems, inventoryLogic, getItemUsageSetFlags, getEquipmentAtIndex)
        {

            MagicItemLabel = SotorText.Rendered("sotor_inventory_magic_item");
            ItemEffectsLabel = SotorText.Rendered("sotor_inventory_item_effects");
        }

        public void SetItemExtra(SPItemVM item)
        {
            ItemTraitList.Clear();
            IsMagicItem = false;
            var itemObject = item?.ItemRosterElement.EquipmentElement.Item;
            if (itemObject == null || !SotorExtendedItemManager.HasTraits(itemObject)) return;

            EnsureSotorSprites();

            IsMagicItem = true;

            bool isBook = SOTOR.CampaignBehaviors.SotorBlueprintBookBehavior.IsBookItemId(itemObject.StringId);
            foreach (var trait in SotorExtendedItemManager.GetTraitsOfItem(itemObject))
            {
                ItemTraitList.Add(new SotorItemTraitIconVM(trait, showRequirement: isBook));
            }
        }

        private static bool _spritesLoaded;

        private static void EnsureSotorSprites()
        {
            if (_spritesLoaded) return;
            try
            {
                var spriteData = TaleWorlds.Engine.GauntletUI.UIResourceManager.SpriteData;
                if (spriteData?.SpriteCategories != null && spriteData.SpriteCategories.TryGetValue("ui_sotor", out var category)
                    && !category.IsLoaded)
                {
                    category.Load(TaleWorlds.Engine.GauntletUI.UIResourceManager.ResourceContext,
                        TaleWorlds.Engine.GauntletUI.UIResourceManager.ResourceDepot);
                }
                _spritesLoaded = true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"EnsureSotorSprites (inventory) failed: {ex.Message}");
            }
        }
    }

    public class SotorItemTraitIconVM : ViewModel
    {
        private string _icon;
        private HintViewModel _hint;

        [DataSourceProperty]
        public string Icon
        {
            get => _icon;
            set { if (_icon != value) { _icon = value; OnPropertyChangedWithValue(value, "Icon"); } }
        }

        [DataSourceProperty]
        public HintViewModel Hint
        {
            get => _hint;
            set { if (_hint != value) { _hint = value; OnPropertyChangedWithValue(value, "Hint"); } }
        }

        public SotorItemTraitIconVM(SotorItemTrait trait, bool showRequirement = false)
        {
            _icon = "<img src=\"" + trait.IconName + "\"/>";
            var text = SotorText.GetObject("sotor_inventory_trait_hint");
            text.SetTextVariable("NAME", trait.ItemTraitName);
            text.SetTextVariable("EFFECT", trait.ItemTraitDescription ?? "");

            string needsLine = showRequirement ? SotorSkillGate.NeedsLine(trait) : "";
            _hint = string.IsNullOrEmpty(needsLine)
                ? new HintViewModel(text)
                : new HintViewModel(new TextObject(text.ToString() + "\n" + needsLine));
        }
    }
}
