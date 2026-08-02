using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorTarget
    {
        public Agent Agent { get; set; }

        public bool IsValid => Agent != null && Agent.IsActive() && Agent.Health > 0f && !Agent.IsFadingOut();

        public Vec3 GetPosition()
        {
            return IsValid ? Agent.CollisionCapsuleCenter : Vec3.Invalid;
        }
    }
}
