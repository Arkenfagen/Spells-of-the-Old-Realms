using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace SOTOR.AbilitySystem.Missions
{

    [ViewCreatorModule]
    public class SotorProvingGroundViewCreator
    {
        [ViewMethod("SotorProvingGround")]
        public static MissionView[] OpenProvingGroundMission(Mission mission)
        {
            return new List<MissionView>
            {

                ViewCreator.CreateMissionSingleplayerEscapeMenu(CampaignOptions.IsIronmanMode),
                ViewCreator.CreateOptionsUIHandler(),

                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateSingleplayerMissionKillNotificationUIHandler(),

                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),

                ViewCreator.CreateMissionAgentLockVisualizerView(mission),

                ViewCreator.CreateMissionBoundaryCrossingView(),
                new MissionBoundaryWallView(),

                ViewCreator.CreateMissionLeaveView(),
            }.ToArray();
        }
    }
}
