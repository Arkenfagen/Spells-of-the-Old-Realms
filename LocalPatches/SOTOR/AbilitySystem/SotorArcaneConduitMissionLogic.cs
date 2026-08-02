using TaleWorlds.MountAndBlade;

namespace SOTOR.AbilitySystem
{

    public class SotorArcaneConduitMissionLogic : MissionLogic
    {

        public static int UsesThisBattle;

        private bool _resetThisMission;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            UsesThisBattle = 0;
            _resetThisMission = true;
        }

        public override void OnMissionTick(float dt)
        {
            if (!_resetThisMission)
            {
                _resetThisMission = true;
                UsesThisBattle = 0;
            }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            UsesThisBattle = 0;
        }
    }
}
