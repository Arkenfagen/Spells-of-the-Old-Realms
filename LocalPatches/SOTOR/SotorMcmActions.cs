using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace SOTOR
{

    internal static class SotorMcmActions
    {

        private static string DialogTitle => SotorText.Rendered("sotor_mcm_dialog_title", "Rival Wizards");

        public static void ShowWorldReport()
        {
            try
            {

                var ui = SotorMcmPending.Instance;
                string report = ui != null
                    ? AbilitySystem.Rivals.SotorWorldPreview.Predict(
                        ui.EnableRivalCasters, ui.RivalCasterLordShare, ui.RivalCasterWandererShare,
                        ui.RivalMemberOnlyLoreClanChance, ui.RivalMinClanTierForCaster,
                        ui.RivalIncludeRulers, ui.RivalIncludeMinorFactions, ui.RivalWorldSeed,
                        ui.RivalLoreSource?.SelectedIndex == 1)
                    : AbilitySystem.Rivals.SotorWorldPreview.Predict(
                        SotorSettings.EnableRivalCasters, SotorSettings.RivalCasterLordShare,
                        SotorSettings.RivalCasterWandererShare, SotorSettings.RivalMemberOnlyLoreClanChance,
                        SotorSettings.RivalMinClanTierForCaster, SotorSettings.RivalIncludeRulers,
                        SotorSettings.RivalIncludeMinorFactions, SotorSettings.RivalWorldSeed,
                        SotorSettings.RivalLoreByCulture);

                SotorLog.Info($"MCM: world preview requested (live={ui != null}).\n" + report);

                ShowDialog(report, SotorText.Rendered("sotor_mcm_preview_title", "World Preview"));
            }
            catch (Exception ex)
            {
                SotorLog.Error($"MCM: World report failed: {ex.GetType().Name}: {ex.Message}");
                ShowDialog(SotorText.Rendered("sotor_mcm_report_failed",
                    "Could not read the campaign. See the SOTOR log for details."));
            }
        }

        public static void ResetRivalOptions()
        {
            try
            {
                var target = SotorMcmPending.Instance;
                if (target == null)
                {
                    ShowDialog(SotorText.Rendered("sotor_mcm_defaults_failed",
                        "Could not restore the defaults. See the SOTOR log for details."));
                    return;
                }

                target.ResetRivalToDefaults();
                ShowDialog(SotorText.Rendered("sotor_mcm_defaults_done"));
            }
            catch (Exception ex)
            {
                SotorLog.Error($"MCM: Defaults failed: {ex.GetType().Name}: {ex.Message}");
                ShowDialog(SotorText.Rendered("sotor_mcm_defaults_failed",
                    "Could not restore the defaults. See the SOTOR log for details."));
            }
        }

        private static void ShowDialog(string body, string title = null)
        {
            InformationManager.ShowInquiry(new InquiryData(
                title ?? DialogTitle, body ?? string.Empty,
                isAffirmativeOptionShown: true, isNegativeOptionShown: false,
                affirmativeText: SotorText.Rendered("sotor_mcm_dialog_ok", "OK"), negativeText: null,
                affirmativeAction: null, negativeAction: null));
        }
    }
}
