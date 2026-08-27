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

                { typeof(ArcaneConduitCastingBehavior), _ => new List<Axis>() },
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
                        list.Add(new ArcaneConduitCastingBehavior(agent, template, index));
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

        public const float ExtremeAllyShare = 0.8f;

        private const float AllyDensityFloor = 0.15f;
        private const float AllyDensityBite = 1.0f;

        private static bool IsHexLike(AbstractAgentCastingBehavior behavior)
        {
            var t = behavior?.AbilityTemplate;
            if (t == null) return false;
            return t.AbilityEffectType == AbilityEffectType.Hex;
        }

        private static bool CanHarmAllies(AbstractAgentCastingBehavior behavior)
        {
            try
            {
                var t = behavior?.AbilityTemplate;
                if (t == null || string.IsNullOrEmpty(t.TriggeredEffectID)) return false;
                var eff = TriggeredEffectManager.GetTemplate(t.TriggeredEffectID);
                if (eff == null) return false;
                return eff.TargetType != TargetType.Enemy && eff.DamageAmount > 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<Axis> CreateHexAxisList(AbstractAgentCastingBehavior behavior)
        {
            var axes = new List<Axis>
            {
                new Axis(0f, 120f, x => 1f - x, CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)) { Name = "dist" },
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()) { Name = "power" },
                new Axis(0f, 1f, x => x + 0.3f, CommonAIDecisionFunctions.RangedUnitRatio()) { Name = "ranged" }
            };

            if (CanHarmAllies(behavior))
            {
                axes.Add(new Axis(0f, 1f, x => x > ExtremeAllyShare ? 0f : Math.Max(AllyDensityFloor, 1f - AllyDensityBite * x),
                    CommonAIDecisionFunctions.AllyDensityInBlast(behavior.Agent, BlastRadiusOf(behavior))) { Name = "allyDens" });
            }

            if (BlastDamageOf(behavior) > 0f)
            {
                axes.Add(SpellWorthAxis(behavior));
                axes.Add(BlastOccupancyAxis(behavior));
            }

            axes.Add(TargetMobilityAxis());

            return axes;
        }

        private static bool IsMindControlSpell(AbstractAgentCastingBehavior behavior)
        {
            return behavior?.AbilityTemplate?.AbilityEffectType == AbilityEffectType.MindControl;
        }

        private static List<Axis> CreateMindControlAxisList(AbstractAgentCastingBehavior behavior)
        {
            float radius = behavior.AbilityTemplate.Radius > 0f ? behavior.AbilityTemplate.Radius : 5f;
            return new List<Axis>
            {
                new Axis(0f, 120f, x => 1f - x, CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)) { Name = "dist" },
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()) { Name = "power" },
                new Axis(0f, 1f, x => x + 0.3f, CommonAIDecisionFunctions.RangedUnitRatio()) { Name = "ranged" },
                new Axis(0f, 1f, x => Math.Max(OccupancyFloor, x),
                    CommonAIDecisionFunctions.BlastOccupancy(behavior.Agent, radius, 0f)) { Name = "occupancy" },
                TargetMobilityAxis()
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateAoETargetedOffensiveSpellAxis()
        {
            return behavior => IsMindControlSpell(behavior)
                ? CreateMindControlAxisList(behavior)
                : new List<Axis>
            {
                new Axis(0f, 120f, x => 1f - x, CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)) { Name = "dist" },
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()) { Name = "power" },
                new Axis(0f, 1f, x => x + 0.3f, CommonAIDecisionFunctions.RangedUnitRatio()) { Name = "ranged" },

                new Axis(0f, 1f, x => x > ExtremeAllyShare ? 0f : Math.Max(AllyDensityFloor, 1f - AllyDensityBite * x),
                    CommonAIDecisionFunctions.AllyDensityInBlast(behavior.Agent, BlastRadiusOf(behavior))) { Name = "allyDens" },

                SpellWorthAxis(behavior),

                BlastOccupancyAxis(behavior),

                TargetMobilityAxis()
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateBuffSpellAxis()
        {
            return behavior => IsHexLike(behavior)
                ? CreateHexAxisList(behavior)
                : new List<Axis>
            {
                new Axis(0f, 50f, x => ScoringFunctions.Logistic(0.4f, 1f, 20f)(1f - x), CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)) { Name = "dist" },
                new Axis(0f, 20f, x => 1f - x, CommonAIDecisionFunctions.TargetDistanceToHostiles()),
                new Axis(0f, CommonAIDecisionFunctions.CalculateTeamTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()) { Name = "power" },
                new Axis(1f, 2.5f, x => 1f - x, CommonAIDecisionFunctions.Dispersedness())
            };
        }

        public static Func<AbstractAgentCastingBehavior, List<Axis>> CreateAoEDirectionalSpellAxis()
        {
            return behavior => new List<Axis>
            {
                new Axis(0f, 50f, x => ScoringFunctions.Logistic(0.4f, 1f, 20f)(1f - x), CommonAIDecisionFunctions.DistanceToTarget(() => behavior.Agent.Position)) { Name = "dist" },
                new Axis(0f, 15f, x => 1f - x, CommonAIDecisionFunctions.TargetDistanceToHostiles()),
                new Axis(0f, CommonAIDecisionFunctions.CalculateEnemyTotalPower(behavior.Agent.Team), x => x, CommonAIDecisionFunctions.FormationPower()) { Name = "power" },
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

        public static float BlastDamageOf(AbstractAgentCastingBehavior behavior)
        {
            try
            {
                var t = behavior?.AbilityTemplate;
                if (t == null || string.IsNullOrEmpty(t.TriggeredEffectID)) return 0f;
                var eff = TriggeredEffectManager.GetTemplate(t.TriggeredEffectID);
                return eff?.DamageAmount ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }

        private const float MobilityMax = 10f;
        private const float MobilityFloor = 0.6f;
        private const float MobilityBite = 0.4f;

        private static Axis TargetMobilityAxis()
        {
            return new Axis(0f, MobilityMax,
                x => Math.Max(MobilityFloor, 1f - MobilityBite * x),
                CommonAIDecisionFunctions.TargetMobility()) { Name = "mobility" };
        }

        private const float SpellWorthMax = 250f;
        private const float SpellWorthFloor = 0.2f;

        private const float OccupancyFloor = 0.25f;

        private static Axis BlastOccupancyAxis(AbstractAgentCastingBehavior behavior)
        {
            var t = behavior?.AbilityTemplate;
            bool lingers = t != null && t.TriggerType == TriggerType.EveryTick && t.TickInterval > 0f;
            return new Axis(0f, 1f, x => Math.Max(OccupancyFloor, x),
                CommonAIDecisionFunctions.BlastOccupancy(
                    behavior.Agent, BlastRadiusOf(behavior), lingers ? t.Duration : 0f)) { Name = "occupancy" };
        }

        private static Axis SpellWorthAxis(AbstractAgentCastingBehavior behavior)
        {
            var t = behavior?.AbilityTemplate;

            bool lingers = t != null && t.TriggerType == TriggerType.EveryTick && t.TickInterval > 0f;

            return new Axis(0f, SpellWorthMax,
                x => Math.Max(SpellWorthFloor, (float)Math.Sqrt(x)),
                CommonAIDecisionFunctions.SpellWorth(
                    behavior.Agent, BlastRadiusOf(behavior), BlastDamageOf(behavior),
                    t?.WindsOfMagicCost ?? 1f,
                    lingers ? t.Duration : 0f,
                    lingers ? t.TickInterval : 0f)) { Name = "worth" };
        }

        public static float BlastRadiusOf(AbstractAgentCastingBehavior behavior)
        {
            try
            {
                var t = behavior?.AbilityTemplate;
                if (t == null || string.IsNullOrEmpty(t.TriggeredEffectID)) return 0f;
                var eff = TriggeredEffectManager.GetTemplate(t.TriggeredEffectID);
                return eff?.Radius ?? 0f;
            }
            catch
            {
                return 0f;
            }
        }
    }
}
