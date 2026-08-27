using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SOTOR.Items
{

    public static class SotorDisenchantPopup
    {
        private static SotorDisenchantLayer _layer;
        private static SotorDisenchantVM _dataSource;

        public static void Open(Action<List<EquipmentElement>> onAccept)
        {
            if (_layer != null) return;
            var screen = ScreenManager.TopScreen;
            if (screen == null) return;

            _dataSource = new SotorDisenchantVM(Close, onAccept);
            _layer = new SotorDisenchantLayer(Close);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
            _layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"));
            _layer.LoadMovie("SotorDisenchant", _dataSource);
            _layer.IsFocusLayer = true;
            screen.AddLayer(_layer);
            ScreenManager.TrySetFocus(_layer);
        }

        public static void Close()
        {
            if (_layer == null) return;
            var layer = _layer;
            _layer = null;
            layer.IsFocusLayer = false;
            ScreenManager.TryLoseFocus(layer);
            ScreenManager.TopScreen?.RemoveLayer(layer);
            _dataSource?.OnFinalize();
            _dataSource = null;
        }

        private class SotorDisenchantLayer : GauntletLayer
        {
            private readonly Action _onExit;

            public SotorDisenchantLayer(Action onExit)
                : base("SotorDisenchant", 1000, shouldClear: false)
            {
                _onExit = onExit;
            }

            protected override void Tick(float dt)
            {
                base.Tick(dt);
                if (Input.IsHotKeyReleased("Exit") || Input.IsKeyReleased(InputKey.Escape))
                {
                    _onExit?.Invoke();
                }
            }
        }
    }
}
