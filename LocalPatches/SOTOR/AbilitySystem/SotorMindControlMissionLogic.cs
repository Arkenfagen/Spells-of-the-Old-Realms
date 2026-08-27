using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorMindControlMissionLogic : MissionLogic
    {

        private class ConvertInfo
        {
            public MobileParty ControllerParty;
            public bool PlayerControlled;
            public PartyBase OriginParty;
        }

        private readonly Dictionary<Agent, ConvertInfo> _converts = new Dictionary<Agent, ConvertInfo>();

        private readonly HashSet<Agent> _freezeFixRemoved = new HashSet<Agent>();
        private bool _freezeFixDone;

        public bool HasConverts => _converts.Count > 0;

        public override void AfterStart()
        {
            base.AfterStart();
            if (PendingAiClaims.Count > 0)
            {
                SotorLog.Warn($"MindControl: discarding {PendingAiClaims.Count} stale AI convert claim(s) from a previous battle.");
                PendingAiClaims.Clear();
            }
        }

        public void OnAgentConverted(Agent agent, Agent caster)
        {
            if (agent == null)
            {
                return;
            }

            var controllerHero = SOTOR.Extensions.AgentExtensions.GetHero(caster);
            var controllerParty = controllerHero?.PartyBelongedTo;
            bool playerControlled = (caster != null && caster.IsMainAgent)
                                    || (controllerParty != null && controllerParty == MobileParty.MainParty);

            if (!_converts.TryGetValue(agent, out var info))
            {
                info = new ConvertInfo { OriginParty = agent.Origin?.BattleCombatant as PartyBase };
                _converts[agent] = info;
            }
            info.ControllerParty = controllerParty;
            info.PlayerControlled = playerControlled;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_freezeFixDone || _converts.Count == 0)
            {
                return;
            }

            Mission mission = Mission.Current;
            if (mission == null || mission.MissionEnded)
            {
                return;
            }

            var playerTeam = mission.PlayerTeam;
            if (playerTeam == null)
            {
                return;
            }
            if (AnyTrueFighter(mission, enemySide: true) && AnyTrueFighter(mission, enemySide: false))
            {
                return;
            }

            RemoveConvertsNonLethally();
            _freezeFixDone = true;
        }

        private bool AnyTrueFighter(Mission mission, bool enemySide)
        {
            foreach (var team in mission.Teams)
            {
                if (team == null || team.IsEnemyOf(mission.PlayerTeam) != enemySide)
                {
                    continue;
                }
                foreach (var agent in team.ActiveAgents)
                {
                    if (agent != null && agent.IsHuman && !_converts.ContainsKey(agent))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void RemoveConvertsNonLethally()
        {
            foreach (var agent in _converts.Keys)
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
            SotorLog.Info($"MindControl freeze-fix: removed {_converts.Count} leftover convert(s) non-lethally to end the battle.");
        }

        public static readonly Dictionary<CharacterObject, int> PendingRecruits = new Dictionary<CharacterObject, int>();

        public class AiConvertClaim
        {
            public CharacterObject Troop;
            public MobileParty ControllerParty;
            public PartyBase OriginParty;
            public int Count;
        }

        public static readonly List<AiConvertClaim> PendingAiClaims = new List<AiConvertClaim>();

        protected override void OnEndMission()
        {
            base.OnEndMission();
            if (_converts.Count == 0)
            {
                return;
            }

            int playerStash = 0, aiStash = 0, reclaimed = 0;
            foreach (var kv in _converts)
            {
                var agent = kv.Key;
                var info = kv.Value;

                if (agent == null || agent.IsHero || !(agent.Character is CharacterObject troop))
                {
                    continue;
                }
                bool survived = _freezeFixRemoved.Contains(agent)
                                || (agent.State != AgentState.Killed && agent.State != AgentState.Deleted);
                if (!survived)
                {
                    continue;
                }

                if (info.PlayerControlled)
                {

                    if (info.OriginParty == PartyBase.MainParty)
                    {
                        reclaimed++;
                        continue;
                    }

                    PendingRecruits.TryGetValue(troop, out int n);
                    PendingRecruits[troop] = n + 1;
                    playerStash++;
                }
                else if (info.ControllerParty != null)
                {

                    if (info.OriginParty == null)
                    {
                        continue;
                    }
                    var claim = PendingAiClaims.Find(c => c.Troop == troop
                                                          && c.ControllerParty == info.ControllerParty
                                                          && c.OriginParty == info.OriginParty);
                    if (claim == null)
                    {
                        claim = new AiConvertClaim
                        {
                            Troop = troop,
                            ControllerParty = info.ControllerParty,
                            OriginParty = info.OriginParty,
                        };
                        PendingAiClaims.Add(claim);
                    }
                    claim.Count++;
                    aiStash++;
                }
            }

            if (playerStash > 0 || aiStash > 0 || reclaimed > 0)
            {
                SotorLog.Info($"MindControl: stashed {playerStash} player-controlled and {aiStash} AI-controlled "
                              + $"surviving convert(s) for post-battle settlement; {reclaimed} of the player's own "
                              + "men were taken back and need no recruiting.");
            }
        }
    }
}
