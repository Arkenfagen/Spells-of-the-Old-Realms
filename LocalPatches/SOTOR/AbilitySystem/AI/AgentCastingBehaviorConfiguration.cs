using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.Extensions;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public static class AgentCastingBehaviorConfiguration
    {
        public static readonly Dictionary<AbilityEffectType, Func<Agent, int, AbilityTemplate, AbstractAgentCastingBehavior>> BehaviorByType =
            new Dictionary<AbilityEffectType, Func<Agent, int, AbilityTemplate, AbstractAgentCastingBehavior>>
            {
                { AbilityEffectType.Blast, (a, i, t) => new AoETargetedCastingBehavior(a, t, i) },
                { AbilityEffectType.Bombardment, (a, i, t) => new AoETargetedCastingBehavior(a, t, i) },
                { AbilityEffectType.Vortex, (a, i, t) => new AoETargetedCastingBehavior(a, t, i) },
                {
                    AbilityEffectType.Heal, (a, i, t) =>
                    {
                        if (t.AbilityTargetType == AbilityTargetType.AlliesInAOE)
                        {
                            return new SelectMultiTargetCastingBehavior(a, t, i);
                        }
                        return (t.AbilityTargetType == AbilityTargetType.Self || t.AbilityTargetType == AbilityTargetType.SingleAlly)
                            ? (AoETargetedCastingBehavior)new SelectSingleTargetCastingBehavior(a, t, i)
                            : new SelectMultiTargetCastingBehavior(a, t, i);
                    }
                },
                {
                    AbilityEffectType.Hex, (a, i, t) =>
                    {
                        if (t.AbilityTargetType == AbilityTargetType.EnemiesInAOE)
                        {
                            return new SelectMultiTargetCastingBehavior(a, t, i);
                        }
                        return t.AbilityTargetType == AbilityTargetType.SingleEnemy
                            ? (AoETargetedCastingBehavior)new SelectSingleTargetCastingBehavior(a, t, i)
                            : new AoETargetedCastingBehavior(a, t, i);
                    }
                },
                {
                    AbilityEffectType.Augment, (a, i, t) =>
                    {
                        if (t.AbilityTargetType == AbilityTargetType.AlliesInAOE)
                        {
                            return new SelectMultiTargetCastingBehavior(a, t, i);
                        }
                        return (t.AbilityTargetType == AbilityTargetType.Self || t.AbilityTargetType == AbilityTargetType.SingleAlly)
                            ? (AoETargetedCastingBehavior)new SelectSingleTargetCastingBehavior(a, t, i)
                            : new SelectMultiTargetCastingBehavior(a, t, i);
                    }
                },
                { AbilityEffectType.Missile, (a, i, t) => new MissileCastingBehavior(a, t, i) },
                { AbilityEffectType.SeekerMissile, (a, i, t) => new MissileCastingBehavior(a, t, i) },
                { AbilityEffectType.Summoning, (a, i, t) => new SummoningCastingBehavior(a, t, i) },
                { AbilityEffectType.Wind, (a, i, t) => new AoEDirectionalCastingBehavior(a, t, i) },

                { AbilityEffectType.MindControl, (a, i, t) => new AoETargetedCastingBehavior(a, t, i) },
            };

        public static readonly Dictionary<Type, Func<AbstractAgentCastingBehavior, List<Axis>>> UtilityByType =
            new Dictionary<Type, Func<AbstractAgentCastingBehavior, List<Axis>>>
            {
                { typeof(PreserveWindsAgentCastingBehavior), CreatePreserveWindsAxis() },
                { typeof(MissileCastingBehavior), CreateAoETargetedOffensiveSpellAxis() },
                { typeof(AoETargetedCastingBehavior), CreateAoETargetedOffensiveSpellAxis() },
                { typeof(AoEDirectionalCastingBehavior), CreateAoEDirectionalSpellAxis() },
                { typeof(SelectMultiTargetCastingBehavior), CreateBuffSpellAxis() },
                { typeof(SelectSingleTargetCastingBehavior), CreateBuffSpellAxis() },
                { typeof(SummoningCastingBehavior), CreateSummoningAxis() },
            };

        public static List<Target> FindTargets(Agent agent, AbilityTemplate abilityTemplate)
        {
            bool friendly = abilityTemplate.AbilityTargetType == AbilityTargetType.AlliesInAOE
                || abilityTemplate.AbilityEffectType == AbilityEffectType.Heal
                || abilityTemplate.AbilityTargetType == AbilityTargetType.SingleAlly;

            if (friendly)
            {
                return agent.Team.GetAllyTeams()
                    .SelectMany(team => team.GetFormations())
                    .Where(IsValidFormationTarget)
                    .Select(form => new Target { Formation = form })
                    .ToList();
            }

            if (abilityTemplate.AbilityEffectType == AbilityEffectType.Summoning
                || abilityTemplate.AbilityTargetType == AbilityTargetType.Self)
            {
                return new List<Target> { new Target { Formation = agent.Formation, Agent = agent } };
            }

            try
            {
                return agent.Team.GetEnemyTeams()
                    .SelectMany(team => team.GetFormations())
                    .Where(IsValidFormationTarget)
                    .Select(form => new Target { Formation = form })
                    .ToList();
            }
            catch (Exception)
            {
                return new List<Target>();
            }
        }

        public static List<AbstractAgentCastingBehavior> PrepareCastingBehaviors(Agent agent)
        {
            var list = new List<AbstractAgentCastingBehavior>();
            int index = 0;
            var component = agent.GetComponent<AbilityComponent>();
            if (component != null)
            {
                foreach (var template in component.GetKnownAbilityTemplates())
                {

                    if (template != null && template.StringID == SotorArcaneConduitHelper.AbilityId)
                    {
                        index++;
                        continue;
                    }

                    var factory = BehaviorByType.TryGetValue(template.AbilityEffectType, out var f)
                        ? f
                        : BehaviorByType[AbilityEffectType.Missile];
                    list.Add(factory(agent, index, template));
                    index++;
                }
            }
            list.Add(new PreserveWindsAgentCastingBehavior(agent, new AbilityTemplate { AbilityTargetType = AbilityTargetType.Self }, index));
            return list;
        }

        private static Func<AbstractAgentCastingBehavior, List<Axis>> CreateSummoningAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 1f, x => 1f - x, CommonAIDecisionFunctions.BalanceOfPower(behavior.Agent))
            };
        }

        private static Func<AbstractAgentCastingBehavior, List<Axis>> CreatePreserveWindsAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 1f, x => (float)Math.Min(0.4, 1f - x), CommonAIDecisionFunctions.WindsOfMagicRemainingRatio(behavior.Agent))
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateAoETargetedOffensiveSpellAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 120f, x => 1f - x, CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)),
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team) / 4f, x => x, CommonAIDecisionFunctions.FormationPower()),
                new Axis(0f, 1f, x => x + 0.3f, CommonAIDecisionFunctions.RangedUnitRatio())
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateBuffSpellAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 50f, x => ScoringFunctions.Logistic(0.4f, 1f, 20f)(1f - x), CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)),
                new Axis(0f, 20f, x => 1f - x, CommonAIDecisionFunctions.TargetDistanceToHostiles()),
                new Axis(0f, CommonAIDecisionFunctions.CalculateTeamTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()),
                new Axis(1f, 2.5f, x => 1f - x, CommonAIDecisionFunctions.Dispersedness())
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateAoEDirectionalSpellAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 50f, x => ScoringFunctions.Logistic(0.4f, 1f, 20f)(1f - x), CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)),
                new Axis(0f, 15f, x => 1f - x, CommonAIDecisionFunctions.TargetDistanceToHostiles()),
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()),
                new Axis(1f, 2.5f, x => 1f - x, CommonAIDecisionFunctions.Dispersedness()),
                new Axis(0f, 1f, x => 1f - x, CommonAIDecisionFunctions.CavalryUnitRatio())
            };
        }

        private static bool IsValidFormationTarget(Formation formation)
        {
            if (formation == null)
            {
                return false;
            }
            try
            {
                if (formation.QuerySystem == null)
                {
                    return false;
                }
                if (formation.CountOfUnits > 0)
                {
                    return true;
                }
                return formation.GetMedianAgent(false, false, formation.CurrentPosition) != null;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }
    }
}
