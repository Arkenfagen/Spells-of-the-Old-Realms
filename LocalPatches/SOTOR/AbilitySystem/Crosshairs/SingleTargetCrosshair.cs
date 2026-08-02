using System;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem.Crosshairs
{

    public class SingleTargetCrosshair : MissileCrosshair
    {
        private Agent _cachedTarget;

        public Agent CachedTarget => _cachedTarget;
        public bool IsTargetLocked { get; private set; }

        public override Vec3 Position
        {
            get
            {
                if (_cachedTarget != null && _cachedTarget.IsActive())
                {
                    return _cachedTarget.GetChestGlobalPosition();
                }
                if (TryGetGroundAimPoint(out var aim))
                {
                    return aim;
                }
                return base.Position;
            }
        }

        private bool TryGetGroundAimPoint(out Vec3 point)
        {
            point = Vec3.Zero;
            if (_missionScreen == null || _caster == null)
            {
                return false;
            }
            try
            {

                if (TryResolveAimPoint(out Vec3 pos, out Vec3 _))
                {

                    if (_caster.Position.Distance(pos) > _template.MaxDistance)
                    {
                        pos = _caster.LookFrame.Advance(_template.MaxDistance).origin;
                        pos.z = ResolveSurfaceZ(pos);
                    }
                    point = pos;
                    return true;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SingleTargetCrosshair.TryGetGroundAimPoint failed: {ex.Message}");
            }
            return false;
        }

        public SingleTargetCrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
            : base(template, mission, missionScreen, caster)
        {
        }

        public override void Show()
        {
            base.Show();
            _cachedTarget = null;
        }

        public override void Hide()
        {
            base.Hide();
            UnlockTarget();
        }

        public override void Tick()
        {
            FindTarget();
        }

        private void FindTarget()
        {
            if (_mission == null || _missionScreen == null || _caster == null)
            {
                return;
            }

            _missionScreen.ScreenPointToWorldRay(Input.MousePositionRanged, out Vec3 rayBegin, out Vec3 rayEnd);

            Agent hit;
            float dist;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                hit = _mission.RayCastForClosestAgent(rayBegin, rayEnd, -1, 0.05f, out dist);
            }

            if (hit == null)
            {
                UnlockTarget();
                return;
            }

            if (hit.IsMount && hit.RiderAgent != null)
            {
                hit = hit.RiderAgent;
            }

            bool wantsAlly = _template.AbilityTargetType == AbilityTargetType.SingleAlly
                || _template.AbilityTargetType == AbilityTargetType.AlliesInAOE;
            bool sideOk = wantsAlly ? !hit.IsEnemyOf(_caster) : hit.IsEnemyOf(_caster);

            if (dist <= _template.MaxDistance && hit.IsActive() && hit.Health > 0f
                && !hit.IsFadingOut() && sideOk)
            {
                if (hit != _cachedTarget)
                {
                    UnlockTarget();
                }

                LockTarget(hit, wantsAlly ? friendColor : enemyColor);
            }
            else
            {
                UnlockTarget();
            }
        }

        private void LockTarget(Agent newTarget, uint? glowColor)
        {
            _cachedTarget = newTarget;
            SetContour(_cachedTarget, glowColor);
            IsTargetLocked = true;
        }

        public void UnlockTarget()
        {
            if (_cachedTarget != null)
            {
                SetContour(_cachedTarget, colorLess);
                _cachedTarget = null;
            }
            IsTargetLocked = false;
        }

        private static void SetContour(Agent agent, uint? color)
        {
            try
            {
                if (agent?.AgentVisuals != null)
                {
                    agent.AgentVisuals.SetContourColor(color, true);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SingleTargetCrosshair.SetContour failed: {ex.Message}");
            }
        }
    }
}
