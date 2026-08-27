using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Inventory;
using TaleWorlds.TwoDimension;

namespace SOTOR.Items
{

    public class SotorImageIdentifierWidget : ImageIdentifierWidget
    {
        private static readonly Color MagicColor = Color.ConvertStringToColor("#FF39FFEB");

        private static readonly List<string> RelevantWidgetIds = new List<string>
        {
            "ColorableEquipmentSlot", "ColorableTooltip", "ColorableCompareTooltip"
        };

        private bool _isMagicItem;

        public SotorImageIdentifierWidget(UIContext context) : base(context)
        {
            PropertyChanged += OnOwnPropertyChanged;
        }

        private void OnOwnPropertyChanged(PropertyOwnerObject owner, string propertyName, object value)
        {
            if (propertyName != "ImageId") return;
            _isMagicItem = value is string id && SotorExtendedItemManager.HasTraitsById(id);
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            BrushWidget colorable = null;
            if (ParentWidget is BrushWidget parentBrush && RelevantWidgetIds.Contains(parentBrush.Id))
            {
                colorable = parentBrush;
            }
            else if (ParentWidget != null)
            {
                colorable = ParentWidget.Children.FirstOrDefault(
                    x => x is BrushWidget && RelevantWidgetIds.Contains(x.Id)) as BrushWidget;
            }

            if (colorable != null)
            {
                colorable.Brush.Color = _isMagicItem ? MagicColor : Color.White;
            }
            base.OnRender(twoDimensionContext, drawContext);
        }
    }

    public class SotorInventoryItemTupleWidget : InventoryItemTupleWidget
    {
        private readonly Brush _magicBrush;
        private ButtonWidget _useButton;
        private bool _useButtonSearched;

        public SotorInventoryItemTupleWidget(UIContext context) : base(context)
        {
            _magicBrush = context.GetBrush("SotorInventoryMagicItemTupleBrush");
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            if (_magicBrush != null && MainContainer != null && ItemID != null
                && !MainContainer.Brush.IsCloneRelated(CharacterCantUseBrush)
                && SotorExtendedItemManager.HasTraitsById(ItemID))
            {
                MainContainer.Brush = _magicBrush;
            }
            ManageUseButton();
            base.OnRender(twoDimensionContext, drawContext);
        }

        private void ManageUseButton()
        {
            if (!_useButtonSearched)
            {
                _useButtonSearched = true;
                _useButton = FindChild("SotorUseButton", true) as ButtonWidget;
                if (_useButton != null) _useButton.EventFire += OnUseButtonEvent;
            }
            if (_useButton == null) return;
            var check = SotorInventoryUse.IsUsable;
            _useButton.IsVisible = check != null && ItemID != null && check(ItemID);
        }

        private void OnUseButtonEvent(Widget widget, string eventName, object[] args)
        {
            if (eventName == "Click")
            {
                if (ItemID != null) SotorInventoryUse.Use?.Invoke(ItemID);
            }
            else if (eventName == "HoverBegin")
            {
                var hint = SotorInventoryUse.HintText;
                if (!string.IsNullOrEmpty(hint)) TaleWorlds.Core.MBInformationManager.ShowHint(hint);
            }
            else if (eventName == "HoverEnd")
            {
                TaleWorlds.Core.MBInformationManager.HideInformations();
            }
        }
    }

    public static class SotorInventoryUse
    {
        public static Func<string, bool> IsUsable;
        public static Action<string> Use;
        public static string HintText;
    }
}
