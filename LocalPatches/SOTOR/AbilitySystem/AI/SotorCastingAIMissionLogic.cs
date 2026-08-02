using System;
using SOTOR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public class SotorCastingAIMissionLogic : MissionLogic
    {

        private bool IsActive()
        {
            return SotorSettings.EnableCompanionSpellcasters
                && AbilityMissionModeHelper.IsBattleAbilityContext(Mission);
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (!IsActive())
            {
                return;
            }

            try
            {
                if (!ShouldControlAsCaster(agent))
                {
                    return;
                }

                if (agent.GetComponent<WizardAIComponent>() != null)
                {
                    return;
                }

                agent.AddComponent(new WizardAIComponent(agent));
            }
            catch (Exception ex)
            {
                SotorLog.Error($"CastingAI OnAgentBuild for '{agent?.Name}' failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static bool ShouldControlAsCaster(Agent agent)
        {
            if (agent == null || agent.IsPlayerControlled || agent.IsMainAgent || !agent.IsAIControlled || !agent.IsHuman)
            {
                return false;
            }
            if (!agent.IsAbilityUser())
            {
                return false;
            }
            var component = agent.GetComponent<AbilityComponent>();
            return component != null && component.KnownAbilitySystem.Count > 0;
        }
    }
}
