using System.ComponentModel;
using MCM.Abstractions.Base.Global;

namespace SOTOR
{

    internal static class SotorMcmBridge
    {
        public static void Initialize()
        {
            var settings = GlobalSettings<SotorMcmSettings>.Instance;
            if (settings == null)
            {
                SotorLog.Info("MCM: SOTOR settings instance not available (MCM not installed?) — using SotorSettings defaults.");
                return;
            }

            settings.SyncToStore();
            SotorLog.Info($"MCM: SOTOR settings bound. UseThrownAmberSpear={SotorSettings.UseThrownAmberSpear}.");

            settings.PropertyChanged -= OnChanged;
            settings.PropertyChanged += OnChanged;
        }

        private static void OnChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is SotorMcmSettings s)
            {
                s.SyncToStore();
                SotorLog.Info($"MCM: setting '{e.PropertyName}' changed → UseThrownAmberSpear={SotorSettings.UseThrownAmberSpear}.");
            }
        }
    }
}
