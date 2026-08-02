using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem.SFX
{

    public class SotorSpinner : ScriptComponentBehavior
    {

        public float RotationSpeed = 100f;

        protected override void OnInit()
        {
            base.OnInit();
            SetScriptComponentToTick(GetTickRequirement());
        }

        private void Rotate(float dt)
        {
            var entity = GameEntity;
            if (entity == null)
            {
                return;
            }
            float num = RotationSpeed * 0.001f * dt;
            MatrixFrame frame = entity.GetFrame();
            frame.rotation.RotateAboutUp(num);
            entity.SetFrame(ref frame);
        }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickParallel | base.GetTickRequirement();
        }

        protected override void OnTickParallel(float dt) => Rotate(dt);
        protected override void OnEditorTick(float dt) => Rotate(dt);
    }
}
