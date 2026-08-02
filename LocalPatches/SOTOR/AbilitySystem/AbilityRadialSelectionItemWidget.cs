using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace SOTOR.AbilitySystem
{
    public class AbilityRadialSelectionItemWidget : ButtonWidget
    {
        public AbilityRadialSelectionItemWidget(UIContext context)
            : base(context)
        {
            EnsureState("Selected");
            EnsureState("Default");
            EnsureState("Pressed");
            EnsureState("Hovered");
            EnsureState("Disabled");
        }

        private void EnsureState(string state)
        {
            if (!ContainsState(state))
            {
                AddState(state);
            }
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();
            boolPropertyChanged += OnBoolPropertyChanged;
        }

        protected override void OnDisconnectedFromRoot()
        {
            boolPropertyChanged -= OnBoolPropertyChanged;
            base.OnDisconnectedFromRoot();
        }

        private void OnBoolPropertyChanged(PropertyOwnerObject widget, string propertyName, bool value)
        {
            if (propertyName != "IsSelected")
            {
                return;
            }

            if (value)
            {
                SetState("Selected");
                EventFired("OnSelected", Array.Empty<object>());
            }
            else
            {
                SetState("Default");
            }
        }
    }
}
