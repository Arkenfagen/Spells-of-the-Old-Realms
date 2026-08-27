using System;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public static class SotorResistanceHelper
    {

        public static AttackTypeMask ChannelForWeapon(bool isMissile)
            => isMissile ? AttackTypeMask.Ranged : AttackTypeMask.Melee;

        public static float GetDamageFactor(
            Agent attacker, Agent victim, AttackTypeMask channel, DamageType resistDamageType,
            out float amp, out float resist, out float ward)
        {
            amp = 0f;
            resist = 0f;
            ward = 0f;

            var attackerStatus = attacker?.GetComponent<StatusEffectComponent>();
            if (attackerStatus != null)
            {
                amp = SumSlots(attackerStatus.GetAmplifiers(channel));
            }

            var victimStatus = victim?.GetComponent<StatusEffectComponent>();
            if (victimStatus != null)
            {
                var resArr = victimStatus.GetResistances(channel);
                if (resArr != null)
                {
                    int slot = (int)resistDamageType;
                    if (slot >= 0 && slot < resArr.Length)
                    {
                        resist = resArr[slot];
                    }

                    int wardSlot = (int)DamageType.All;
                    if (wardSlot >= 0 && wardSlot < resArr.Length)
                    {
                        ward = resArr[wardSlot];
                    }
                }
            }

            if (amp == 0f && resist == 0f && ward == 0f)
            {
                return 1f;
            }

            float factor = (1f + amp) * (1f - Math.Min(resist, 1f));
            return factor < 0f ? 0f : factor;
        }

        private static float SumSlots(float[] arr)
        {
            if (arr == null) return 0f;
            float s = 0f;
            for (int i = 0; i < arr.Length; i++) s += arr[i];
            return s;
        }
    }
}
