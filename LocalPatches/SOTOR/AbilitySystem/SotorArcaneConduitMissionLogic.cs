using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorArcaneConduitMissionLogic : MissionLogic
    {

        private static readonly Dictionary<int, int> _usesByAgent = new Dictionary<int, int>();

        public static int GetUses(Agent agent)
        {
            if (agent == null) return 0;
            return _usesByAgent.TryGetValue(agent.Index, out int n) ? n : 0;
        }

        public static void RegisterUse(Agent agent)
        {
            if (agent == null) return;
            _usesByAgent[agent.Index] = GetUses(agent) + 1;
        }

        private bool _resetThisMission;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _usesByAgent.Clear();
            _resetThisMission = true;
        }

        public override void OnMissionTick(float dt)
        {
            if (!_resetThisMission)
            {
                _resetThisMission = true;
                _usesByAgent.Clear();
            }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            _usesByAgent.Clear();
        }
    }
}
