using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem.Crosshairs
{

    public class WindCrosshair : AbilityCrosshair
    {
        private const string RunePrefab = "linear_targeting_rune";

        private GameEntity _parent;
        private GameEntity _runeEntity;
        private Vec3 _position;
        private Vec3 _normal = Vec3.Up;
        private MatrixFrame _frame = MatrixFrame.Identity;

        public override Vec3 Position => _position;

        public override MatrixFrame Frame => _frame;

        public WindCrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
            : base(template, mission, missionScreen, caster)
        {
            TryCreateRune();
        }

        private void TryCreateRune()
        {
            try
            {

                _parent = GameEntity.CreateEmpty(_mission.Scene, false, true, true);

                _runeEntity = GameEntity.Instantiate(_mission.Scene, RunePrefab, false, true, string.Empty);
                if (_runeEntity != null)
                {

                    var frame = _runeEntity.GetFrame();
                    var scale = new Vec3(_template.Radius, _template.Radius, 1f, -1f);
                    frame.Scale(scale);
                    frame = frame.Advance(-0.8f);
                    frame = frame.Strafe(0.025f);
                    _runeEntity.SetFrame(ref frame, true);
                    _parent.AddChild(_runeEntity, false);
                    _parent.SetVisibilityExcludeParents(false);
                    _runeEntity.SetVisibilityExcludeParents(false);
                    SotorLog.Info($"WindCrosshair: rune '{RunePrefab}' created (parent/child), scale=Radius({_template.Radius}).");
                }
                else
                {
                    SotorLog.Debug($"WindCrosshair: rune prefab '{RunePrefab}' not found; using position-only fallback.");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"WindCrosshair.TryCreateRune failed: {ex.Message}");
                _runeEntity = null;
            }
        }

        public override void Show()
        {
            base.Show();
            _parent?.SetVisibilityExcludeParents(true);
            _runeEntity?.SetVisibilityExcludeParents(true);
        }

        public override void Hide()
        {
            base.Hide();
            _parent?.SetVisibilityExcludeParents(false);
            _runeEntity?.SetVisibilityExcludeParents(false);
        }

        public override void Tick()
        {
            if (_caster == null || _mission == null || _missionScreen == null)
            {
                return;
            }

            if (!TryResolveAimPoint(out Vec3 pos, out Vec3 normal))
            {
                pos = _caster.Position;
                normal = Vec3.Up;
            }

            float dist = _caster.Position.Distance(pos);

            Vec3 flatDir = _caster.LookDirection;
            flatDir.z = 0f;
            MatrixFrame aimFrame = (flatDir.LengthSquared < 1e-4f)
                ? _caster.LookFrame
                : new MatrixFrame(Mat3.CreateMat3WithForward(flatDir.NormalizedCopy()), _caster.Position);
            aimFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();

            if (dist < _template.MinDistance)
            {
                pos = aimFrame.Advance(_template.MinDistance).origin;
            }
            else if (dist > _template.MaxDistance)
            {
                pos = aimFrame.Advance(_template.MaxDistance).origin;
            }

            pos.z = ResolveSurfaceZ(pos);

            _position = pos;
            _normal = normal;

            _frame = aimFrame;
            _frame.origin = _position;
            var rotN = Mat3.CreateMat3WithForward(normal);
            _frame.rotation.u = rotN.f;
            _frame.rotation.RotateAboutSide(MBMath.ToRadians(5f));
            _frame.rotation.Orthonormalize();

            _parent?.SetGlobalFrame(_frame, true);

            CycleColor(_runeEntity);
        }

        public override void Dispose()
        {
            if (_runeEntity != null)
            {
                try { GameEntityExtensions.FadeOut(_runeEntity, 0.05f, true); }
                catch { try { _runeEntity.Remove(0); } catch { } }
                _runeEntity = null;
            }
            if (_parent != null)
            {
                try { GameEntityExtensions.FadeOut(_parent, 0.05f, true); }
                catch { try { _parent.Remove(0); } catch { } }
                _parent = null;
            }
        }
    }
}
