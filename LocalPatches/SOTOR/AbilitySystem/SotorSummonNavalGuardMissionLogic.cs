using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorSummonNavalGuardMissionLogic : MissionLogic
    {

        private const float BindGraceSeconds = 1.5f;

        private struct Pending
        {
            public Agent Agent;
            public float CheckAtTime;
        }

        private static readonly List<Pending> _pending = new List<Pending>(16);

        public static void EnqueueSummonedAgent(Agent agent)
        {
            try
            {
                var mission = Mission.Current;
                if (agent == null || mission == null || !SotorNavalBridge.IsNavalMission(mission))
                {
                    return;
                }
                _pending.Add(new Pending { Agent = agent, CheckAtTime = mission.CurrentTime + BindGraceSeconds });
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SummonNavalGuard.Enqueue failed: {ex.Message}");
            }
        }

        public override void OnMissionTick(float dt)
        {

            if (SotorNavalBridge.IsNavalMission(Mission.Current))
            {
                SotorNavalMarinerXpPatch.ApplyIfNeeded();
            }

            if (_pending.Count == 0)
            {
                return;
            }
            var mission = Mission.Current;
            if (mission == null || mission.MissionEnded || mission.IsMissionEnding)
            {
                _pending.Clear();
                return;
            }

            float now = mission.CurrentTime;
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                Pending p = _pending[i];
                if (now < p.CheckAtTime)
                {
                    continue;
                }
                _pending.RemoveAt(i);

                Agent a = p.Agent;
                if (a == null || !a.IsActive())
                {
                    continue;
                }

                int binding = SotorNavalBridge.GetSummonedAgentNavalBinding(a);

                if (binding != 2)
                {
                    continue;
                }

                SotorLog.Warn($"Summon naval guard: '{a.Name}' never bound to a ship in a naval battle -> removing (would crash Mission.Tick).");
                try { a.FadeOut(true, true); } catch { }
            }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            _pending.Clear();
        }
    }
}
