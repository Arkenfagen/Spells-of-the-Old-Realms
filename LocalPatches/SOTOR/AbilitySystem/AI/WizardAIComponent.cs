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

        private AbstractAgentCastingBehavior DetermineBehavior(List<IAgentBehavior> available, AbstractAgentCastingBehavior current)
        {
            var option = DecisionManager.EvaluateCastingBehaviors(available);
            if (option == null)
            {
                return current;
            }
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
