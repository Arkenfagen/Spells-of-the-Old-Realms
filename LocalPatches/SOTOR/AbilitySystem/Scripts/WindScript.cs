using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class WindScript : AbilityScript
    {
        protected override MatrixFrame GetNextGlobalFrame(MatrixFrame oldFrame, float dt)
        {
            var next = base.GetNextGlobalFrame(oldFrame, dt);

            float groundHeight;
            float waterHeight;
            using (new TWSharedMutexReadLock(Scene.PhysicsAndRayCastLock))
            {
                groundHeight = Mission.Current.Scene.GetGroundHeightAtPosition(next.origin, (BodyFlags)544321929);
                waterHeight = Mission.Current.Scene.GetWaterLevelAtPosition(next.origin.AsVec2, true, true);
            }
            next.origin.z = Math.Max(groundHeight, waterHeight) + Ability.Template.Radius / 2f;
            return next;
        }
    }
}
