using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorAbandonShipMissionLogic : MissionLogic
    {
        private const float SweepInterval = 0.75f;
        private const string SkeletonCultureId = "sotor_skeleton";

        private float _timeElapsed;

        private const float ScreamCooldownMin = 6f;
        private const float ScreamCooldownMax = 9f;

        private sealed class FleeState
        {
            public int TargetShipIndex;
            public Vec3 TargetPoint;
            public int Quality;
            public bool NeedsSwim;
            public Vec3 WaterEntryPoint;
            public bool InWaterPhase;
            public float NextScreamTime;
            public Vec3 LastPos;
            public int StuckSweeps;
            public bool Jumped;
        }
        private readonly Dictionary<int, FleeState> _fleeing = new Dictionary<int, FleeState>();
        private readonly List<int> _pruneScratch = new List<int>(64);

        private static bool IsSkeleton(Agent agent)
        {
            var culture = agent?.Character?.Culture;
            return culture != null && culture.StringId == SkeletonCultureId;
        }

        public override void OnMissionTick(float dt)
        {
            _timeElapsed += dt;
            if (_timeElapsed < SweepInterval)
            {
                return;
            }
            _timeElapsed = 0f;

            if (!SotorSettings.EnableAbandonShipAI)
            {
                if (_fleeing.Count > 0) _fleeing.Clear();
                return;
            }
            var mission = Mission.Current;
            if (!SotorNavalBridge.IsNavalMission(mission))
            {
                return;
            }

            if (mission.MissionEnded || mission.IsMissionEnding)
            {
                if (_fleeing.Count > 0)
                {
                    foreach (var idx in _fleeing.Keys)
                    {
                        Agent fa = mission.FindAgentWithIndex(idx);
                        if (fa != null && fa.IsActive())
                        {
                            Release(fa, _fleeing[idx]);
                        }
                    }
                    _fleeing.Clear();
                }
                return;
            }

            _pruneScratch.Clear();
            foreach (var idx in _fleeing.Keys)
            {
                Agent fa = mission.FindAgentWithIndex(idx);
                if (fa == null || !fa.IsActive() || !fa.IsHuman || fa.Health < 1f)
                {
                    _pruneScratch.Add(idx);
                }
            }
            for (int i = 0; i < _pruneScratch.Count; i++)
            {
                _fleeing.Remove(_pruneScratch[i]);
            }

            var agents = mission.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                Agent a = agents[i];
                if (a == null || !a.IsHuman)
                {
                    continue;
                }

                if (a.IsPlayerControlled || a == Mission.Current?.MainAgent)
                {
                    if (_fleeing.TryGetValue(a.Index, out FleeState pst))
                    {
                        Release(a, pst);
                        _fleeing.Remove(a.Index);
                    }
                    continue;
                }
                bool isFleeing = _fleeing.TryGetValue(a.Index, out FleeState st);

                if (!a.IsActive() || a.Health < 1f)
                {
                    if (isFleeing) _fleeing.Remove(a.Index);
                    continue;
                }
                if (IsSkeleton(a))
                {
                    continue;
                }

                bool hasTarget = SotorNavalBridge.TryGetEscapeTarget(a, out Vec3 pt, out int shipIndex,
                                                                     out int quality, out bool needsSwim, out Vec3 waterPt,
                                                                     alreadyCommitted: isFleeing);

                float now = mission.CurrentTime;

                if (!isFleeing)
                {

                    if (hasTarget)
                    {
                        var newState = new FleeState
                        {
                            TargetShipIndex = shipIndex, TargetPoint = pt, Quality = quality,
                            NeedsSwim = needsSwim, WaterEntryPoint = waterPt, InWaterPhase = false,
                        };

                        if (DriveFor(a, newState))
                        {

                            newState.NextScreamTime = now + MBRandom.RandomFloatRanged(0f, 2.5f);
                            _fleeing[a.Index] = newState;
                        }
                    }
                    continue;
                }

                if (SotorNavalBridge.IsAgentOnSafeShip(a))
                {
                    Release(a, st);
                    _fleeing.Remove(a.Index);
                    continue;
                }

                if (!hasTarget)
                {
                    Release(a, st);
                    _fleeing.Remove(a.Index);
                    continue;
                }

                if (st.NeedsSwim && !st.InWaterPhase && a.IsInWater())
                {
                    st.InWaterPhase = true;

                    try { a.StopRetreating(); } catch { }
                    DriveFor(a, st);
                }

                else if (!st.InWaterPhase && !a.IsInWater())
                {
                    float moved = a.Position.Distance(st.LastPos);
                    if (st.LastPos != Vec3.Zero && moved < 1f)
                    {
                        st.StuckSweeps++;
                        if (st.StuckSweeps >= 4)
                        {
                            st.StuckSweeps = 0;
                            if (st.NeedsSwim)
                            {

                                st.Jumped = false;
                                DriveFor(a, st);
                            }
                            else
                            {

                                st.NeedsSwim = true;
                                st.Jumped = false;
                                DriveFor(a, st);
                            }
                        }
                    }
                    else
                    {
                        st.StuckSweeps = 0;
                    }
                    st.LastPos = a.Position;
                }

                if (SotorNavalBridge.IsShipIndexDoomed(st.TargetShipIndex))
                {
                    try { if (a.IsUsingGameObject) a.StopUsingGameObject(true); } catch { }
                    if (hasTarget && shipIndex != st.TargetShipIndex)
                    {
                        st.TargetShipIndex = shipIndex;
                        st.TargetPoint = pt;
                        st.Quality = quality;
                        st.NeedsSwim = needsSwim;
                        st.WaterEntryPoint = waterPt;
                        DriveFor(a, st);
                    }
                    else if (!hasTarget)
                    {
                        Release(a, st);
                        _fleeing.Remove(a.Index);
                    }
                    continue;
                }

                if (st.NeedsSwim && st.InWaterPhase)
                {
                    DriveFor(a, st);
                }

                bool climbing = a.IsUsingGameObject;
                bool strictlyBetter = quality > st.Quality;
                bool targetGone = shipIndex != st.TargetShipIndex && quality >= st.Quality;
                if (!climbing && (strictlyBetter || targetGone))
                {
                    st.TargetShipIndex = shipIndex;
                    st.TargetPoint = pt;
                    st.Quality = quality;
                    st.NeedsSwim = needsSwim;
                    st.WaterEntryPoint = waterPt;

                    if (!a.IsInWater()) st.InWaterPhase = false;
                    DriveFor(a, st);
                }

                if (now >= st.NextScreamTime)
                {
                    ScreamInPanic(a, st, now);
                }
            }

        }

        private bool DriveFor(Agent agent, FleeState st)
        {
            try
            {
                if (st.NeedsSwim)
                {
                    if (!st.InWaterPhase)
                    {

                        if (st.Jumped)
                        {
                            return true;
                        }
                        st.Jumped = SotorNavalBridge.MakeAgentJumpOverboard(agent, st.TargetPoint);
                        return st.Jumped;
                    }

                    if (SotorNavalBridge.TryBoardViaClimbingNet(agent, st.TargetShipIndex))
                    {
                        return true;
                    }
                    agent.SetTargetPosition(st.TargetPoint.AsVec2);
                    return true;
                }

                agent.SetTargetPosition(st.TargetPoint.AsVec2);
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AbandonShip.DriveFor failed for '{agent?.Name}': {ex.Message}");
                return false;
            }
        }

        private void Release(Agent agent, FleeState st)
        {
            try
            {

                agent.StopRetreating();
                agent.ClearTargetFrame();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AbandonShip.Release failed for '{agent?.Name}': {ex.Message}");
            }
        }

        private static void ScreamInPanic(Agent a, FleeState st, float now)
        {
            try
            {
                a.MakeVoice(SkinVoiceManager.VoiceType.Fear, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                st.NextScreamTime = now + MBRandom.RandomFloatRanged(ScreamCooldownMin, ScreamCooldownMax);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AbandonShip.ScreamInPanic failed for '{a?.Name}': {ex.Message}");
            }
        }

        protected override void OnEndMission()
        {
            _fleeing.Clear();
        }
    }
}
