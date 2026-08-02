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

            return !mission.IsFriendlyMission
                && (int)mission.CombatType != 1
                && (int)mission.CombatType != 2;
        }
    }
}
