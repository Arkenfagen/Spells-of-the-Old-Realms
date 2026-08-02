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
        public KeepSafeAgentTacticalBehavior(Agent agent, HumanAIComponent aiComponent)
            : base(agent, aiComponent)
        {
        }

        public override void Tick()
        {
            var active = Agent.Formation?.AI?.ActiveBehavior;
            if (Agent.Team != null && Agent.Team.GeneralAgent == Agent && Agent.Team.HasTeamAi
                && active != null && active.GetType() == typeof(BehaviorCharge))
            {
                Agent.Formation.AI.SetBehaviorWeight<BehaviorCharge>(0f);
            }
        }

        public override void Terminate()
        {
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
