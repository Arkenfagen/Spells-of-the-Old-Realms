using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.Missions
{

    public static class SotorApprenticeWinds
    {
        private static readonly Dictionary<int, float> _winds = new Dictionary<int, float>();

        public static void Clear()
        {
            _winds.Clear();
            _reportedDry.Clear();
        }

        public static void Initialize(Agent agent, float max)
        {
            if (agent == null) return;
            _winds[agent.Index] = max;
            _reportedDry.Remove(agent.Index);
            SotorLog.Info($"ApprenticeWinds: '{agent.Name}' starts the duel with {max:0} Winds.");
        }

        public static bool IsTracked(Agent agent) => agent != null && _winds.ContainsKey(agent.Index);

        public static float Get(Agent agent)
        {
            if (agent == null) return 0f;
            return _winds.TryGetValue(agent.Index, out float value) ? value : 0f;
        }

        private static readonly HashSet<int> _reportedDry = new HashSet<int>();

        public static void NoteCannotAfford(Agent agent, string spellId, int cost)
        {
            if (agent == null || !_reportedDry.Add(agent.Index)) return;
            SotorLog.Info($"ApprenticeWinds: '{agent.Name}' is out of Winds - '{spellId}' costs {cost}, he has "
                          + $"{Get(agent):0}. From here he fights with steel.");
        }

        public static void Spend(Agent agent, float amount)
        {
            if (agent == null || !_winds.ContainsKey(agent.Index)) return;
            float before = _winds[agent.Index];
            float after = before - amount;
            if (after < 0f) after = 0f;
            _winds[agent.Index] = after;
            SotorLog.Info($"ApprenticeWinds: '{agent.Name}' spent {amount:0} | {before:0} -> {after:0} "
                          + $"/ {SotorApprenticeCaster.MaxWindsFor(agent):0}.");
        }
    }
}
