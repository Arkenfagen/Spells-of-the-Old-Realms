using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
#if BL13
using SotorSpawnLogic = TaleWorlds.MountAndBlade.MissionAgentSpawnLogic;
#else
using SotorSpawnLogic = TaleWorlds.MountAndBlade.DefaultBattleMissionAgentSpawnLogic;
#endif

namespace SOTOR.AbilitySystem.TriggeredScripts
{

    public static class SummonHelper
    {

        private const int AgentSafetyBuffer = 150;
        private const int MinSlotsForSummoning = 5;

        private static ActionIndexCache? _actRaiseFromGround;
        private static bool _riseAnimResolved;

        private static ActionIndexCache RiseFromGroundAction
        {
            get
            {
                if (!_riseAnimResolved)
                {
                    try { _actRaiseFromGround = ActionIndexCache.Create("act_raisefromground"); }
                    catch { _actRaiseFromGround = null; }
                    _riseAnimResolved = true;
                }
                return _actRaiseFromGround ?? ActionIndexCache.act_none;
            }
        }

        private static int GetCurrentAgentCount()
            => Mission.Current == null ? 0 : Mission.Current.AllAgents.Count;

        private static int GetMissionAgentLimit()
            => Math.Max(0, SotorSpawnLogic.MaxNumberOfAgentsForMission - AgentSafetyBuffer);

        public static int GetAvailableSummonSlots()
            => Math.Max(0, GetMissionAgentLimit() - GetCurrentAgentCount());

        public static bool CanSummon() => GetAvailableSummonSlots() >= MinSlotsForSummoning;

        public static int GetClampedSummonCount(int desiredCount)
            => Math.Min(desiredCount, GetAvailableSummonSlots());

        public static AgentBuildData GetAgentBuildData(Agent caster, string summonedUnitId)
        {
            var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(summonedUnitId);
            if (troop == null)
            {
                SotorLog.Warn($"SummonHelper: troop '{summonedUnitId}' not found (troop XML not loaded?).");
                return null;
            }

            var team = caster.Team;
            Formation formation = team?.GetFormation(FormationClass.Infantry) ?? caster.Formation;

            var origin = new BasicBattleAgentOrigin(troop);
            var dir = Vec2.Forward;

            return new AgentBuildData(troop)
                .Team(team)
                .Formation(formation)
                .ClothingColor1(team != null ? team.Color : uint.MaxValue)
                .ClothingColor2(team != null ? team.Color2 : uint.MaxValue)

                .Equipment(troop.RandomBattleEquipment)
                .TroopOrigin(origin)
                .IsReinforcement(true)
                .InitialDirection(dir);
        }

        public static Agent SpawnAgent(AgentBuildData buildData, Vec3 position, bool withAnimation)
        {

            Vec3 spawnPos = position;
            bool offMesh = Mission.Current.Scene.GetNavigationMeshForPosition(position) == UIntPtr.Zero;
            if (offMesh)
            {
                spawnPos = Mission.Current.GetRandomPositionAroundPoint(position, 0.05f, 5f, true);
            }
            bool stillBad = !spawnPos.IsValid || !spawnPos.IsNonZero
                            || Mission.Current.Scene.GetNavigationMeshForPosition(spawnPos) == UIntPtr.Zero;
            if (stillBad)
            {
                SotorLog.Warn($"SummonHelper.SpawnAgent: no valid navmesh at {position} (nudged {spawnPos}); spawning nothing.");
                return null;
            }

            buildData = buildData.InitialPosition(spawnPos);

            Agent agent = Mission.Current.SpawnAgent(buildData, false);
            if (agent == null)
            {
                return null;
            }

            agent.FadeIn();
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
            agent.SetWatchState(Agent.WatchState.Alarmed);

            if (withAnimation)
            {
                var rise = RiseFromGroundAction;
                if (rise.Index != ActionIndexCache.act_none.Index)
                {
                    agent.SetActionChannel(0, rise, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                    agent.SetCurrentActionProgress(0, 0f);
                    agent.SetCurrentActionSpeed(0, 1f);
                }
            }

            return agent;
        }
    }
}
