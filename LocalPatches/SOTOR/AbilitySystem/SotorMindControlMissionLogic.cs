using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorMindControlMissionLogic : MissionLogic
    {

        private readonly List<Agent> _convertedAgents = new List<Agent>();

        private readonly Dictionary<CharacterObject, int> _recruitTally = new Dictionary<CharacterObject, int>();

        private readonly HashSet<Agent> _freezeFixRemoved = new HashSet<Agent>();
        private bool _freezeFixDone;

        public bool HasConverts => _convertedAgents.Count > 0;

        public void OnAgentConverted(Agent agent)
        {
            if (agent == null)
            {
                return;
            }
            if (!_convertedAgents.Contains(agent))
            {
                _convertedAgents.Add(agent);
            }

            if (!agent.IsHero && agent.Character is CharacterObject troop)
            {
                _recruitTally.TryGetValue(troop, out int n);
                _recruitTally[troop] = n + 1;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_freezeFixDone || _convertedAgents.Count == 0)
            {
                return;
            }

            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded)
            {
                return;
            }

            var playerEnemy = mission.PlayerEnemyTeam;
            if (playerEnemy == null)
            {
                return;
            }
            if (AnyActiveTrueEnemy(mission))
            {
                return;
            }

            RemoveConvertsNonLethally();
            _freezeFixDone = true;
        }

        private bool AnyActiveTrueEnemy(Mission mission)
        {
            foreach (var team in mission.Teams)
            {
                if (team == null || !team.IsEnemyOf(mission.PlayerTeam))
                {
                    continue;
                }
                foreach (var agent in team.ActiveAgents)
                {
                    if (agent != null && agent.IsHuman)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void RemoveConvertsNonLethally()
        {
            foreach (var agent in _convertedAgents)
            {
                try
                {
                    if (agent == null || !agent.IsActive())
                    {
                        continue;
                    }
                    var blow = new Blow(agent.Index)
                    {
                        DamageType = DamageTypes.Invalid,
                        BaseMagnitude = 10000f,
                        GlobalPosition = agent.Position,
                        DamagedPercentage = 1f,
                    };
                    _freezeFixRemoved.Add(agent);
                    agent.Die(blow, Agent.KillInfo.TeamSwitch);
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"MindControl freeze-fix: Die(TeamSwitch) failed: {ex.Message}");
                }
            }
            SotorLog.Info($"MindControl freeze-fix: removed {_convertedAgents.Count} leftover convert(s) non-lethally to end the battle.");
        }

        public static readonly Dictionary<CharacterObject, int> PendingRecruits = new Dictionary<CharacterObject, int>();

        protected override void OnEndMission()
        {
            base.OnEndMission();
            if (_recruitTally.Count == 0)
            {
                return;
            }
            foreach (var kv in _recruitTally)
            {
                CharacterObject troop = kv.Key;
                int count = CountSurvivors(troop);
                if (troop == null || count <= 0)
                {
                    continue;
                }
                PendingRecruits.TryGetValue(troop, out int n);
                PendingRecruits[troop] = n + count;
            }
            SotorLog.Info($"MindControl: stashed {PendingRecruits.Count} troop type(s) of surviving converts for post-battle recruit.");
        }

        private int CountSurvivors(CharacterObject troop)
        {
            int n = 0;
            foreach (var agent in _convertedAgents)
            {
                if (agent == null || agent.IsHero || agent.Character != troop)
                {
                    continue;
                }

                if (_freezeFixRemoved.Contains(agent)
                    || (agent.State != AgentState.Killed && agent.State != AgentState.Deleted))
                {
                    n++;
                }
            }
            return n;
        }
    }
}
