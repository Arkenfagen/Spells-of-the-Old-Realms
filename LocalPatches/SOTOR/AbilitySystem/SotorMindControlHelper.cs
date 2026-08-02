using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace SOTOR.AbilitySystem
{

    public static class SotorMindControlHelper
    {
        public const float BaseChance = 0.50f;
        public const float PerSocialSkillRate = 0.000625f;

        public const float GateEffectiveness =
            1f + SotorSpellcraftHelper.DamagePerSpellcraftPoint * 300f;

        public const float PerLevelPenalty = 0.02f;
        public const float MinChance = 0.05f;

        public static float GetBaseChance(Hero caster)
        {
            if (caster == null)
            {
                return BaseChance;
            }

            int charm = caster.GetSkillValue(DefaultSkills.Charm);
            int leadership = caster.GetSkillValue(DefaultSkills.Leadership);
            int roguery = caster.GetSkillValue(DefaultSkills.Roguery);
            float social = (charm + leadership + roguery) * PerSocialSkillRate;

            float effMult = SotorSpellcraftHelper.GetSpellDamageFactor(caster) / GateEffectiveness;

            float chance = (BaseChance + social) * effMult;
            return chance < MinChance ? MinChance : chance;
        }

        public static float GetTargetChance(Hero caster, int casterLevel, int enemyLevel, float enemyHpFraction)
        {
            float chance = GetBaseChance(caster)
                           - (enemyLevel - casterLevel) * PerLevelPenalty * enemyHpFraction;
            return chance < MinChance ? MinChance : chance;
        }
    }
}
