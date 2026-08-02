using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SOTOR.Extensions;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
#if BL13
using SotorSpawnLogic = TaleWorlds.MountAndBlade.MissionAgentSpawnLogic;
#else
using SotorSpawnLogic = TaleWorlds.MountAndBlade.DefaultBattleMissionAgentSpawnLogic;
#endif

namespace SOTOR.AbilitySystem.AI
{

    public abstract class AbstractAgentCastingBehavior : IAgentBehavior
    {
        private WizardAIComponent _component;

        public Agent Agent;
        protected float Hysteresis = 0.2f;
        public readonly AbilityTemplate AbilityTemplate;
        protected readonly int AbilityIndex;
        private readonly List<Axis> _axisList;

        public Target CurrentTarget = new Target();

        public List<BehaviorOption> LatestScores { get; private set; }
        public AbstractAgentTacticalBehavior TacticalBehavior { get; protected set; }
        public WizardAIComponent Component => _component ?? (_component = Agent.GetComponent<WizardAIComponent>());

        protected AbstractAgentCastingBehavior(Agent agent, AbilityTemplate abilityTemplate, int abilityIndex)
        {
            Agent = agent;
            AbilityIndex = abilityIndex;
            AbilityTemplate = abilityTemplate;
            _axisList = AgentCastingBehaviorConfiguration.UtilityByType[GetType()](this);
            TacticalBehavior = new KeepSafeAgentTacticalBehavior(Agent, Agent.GetComponent<WizardAIComponent>());
        }

        public virtual void Execute()
        {
            if (Agent.GetAbility(AbilityIndex)?.IsOnCooldown() != false)
            {
                return;
            }

            var spawnLogic = Mission.Current.GetMissionBehavior<SotorSpawnLogic>();
            if (spawnLogic != null)
            {
                var spawning = Traverse.Create(spawnLogic).Field("_spawningReinforcements").GetValue() as bool?;
                if (spawning == true)
                {
                    return;
                }
            }

            CurrentTarget = UpdateTarget(CurrentTarget);
            if (HaveLineOfSightToTarget(CurrentTarget))
            {
                Agent.SelectAbility(AbilityIndex);
                CastSpellAtCurrentTarget();
            }
        }

        public virtual void Terminate()
        {
        }

        protected virtual Target UpdateTarget(Target target)
        {
            return target;
        }

        protected virtual bool HaveLineOfSightToTarget(Target target)
        {
            return IsTargetWithinAbilityRange(target);
        }

        protected bool IsTargetWithinAbilityRange(Target target)
        {
            var pos = target.GetPositionPrioritizeCalculated();
            if (pos == Vec3.Invalid)
            {
                return false;
            }
            return Agent.Position.Distance(pos) <= AbilityTemplate.MaxDistance;
        }

        protected virtual void CastSpellAtCurrentTarget()
        {
            Agent.TryCastCurrentAbility(out _);
        }

        protected Vec3 ComputeSpellAngleVelocityCorrection(Vec3 targetPosition, Vec3 targetVelocity)
        {
            var type = AbilityTemplate.AbilityEffectType;
            bool usesCastTime = type == AbilityEffectType.Vortex || type == AbilityEffectType.Heal
                || type == AbilityEffectType.Augment || type == AbilityEffectType.Hex
                || type == AbilityEffectType.Bombardment;
            float t;
            if (usesCastTime)
            {
                t = AbilityTemplate.CastTime;
            }
            else
            {
                t = AbilityTemplate.BaseMovementSpeed != 0f
                    ? targetPosition.Distance(Agent.Position) / AbilityTemplate.BaseMovementSpeed
                    : AbilityTemplate.CastTime;
            }
            return targetVelocity * t;
        }

        public virtual List<BehaviorOption> CalculateUtility()
        {
            LatestScores = AgentCastingBehaviorConfiguration.FindTargets(Agent, AbilityTemplate)
                .Select(target =>
                {
                    target.UtilityValue = CalculateUtility(target);
                    return new BehaviorOption
                    {
                        Target = target,
                        Behavior = this,
                        UtilityValue = target.UtilityValue
                    };
                }).ToList();
            return LatestScores;
        }

        protected virtual float CalculateUtility(Target target)
        {
            var ability = Agent.GetAbility(AbilityIndex);
            if (ability == null || ability.IsOnCooldown() || !ability.CanCast(Agent, out _)
                || (target.Formation == null && target.TacticalPosition == null))
            {
                return 0f;
            }
            float hysteresis = (Component != null && Component.CurrentCastingBehavior == this
                && target.Formation == CurrentTarget.Formation) ? Hysteresis : 0f;
            return _axisList.GeometricMean(target) + hysteresis;
        }

        public void SetCurrentTarget(Target target)
        {
            CurrentTarget = target;
        }
    }

    public class MissileCastingBehavior : AbstractAgentCastingBehavior
    {
        public MissileCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.1f;
        }

