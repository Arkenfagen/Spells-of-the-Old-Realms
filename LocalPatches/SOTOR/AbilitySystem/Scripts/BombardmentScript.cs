using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    public class BombardmentScript : AbilityScript
    {
        private bool _impulseGiven;

        protected override void OnAfterTick(float dt)
        {
            if (!_impulseGiven && Ability != null && Ability.Template.TriggerType == TriggerType.OnCollision)
            {
                _impulseGiven = true;
                var entity = GameEntity;
                if (entity.IsValid)
                {
                    GameEntityPhysicsExtensions.ApplyLocalImpulseToDynamicBody(
                        entity, entity.CenterOfMass, new Vec3(0f, 0f, -100f, -1f));
                }
            }

            if (TryGetWaterCrossing(LastFrameGlobalPosition, CurrentGlobalPosition, out Vec3 waterPos, out float _))
            {

                HandleCollision(waterPos, Vec3.Up);
            }
        }

        protected override void HandleCollision(Vec3 position, Vec3 normal)
        {
            normal.RotateAboutX(MBMath.ToRadians(90f));
            base.HandleCollision(position, normal);
        }
    }
}
