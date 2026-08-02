using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem.SFX
{

    public class SotorSimpleObjectAnimator : ScriptComponentBehavior
    {

        public string AnimationName = "";

        private Skeleton _skeleton;
        private bool _init;

        protected override void OnInit()
        {
            base.OnInit();
            Init();
            SetScriptComponentToTick(GetTickRequirement());
        }

        protected override void OnEditorInit()
        {
            base.OnInit();
            if (AnimationName == null)
            {
                AnimationName = "";
            }
            Init();
        }

        private void Init()
        {
            if (AnimationName == "")
            {
                return;
            }

            var entity = GameEntity;
            if (entity == null)
            {
                return;
            }

            _skeleton = entity.Skeleton;
            if (_skeleton == null)
            {
                return;
            }

            _skeleton.SetAnimationAtChannel(AnimationName, 0, 1f, 0f, 0f);
            _skeleton.SetAgentActionChannel(0, ActionIndexCache.act_none, 0f, 0f);
            _init = true;
        }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick | base.GetTickRequirement();
        }

        protected override void OnTick(float dt) => KeepAlive();
        protected override void OnEditorTick(float dt) => KeepAlive();

        private void KeepAlive()
        {
            if (!_init)
            {
                Init();
                return;
            }

            if (_skeleton != null && AnimationName != _skeleton.GetAnimationAtChannel(0))
            {
                Init();
            }
        }
    }
}
