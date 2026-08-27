using SOTOR.Items;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    public static class SotorWardSave
    {

        public const float MinDamageFactor = 0.25f;

        public const float MaxWard = 1f - MinDamageFactor;

        public static float StatusWard(Agent victim, AttackTypeMask channel)
        {
            var status = victim?.GetComponent<StatusEffectComponent>();
            var resArr = status?.GetResistances(channel);
            int slot = (int)DamageType.All;
            if (resArr == null || slot < 0 || slot >= resArr.Length) return 0f;
            return resArr[slot];
        }

        public static float ItemWard(Agent victim)
        {
            if (victim == null) return 0f;
            int typeCount = System.Enum.GetValues(typeof(DamageType)).Length;
            var resist = new float[typeCount];
            SotorItemExtensions.SumResistTuples(victim, resist);
            return resist[(int)DamageType.All];
        }

        public static float FactorFrom(float itemWard, float statusWard)
        {
            float total = itemWard + statusWard;
            if (total <= 0f) return 1f;
            float factor = 1f - total;
            if (factor > 1f) return 1f;
            return factor < MinDamageFactor ? MinDamageFactor : factor;
        }

        public static float Factor(Agent victim, AttackTypeMask channel)
        {
            return FactorFrom(ItemWard(victim), StatusWard(victim, channel));
        }
    }
}
