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

        public List<BehaviorOption> LatestScores { get; protected set; }

        public IEnumerable<Axis> Axes => _axisList;
        public AbstractAgentTacticalBehavior TacticalBehavior { get; protected set; }
        public WizardAIComponent Component => _component ?? (_component = Agent.GetComponent<WizardAIComponent>());

        protected AbstractAgentCastingBehavior(Agent agent, AbilityTemplate abilityTemplate, int abilityIndex)
        {
            Agent = agent;
            AbilityIndex = abilityIndex;
            AbilityTemplate = abilityTemplate;
            _axisList = AgentCastingBehaviorConfiguration.UtilityByType[GetType()](this);
            TacticalBehavior = new KeepSafeAgentTacticalBehavior(Agent, Agent.GetComponent<WizardAIComponent>(), this);
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

        public bool CanDeliverTo(Target target)
        {
            if (target == null) return false;
            try
            {
                return HaveLineOfSightToTarget(target);
            }
            catch
            {

                return false;
            }
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

        public string DescribeZeroScore(Target target)
        {
            var ability = Agent.GetAbility(AbilityIndex);
            if (ability == null) return $"no ability at index {AbilityIndex}";
            if (ability.IsOnCooldown()) return $"ON COOLDOWN {ability.GetCoolDownLeft()}s";
            if (!ability.CanCast(Agent, out var reason)) return $"cannot cast: {reason}";
            if (target != null && target.Formation == null && target.TacticalPosition == null)
            {
                return "no formation and no tactical position";
            }
            return "axes scored it zero";
        }
    }

    public class MissileCastingBehavior : AbstractAgentCastingBehavior
    {

        private const int PathSamples = 4;

        private const float MinPathStep = 3f;

        private const float FriendlyFireHoldSeconds = 1f;
        private float _friendlyFireHoldUntil;

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

                var linger = (AbilityTemplate.TriggerType == TriggerType.EveryTick && AbilityTemplate.TickInterval > 0f)
                    ? AbilityTemplate.Duration : 0f;
                var spot = CommonAIFunctions.FindBestBlastSpot(
                    Agent, formation, AgentCastingBehaviorConfiguration.BlastRadiusOf(this), linger);
                if (spot.Agent != null)
                {
                    target.Agent = spot.Agent;
                    var lead = ComputeSpellAngleVelocityCorrection(spot.Position, spot.Agent.Velocity);
                    var pos = spot.Position + lead;
                    target.SelectedWorldPosition = pos;

                    SotorAimDiagnostics.LogTargetPick(Agent, AbilityTemplate, "best-of-6",
                        formation, spot.Agent, spot.Position, lead, pos,
                        $"ep={spot.EnemyPower:0} n={spot.EnemyCount} ally={spot.AllyPower:0}");
                }
            }
            else
            {
                var median = formation.GetMedianAgent(true, false, formation.GetAveragePositionOfUnits(true, false));
                if (median != null)
                {
                    target.Agent = median;
                    SotorAimDiagnostics.LogTargetPickMedian(Agent, AbilityTemplate, formation, median);
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
                SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, null, false, -1f,
                    float.NaN, -1f, false, dist < AbilityTemplate.MinDistance ? "too close" : "out of range");
                return false;
            }

            var from = Agent.Position + new Vec3(0f, 0f, Agent.GetEyeGlobalHeight(), -1f);
            Agent hitAgent;
            float terrainDist = 0f;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {

                hitAgent = Mission.Current.RayCastForClosestAgent(from, pos, Agent.Index, 0.5f, out _);
                Mission.Current.Scene.RayCastForClosestEntityOrTerrain(from, pos, out terrainDist, out _, out _, 0.25f, (BodyFlags)79617);
            }

            float rayLen = pos.Distance(from);
            bool blockerIsEnemy = hitAgent != null && hitAgent.IsEnemyOf(Agent);
            float blockerToTarget = hitAgent != null ? hitAgent.GetChestGlobalPosition().Distance(pos) : -1f;

            if (Agent.GetChestGlobalPosition().Distance(pos) <= 1f
                || (!float.IsNaN(terrainDist) && terrainDist <= 1f))
            {
                SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent, blockerIsEnemy,
                    blockerToTarget, terrainDist, rayLen, false, "target inside 1m of chest, or terrain within 1m");
                return false;
            }

            if (hitAgent != null && !hitAgent.IsEnemyOf(Agent)
                && hitAgent.GetChestGlobalPosition().Distance(pos) >= 4f)
            {
                SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent, false,
                    blockerToTarget, terrainDist, rayLen, false, "friendly on the line, more than 4m from the target");
                return false;
            }

            bool clear = float.IsNaN(terrainDist) || Math.Abs(terrainDist - rayLen) < 0.3f;
            if (!clear)
            {
                SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent, blockerIsEnemy,
                    blockerToTarget, terrainDist, rayLen, false, "terrain cut the ray short");
                return false;
            }

            string pathProbeReport = string.Empty;
            if (AgentCastingBehaviorConfiguration.BlastRadiusOf(this) > 0f)
            {
                float now = Mission.Current?.CurrentTime ?? 0f;
                if (now < _friendlyFireHoldUntil)
                {
                    SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent,
                        blockerIsEnemy, blockerToTarget, terrainDist, rayLen, false,
                        $"holding fire, corridor was unsafe {_friendlyFireHoldUntil - now:0.00}s ago");
                    return false;
                }

                float blast = AgentCastingBehaviorConfiguration.BlastRadiusOf(this);

                float step = Math.Max(blast * 2f, MinPathStep);
                var dirToTarget = (pos - from).NormalizedCopy();

                var probeLog = new System.Text.StringBuilder();
                for (int i = 1; i <= PathSamples; i++)
                {
                    float along = step * i;
                    if (along >= rayLen) break;
                    var probe = from + dirToTarget * along;
                    float share = CommonAIFunctions.AllyDensityAtPosition(Agent, probe, blast);
                    var raw = CommonAIFunctions.AssessBlast(Agent, probe, blast);
                    probeLog.Append($" [{along:0.0}m share={share:0.00} ally={raw.AllyCount} enemy={raw.EnemyCount}]");
                    if (share > AgentCastingBehaviorConfiguration.ExtremeAllyShare)
                    {
                        SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent,
                            blockerIsEnemy, blockerToTarget, terrainDist, rayLen, false,
                            $"flight path is {share:P0} our own men {along:0.0}m out (of {rayLen:0.0}m)");
                        _friendlyFireHoldUntil = now + FriendlyFireHoldSeconds;
                        return false;
                    }
                }
                pathProbeReport = " probes:" + probeLog;
            }

            SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, hitAgent, blockerIsEnemy,
                blockerToTarget, terrainDist, rayLen, true, "ok" + pathProbeReport);
            return true;
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
                || type == AbilityEffectType.Bombardment || type == AbilityEffectType.MindControl;
            if (groundProjected)
            {
                var pos = target.GetPositionPrioritizeCalculated();
                if (pos == Vec3.Invalid)
                {
                    return false;
                }
                float dist = Agent.Position.Distance(pos);
                if (dist < AbilityTemplate.MinDistance || dist > AbilityTemplate.MaxDistance)
                {
                    SotorAimDiagnostics.LogLineOfSight(Agent, AbilityTemplate, pos, dist, null, false, -1f,
                        float.NaN, -1f, false, dist < AbilityTemplate.MinDistance ? "too close" : "out of range");
                    return false;
                }

                return true;
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

    public class ArcaneConduitCastingBehavior : AbstractAgentCastingBehavior
    {

        private const float ChannelUtility = 0.7f;

        private string _lastReason;

        public ArcaneConduitCastingBehavior(Agent agent, AbilityTemplate template, int abilityIndex)
            : base(agent, template, abilityIndex)
        {
            Hysteresis = 0.1f;
        }

        protected override Target UpdateTarget(Target target)
        {
            target.Agent = Agent;
            target.SelectedWorldPosition = Agent.Position;
            return target;
        }

        protected override bool HaveLineOfSightToTarget(Target target)
        {
            var ability = Agent.GetAbility(AbilityIndex);
            return ability != null
                   && ability.Template?.StringID == SotorArcaneConduitHelper.AbilityId
                   && SotorConduitAI.ShouldChannel(Agent, out _);
        }

        protected override float CalculateUtility(Target target)
        {

            var ability = Agent.GetAbility(AbilityIndex);
            if (ability == null || ability.Template?.StringID != SotorArcaneConduitHelper.AbilityId)
            {
                return 0f;
            }
            if (ability.IsOnCooldown() || !ability.CanCast(Agent, out _))
            {
                return 0f;
            }

            bool should = SotorConduitAI.ShouldChannel(Agent, out var reason);
            if (reason != _lastReason)
            {
                SotorLog.Info($"ConduitAI {Agent.Name}: {(should ? "CHANNEL" : "hold")} - {reason}");
                _lastReason = reason;
            }
            return should ? ChannelUtility : 0f;
        }

        public override List<BehaviorOption> CalculateUtility()
        {

            var target = new Target { Formation = Agent.Formation, Agent = Agent };
            target.UtilityValue = CalculateUtility(target);
            LatestScores = new List<BehaviorOption>
            {
                new BehaviorOption { Target = target, Behavior = this, UtilityValue = target.UtilityValue }
            };
            return LatestScores;
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
