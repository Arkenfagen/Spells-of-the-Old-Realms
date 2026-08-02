using System;
using HarmonyLib;
using SandBox.ViewModelCollection;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    [HarmonyPatch(typeof(SandBoxUIHelper), nameof(SandBoxUIHelper.GetRecruitNotificationText))]
    public static class SotorGraveyardRecruitTextPatch
    {
        public static bool ActiveForGraveyardRaise;

        public static IDisposable Scope() => new ScopedActivation();

        public static void Postfix(int recruitmentAmount, ref string __result)
        {
            try
            {
                if (!ActiveForGraveyardRaise) return;
                var text = new TextObject("+{COUNT} Skeleton{?PLURAL}s{?}{\\?}");
                text.SetTextVariable("COUNT", recruitmentAmount);
                text.SetTextVariable("PLURAL", recruitmentAmount > 1 ? 1 : 0);
                __result = text.ToString();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorGraveyardRecruitTextPatch.Postfix failed: {ex.Message}");
            }
        }

        private sealed class ScopedActivation : IDisposable
        {
            public ScopedActivation() { ActiveForGraveyardRaise = true; }
            public void Dispose() { ActiveForGraveyardRaise = false; }
        }
    }
}
