using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.CampaignBehaviors
{

    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnTroop))]
    public static class SotorGraveyardMountPatch
    {

        public static bool SuppressPlayerMount;

        private static readonly int MountArgIndex;

        private static readonly bool DismountValue;

        static SotorGraveyardMountPatch()
        {
            MountArgIndex = -1;
            try
            {
                var method = AccessTools.Method(typeof(Mission), nameof(Mission.SpawnTroop));
                var ps = method?.GetParameters();
                if (ps != null)
                {

                    string wantName =
#if BL13
                        "forceDismounted";
#else
                        "spawnWithHorse";
#endif
                    bool wantValue =
#if BL13
                        true;
#else
                        false;
#endif
                    for (int i = 0; i < ps.Length; i++)
                    {
                        if (ps[i].Name == wantName) { MountArgIndex = i; DismountValue = wantValue; break; }
                    }
                }
                if (MountArgIndex < 0)
                    SotorLog.Warn("SotorGraveyardMountPatch: no spawnWithHorse/forceDismounted param on SpawnTroop; player-dismount is a no-op.");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorGraveyardMountPatch static init failed: {ex.Message}");
            }
        }

        public static void Prefix(object[] __args)
        {
            try
            {
                if (!SuppressPlayerMount || MountArgIndex < 0 || __args == null || __args.Length <= MountArgIndex)
                    return;

                var troopOrigin = __args.Length > 0 ? __args[0] as IAgentOriginBase : null;
                if (troopOrigin != null && troopOrigin.Troop != null && troopOrigin.Troop.IsPlayerCharacter)
                {
                    __args[MountArgIndex] = DismountValue;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorGraveyardMountPatch.Prefix failed: {ex.Message}");
            }
        }
    }
}