        protected override Target UpdateTarget(Target target)
        {
            var formation = CurrentTarget.Formation;
            if (formation == null || formation.CountOfUnitsWithoutDetachedOnes < 1)
            {
                return target;
            }
            if (formation.CountOfUnitsWithoutDetachedOnes > 10)
            {
                var randomAgent = CommonAIFunctions.GetRandomAgent(formation);
                if (randomAgent != null)
                {
                    target.Agent = randomAgent;
                    var pos = randomAgent.Position + ComputeSpellAngleVelocityCorrection(randomAgent.Position, randomAgent.Velocity);
                    target.SelectedWorldPosition = pos;
                }
            }
            else
            {
                var median = formation.GetMedianAgent(true, false, formation.GetAveragePositionOfUnits(true, false));
                if (median != null)
                {
                    target.Agent = median;
                }
            }
            return target;
        }

        protected override bool HaveLineOfSightToTarget(Target target)
        {
            var pos = target.GetPositionPrioritizeCalculated();
            if (pos == Vec3.Invalid)
            {
                return false;
            }
            pos.z += 0.75f;
            float dist = Agent.Position.Distance(pos);
            if (dist < AbilityTemplate.MinDistance || dist > AbilityTemplate.MaxDistance)
            {
                return false;
            }

            var from = Agent.Position + new Vec3(0f, 0f, Agent.GetEyeGlobalHeight(), -1f);
            Agent hitAgent;
            float terrainDist = 0f;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                hitAgent = Mission.Current.RayCastForClosestAgent(from, pos, Agent.Index, 0.25f, out _);
                Mission.Current.Scene.RayCastForClosestEntityOrTerrain(from, pos, out terrainDist, out _, out _, 0.25f, (BodyFlags)79617);
            }

            if (Agent.GetChestGlobalPosition().Distance(pos) > 1f && (float.IsNaN(terrainDist) || terrainDist > 1f))
            {
                if (hitAgent != null && !hitAgent.IsEnemyOf(Agent) && hitAgent.GetChestGlobalPosition().Distance(pos) < 4f)
                {
                    return false;
                }
            }

            return float.IsNaN(terrainDist) || Math.Abs(terrainDist - pos.Distance(from)) < 0.3f;
        }
    }

    public class AoETargetedCastingBehavior : MissileCastingBehavior
    {
        public AoETargetedCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
        }

        protected override bool HaveLineOfSightToTarget(Target target)
        {
            var type = AbilityTemplate.AbilityEffectType;
            bool groundProjected = type == AbilityEffectType.Vortex || type == AbilityEffectType.Heal
                || type == AbilityEffectType.Augment || type == AbilityEffectType.Hex
                || type == AbilityEffectType.Bombardment;
            if (groundProjected)
            {
                var pos = target.GetPositionPrioritizeCalculated();
                if (pos == Vec3.Invalid)
                {
                    return false;
                }
                float dist = Agent.Position.Distance(pos);
                return dist >= AbilityTemplate.MinDistance && dist <= AbilityTemplate.MaxDistance;
            }
            return base.HaveLineOfSightToTarget(target);
        }
    }

    public class SelectSingleTargetCastingBehavior : AoETargetedCastingBehavior
    {
        public SelectSingleTargetCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.1f;
        }

        protected override Target UpdateTarget(Target target)
        {
            if (AbilityTemplate.AbilityTargetType == AbilityTargetType.Self)
            {
                target.Agent = Agent;
                return target;
            }
            return base.UpdateTarget(target);
        }
    }

    public class SelectMultiTargetCastingBehavior : AoETargetedCastingBehavior
    {
        public SelectMultiTargetCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.1f;
        }
    }

    public class AoEDirectionalCastingBehavior : AbstractAgentCastingBehavior
    {
        public AoEDirectionalCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.35f;
            TacticalBehavior = new DirectionalAoETacticalBehavior(agent, agent.GetComponent<WizardAIComponent>(), this);
        }

        public override void Execute()
        {
            var castPos = (TacticalBehavior as DirectionalAoETacticalBehavior)?.CastingPosition;
            if (castPos.HasValue && Agent.Position.AsVec2.Distance(castPos.Value.AsVec2) > 6f)
            {
                return;
            }
            base.Execute();
        }

        protected override float CalculateUtility(Target target)
        {
            if (!CommonAIStateFunctions.CanAgentMoveFreely(Agent) || Agent.GetAbility(AbilityIndex)?.IsOnCooldown() != false)
            {
                return 0f;
            }
            return base.CalculateUtility(target);
        }
    }

    public class SummoningCastingBehavior : AbstractAgentCastingBehavior
    {
        public SummoningCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.1f;
        }

        protected override Target UpdateTarget(Target target)
        {
            target.SelectedWorldPosition = Agent.Position + Agent.LookDirection * 2f;
            return target;
        }
    }

    public class PreserveWindsAgentCastingBehavior : AbstractAgentCastingBehavior
    {
        public PreserveWindsAgentCastingBehavior(Agent agent, AbilityTemplate abilityTemplate, int abilityIndex)
            : base(agent, abilityTemplate, abilityIndex)
        {
        }

        public override void Execute()
        {
        }

        protected override float CalculateUtility(Target target)
        {
            if (target.Formation == null && target.TacticalPosition == null)
            {
                return 0f;
            }
            return _axisListForPreserve.GeometricMean(target);
        }

        private List<Axis> _axisListForPreserve => AgentCastingBehaviorConfiguration.UtilityByType[typeof(PreserveWindsAgentCastingBehavior)](this);
    }
}
