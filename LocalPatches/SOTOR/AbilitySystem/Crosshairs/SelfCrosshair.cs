using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace SOTOR.AbilitySystem.Crosshairs
{

    public class SelfCrosshair : AbilityCrosshair
    {
        public SelfCrosshair(AbilityTemplate template, Mission mission, MissionScreen missionScreen, Agent caster)
            : base(template, mission, missionScreen, caster)
        {
        }

        public override void Show() { IsVisible = false; }
        public override void Tick() { }
    }
}
