using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem.Crosshairs
{

    public class TargetedAOECrosshair : AbilityCrosshair
    {
        private const string RunePrefab = "circular_targeting_rune";

        private GameEntity _runeEntity;
        private Vec3 _groundPosition;
        private Vec3 _groundNormal = Vec3.Up;
        private readonly AbilityTargetType _targetType;
        private readonly MBList<Agent> _targets = new MBList<Agent>();
        private readonly List<Agent> _previousTargets = new List<Agent>();

        public MBReadOnlyList<Agent> Targets => new MBReadOnlyList<Agent>(_targets);
        public override Vec3 Position => _groundPosition;

        public TargetedAOECrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
            : base(template, mission, missionScreen, caster)
        {
            _targetType = template.AbilityTargetType;
            TryCreateRune();
        }

        private void TryCreateRune()
        {
            try
            {
                _runeEntity = GameEntity.Instantiate(_mission.Scene, RunePrefab, true, true, string.Empty);
                if (_runeEntity != null)
                {
                    var frame = _runeEntity.GetFrame();
                    float d = _template.TargetCapturingRadius > 0f ? _template.TargetCapturingRadius : _template.Radius;
                    var scale = new Vec3(d * 2f, d * 2f, 1f, -1f);
                    frame.Scale(scale);
                    _runeEntity.SetFrame(ref frame, true);
                    _runeEntity.SetVisibilityExcludeParents(false);
                }
                else
                {
                    SotorLog.Debug($"TargetedAOECrosshair: rune prefab '{RunePrefab}' not found; using position-only fallback.");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TargetedAOECrosshair.TryCreateRune failed: {ex.Message}");
                _runeEntity = null;
            }
        }

        public override void Show()
        {
            base.Show();
            _runeEntity?.SetVisibilityExcludeParents(true);
        }

        public override void Hide()
        {
            base.Hide();
            _runeEntity?.SetVisibilityExcludeParents(false);
            ClearGlow();
        }

        public override void Tick()
        {
            if (_caster == null || _mission == null || _missionScreen == null)
            {
                return;
            }

            UpdatePosition();
            _previousTargets.Clear();
            _previousTargets.AddRange(_targets);
            UpdateTargets();
            UpdateGlow();
            CycleColor(_runeEntity);
        }

        private void UpdatePosition()
        {

            if (TryResolveAimPoint(out Vec3 pos, out Vec3 normal))
            {
                float dist = _caster.Position.Distance(pos);
                if (dist > _template.MaxDistance)
                {
                    var look = _caster.LookFrame;
                    pos = look.Advance(_template.MaxDistance).origin;
                    pos.z = ResolveSurfaceZ(pos);
                    normal = Vec3.Up;
                }
                _groundPosition = pos;
                _groundNormal = normal;
            }
            else
            {
                var look = _caster.LookFrame;
                pos = look.Advance(_template.MaxDistance).origin;
                pos.z = ResolveSurfaceZ(pos);
                _groundPosition = pos;
                _groundNormal = Vec3.Up;
            }

            if (_runeEntity != null)
            {

                var rot = Mat3.CreateMat3WithForward(_groundNormal);
                rot.RotateAboutSide(-MBMath.ToRadians(90f));
                rot.Orthonormalize();

                var frame = new MatrixFrame(rot, _groundPosition);

                float d = _template.TargetCapturingRadius > 0f ? _template.TargetCapturingRadius : _template.Radius;
                frame.Scale(new Vec3(d * 2f, d * 2f, 1f, -1f));
                _runeEntity.SetFrame(ref frame, true);
            }
        }

        private void UpdateTargets()
        {
            _targets.Clear();
            float r = _template.TargetCapturingRadius > 0f ? _template.TargetCapturingRadius : _template.Radius;
            switch (_targetType)
            {
                case AbilityTargetType.AlliesInAOE:
                    _mission.GetNearbyAllyAgents(_groundPosition.AsVec2, r, _mission.PlayerTeam, _targets);
                    break;
                case AbilityTargetType.EnemiesInAOE:
                    _mission.GetNearbyEnemyAgents(_groundPosition.AsVec2, r, _mission.PlayerTeam, _targets);
                    break;
            }
        }

        private void UpdateGlow()
        {
            uint? color = _targetType == AbilityTargetType.AlliesInAOE ? friendColor : enemyColor;
            foreach (var a in _targets)
            {
                SetContour(a, color);
            }
            foreach (var a in _previousTargets.Except(_targets))
            {
                SetContour(a, colorLess);
            }
        }

        private void ClearGlow()
        {
            foreach (var a in _targets) SetContour(a, colorLess);
            foreach (var a in _previousTargets) SetContour(a, colorLess);
            _targets.Clear();
            _previousTargets.Clear();
        }

        private static void SetContour(Agent agent, uint? color)
        {
            try
            {
                if (agent != null && agent.AgentVisuals != null && ((int)agent.State == 1 || (int)agent.State == 2))
                {
                    agent.AgentVisuals.SetContourColor(color, true);
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TargetedAOECrosshair.SetContour failed: {ex.Message}");
            }
        }

        public override void Dispose()
        {
            ClearGlow();
            if (_runeEntity != null)
            {
                try { GameEntityExtensions.FadeOut(_runeEntity, 0.05f, true); }
                catch { try { _runeEntity.Remove(0); } catch { } }
                _runeEntity = null;
            }
        }
    }
}
