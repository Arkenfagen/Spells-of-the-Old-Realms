using System.Collections.Generic;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public class WizardAIComponent : HumanAIComponent
    {
        private const float EvalInterval = 3f;

        private float _dtSinceLastOccasional;

        public AbstractAgentCastingBehavior CurrentCastingBehavior;

        private List<IAgentBehavior> _availableCastingBehaviors;

        public List<IAgentBehavior> AvailableCastingBehaviors => _availableCastingBehaviors
            ?? (_availableCastingBehaviors = AgentCastingBehaviorConfiguration.PrepareCastingBehaviors(Agent)
                .Cast<IAgentBehavior>().ToList());

        public WizardAIComponent(Agent agent)
            : base(agent)
        {
            _dtSinceLastOccasional = (agent.Index % 30) / 30f * EvalInterval;

            var existing = agent.Components.OfType<HumanAIComponent>().Where(c => !ReferenceEquals(c, this)).ToList();
            foreach (var component in existing)
            {
                agent.RemoveComponent(component);
            }
        }

        public override void OnTick(float dt)
        {
            if (Agent.IsPaused)
            {
                return;
            }

            _dtSinceLastOccasional += dt;
            if (_dtSinceLastOccasional >= EvalInterval)
            {
                TickOccasionally();
            }

            var firingOrder = Agent?.Formation?.FiringOrder;
            if (firingOrder.HasValue && firingOrder.Value.OrderType == OrderType.HoldFire)
            {
                CurrentCastingBehavior?.Terminate();
                CurrentCastingBehavior?.TacticalBehavior?.Terminate();
                return;
            }

            CurrentCastingBehavior?.TacticalBehavior?.Execute();
            CurrentCastingBehavior?.Execute();
        }

        private void TickOccasionally()
        {
            _dtSinceLastOccasional = 0f;
            CurrentCastingBehavior = DetermineBehavior(AvailableCastingBehaviors, CurrentCastingBehavior);
        }

        private const float SwitchMargin = 0.08f;

        private AbstractAgentCastingBehavior DetermineBehavior(List<IAgentBehavior> available, AbstractAgentCastingBehavior current)
        {

            var ranked = DecisionManager.RankCastingBehaviors(available);
            var best = ranked.Count > 0 ? ranked[0] : null;

            if (best == null)
            {

                SotorAimDiagnostics.LogBehaviorRanking(Agent, available, null);
                return current;
            }

            BehaviorOption option = null;
            foreach (var candidate in ranked)
            {
                if (candidate?.Behavior is AbstractAgentCastingBehavior cast
                    && cast.CanDeliverTo(candidate.Target))
                {
                    option = candidate;
                    break;
                }
            }

            if (option == null)
            {
                option = best;
            }

            if (current != null && option.Behavior != current)
            {
                BehaviorOption currentOption = null;
                foreach (var candidate in ranked)
                {
                    if (candidate?.Behavior == current)
                    {
                        currentOption = candidate;
                        break;
                    }
                }

                bool currentStillDeliverable = currentOption != null && current.CanDeliverTo(current.CurrentTarget);
                if (currentStillDeliverable
                    && option.Target.UtilityValue < currentOption.Target.UtilityValue + SwitchMargin)
                {

                    SotorAimDiagnostics.LogBehaviorRanking(Agent, available, currentOption);
                    return current;
                }
            }

            SotorAimDiagnostics.LogBehaviorRanking(Agent, available, option);

            if (option.Behavior != current)
            {
                current?.Terminate();
                current?.TacticalBehavior?.Terminate();
            }
            if (option.Behavior is AbstractAgentCastingBehavior chosen)
            {
                chosen.CurrentTarget = option.Target;
                return chosen;
            }
            return null;
        }
    }
}
