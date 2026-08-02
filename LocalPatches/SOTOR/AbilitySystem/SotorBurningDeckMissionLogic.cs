using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorBurningDeckMissionLogic : MissionLogic
    {
        private const float TickInterval = 1.0f;
        private float _timeElapsed;

        private readonly List<int> _ablazeDeckAgents = new List<int>(64);

        public override void OnMissionTick(float dt)
        {
            _timeElapsed += dt;
            if (_timeElapsed < TickInterval)
            {
                return;
            }
            float elapsed = _timeElapsed;
            _timeElapsed = 0f;

            if (!SotorSettings.EnableBurningDeckDamage)
            {
                return;
            }
            var mission = Mission.Current;
            if (!SotorNavalBridge.IsNavalMission(mission))
            {
                return;
            }

            if (mission.MissionEnded || mission.IsMissionEnding)
            {
                return;
            }

            int burn = (int)Math.Max(1f, SotorSettings.BurningDeckDamagePerSecond * elapsed);

            _ablazeDeckAgents.Clear();
            SotorNavalBridge.CollectAgentsOnAblazeDecks(mission, _ablazeDeckAgents);
            if (_ablazeDeckAgents.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _ablazeDeckAgents.Count; i++)
            {
                Agent a = mission.FindAgentWithIndex(_ablazeDeckAgents[i]);
                if (a == null || !a.IsHuman || !a.IsActive() || a.Health < 1f)
                {
                    continue;
                }

                try { SotorDamageHelper.ApplyDamageOverTime(a, burn, a); }
                catch (Exception ex) { SotorLog.Warn($"BurningDeck DoT failed on '{a.Name}': {ex.Message}"); }
            }
        }
    }
}
