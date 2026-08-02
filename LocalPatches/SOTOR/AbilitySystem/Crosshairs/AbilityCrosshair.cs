using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem.Crosshairs
{

    public abstract class AbilityCrosshair : IDisposable, ICrosshair
    {

        protected readonly uint? friendColor = new Color(0f, 0.255f, 0f, 1f).ToUnsignedInteger();
        protected readonly uint? enemyColor = new Color(0.255f, 0f, 0f, 1f).ToUnsignedInteger();
        protected readonly uint? colorLess = new Color(0f, 0f, 0f, 0f).ToUnsignedInteger();

        protected readonly AbilityTemplate _template;
        protected readonly Mission _mission;
        protected readonly MissionScreen _missionScreen;
        protected readonly Agent _caster;

        private List<uint> _cycleColors;
        private int _colorIndex;

        protected void CycleColor(GameEntity runeEntity)
        {
            if (runeEntity == null) return;
            if (_cycleColors == null) BuildCycleColors();

            _colorIndex = (_colorIndex + 1) % _cycleColors.Count;
            runeEntity.SetFactorColor(_cycleColors[_colorIndex]);
        }

        private void BuildCycleColors()
        {
            _cycleColors = new List<uint>();
            float r = 0.255f, g = 0f, b = 0f;
            for (g = 0f; g < 0.254f; g += 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
            for (r = 0.254f; r > 0.001f; r -= 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
            for (b = 0f; b < 0.254f; b += 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
            for (g = 0.254f; g > 0.001f; g -= 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
            for (r = 0f; r < 0.254f; r += 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
            for (b = 0.254f; b > 0.001f; b -= 0.001f) _cycleColors.Add(new Color(r, g, b, 1f).ToUnsignedInteger());
        }

        public CrosshairType CrosshairType { get; }

        public virtual bool IsVisible { get; protected set; }

        public virtual Vec3 Position => _caster != null ? _caster.GetChestGlobalPosition() : Vec3.Zero;

        public virtual MatrixFrame Frame =>
            _caster != null ? _caster.LookFrame : MatrixFrame.Identity;

        protected AbilityCrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
        {
            _template = template;
            _mission = mission;
            _missionScreen = missionScreen;
            _caster = caster;
            CrosshairType = template.CrosshairType;
        }

        public bool TryGetCameraAimDirection(out Vec3 direction)
        {
            direction = Vec3.Forward;
            if (_missionScreen == null || _caster == null)
            {
                return false;
            }

            try
            {

                _missionScreen.ScreenPointToWorldRay(Input.MousePositionRanged, out Vec3 rayBegin, out Vec3 rayEnd);
                Vec3 segment = rayEnd - rayBegin;
                if (segment.LengthSquared < 1e-6f)
                {
                    return false;
                }

                Vec3 aimPoint;
                float agentDist;
                Agent hitAgent;
                using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
                {
                    hitAgent = _mission.RayCastForClosestAgent(rayBegin, rayEnd, -1, 0.05f, out agentDist);
                }

                aimPoint = hitAgent != null ? hitAgent.GetChestGlobalPosition() : rayEnd;

                Vec3 eye = _caster.GetEyeGlobalPosition();
                Vec3 aimDir = aimPoint - eye;
                if (aimDir.LengthSquared < 1e-6f)
                {
                    return false;
                }

                direction = aimDir.NormalizedCopy();
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TryGetCameraAimDirection failed: {ex.Message}");
                return false;
            }
        }

        protected float ResolveSurfaceZ(Vec3 at)
        {
            float ground;
            float water;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                ground = _mission.Scene.GetGroundHeightAtPosition(at, (BodyFlags)544321929);
                water = _mission.Scene.GetWaterLevelAtPosition(at.AsVec2, true, true);
            }
            return Math.Max(ground, water);
        }

        protected bool TryResolveAimPoint(out Vec3 point, out Vec3 normal)
        {
            point = Vec3.Zero;
            normal = Vec3.Up;

            bool gotGround = _missionScreen.GetProjectedMousePositionOnGround(out Vec3 groundPos, out Vec3 groundNormal, (BodyFlags)79617, true);
            bool gotWater = _missionScreen.GetProjectedMousePositionOnWater(out Vec3 waterPos);

            if (!gotGround && !gotWater)
            {
                return false;
            }
            if (gotGround && !gotWater)
            {
                point = groundPos;
                normal = groundNormal;
                return true;
            }
            if (gotWater && !gotGround)
            {
                point = waterPos;
                normal = Vec3.Up;
                return true;
            }

            Vec3 eye = _caster != null ? _caster.GetEyeGlobalPosition() : groundPos;
            if (eye.DistanceSquared(waterPos) < eye.DistanceSquared(groundPos))
            {
                point = waterPos;
                normal = Vec3.Up;
            }
            else
            {
                point = groundPos;
                normal = groundNormal;
            }
            return true;
        }

        public virtual void Tick() { }

        public virtual void Show()
        {
            IsVisible = true;
        }

        public virtual void Hide()
        {
            IsVisible = false;
        }

        public virtual void Dispose() { }
    }
}
