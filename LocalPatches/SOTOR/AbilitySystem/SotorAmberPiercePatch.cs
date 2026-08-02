using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    [HarmonyPatch(typeof(SandboxAgentApplyDamageModel), nameof(SandboxAgentApplyDamageModel.DecideMissileWeaponFlags))]
    public static class SotorAmberPiercePatch
    {
        private const ulong MultiplePenetration = 0x40000000uL;
        private const ulong CanPenetrateShield = 0x20000uL;

        public static void Postfix(in MissionWeapon missileWeapon, ref WeaponFlags missileWeaponFlags)
        {
            try
            {
                if (missileWeapon.IsEmpty) return;
                if (missileWeapon.Item?.StringId != ThrownWeaponAbility.AmberJavelinItemId) return;
                missileWeaponFlags = (WeaponFlags)((ulong)missileWeaponFlags | MultiplePenetration | CanPenetrateShield);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorAmberPiercePatch failed: {ex.Message}");
            }
        }
    }
}
