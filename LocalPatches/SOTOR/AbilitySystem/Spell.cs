using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class Spell : Ability
    {
        public Spell(AbilityTemplate template)
            : base(template)
        {
        }

        private static bool TryGetWindsHero(Agent casterAgent, out Hero hero)
        {
            hero = null;
            if (!(Game.Current?.GameType is Campaign))
            {
                return false;
            }
            hero = casterAgent?.GetHero();
            return hero != null && hero.GetExtendedInfo() != null;
        }

        public override bool IsDisabled(Agent casterAgent, out TextObject disabledReason)
        {

            if (base.IsDisabled(casterAgent, out disabledReason))
            {
                return true;
            }

            if (StringID == SotorArcaneConduitHelper.AbilityId)
            {

                var acHero = casterAgent?.GetHero();
                if (acHero != null)
                {
                    int max = SotorArcaneConduitHelper.GetUsesPerBattle(acHero);
                    if (SotorArcaneConduitMissionLogic.GetUses(casterAgent) >= max)
                    {
                        disabledReason = new TextObject("{=sotor_ac_no_uses}No Arcane Conduit uses left this battle");
                        return true;
                    }
                }
            }

            if (SOTOR.SotorSettings.DisableMagicInSieges
                && Mission.Current != null && Mission.Current.IsSiegeBattle)
            {
                disabledReason = new TextObject("{=sotor_spell_no_siege_magic}Magic is disabled during sieges");
                return true;
            }

            if (TryGetWindsHero(casterAgent, out var hero))
            {
                int cost = hero.GetEffectiveWindsCostForSpell(Template);
                if (hero.GetWindsOfMagic() < cost)
                {
                    disabledReason = new TextObject("{=sotor_spell_not_enough_wom}Not enough Winds of Magic");
                    return true;
                }
            }

            else if (Missions.SotorApprenticeWinds.IsTracked(casterAgent))
            {
                if (Missions.SotorApprenticeWinds.Get(casterAgent) < Template.WindsOfMagicCost)
                {
                    Missions.SotorApprenticeWinds.NoteCannotAfford(casterAgent, StringID, Template.WindsOfMagicCost);
                    disabledReason = new TextObject("{=sotor_spell_not_enough_wom}Not enough Winds of Magic");
                    return true;
                }
            }

            return false;
        }

        protected override void OnCastSucceeded(Agent casterAgent)
        {
            if (TryGetWindsHero(casterAgent, out var hero))
            {
                int cost = hero.GetEffectiveWindsCostForSpell(Template);
                if (cost > 0)
                {
                    float before = hero.GetWindsOfMagic();
                    hero.AddWindsOfMagic(-cost);
                    SotorLog.Info(
                        $"Winds spent: {StringID} cost {cost} | {before:0} -> {hero.GetWindsOfMagic():0} / {hero.GetMaxWindsOfMagic():0}.");
                }

                GrantSpellcraftXp(hero);
            }
            else if (Missions.SotorApprenticeWinds.IsTracked(casterAgent))
            {

                Missions.SotorApprenticeWinds.Spend(casterAgent, Template.WindsOfMagicCost);
            }

            if (StringID == SotorArcaneConduitHelper.AbilityId)
            {
                SotorArcaneConduitMissionLogic.RegisterUse(casterAgent);
                var acHero = casterAgent?.GetHero();
                if (acHero != null)
                {
                    int cd = SotorArcaneConduitHelper.GetCooldown(acHero);
                    SetCoolDown(cd);

                    if (SotorArcaneConduitHelper.ResetsOtherCooldowns(acHero))
                    {
                        var component = casterAgent.GetComponent<AbilityComponent>();
                        if (component != null)
                        {
                            foreach (var other in component.KnownAbilitySystem)
                            {
                                if (other != null && other != this)
                                {
                                    other.SetCoolDown(0);
                                }
                            }
                        }
                    }
                }
            }
        }

        protected void GrantSpellcraftXp(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            if (skill == null)
            {
                return;
            }

            if (StringID == SotorArcaneConduitHelper.AbilityId)
            {
                return;
            }

            int xp = System.Math.Max(1, Template.WindsOfMagicCost) * 20;

            bool librarian = SotorPerks.Librarian != null && hero.GetPerkValue(SotorPerks.Librarian);
            if (librarian)
            {
                xp = (int)(xp * 1.25f);
            }

            hero.AddSkillXp(skill, xp);
            SotorLog.Info($"Spellcraft XP: {StringID} +{xp} (librarian={librarian}) -> skill now {hero.GetSkillValue(skill)}.");
        }
    }
}
