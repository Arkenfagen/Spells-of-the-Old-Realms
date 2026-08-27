using System;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public class SotorCastingAIMissionLogic : MissionLogic
    {

        private bool IsActive()
        {

            return (SotorSettings.EnableCompanionSpellcasters || SotorSettings.EnableRivalCasters)
                && AbilityMissionModeHelper.IsBattleAbilityContext(Mission);
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();

            AbilitySystem.Rivals.SotorBattleAllyTally.Clear();

            SotorAimDiagnostics.Clear();

            CommonAIFunctions.ClearBlastSpotMemo();
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

                var component = agent.GetComponent<AbilityComponent>();
                SotorLog.Info(
                    $"CastingAI: wizard brain attached to '{agent.Name}' " +
                    $"(known={component?.KnownAbilitySystem.Count ?? 0}, mode={(int)Mission.Mode}).");
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

            bool playerSide = IsPlayerSideAgent(agent);
            if (playerSide && !SotorSettings.EnableCompanionSpellcasters) return false;
            if (!playerSide && !SotorSettings.EnableRivalCasters) return false;

            var component = agent.GetComponent<AbilityComponent>();
            return component != null && component.KnownAbilitySystem.Count > 0;
        }

        private static bool IsPlayerSideAgent(Agent agent)
        {
            var hero = (agent.Character as CharacterObject)?.HeroObject;
            return hero != null && ExtendedInfoManager.IsPlayerSideCaster(hero);
        }
    }
}
