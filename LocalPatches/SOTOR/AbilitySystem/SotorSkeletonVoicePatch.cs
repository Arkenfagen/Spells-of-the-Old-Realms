using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public static class SkeletonVoice
    {

        public const string SkeletonCultureId = "sotor_skeleton";

        private const string BoneRattleSound = "sotor_skeleton_bonerattle";

        public static bool IsSkeleton(Agent agent)
        {
            var culture = agent?.Character?.Culture;
            return culture != null && culture.StringId == SkeletonCultureId;
        }

        public static void PlayRattle(Agent agent)
        {
            if (agent == null || Mission.Current == null) return;
            int eventId = SoundEvent.GetEventIdFromString(BoneRattleSound);
            if (eventId >= 0)
            {
                Mission.Current.MakeSound(eventId, agent.Position, false, true, agent.Index, -1);
            }
        }
    }

    [HarmonyPatchCategory(SotorPatchCategories.MissionOnly)]
    [HarmonyPatch(typeof(Agent), nameof(Agent.MakeVoice))]
    public static class SotorSkeletonVoicePatch
    {

        private static bool Prefix(Agent __instance, SkinVoiceManager.SkinVoiceType voiceType)
        {
            try
            {

                if (!SkeletonVoice.IsSkeleton(__instance))
                {
                    return true;
                }

                SkeletonVoice.PlayRattle(__instance);
                return false;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SkeletonVoicePatch failed: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatchCategory(SotorPatchCategories.MissionOnly)]
    [HarmonyPatch(typeof(Agent), "HandleBlowAux")]
    public static class SotorSkeletonPainPatch
    {
        private static bool Prefix(Agent __instance)
        {
            try
            {
                if (!SkeletonVoice.IsSkeleton(__instance))
                {
                    return true;
                }
                SkeletonVoice.PlayRattle(__instance);
                return false;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SkeletonPainPatch failed: {ex.Message}");
                return true;
            }
        }
    }
}
