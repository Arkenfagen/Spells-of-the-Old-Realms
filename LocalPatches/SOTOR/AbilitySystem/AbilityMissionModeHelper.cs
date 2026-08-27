using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{
    internal static class AbilityMissionModeHelper
    {
        public static bool IsAbilityHudMissionMode(Mission mission)
        {
            if (mission == null)
            {
                return false;
            }

            var mode = (int)mission.Mode;
            return mode == 2 || mode == 4;
        }

        public static bool IsBattleAbilityContext(Mission mission)
        {
            if (mission == null)
            {
                return false;
            }

            if (IsAbilityHudMissionMode(mission))
            {
                return true;
            }

            return IsMagicAllowedInMission(mission);
        }

        private const int CombatTypeArena = 1;
        private const int CombatTypeNone = 2;

        public static bool IsArenaMission(Mission mission)
        {
            return mission != null && (int)mission.CombatType == CombatTypeArena;
        }

        private static Mission _arenaCacheMission;
        private static bool _arenaCacheResult;

        public static bool IsArenaOrTournamentMission(Mission mission)
        {
            if (mission == null) return false;
            if (ReferenceEquals(mission, _arenaCacheMission)) return _arenaCacheResult;

            bool found = false;
            try
            {
                foreach (var behavior in mission.MissionBehaviors)
                {
                    string name = behavior?.GetType().Name;
                    if (name == "TournamentFightMissionController"
                        || name == "ArenaPracticeFightMissionController")
                    {
                        found = true;
                        break;
                    }
                }
            }
            catch
            {
                found = false;
            }

            _arenaCacheMission = mission;
            _arenaCacheResult = found;
            return found;
        }

        public static bool IsMagicAllowedInMission(Mission mission)
        {
            if (mission == null) return false;

            if ((int)mission.CombatType == CombatTypeNone) return false;

            if (!mission.IsFriendlyMission) return true;

            if (!IsAbilityHudMissionMode(mission)) return false;

            if (IsArenaOrTournamentMission(mission)) return true;

            return true;
        }
    }
}
