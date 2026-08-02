using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorUndeadMoraleMissionLogic : MissionLogic
    {
        private const string SkeletonCultureId = "sotor_skeleton";
        private const float CrumbleThreshold = 15f;
        private const float TickInterval = 0.5f;
        private const int CrumbleDamage = 9999;
        private const float OverboardSinkAccel = 60f;

        private float _timeElapsed;

        private static bool IsSkeleton(Agent agent)
        {
            var culture = agent?.Character?.Culture;
            return culture != null && culture.StringId == SkeletonCultureId;
        }

        public override void OnMissionTick(float dt)
        {
            _timeElapsed += dt;
            if (_timeElapsed < TickInterval)
            {
                return;
            }
            _timeElapsed = 0f;

            if (Mission.Current == null)
            {
                return;
            }

            var agents = Mission.Current.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || !agent.IsHuman || !agent.IsActive() || agent.Health < 1f)
                {
                    continue;
                }
                if (!IsSkeleton(agent))
                {
                    continue;
                }

                if (agent.IsInWater())
                {
                    try
                    {
                        var down = new Vec3(0f, 0f, -OverboardSinkAccel, -1f);
                        agent.AddAcceleration(in down);
                        SotorDamageHelper.ApplyDamageOverTime(agent, CrumbleDamage, agent);
                    }
                    catch (Exception ex) { SotorLog.Warn($"UndeadMorale overboard-sink failed: {ex.Message}"); }
                    continue;
                }

                var ai = agent.CommonAIComponent;
                if (ai != null && ai.Morale < CrumbleThreshold)
                {

                    try { SotorDamageHelper.ApplyDamageOverTime(agent, CrumbleDamage, agent); }
                    catch (Exception ex) { SotorLog.Warn($"UndeadMorale crumble failed: {ex.Message}"); }
                }
            }
        }
    }
}
