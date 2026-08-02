using TaleWorlds.CampaignSystem;

namespace SOTOR.AbilitySystem
{

    public static class SotorSpellcraftHelper
    {

        public const float DamagePerSpellcraftPoint = 0.0005f;

        public static SpellCastingLevel GetCastingLevel(Hero hero)
        {
            if (hero == null)
            {
                return SpellCastingLevel.None;
            }

            if (SotorPerks.Instance != null)
            {
                if (SotorPerks.Archmage != null && hero.GetPerkValue(SotorPerks.Archmage))
                {
                    return SpellCastingLevel.Archmage;
                }
                if (SotorPerks.MasterSpells != null && hero.GetPerkValue(SotorPerks.MasterSpells))
                {
                    return SpellCastingLevel.Master;
                }
                if (SotorPerks.AdeptSpells != null && hero.GetPerkValue(SotorPerks.AdeptSpells))
                {
                    return SpellCastingLevel.Adept;
                }
                if (SotorPerks.EntrySpells != null && hero.GetPerkValue(SotorPerks.EntrySpells))
                {
                    return SpellCastingLevel.Entry;
                }
                return SpellCastingLevel.Minor;
            }

            var skill = SotorSkills.Spellcraft;
            int value = skill != null ? hero.GetSkillValue(skill) : 0;
            if (value >= 200) return SpellCastingLevel.Master;
            if (value >= 100) return SpellCastingLevel.Adept;
            if (value >= 25) return SpellCastingLevel.Entry;
            return SpellCastingLevel.Minor;
        }

        public static int GetSpellBaseGoldCost(AbilityTemplate template)
        {
            if (template == null)
            {
                return 0;
            }

            return SotorPriceTable.GetSpellCost(template.StringID, template.SpellTier);
        }

        public static int GetSpellGoldCost(Hero hero, AbilityTemplate template)
        {
            int baseCost = GetSpellBaseGoldCost(template);
            if (hero != null && SotorPerks.Librarian != null && hero.GetPerkValue(SotorPerks.Librarian))
            {
                baseCost = (int)(baseCost * 0.5f);
            }
            return baseCost;
        }

        public const float DurationPerSpellcraftPoint = 0.0005f;
        public const float MaxWindsPerSpellcraftPoint = 0.3f;
        public const float WindsRechargePerSpellcraftPoint = 0.0075f;

        public const float BaseMaxWinds = 10f;

        private static int SpellcraftValue(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            return (hero != null && skill != null) ? hero.GetSkillValue(skill) : 0;
        }

        public static float GetSpellDurationFactor(Hero hero)
        {
            float factor = 1f + DurationPerSpellcraftPoint * SpellcraftValue(hero);
            if (hero != null && SotorPerks.Selfish != null && hero.GetPerkValue(SotorPerks.Selfish))
            {
                factor += 0.15f;
            }
            return factor;
        }

        public static float GetMaxWinds(Hero hero)
        {
            return BaseMaxWinds + MaxWindsPerSpellcraftPoint * SpellcraftValue(hero);
        }

        public static float GetWindsRechargeSkillBonus(Hero hero)
        {
            return WindsRechargePerSpellcraftPoint * SpellcraftValue(hero);
        }

        public static float GetSpellDamageFactor(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            if (hero == null || skill == null)
            {
                return 1f;
            }

            return 1f + DamagePerSpellcraftPoint * hero.GetSkillValue(skill)
                   + SOTOR.SotorSettings.SpellEffectivenessBonusFraction;
        }

        public static float GetCasterPerkDamageFactor(Hero hero)
        {
            if (hero == null)
            {
                return 1f;
            }

            float factor = 1f;
            if (SotorPerks.OverCaster != null && hero.GetPerkValue(SotorPerks.OverCaster))
            {
                factor += 0.2f;
            }
            if (SotorPerks.EfficientSpellCaster != null && hero.GetPerkValue(SotorPerks.EfficientSpellCaster))
            {
                factor -= 0.2f;
            }
            if (SotorPerks.Dampener != null && hero.GetPerkValue(SotorPerks.Dampener))
            {
                factor -= 0.15f;
            }

            return factor;
        }

        public static float GetVictimPerkDamageFactor(Hero casterHero, TaleWorlds.MountAndBlade.Agent caster, TaleWorlds.MountAndBlade.Agent victim)
        {
            if (casterHero == null || caster == null || victim == null)
            {
                return 1f;
            }

            float factor = 1f;

            if (victim == caster && SotorPerks.Selfish != null && casterHero.GetPerkValue(SotorPerks.Selfish))
            {
                factor *= 0.1f;
            }

            else if (victim != caster && !victim.IsEnemyOf(caster)
                     && SotorPerks.WellControlled != null && casterHero.GetPerkValue(SotorPerks.WellControlled))
            {
                factor *= 0.7f;
            }

            return factor;
        }

        public static void GrantAbilityOutcomeXp(Hero caster, int rawXp, bool singleTarget)
        {
            var skill = SotorSkills.Spellcraft;
            if (caster == null || skill == null || rawXp <= 0)
            {
                return;
            }
            int xp = singleTarget ? rawXp * 5 : rawXp;
            caster.AddSkillXp(skill, xp);
            SotorLog.Debug($"Spellcraft outcome XP: +{xp} (raw={rawXp}, singleTarget={singleTarget}) -> skill now {caster.GetSkillValue(skill)}.");
        }
    }
}
