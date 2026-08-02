using System;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public static class CommonAIDecisionFunctions
    {
        public static Func<Target, float> WindsOfMagicRemainingRatio(Agent behaviorAgent)
        {
            var info = behaviorAgent.GetHero()?.GetExtendedInfo();
            return _ =>
            {
                if (info == null || info.MaxWindsOfMagic <= 0f)
                {
                    return 1f;
                }
                return info.WindsOfMagic / info.MaxWindsOfMagic;
            };
        }

        public static Func<Target, float> FormationUnderFire()
        {
            return target => target.Formation.QuerySystem.UnderRangedAttackRatio;
        }

        public static Func<Target, float> TargetDistanceToHostiles(Team team = null)
        {
            return target =>
            {
                if (team != null)
                {
                    var pos = target.TacticalPosition.Position.AsVec2;
                    return pos.Distance(team.QuerySystem.AverageEnemyPosition);
                }
                if (target.Formation != null)
                {
                    var closest = target.Formation.CachedClosestEnemyFormation;
                    if (closest == null || closest.Formation == null)
                    {
                        return float.MaxValue;
                    }
                    var here = target.GetPositionPrioritizeCalculated();
                    if (here == Vec3.Invalid)
                    {
                        return float.MaxValue;
                    }
                    return here.AsVec2.Distance(closest.Formation.CachedMedianPosition.AsVec2);
                }
                return 0f;
            };
        }

        public static Func<Target, float> DistanceToTarget(Func<Vec3> provider)
        {
            return target =>
            {
                var position = target.GetPosition();
                if (position == Vec3.Invalid)
                {
                    return float.MaxValue;
                }
                var from = provider();
                return from.Distance(position);
            };
        }

        public static Func<Target, float> FormationPower()
        {
            return target => target.Formation.QuerySystem.FormationPower;
        }

        public static Func<Target, float> RangedUnitRatio()
        {
            return target => target.Formation.QuerySystem.RangedUnitRatio;
        }

        public static Func<Target, float> CavalryUnitRatio()
        {
            return target => target.Formation.QuerySystem.CavalryUnitRatio;
        }

        public static Func<Target, float> Dispersedness()
        {
            return target => target.Formation.UnitSpacing;
        }

        public static Func<Target, float> TargetSpeed()
        {
            return target => target.Formation.CachedCurrentVelocity.Length;
        }

        public static Func<Target, float> BalanceOfPower(Agent agent)
        {
            return _ => agent.Team.QuerySystem.TeamPower
                / (CalculateEnemyTotalPower(agent.Team) + agent.Team.QuerySystem.TeamPower);
        }

        public static float CalculateEnemyTotalPower(Team chosenTeam)
        {
            float total = 0f;
            foreach (var team in Mission.Current.GetEnemyTeamsOf(chosenTeam))
            {
                total += team.QuerySystem.TeamPower;
            }
            return total;
        }

        public static float CalculateTeamTotalPower(Team chosenTeam)
        {
            return chosenTeam.QuerySystem.TeamPower;
        }
    }
}
