using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class VortexScript : AbilityScript
    {
        private float _counter = 1f;
        private float _maxDeviation;
        private float _currentDeviation;

        public override void Initialize(Ability ability, ref GameEntity entity)
        {
            base.Initialize(ability, ref entity);
            _maxDeviation = ability.Template.MaxRandomDeviation;
        }

        protected override MatrixFrame GetNextGlobalFrame(MatrixFrame oldFrame, float dt)
        {
            var frame = new MatrixFrame(oldFrame.rotation, oldFrame.origin);
            if (_counter >= 1f)
            {
                _counter = 0f;
                _currentDeviation = MBRandom.RandomFloatRanged(-_maxDeviation, _maxDeviation) * dt;
            }
            else
            {
                _counter += dt;
            }

            frame.rotation.RotateAboutUp(_currentDeviation);
            frame.Advance(Ability.Template.BaseMovementSpeed * dt);

            float groundHeight;
            float waterHeight;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                groundHeight = Mission.Current.Scene.GetGroundHeightAtPosition(frame.origin, (BodyFlags)544321929);
                waterHeight = Mission.Current.Scene.GetWaterLevelAtPosition(frame.origin.AsVec2, true, true);
            }
            frame.origin.z = Math.Max(groundHeight, waterHeight) + Ability.Template.Offset;

            oldFrame.origin = frame.origin;
            return oldFrame;
        }
    }
}
