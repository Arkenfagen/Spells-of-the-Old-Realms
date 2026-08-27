using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public interface IAgentBehavior
    {
        void Execute();
        void Terminate();
        List<BehaviorOption> CalculateUtility();
        void SetCurrentTarget(Target target);
    }

    public class BehaviorOption
    {
        public Target Target;
        public IAgentBehavior Behavior;
        public float UtilityValue;
    }

    public class Target : Threat
    {
        public Vec3 SelectedWorldPosition = Vec3.Zero;
        public TacticalPosition TacticalPosition;

        public float UtilityValue
        {
            get => ThreatValue;
            set => ThreatValue = value;
        }

        public new Agent Agent
        {
            get
            {
                if (base.Agent == null && base.Formation != null)
                {
                    return base.Formation.GetMedianAgent(false, false,
                        SelectedWorldPosition == Vec3.Zero ? base.Formation.CurrentPosition : SelectedWorldPosition.AsVec2);
                }
                return base.Agent;
            }
            set => base.Agent = value;
        }

        public Vec3 Position => GetPosition();

        public Vec3 GetPosition()
        {
            try
            {
                if (Agent != null)
                {
                    return Agent.CollisionCapsuleCenter;
                }
                if (base.Formation != null)
                {
                    var medianAgent = base.Formation.GetMedianAgent(false, false, base.Formation.GetAveragePositionOfUnits(false, false));
                    if (medianAgent != null)
                    {
                        return medianAgent.Position;
                    }
                    return Vec3.Invalid;
                }
                if (SelectedWorldPosition != Vec3.Zero)
                {
                    return SelectedWorldPosition;
                }
                if (TacticalPosition != null)
                {
                    var position = TacticalPosition.Position;
                    return position.GetGroundVec3MT();
                }
                return Vec3.Invalid;
            }
            catch (NullReferenceException)
            {
                return Vec3.Invalid;
            }
        }

        public Vec3 GetPositionPrioritizeCalculated()
        {
            if (SelectedWorldPosition != Vec3.Zero)
            {
                return SelectedWorldPosition;
            }
            if (TacticalPosition != null)
            {
                var position = TacticalPosition.Position;
                return position.GetGroundVec3MT();
            }
            try
            {
                return Position;
            }
            catch (NullReferenceException)
            {
                return Vec3.Invalid;
            }
        }
    }

    public class Axis
    {
        private readonly float _min;
        private readonly float _max;
        private readonly float _range;
        private readonly Func<float, float> _outputFunction;
        private readonly Func<Target, float> _parameterFunction;
        private readonly Func<Target, bool> _activationFunction;

        public string Name { get; set; } = "axis";

        public float Max => _max;
        public float LastRaw { get; private set; }
        public float LastValue { get; private set; }
        public bool LastClamped { get; private set; }

        public Axis(float minInput, float maxInput, Func<float, float> outputFunction,
            Func<Target, float> parameterFunction, Func<Target, bool> activationFunction = null)
        {
            _min = minInput;
            _max = maxInput;
            _range = maxInput - minInput;
            _outputFunction = outputFunction;
            _parameterFunction = parameterFunction;
            _activationFunction = activationFunction;
        }

        public float Evaluate(Target target)
        {
            float val = _parameterFunction(target);
            float clamped = Math.Max(_min, Math.Min(_max, val));
            float arg = _range > 0f ? (clamped - _min) / _range : 0f;
            float output = _outputFunction(arg);
            float result = Math.Max(0f, Math.Min(1f, output));

            LastRaw = val;
            LastClamped = val > _max || val < _min;
            LastValue = result;
            return result;
        }

        public bool IsActive(Target target)
        {
            return _activationFunction == null || _activationFunction(target);
        }
    }

    public static class AxisExtensions
    {
        public static float GeometricMean(this List<Axis> axes, Target target)
        {
            var active = axes.FindAll(axis => axis.IsActive(target));
            var scores = active.Select(axis => axis.Evaluate(target)).ToList();
            return target.UtilityValue = !scores.Any()
                ? 0f
                : (float)Math.Pow(scores.Aggregate((a, x) => a * x), 1.0 / active.Count);
        }
    }

    public static class ScoringFunctions
    {
        public static Func<float, float> Logistic(float mid = 0f, float L = 1f, float k = 10f, float m = 1f)
        {
            return x => (float)(L / (1.0 + m * Math.Pow(Math.E, (0f - k) * (x - mid))));
        }
    }

    public static class DecisionManager
    {
        public static BehaviorOption EvaluateCastingBehaviors(List<IAgentBehavior> behaviors)
        {

            return TaleWorlds.Core.Extensions.MaxBy(
                behaviors.SelectMany(behavior => behavior.CalculateUtility()),
                option => option.Target.UtilityValue);
        }

        public static List<BehaviorOption> RankCastingBehaviors(List<IAgentBehavior> behaviors)
        {
            var all = behaviors.SelectMany(behavior => behavior.CalculateUtility()).ToList();
            all.Sort((a, b) => b.Target.UtilityValue.CompareTo(a.Target.UtilityValue));
            return all;
        }
    }

    public static class CastingAiTeamExtensions
    {
        public static List<Team> GetEnemyTeams(this Team team)
        {
            return Mission.Current.Teams.Where(x => x.IsEnemyOf(team)).ToList();
        }

        public static List<Team> GetAllyTeams(this Team team)
        {
            return Mission.Current.Teams.Where(x => x.IsFriendOf(team)).ToList();
        }

        public static List<Formation> GetFormations(this Team team)
        {
            return team.FormationsIncludingEmpty.ToList().FindAll(form => form.CountOfUnits > 0);
        }

        public static List<Team> GetEnemyTeamsOf(this Mission mission, Team team)
        {
            return mission.Teams.Where(x => x.IsEnemyOf(team)).ToList();
        }
    }

    public static class CommonAIStateFunctions
    {

        public static bool CanAgentMoveFreely(Agent agent)
        {
            var formation = agent?.Formation;
            if (formation == null)
            {
                return false;
            }

            ref readonly MovementOrder order = ref formation.GetReadonlyMovementOrderReference();
            var orderType = order.OrderType;

            if (orderType == OrderType.Charge || orderType == OrderType.ChargeWithTarget)
            {
                return true;
            }

            var active = formation.AI?.ActiveBehavior;
            return active != null && active.GetType().Name.Contains("Skirmish");
        }
    }

    public static class CommonAIFunctions
    {

        private static int _rollCounter;

        public static Agent GetRandomAgent(Formation targetFormation)
        {
            if (targetFormation == null)
            {
                return null;
            }
            var median = targetFormation.GetMedianAgent(true, false, targetFormation.GetAveragePositionOfUnits(true, false));
            if (median == null)
            {
                return null;
            }

            _rollCounter = (_rollCounter + 1) & 0x7fffffff;
            float t1 = ((_rollCounter * 1103515245 + 12345) & 0x7fffffff) / (float)0x7fffffff;
            float t2 = ((_rollCounter * 1140671485 + 12820163) & 0x7fffffff) / (float)0x7fffffff;

            Vec3 pos = median.Position;
            Vec2 dir = targetFormation.QuerySystem.EstimatedDirection;
            Vec2 right = dir.RightVec();
            pos += dir.ToVec3(0f) * (t1 * targetFormation.Depth - targetFormation.Depth / 2f);
            float width = targetFormation.Width * 0.9f;
            pos += right.ToVec3(0f) * (t2 * width - width / 2f);
            return targetFormation.GetMedianAgent(true, false, pos.AsVec2);
        }

        private const int MinAlliesToCare = 3;

        private static readonly MBList<Agent> _allyScratch = new MBList<Agent>();
        private static readonly MBList<Agent> _enemyScratch = new MBList<Agent>();

        public struct BlastAssessment
        {
            public float AllyPower;
            public float EnemyPower;
            public int AllyCount;
            public int EnemyCount;
        }

        public static BlastAssessment AssessBlast(Agent caster, Vec3 position, float radius)
        {
            var result = new BlastAssessment();
            if (caster == null || caster.Team == null || Mission.Current == null || radius <= 0f)
            {
                return result;
            }

            try
            {
                _allyScratch.Clear();
                _enemyScratch.Clear();
                var allies = Mission.Current.GetNearbyAllyAgents(position.AsVec2, radius, caster.Team, _allyScratch);
                var enemies = Mission.Current.GetNearbyEnemyAgents(position.AsVec2, radius, caster.Team, _enemyScratch);

                if (allies != null)
                {
                    foreach (var a in allies)
                    {
                        if (a == null || a == caster || !a.IsHuman) continue;
                        result.AllyCount++;
                        result.AllyPower += PowerOf(a);
                    }
                }
                if (enemies != null)
                {
                    foreach (var e in enemies)
                    {
                        if (e == null || !e.IsHuman) continue;
                        result.EnemyCount++;
                        result.EnemyPower += PowerOf(e);
                    }
                }
            }
            catch
            {

            }
            return result;
        }

        private static float PowerOf(Agent agent)
        {
            try
            {
                var c = agent?.Character;
                return c != null ? c.GetPower() : 10f;
            }
            catch
            {
                return 10f;
            }
        }

        public static float AllyDensityAtPosition(Agent caster, Vec3 position, float radius)
        {
            if (caster == null || caster.Team == null || Mission.Current == null || radius <= 0f)
            {
                return 0f;
            }

            var blast = AssessBlast(caster, position, radius);

            if (blast.AllyCount < MinAlliesToCare) return 0f;

            float total = blast.AllyPower + blast.EnemyPower;
            return total <= 0f ? 0f : blast.AllyPower / total;
        }

        public static Agent GetAimAgentPreferringFewerAllies(Agent caster, Formation targetFormation, float blastRadius, int samples = 3)
        {
            return FindBestBlastSpot(caster, targetFormation, blastRadius, 0f, samples).Agent;
        }

        public struct BlastSpot
        {
            public Agent Agent;
            public Vec3 Position;
            public float EnemyPower;
            public float AllyPower;
            public int EnemyCount;
            public float Score;
        }

        private const float ApproachWeight = 0.25f;

        private const float ApproachReachRadii = 1f;

        private const float AllyCostWeight = 1.5f;

        private const float SpotMemoSeconds = 0.35f;

        private struct SpotMemo
        {
            public BlastSpot Spot;
            public float Time;
        }

        private static readonly Dictionary<long, SpotMemo> _spotMemo = new Dictionary<long, SpotMemo>();
        private static float _spotMemoSweep;

        public static void ClearBlastSpotMemo()
        {
            _spotMemo.Clear();
            _spotMemoSweep = 0f;
        }

        public static BlastSpot FindBestBlastSpot(Agent caster, Formation targetFormation, float blastRadius,
            float lingerSeconds, int samples = 9)
        {

            try
            {

                float now = Mission.Current?.CurrentTime ?? 0f;
                long key = ((long)(caster?.Index ?? -1) << 40)
                           ^ ((long)(targetFormation?.Index ?? -1) << 20)
                           ^ (long)(blastRadius * 100f);

                if (_spotMemo.TryGetValue(key, out var memo))
                {
                    float age = now - memo.Time;
                    if (age >= 0f && age < SpotMemoSeconds)
                    {
                        var cached = memo.Spot;

                        if (cached.Agent != null && cached.Agent.IsActive())
                        {
                            cached.Position = cached.Agent.Position;
                            return cached;
                        }
                    }
                }

                if (now - _spotMemoSweep > 30f || now < _spotMemoSweep)
                {
                    _spotMemo.Clear();
                    _spotMemoSweep = now;
                }

                var fresh = FindBestBlastSpotUncached(caster, targetFormation, blastRadius, lingerSeconds, samples);
                _spotMemo[key] = new SpotMemo { Spot = fresh, Time = now };
                return fresh;
            }
            catch (Exception ex)
            {

                _spotMemo.Clear();
                LogNoBlastSpot(targetFormation, ex);
                return NoBlastSpot();
            }
        }

        private static BlastSpot NoBlastSpot()
        {
            return new BlastSpot { Agent = null, Position = Vec3.Invalid, Score = float.MinValue };
        }

        private static readonly HashSet<int> _loggedNoSpot = new HashSet<int>();

        private static void LogNoBlastSpot(Formation formation, Exception ex)
        {
            int key = formation?.Index ?? -1;
            if (!_loggedNoSpot.Add(key)) return;
            SotorLog.Debug($"CastingAI: no blast spot for formation {key} "
                           + $"(units={formation?.CountOfUnits ?? -1}): {ex.GetType().Name}. Spell scores 0.");
        }

        private static BlastSpot FindBestBlastSpotUncached(Agent caster, Formation targetFormation, float blastRadius,
            float lingerSeconds, int samples)
        {
            var result = new BlastSpot { Score = float.MinValue };
            if (targetFormation == null || caster == null || blastRadius <= 0f)
            {
                var fallback = GetRandomAgent(targetFormation);
                result.Agent = fallback;
                result.Position = fallback?.Position ?? Vec3.Invalid;
                return result;
            }

            float approach = 0f;
            if (lingerSeconds > 0f)
            {
                float speed = targetFormation.QuerySystem?.MovementSpeedMaximum ?? 0f;
                approach = speed * lingerSeconds;
                if (approach > blastRadius * ApproachReachRadii) approach = blastRadius * ApproachReachRadii;
            }

            var median = targetFormation.GetMedianAgent(false, false, targetFormation.GetAveragePositionOfUnits(false, false));
            for (int i = 0; i <= samples; i++)
            {
                var candidate = (i == 0) ? median : GetRandomAgent(targetFormation);
                if (candidate == null) continue;

                var pos = candidate.Position;
                var inside = AssessBlast(caster, pos, blastRadius);
                float enemy = inside.EnemyPower;
                float ally = inside.AllyPower;
                int count = inside.EnemyCount;

                if (approach > 0f)
                {

                    var wider = AssessBlast(caster, pos, blastRadius + approach);
                    enemy += (wider.EnemyPower - inside.EnemyPower) * ApproachWeight;
                    ally += (wider.AllyPower - inside.AllyPower) * ApproachWeight;
                    count += (int)((wider.EnemyCount - inside.EnemyCount) * ApproachWeight);
                }

                float score = enemy - ally * AllyCostWeight;
                if (score > result.Score)
                {
                    result.Agent = candidate;
                    result.Position = pos;
                    result.EnemyPower = enemy;
                    result.AllyPower = ally;
                    result.EnemyCount = count;
                    result.Score = score;
                }
            }

            if (result.Agent == null)
            {
                result.Agent = median;
                result.Position = median?.Position ?? Vec3.Invalid;
            }
            return result;
        }
    }
}
