using System.Collections.Generic;
using SOTOR.AbilitySystem;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Extensions
{
    public static class AgentExtensions
    {
        public static bool IsAbilityUser(this Agent agent)
        {
            return agent.GetAttributes().Contains("AbilityUser");
        }

        public static bool IsSpellCaster(this Agent agent)
        {
            return agent.GetAttributes().Contains("SpellCaster");
        }

        public static Ability GetCurrentAbility(this Agent agent)
        {
            return agent.GetComponent<AbilityComponent>()?.CurrentAbility;
        }

        public static Ability GetAbility(this Agent agent, int index)
        {
            var known = agent.GetComponent<AbilityComponent>()?.KnownAbilitySystem;
            if (known == null || index < 0 || index >= known.Count)
            {
                return null;
            }
            return known[index];
        }

        public static void SelectAbility(this Agent agent, int abilityIndex)
        {
            agent.GetComponent<AbilityComponent>()?.SelectAbility(abilityIndex);
        }

        public static void SelectAbility(this Agent agent, Ability ability)
        {
            agent.GetComponent<AbilityComponent>()?.SelectAbility(ability);
        }

        public static bool TryCastCurrentAbility(this Agent agent, out TextObject failureReason)
        {
            var component = agent.GetComponent<AbilityComponent>();
            if (component?.CurrentAbility != null)
            {
                return component.CurrentAbility.TryCast(agent, out failureReason);
            }

            failureReason = new TextObject("{=sotor_cast_fail_no_ability}No ability selected.");
            return false;
        }

        public static Hero GetHero(this Agent agent)
        {
            if (agent?.Character == null || !(Game.Current?.GameType is Campaign))
            {
                return null;
            }

            if (agent.Character is CharacterObject character && character.IsHero)
            {
                return character.HeroObject;
            }

            return null;
        }

        public static List<string> GetSelectedAbilities(this Agent agent)
        {
            var hero = agent?.GetHero();
            if (hero != null)
            {
                var info = hero.GetExtendedInfo();
                if (info != null)
                {
                    var list = new List<string>(info.SelectedAbilities);

                    if (!SOTOR.SotorSettings.EnableArcaneConduit)
                    {
                        list.RemoveAll(id => id == SOTOR.AbilitySystem.SotorArcaneConduitHelper.AbilityId);
                    }
                    return list;
                }
            }

            return new List<string>();
        }

        public static List<string> GetAttributes(this Agent agent)
        {
            var list = new List<string>();
            var hero = agent?.GetHero();
            if (hero != null)
            {
                var info = hero.GetExtendedInfo();
                if (info != null)
                {
                    foreach (var attribute in info.AllAttributes)
                    {
                        if (!list.Contains(attribute))
                        {
                            list.Add(attribute);
                        }
                    }
                }
            }
            else if (agent?.Character != null)
            {
                foreach (var attribute in agent.Character.GetAttributes())
                {
                    if (!list.Contains(attribute))
                    {
                        list.Add(attribute);
                    }
                }
            }

            return list;
        }
    }
}
