using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    public class SeekerController
    {
        private readonly SotorTarget _target;
        private readonly SeekerParameters _parameters;
        private Vec3 _prevError;
        private bool _enabled = true;

        public SeekerController(SotorTarget target, SeekerParameters parameters)
        {
            _target = target;
            _parameters = parameters;
            _prevError = Vec3.Zero;
        }

        public MatrixFrame CalculateRotatedFrame(MatrixFrame globalFrame, float dt)
        {
            if (!_enabled || _target == null || !_target.IsValid)
            {
                return globalFrame;
            }

            Vec3 ahead = globalFrame.origin + globalFrame.rotation.f.NormalizedCopy();
            Vec3 error = _target.GetPosition() - ahead;
            float dist = error.Length;

            if (dist < _parameters.DisableDistance)
            {
                _enabled = false;
                return globalFrame;
            }

            if (dist < _parameters.MaxDistance && dist > _parameters.MinDistance)
            {
                Vec3 correction = error * _parameters.Proportional + (error - _prevError) * _parameters.Derivative;
                Vec3 newAhead = ahead + correction * dt;
                Vec3 newForward = newAhead - globalFrame.origin;
                globalFrame.rotation = Mat3.CreateMat3WithForward(newForward);
            }

            _prevError = error;
            return globalFrame;
        }
    }
}
