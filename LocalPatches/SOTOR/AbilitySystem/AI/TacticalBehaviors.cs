using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public abstract class AbstractAgentTacticalBehavior : IAgentBehavior
    {
        protected HumanAIComponent AIComponent;
        protected Agent Agent;

        protected AbstractAgentTacticalBehavior(Agent agent, HumanAIComponent aiComponent)
        {
            Agent = agent;
            AIComponent = aiComponent;
        }

        protected OrderType? GetMovementOrderType()
        {
            var formation = Agent?.Formation;
            if (formation == null)
            {
                return null;
            }
            ref readonly MovementOrder order = ref formation.GetReadonlyMovementOrderReference();
            return order.OrderType;
        }

        public void Execute()
        {
            ApplyBehaviorParams();
            Tick();
        }

        public abstract void Tick();
        public abstract void Terminate();
        public abstract void ApplyBehaviorParams();
        public abstract void SetCurrentTarget(Target target);

        public List<BehaviorOption> CalculateUtility()
        {
            return new List<BehaviorOption>();
        }
    }

    public class KeepSafeAgentTacticalBehavior : AbstractAgentTacticalBehavior
    {

        private const float StandoffFraction = 0.8f;

        private const float MinUsefulRange = 5f;

        private const float MaxChaseMultiple = 2f;
        private const float MaxApproachDistance = 120f;

        private const float ReleaseFraction = 0.9f;

        private const float MinFlipSeconds = 0.5f;
        private float _lastFlipTime;

        private readonly AbstractAgentCastingBehavior _castingBehavior;
        private bool _scripted;

        public KeepSafeAgentTacticalBehavior(Agent agent, HumanAIComponent aiComponent,
            AbstractAgentCastingBehavior castingBehavior = null)
            : base(agent, aiComponent)
        {
            _castingBehavior = castingBehavior;
        }

        public override void Tick()
        {
            var active = Agent.Formation?.AI?.ActiveBehavior;
            if (Agent.Team != null && Agent.Team.GeneralAgent == Agent && Agent.Team.HasTeamAi
                && active != null && active.GetType() == typeof(BehaviorCharge))
            {
                Agent.Formation.AI.SetBehaviorWeight<BehaviorCharge>(0f);
            }

            TickApproach();
        }

        private void TickApproach()
        {
            try
            {
                var template = _castingBehavior?.AbilityTemplate;
                if (template == null || !CommonAIStateFunctions.CanAgentMoveFreely(Agent))
                {
                    return;
                }

                float maxRange = template.MaxDistance;
                if (maxRange <= MinUsefulRange || float.IsInfinity(maxRange) || maxRange > 1000f)
                {
                    ReleaseScript();
                    return;
                }

                var target = _castingBehavior.CurrentTarget;
                var targetPos = target?.GetPositionPrioritizeCalculated() ?? Vec3.Invalid;
                if (targetPos == Vec3.Invalid)
                {
                    ReleaseScript();
                    return;
                }

                float dist = Agent.Position.Distance(targetPos);

                float threshold = _scripted ? maxRange * ReleaseFraction : maxRange;
                if (dist <= threshold)
                {
                    ReleaseScript();
                    return;
                }

                if (dist > maxRange * MaxChaseMultiple || dist > MaxApproachDistance)
                {

                    ReleaseScript();
                    return;
                }

                var toCaster = (Agent.Position - targetPos);
                if (toCaster.Length < 0.01f)
                {
                    return;
                }
                var standoff = targetPos + toCaster.NormalizedCopy() * (maxRange * StandoffFraction);

                float now = Mission.Current?.CurrentTime ?? 0f;
                if (!_scripted && now - _lastFlipTime < MinFlipSeconds)
                {
                    return;
                }

                var worldPos = new WorldPosition(Mission.Current.Scene, standoff);
                Agent.SetScriptedPosition(ref worldPos, false, (Agent.AIScriptedFrameFlags)0);
                if (!_scripted)
                {
                    _lastFlipTime = now;
                    SotorLog.Info($"CastApproach: {Agent.Name} closing for {template.StringID} "
                                  + $"({dist:0}m > {maxRange:0}m), standoff {maxRange * StandoffFraction:0}m.");
                    _scripted = true;
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"CastApproach failed harmlessly: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ReleaseScript()
        {
            if (!_scripted) return;
            _scripted = false;
            _lastFlipTime = Mission.Current?.CurrentTime ?? 0f;
            Agent.DisableScriptedMovement();
            SotorLog.Info($"CastApproach: {Agent.Name} in range, movement released.");
        }

        public override void Terminate()
        {
            ReleaseScript();
        }

        public override void ApplyBehaviorParams()
        {
            var orderType = GetMovementOrderType();
            if (!orderType.HasValue || (orderType.Value != OrderType.FollowMe && orderType.Value != OrderType.FollowEntity))
            {
                AIComponent.SetBehaviorValueSet((HumanAIComponent.BehaviorValueSet)5);
            }
        }

        public override void SetCurrentTarget(Target target)
        {
        }
    }

    public class DirectionalAoETacticalBehavior : AbstractAgentTacticalBehavior
    {
        public Vec3 CastingPosition;
        public AbstractAgentCastingBehavior CastingBehavior { get; set; }

        public DirectionalAoETacticalBehavior(Agent agent, HumanAIComponent aiComponent, AbstractAgentCastingBehavior castingBehavior)
            : base(agent, aiComponent)
        {
            CastingBehavior = castingBehavior;
        }

        private Vec3 CalculateCastingPosition(Formation targetFormation)
        {
            Vec2 dir = targetFormation.QuerySystem.EstimatedDirection;
            var median = targetFormation.GetMedianAgent(true, false, targetFormation.GetAveragePositionOfUnits(true, false));
            if (median == null)
            {
                return Vec3.Zero;
            }
            float halfWidth = targetFormation.Width / 1.95f;
            Vec3 left = median.Position + dir.LeftVec().ToVec3(0f) * halfWidth;
            Vec3 right = median.Position + dir.RightVec().ToVec3(0f) * halfWidth;
            float toLeft = Agent.Position.Distance(left);
            float toRight = Agent.Position.Distance(right);
            return toLeft < toRight ? left : right;
        }

        public override void ApplyBehaviorParams()
        {
        }

        public override void Tick()
        {
            if (!CommonAIStateFunctions.CanAgentMoveFreely(Agent))
            {
                return;
            }
            var target = CastingBehavior.CurrentTarget;
            CastingPosition = target.Formation != null ? CalculateCastingPosition(target.Formation) : Agent.Position;
            CastingPosition = CastingPosition != Vec3.Zero ? CastingPosition : Agent.Position;
            var worldPos = new WorldPosition(Mission.Current.Scene, CastingPosition);
            Agent.SetScriptedPosition(ref worldPos, false, (Agent.AIScriptedFrameFlags)0);
        }

        public override void SetCurrentTarget(Target target)
        {
            CastingBehavior.SetCurrentTarget(target);
        }

        public override void Terminate()
        {
            Agent.DisableScriptedMovement();
        }
    }
}
