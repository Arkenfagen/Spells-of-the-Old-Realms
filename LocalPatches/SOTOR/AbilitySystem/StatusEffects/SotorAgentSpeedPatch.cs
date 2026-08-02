using System;
using HarmonyLib;
using SandBox.GameComponents;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.StatusEffects
{

    [HarmonyPatch(typeof(SandboxAgentStatCalculateModel), nameof(SandboxAgentStatCalculateModel.UpdateAgentStats))]
    public static class SotorAgentSpeedPatch
    {
        public static void Postfix(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            try
            {
                if (agent == null || agentDrivenProperties == null || !agent.IsActive())
                {
                    return;
                }

                if (agent.IsMount)
                {
                    var rider = agent.RiderAgent;
                    var riderComp = rider != null && rider.IsActive() ? rider.GetComponent<StatusEffectComponent>() : null;
                    float riderMove = riderComp != null ? riderComp.GetMovementSpeedModifier() : 0f;
                    if (riderMove != 0f)
                    {
                        float mf = Math.Max(0f, 1f + riderMove);
                        agentDrivenProperties.MountSpeed *= mf;
                        agentDrivenProperties.MountManeuver *= mf;
                        agentDrivenProperties.MountDashAccelerationMultiplier *= mf;
                    }
                    return;
                }

                var component = agent.GetComponent<StatusEffectComponent>();
                if (component == null)
                {
                    return;
                }

                float moveMod = component.GetMovementSpeedModifier();
                float atkMod = component.GetAttackSpeedModifier();

                if (moveMod != 0f)
                {
                    float f = Math.Max(0f, 1f + moveMod);

                    agentDrivenProperties.MaxSpeedMultiplier *= f;
                    agentDrivenProperties.CombatMaxSpeedMultiplier *= f;
                }
                if (atkMod != 0f)
                {
                    float f = Math.Max(0f, 1f + atkMod);
                    agentDrivenProperties.SwingSpeedMultiplier *= f;
                    agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= f;
                    agentDrivenProperties.ReloadSpeed *= f;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorAgentSpeedPatch failed: {ex.Message}");
            }
        }
    }
}
