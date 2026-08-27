using System;
using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.AI
{

    public static class SotorAimDiagnostics
    {

        public static bool ForceEnabled = false;
        public static bool Enabled => ForceEnabled || SotorLog.MinLevel <= SotorLog.Level.Debug;

        private const float PickInterval = 5f;
        private const float LosInterval = 5f;

        private const float RankInterval = 3f;

        private class CastIntent
        {
            public string AbilityId;
            public Vec3 AimPoint;
            public Agent AimAgent;
            public Vec3 SpawnOrigin;
            public float AimErrorDeg;
            public float MissionTime;
        }

        private static readonly Dictionary<int, CastIntent> _intents = new Dictionary<int, CastIntent>();

        private static readonly Dictionary<int, string> _lastPick = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> _lastLos = new Dictionary<int, string>();

        public static void Clear()
        {
            _intents.Clear();
            _lastPick.Clear();
            _lastLos.Clear();
            _lastPickTime.Clear();
            _lastLosTime.Clear();
            _lastRank.Clear();
            _lastRankTime.Clear();
        }

        private static readonly Dictionary<int, float> _lastPickTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> _lastLosTime = new Dictionary<int, float>();
        private static readonly Dictionary<int, string> _lastRank = new Dictionary<int, string>();
        private static readonly Dictionary<int, float> _lastRankTime = new Dictionary<int, float>();

        private static bool ShouldLog(Dictionary<int, string> keys, Dictionary<int, float> times,
            int index, string key, float minInterval)
        {
            float now = Mission.Current?.CurrentTime ?? 0f;
            bool keyChanged = !keys.TryGetValue(index, out var prev) || prev != key;
            bool timeUp = !times.TryGetValue(index, out var last) || now - last >= minInterval;
            if (!keyChanged && !timeUp) return false;
            keys[index] = key;
            times[index] = now;
            return true;
        }

        public static void LogTargetPick(Agent caster, AbilityTemplate template, string branch,
            Formation formation, Agent picked, Vec3 rawPos, Vec3 lead, Vec3 finalPos, string detail = null)
        {
            if (!Enabled || caster == null) return;
            try
            {
                if (!ShouldLog(_lastPick, _lastPickTime, caster.Index, $"{template?.StringID}|{branch}", PickInterval)) return;
                SotorLog.Info(
                    $"AimPick {template?.StringID} by {Who(caster)}: branch={branch} "
                    + $"formation={Describe(formation)} picked={picked?.Name ?? "(none)"} "
                    + $"rawPos={Short(rawPos)} lead={Short(lead)} |lead|={lead.Length:0.00}m "
                    + $"aimPoint={Short(finalPos)} distFromCaster={Dist(caster, finalPos):0.0}m"
                    + (detail != null ? " " + detail : string.Empty));
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogTargetPickMedian(Agent caster, AbilityTemplate template, Formation formation, Agent median)
        {
            if (!Enabled || caster == null) return;
            try
            {
                if (!ShouldLog(_lastPick, _lastPickTime, caster.Index, $"{template?.StringID}|median", PickInterval)) return;
                SotorLog.Info(
                    $"AimPick {template?.StringID} by {Who(caster)}: branch=median(<=10) "
                    + $"formation={Describe(formation)} picked={median?.Name ?? "(none)"} "
                    + $"NO velocity lead applied (small-formation branch) "
                    + $"targetVel={(median != null ? Short(median.Velocity) : "n/a")} "
                    + $"|targetVel|={(median != null ? median.Velocity.Length : 0f):0.00}m/s");
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogBehaviorRanking(Agent caster, List<IAgentBehavior> available, BehaviorOption winner)
        {
            if (!Enabled || caster == null || available == null) return;
            try
            {
                if (!ShouldLog(_lastRank, _lastRankTime, caster.Index, "rank", RankInterval)) return;

                var rows = new List<string>();
                foreach (var behavior in available)
                {
                    var cast = behavior as AbstractAgentCastingBehavior;
                    var scores = cast?.LatestScores;
                    if (scores == null || scores.Count == 0) continue;

                    BehaviorOption best = null;
                    foreach (var o in scores)
                    {
                        if (best == null || o.UtilityValue > best.UtilityValue) best = o;
                    }
                    if (best == null) continue;

                    string name = cast.AbilityTemplate?.StringID ?? cast.GetType().Name;

                    string why = best.UtilityValue <= 0f ? $" ({cast.DescribeZeroScore(best.Target)})" : string.Empty;

                    var parts = new List<string>();
                    foreach (var axis in cast.Axes)
                    {
                        if (!axis.IsActive(best.Target)) continue;
                        axis.Evaluate(best.Target);

                        parts.Add($"{axis.Name}={axis.LastValue:0.00}/raw={axis.LastRaw:0.##}"
                                  + (axis.LastClamped ? $"!max={axis.Max:0.##}" : string.Empty)
                                  + (axis.Name == "worth"
                                     ? $"<{CommonAIDecisionFunctions.LastWorthDetail}>" : string.Empty));
                    }

                    rows.Add($"{name}={best.UtilityValue:0.000}{why}"
                             + $"[{(best.Target?.Formation != null ? Describe(best.Target.Formation) : "no formation")}]"
                             + (parts.Count > 0 ? "{" + string.Join(" ", parts.ToArray()) + "}" : string.Empty));
                }

                rows.Sort((a, b) => string.CompareOrdinal(b.Substring(b.IndexOf('=') + 1), a.Substring(a.IndexOf('=') + 1)));

                var winnerName = (winner?.Behavior as AbstractAgentCastingBehavior)?.AbilityTemplate?.StringID
                                 ?? winner?.Behavior?.GetType().Name ?? "(none)";
                SotorLog.Info($"AimRank {Who(caster)}: picked {winnerName}={winner?.UtilityValue ?? 0f:0.000} | "
                              + string.Join("  ", rows.ToArray()));
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogLineOfSight(Agent caster, AbilityTemplate template, Vec3 targetPos, float dist,
            Agent blocker, bool blockerIsEnemy, float blockerToTarget, float terrainDist, float rayLen,
            bool verdict, string reason)
        {
            if (!Enabled || caster == null) return;
            try
            {

                if (!ShouldLog(_lastLos, _lastLosTime, caster.Index, $"{template?.StringID}|{verdict}|{reason}", LosInterval)) return;
                SotorLog.Info(
                    $"AimLOS {template?.StringID} by {Who(caster)}: {(verdict ? "CLEAR" : "BLOCKED")} ({reason}) "
                    + $"targetPos={Short(targetPos)} dist={dist:0.0}m range=[{template?.MinDistance:0.0}..{template?.MaxDistance:0.0}] "
                    + $"blocker={(blocker != null ? blocker.Name + (blockerIsEnemy ? " [enemy]" : " [FRIENDLY]") : "(none)")} "
                    + $"blockerToTarget={(blocker != null ? blockerToTarget : -1f):0.0}m "
                    + $"terrainDist={(float.IsNaN(terrainDist) ? -1f : terrainDist):0.0} rayLen={rayLen:0.0}");
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogCastGeometry(Agent caster, Ability ability, MatrixFrame frame)
        {
            if (!Enabled || caster == null || ability == null) return;
            try
            {
                if (caster.IsPlayerControlled || caster.IsMainAgent) return;

                var template = ability.Template;
                var target = CurrentTargetOf(caster);
                Vec3 aimPoint = target != null ? target.GetPositionPrioritizeCalculated() : Vec3.Invalid;
                var aimAgent = target?.Agent;

                Vec3 origin = frame.origin;
                Vec3 forward = frame.rotation.f;

                string headline;
                float aimErrDeg = float.NaN;
                float missAtRange = float.NaN;
                float originErr = float.NaN;
                float rangeToTarget = float.NaN;

                var effect = template?.AbilityEffectType;
                bool placed = effect == AbilityEffectType.Bombardment || effect == AbilityEffectType.Vortex
                    || effect == AbilityEffectType.Hex || effect == AbilityEffectType.Heal
                    || effect == AbilityEffectType.Augment || effect == AbilityEffectType.Summoning;

                if (aimPoint == Vec3.Invalid)
                {
                    headline = "NO AIM POINT (brain has no current target)";
                }
                else
                {
                    Vec3 toTarget = aimPoint - origin;
                    rangeToTarget = toTarget.Length;
                    originErr = origin.Distance(aimPoint);

                    if (rangeToTarget > 0.001f && forward.Length > 0.001f)
                    {
                        float dot = Vec3.DotProduct(forward.NormalizedCopy(), toTarget.NormalizedCopy());
                        dot = MBMath.ClampFloat(dot, -1f, 1f);
                        aimErrDeg = (float)(Math.Acos(dot) * 180.0 / Math.PI);

                        missAtRange = rangeToTarget * (float)Math.Sin(aimErrDeg * Math.PI / 180.0);
                    }
                    if (placed)
                    {

                        float flatMiss = (aimPoint.AsVec2 - origin.AsVec2).Length;
                        headline = flatMiss < 2f ? "PLACED ON TARGET" : $"PLACED {flatMiss:0.0}m OFF";
                        aimErrDeg = float.NaN;
                        missAtRange = flatMiss;
                    }
                    else
                    {
                        headline = aimErrDeg < 2f ? "ON TARGET" : (aimErrDeg < 10f ? "OFF by a little" : "POINTED SOMEWHERE ELSE");
                    }
                }

                Vec3 bodyF = caster.Frame.rotation.f;
                Vec3 lookF = caster.LookFrame.rotation.f;
                Vec3 wanted = (aimPoint != Vec3.Invalid) ? (aimPoint - origin).NormalizedCopy() : Vec3.Zero;

                SotorLog.Info(
                    $"AimCast {template?.StringID} ({template?.AbilityEffectType}) by {Who(caster)}: {headline} "
                    + $"aimErr={aimErrDeg:0.0}deg missAtRange={missAtRange:0.0}m originErr={originErr:0.0}m "
                    + $"range={rangeToTarget:0.0}m");
                SotorLog.Info(
                    $"AimCast {template?.StringID} detail: spawnOrigin={Short(origin)} aimPoint={Short(aimPoint)} "
                    + $"aimAgent={aimAgent?.Name ?? "(formation only)"} "
                    + $"spawnFwd={Short(forward)} wantedFwd={Short(wanted)} "
                    + $"bodyFwd={Short(bodyF)} lookFwd={Short(lookF)} "
                    + $"casterPos={Short(caster.Position)} eye={Short(caster.GetEyeGlobalPosition())} "
                    + $"mounted={caster.HasMount} behavior={BehaviorName(caster)}");

                if (template?.SeekerParameters != null)
                {
                    SotorLog.Info(
                        $"AimCast {template.StringID}: HOMING spell, seeker target is chosen by Ability.TryCast "
                        + $"(nearest enemy), not by the brain. Brain wanted '{aimAgent?.Name ?? "(formation)"}'.");
                }

                _intents[caster.Index] = new CastIntent
                {
                    AbilityId = template?.StringID,
                    AimPoint = aimPoint,
                    AimAgent = aimAgent,
                    SpawnOrigin = origin,
                    AimErrorDeg = aimErrDeg,
                    MissionTime = Mission.Current?.CurrentTime ?? 0f
                };
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogSeekerTarget(Agent caster, Ability ability, Agent seekerTarget)
        {
            if (!Enabled || caster == null) return;
            try
            {
                if (caster.IsPlayerControlled || caster.IsMainAgent) return;
                var brainTarget = CurrentTargetOf(caster)?.Agent;
                bool agree = brainTarget != null && seekerTarget != null && brainTarget.Index == seekerTarget.Index;

                SotorLog.Info(
                    $"AimSeeker {ability?.Template?.StringID} by {Who(caster)}: "
                    + $"seeker='{seekerTarget?.Name ?? "(none)"}'#{seekerTarget?.Index.ToString() ?? "-"} "
                    + $"brain='{brainTarget?.Name ?? "(none)"}'#{brainTarget?.Index.ToString() ?? "-"} -> {(agree ? "AGREE" : "DISAGREE")}"
                    + (seekerTarget != null ? $" seekerDist={caster.Position.Distance(seekerTarget.Position):0.0}m" : "")
                    + (brainTarget != null ? $" brainDist={caster.Position.Distance(brainTarget.Position):0.0}m" : ""));
            }
            catch (Exception ex) { Swallow(ex); }
        }

        public static void LogImpact(Agent caster, Ability ability, Vec3 impactPos, Agent hitAgent, string kind)
        {
            if (!Enabled || caster == null || ability == null) return;
            try
            {
                if (caster.IsPlayerControlled || caster.IsMainAgent) return;
                if (!_intents.TryGetValue(caster.Index, out var intent)) return;
                if (intent.AbilityId != ability.Template?.StringID) return;

                float missFromAim = intent.AimPoint == Vec3.Invalid ? float.NaN : impactPos.Distance(intent.AimPoint);
                float missFromNow = float.NaN;
                bool targetStillAlive = intent.AimAgent != null && intent.AimAgent.IsActive();
                if (targetStillAlive)
                {
                    missFromNow = impactPos.Distance(intent.AimAgent.Position);
                }

                bool hitIntended = hitAgent != null && intent.AimAgent != null && hitAgent.Index == intent.AimAgent.Index;
                float flight = (Mission.Current?.CurrentTime ?? 0f) - intent.MissionTime;

                SotorLog.Info(
                    $"AimImpact {ability.Template?.StringID} by {Who(caster)}: {kind} "
                    + $"{(hitAgent != null ? (hitIntended ? "HIT THE INTENDED TARGET" : $"hit '{hitAgent.Name}' (NOT the intended target)") : "hit nobody")} "
                    + $"impact={Short(impactPos)} "
                    + $"missFromAimPoint={missFromAim:0.0}m missFromTargetNow={missFromNow:0.0}m "
                    + $"intended='{intent.AimAgent?.Name ?? "(formation)"}'{(targetStillAlive ? "" : " (gone)")} "
                    + $"castAimErr={intent.AimErrorDeg:0.0}deg flight={flight:0.00}s "
                    + $"flightDist={impactPos.Distance(intent.SpawnOrigin):0.0}m");

                _intents.Remove(caster.Index);
            }
            catch (Exception ex) { Swallow(ex); }
        }

        private static Target CurrentTargetOf(Agent agent)
        {
            return agent?.GetComponent<WizardAIComponent>()?.CurrentCastingBehavior?.CurrentTarget;
        }

        private static string BehaviorName(Agent agent)
        {
            var b = agent?.GetComponent<WizardAIComponent>()?.CurrentCastingBehavior;
            return b == null ? "(none)" : b.GetType().Name;
        }

        private static string Who(Agent agent)
        {
            if (agent == null) return "(null)";
            string name = agent.Name ?? "(unnamed)";
            var team = agent.Team;
            if (team == null) return name + " [AI, no team]";
            return name + (team.IsPlayerTeam || team.IsPlayerAlly ? " [ALLY-AI]" : " [ENEMY-AI]");
        }

        private static string Describe(Formation f)
        {
            if (f == null) return "(none)";
            return $"{f.PhysicalClass}/{f.CountOfUnitsWithoutDetachedOnes}u";
        }

        private static float Dist(Agent a, Vec3 p)
        {
            return a == null || p == Vec3.Invalid ? -1f : a.Position.Distance(p);
        }

        private static string Short(Vec3 v)
        {
            if (v == Vec3.Invalid) return "(invalid)";
            return $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
        }

        private static void Swallow(Exception ex)
        {
            SotorLog.Warn($"SotorAimDiagnostics: diagnostic failed harmlessly: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
