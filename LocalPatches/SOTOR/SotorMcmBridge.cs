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
            if (!(sender is SotorMcmSettings s)) return;

            s.SyncToStore();

            SotorLog.Info($"MCM: '{e?.PropertyName}' changed.");

            RebuildRivalWorldIfSettingsChanged(e?.PropertyName);
        }

        private static void RebuildRivalWorldIfSettingsChanged(string propertyName)
        {

            if (propertyName != "SAVE_TRIGGERED") return;

            try
            {

                if (TaleWorlds.CampaignSystem.Campaign.Current == null)
                {
                    SotorLog.Info("MCM: save with no campaign loaded; the next session launch will seed.");
                    return;
                }

                var ui = SotorMcmPending.Instance;
                if (ui != null)
                {
                    ui.SyncToStore();
                }

                string now = CampaignBehaviors.SotorRivalBehavior.RivalSettingsFingerprint();
                string built = CampaignBehaviors.SotorRivalBehavior.LastBuiltFingerprint;

                SotorLog.Info($"MCM: save committed (ui={ui != null}).\n  built: {built ?? "(none)"}\n  now:   {now}");

                if (built == null || string.Equals(built, now))
                {
                    SotorLog.Info("MCM: Rival Wizards settings are unchanged, leaving the world alone.");
                    return;
                }

                SotorLog.Info("MCM: Rival Wizards settings changed on save, rebuilding the world.");
                string result = CampaignBehaviors.SotorRivalBehavior.RegenerateWorld();
                SotorLog.Info("MCM: rebuild finished. " + (result ?? "(no result)"));
            }
            catch (System.Exception ex)
            {

                SotorLog.Error($"MCM: world rebuild failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
