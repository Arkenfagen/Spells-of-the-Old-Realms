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

        private const float BlastCapacityPerRadius = 6f;

        public static Func<Target, float> BlastOccupancy(Agent caster, float blastRadius, float lingerSeconds)
        {
            return target =>
            {
                if (target?.Formation == null || blastRadius <= 0f) return 1f;
                float capacity = BlastCapacityPerRadius * blastRadius;
                if (capacity <= 0f) return 1f;
                var spot = CommonAIFunctions.FindBestBlastSpot(caster, target.Formation, blastRadius, lingerSeconds);
                return spot.EnemyCount / capacity;
            };
        }

        public static Func<Target, float> AllyDensityInBlast(Agent caster, float blastRadius)
        {
            return target =>
            {
                if (target == null) return 0f;
                var pos = target.GetPositionPrioritizeCalculated();
                if (pos == Vec3.Invalid || pos == Vec3.Zero) return 0f;
                return CommonAIFunctions.AllyDensityAtPosition(caster, pos, blastRadius);
            };
        }

        public static Func<Target, float> SpellWorth(Agent caster, float blastRadius, float damage, float windsCost,
            float duration = 0f, float tickInterval = 0f)
        {
            bool lingers = duration > 0f && tickInterval > 0f;
            float lingerSeconds = lingers ? duration : 0f;
            return target =>
            {
                if (target == null || damage <= 0f) return 0f;

                CommonAIFunctions.BlastAssessment blast;
                if (target.Formation != null)
                {
                    var spot = CommonAIFunctions.FindBestBlastSpot(caster, target.Formation, blastRadius, lingerSeconds);
                    blast = new CommonAIFunctions.BlastAssessment
                    {
                        EnemyPower = spot.EnemyPower,
                        AllyPower = spot.AllyPower,
                        EnemyCount = spot.EnemyCount
                    };
                }
                else
                {
                    var pos = target.GetPositionPrioritizeCalculated();
                    if (pos == Vec3.Invalid || pos == Vec3.Zero) return 0f;
                    blast = CommonAIFunctions.AssessBlast(caster, pos, blastRadius);
                }
                if (blast.EnemyPower <= 0f) return 0f;

                float effectiveDamage = damage;
                if (lingers)
                {

                    float speed = target.Formation?.QuerySystem?.MovementSpeedMaximum ?? 0f;

                    float dwell = duration;
                    if (speed > 0.01f)
                    {
                        float crossing = (blastRadius * 2f) / speed;
                        if (crossing < dwell) dwell = crossing;
                    }

                    float ticks = dwell / tickInterval;
                    if (ticks < 1f) ticks = 1f;
                    effectiveDamage = damage * ticks;
                }

                float worth = blast.EnemyPower * effectiveDamage / Math.Max(windsCost, 1f);

                LastWorthDetail = $"ep={blast.EnemyPower:0} n={blast.EnemyCount} dmg={effectiveDamage:0} "
                                  + $"winds={windsCost:0} r={blastRadius:0.#}";
                return worth;
            };
        }

        public static string LastWorthDetail { get; private set; } = string.Empty;

        public static Func<Target, float> TargetMobility()
        {
            return target =>
            {
                var f = target?.Formation;
                return f == null ? 0f : f.QuerySystem.MovementSpeedMaximum;
            };
        }
    }
}
