using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.TriggeredScripts
{

    public class Summon : ITriggeredScript
    {
        public void OnTrigger(Vec3 position, Agent triggeredByAgent, IEnumerable<Agent> triggeredAgents, float duration, TriggeredEffectTemplate template, string originSpell)
        {

            if (AbilityMissionModeHelper.IsArenaOrTournamentMission(Mission.Current))
            {
                SotorLog.Info($"Summon '{originSpell}': BLOCKED — arena/tournament mission (unregistered agents crash the tournament controller).");
                return;
            }

            if (triggeredByAgent == null || !triggeredByAgent.IsActive() || triggeredByAgent.Team == null)
            {
                return;
            }
            if (template == null || string.IsNullOrWhiteSpace(template.TroopIdToSummon)
                || template.TroopIdToSummon.Equals("none", System.StringComparison.OrdinalIgnoreCase)
                || template.NumberToSummon <= 0)
            {
                return;
            }

            if (!IsSummonPointValid(position))
            {
                if (triggeredByAgent == Agent.Main)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        SotorText.Rendered("sotor_summon_over_water",
                            "You cannot raise the dead over open water."), Colors.Red));
                }
                SotorLog.Info($"Summon '{originSpell}': BLOCKED — cast point {position} is off-navmesh (open water?); no skeletons spawned.");
                return;
            }

            int count = template.NumberToSummon;

            Vec3 at = position;
            for (int i = 0; i < count; i++)
            {

                var buildData = SummonHelper.GetAgentBuildData(triggeredByAgent, template.TroopIdToSummon);
                if (buildData == null)
                {
                    SotorLog.Warn($"Summon: troop '{template.TroopIdToSummon}' not found; aborting summon.");
                    return;
                }

                at = Mission.Current.GetRandomPositionAroundPoint(at, 0.1f, 0.6f, false);

                try
                {
                    Agent spawned = SummonHelper.SpawnAgent(buildData, at, withAnimation: true);
                    if (spawned != null)
                    {
                        SotorSummonNavalGuardMissionLogic.EnqueueSummonedAgent(spawned);
                    }
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"Summon: spawn {i + 1}/{count} of '{template.TroopIdToSummon}' failed ({ex.GetType().Name}): {ex.Message}");
                }
            }
        }

        private static bool IsSummonPointValid(Vec3 position)
        {
            try
            {
                var scene = Mission.Current?.Scene;
                if (scene == null)
                {
                    return false;
                }

                if (scene.GetNavigationMeshForPosition(position) != UIntPtr.Zero)
                {
                    return true;
                }

                Vec3 nudged = Mission.Current.GetRandomPositionAroundPoint(position, 0.05f, 5f, true);
                if (!nudged.IsValid || !nudged.IsNonZero)
                {
                    return false;
                }
                return scene.GetNavigationMeshForPosition(nudged) != UIntPtr.Zero;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Summon.IsSummonPointValid failed: {ex.Message}");
                return false;
            }
        }
    }
}
