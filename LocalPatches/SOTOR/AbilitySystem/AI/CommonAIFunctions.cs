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
            return Math.Max(0f, Math.Min(1f, output));
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
    }
}
