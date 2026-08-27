using System;
using System.Collections.Generic;
using System.Reflection;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRivalReset
    {
        public sealed class Result
        {
            public int Heroes;
            public int Lores;
            public int Spells;
            public int Perks;
            public int StandingEntries;
        }

        private static MethodInfo _setPerkValue;
        private static bool _setPerkValueResolved;

        private static MethodInfo SetPerkValueMethod()
        {
            if (!_setPerkValueResolved)
            {
                _setPerkValueResolved = true;
                try
                {
                    _setPerkValue = typeof(Hero).GetMethod("SetPerkValueInternal",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                catch
                {
                    _setPerkValue = null;
                }
            }
            return _setPerkValue;
        }

        public static int StripHero(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null) return 0;

            int removed = 0;

            foreach (var loreId in new List<string>(info.AcquiredLores ?? new List<string>()))
            {
                info.RemoveLore(loreId);
                removed++;
            }
            foreach (var spellId in new List<string>(info.AcquiredSpells ?? new List<string>()))
            {
                info.RemoveSpell(spellId);
                info.RemoveSelectedAbility(spellId);
            }
            foreach (var abilityId in new List<string>(info.AcquiredAbilities ?? new List<string>()))
            {
                info.AcquiredAbilities.Remove(abilityId);
                info.RemoveSelectedAbility(abilityId);
            }

            try
            {
                var skill = SotorSkills.Spellcraft;
                if (skill != null && hero.HeroDeveloper != null && hero.GetSkillValue(skill) > 0)
                {
                    hero.HeroDeveloper.SetInitialSkillLevel(skill, 0);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"StripHero: could not reset Spellcraft for {hero.Name}: {ex.Message}");
            }

            hero.RemoveAttribute("AbilityUser");
            hero.RemoveAttribute("SpellCaster");

            return removed;
        }

        public static Result Run()
        {
            var result = new Result();
            if (Campaign.Current == null) return result;

            var tierPerks = new[]
            {
                SotorPerks.EntrySpells, SotorPerks.AdeptSpells, SotorPerks.MasterSpells, SotorPerks.Archmage,
            };

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (hero == null || hero == Hero.MainHero) continue;
                if (ExtendedInfoManager.IsPlayerSideCaster(hero)) continue;

                if (SotorPlayerInvestment.WasInvestedIn(hero)) continue;

                var info = hero.GetExtendedInfo();
                if (info == null) continue;

                bool touched = false;

                foreach (var loreId in new List<string>(info.AcquiredLores ?? new List<string>()))
                {
                    info.RemoveLore(loreId);
                    result.Lores++;
                    touched = true;
                }
                foreach (var spellId in new List<string>(info.AcquiredSpells ?? new List<string>()))
                {
                    info.RemoveSpell(spellId);
                    info.RemoveSelectedAbility(spellId);
                    result.Spells++;
                    touched = true;
                }
                foreach (var abilityId in new List<string>(info.AcquiredAbilities ?? new List<string>()))
                {
                    info.AcquiredAbilities.Remove(abilityId);
                    info.RemoveSelectedAbility(abilityId);
                    touched = true;
                }

                try
                {
                    var skill = SotorSkills.Spellcraft;
                    if (skill != null && hero.HeroDeveloper != null && hero.GetSkillValue(skill) > 0)
                    {
                        hero.HeroDeveloper.SetInitialSkillLevel(skill, 0);
                        touched = true;
                    }
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"Reset: could not clear Spellcraft on {hero.Name}: {ex.Message}");
                }

                var setPerk = SetPerkValueMethod();
                if (setPerk != null)
                {
                    foreach (var perk in tierPerks)
                    {
                        if (perk == null || !hero.GetPerkValue(perk)) continue;
                        try
                        {
                            setPerk.Invoke(hero, new object[] { perk, false });
                            result.Perks++;
                            touched = true;
                        }
                        catch
                        {

                        }
                    }
                }

                if (touched)
                {
                    hero.RemoveAttribute("AbilityUser");
                    hero.RemoveAttribute("SpellCaster");
                    result.Heroes++;
                }
            }

            result.StandingEntries = SotorRivalStanding.ClearAll();
            SotorRivalReveal.ClearAll();
            SotorCoercionRecord.ClearAll();

            SotorLog.Info($"RivalReset: stripped {result.Heroes} hero(es), {result.Lores} lore(s), "
                          + $"{result.Spells} spell(s), {result.Perks} perk(s), {result.StandingEntries} standing entr(ies).");
            return result;
        }
    }
}
