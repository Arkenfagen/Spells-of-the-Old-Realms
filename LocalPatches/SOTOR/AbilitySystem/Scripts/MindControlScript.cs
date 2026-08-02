using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class MindControlScript : AbilityScript
    {
        public const int MaxTargets = 10;
        private const string ConvertMarkParticle = "general_life_buff";

        private bool _done;

        protected override void OnAfterTick(float dt)
        {
            if (_done || CasterAgent == null || Ability == null)
            {
                return;
            }

            if (!HasTickedOnce)
            {
                return;
            }
            _done = true;

            try
            {
                DoMindControl();
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"MindControlScript.DoMindControl failed: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        private void DoMindControl()
        {
            Mission mission = Mission.Current;
            if (mission == null)
            {
                return;
            }

            var caster = CasterAgent;
            var casterTeam = caster.Team;
            if (casterTeam == null)
            {
                return;
            }

            var casterHero = SOTOR.Extensions.AgentExtensions.GetHero(caster);
            int casterLevel = caster.Character != null ? caster.Character.Level : 1;

            float radius = Ability.Template.Radius > 0f ? Ability.Template.Radius : 5f;
            Vec3 center = CurrentGlobalPosition;

            var nearby = mission.GetNearbyEnemyAgents(center.AsVec2, radius, casterTeam, new MBList<Agent>());
            var logic = mission.GetMissionBehavior<SotorMindControlMissionLogic>();

            var chosen = ((IEnumerable<Agent>)nearby)
                .OrderBy(_ => MBRandom.RandomFloat)
                .Take(MaxTargets);

            int tries = 0;
            foreach (var target in chosen)
            {
                if (target == null || !target.IsActive() || target.IsFadingOut())
                {
                    continue;
                }
                tries++;

                int enemyLevel = target.Character != null ? target.Character.Level : casterLevel;
                float hpFraction = target.HealthLimit > 0f ? target.Health / target.HealthLimit : 1f;
                float chance = SotorMindControlHelper.GetTargetChance(casterHero, casterLevel, enemyLevel, hpFraction);

                if (MBRandom.RandomFloat < chance)
                {
                    Convert(target, casterTeam, logic);
                }
            }

            SotorLog.Info($"MindControl: cast by '{caster.Name}' at {center}, {tries} target(s) rolled (radius {radius}).");
        }

        private void Convert(Agent target, Team casterTeam, SotorMindControlMissionLogic logic)
        {

            target.SetTeam(casterTeam, false);

            logic?.OnAgentConverted(target);

            ApplyConvertMark(target);
        }

        private void ApplyConvertMark(Agent target)
        {
            try
            {
                var visuals = target.AgentVisuals;
                var skeleton = visuals?.GetSkeleton();
                if (skeleton == null || !skeleton.IsValid)
                {
                    return;
                }
                var scene = Mission.Current?.Scene;
                if (scene == null)
                {
                    return;
                }
                var child = TaleWorlds.Engine.GameEntity.CreateEmpty(scene, true, true, true);
                var frame = MatrixFrame.Identity;
                var ps = ParticleSystem.CreateParticleSystemAttachedToEntity(ConvertMarkParticle, child, ref frame);
                if (ps == null)
                {
                    child.Remove(0);
                    return;
                }
                visuals.AddChildEntity(child);

                sbyte bone = 1;
                if (bone < skeleton.GetBoneCount())
                {
                    skeleton.AddComponentToBone(bone, ps);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Debug($"MindControlScript.ApplyConvertMark: {ex.Message} (particle not carved yet?).");
            }
        }
    }
}
