using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
#if BL13
using SotorSpawnLogic = TaleWorlds.MountAndBlade.MissionAgentSpawnLogic;
#else
using SotorSpawnLogic = TaleWorlds.MountAndBlade.DefaultBattleMissionAgentSpawnLogic;
#endif

namespace SOTOR.CampaignBehaviors
{

    public class SotorGraveyardFightMissionController : MissionLogic
    {

        public static int DefenderSpawnCap = 5;

        private SotorSpawnLogic _missionAgentSpawnLogic;
        private MapEvent _mapEvent;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();

            SotorGraveyardDeploymentPatch.SuppressOrderOfBattleDeployment = true;

            SotorGraveyardMountPatch.SuppressPlayerMount = true;
            _missionAgentSpawnLogic = Mission.GetMissionBehavior<SotorSpawnLogic>();
            _mapEvent = MapEvent.PlayerMapEvent;
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            SotorGraveyardDeploymentPatch.SuppressOrderOfBattleDeployment = false;
            SotorGraveyardMountPatch.SuppressPlayerMount = false;
        }

        public override void AfterStart()
        {

            int def = MathF.Min(_mapEvent.GetNumberOfInvolvedMen(BattleSideEnum.Defender), DefenderSpawnCap);
            int atk = _mapEvent.GetNumberOfInvolvedMen(BattleSideEnum.Attacker);
            Mission.DoesMissionRequireCivilianEquipment = false;
            _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Defender, false);
            _missionAgentSpawnLogic.SetSpawnHorses(BattleSideEnum.Attacker, false);
            MissionSpawnSettings spawnSettings = MissionSpawnSettings.CreateDefaultSpawnSettings();
            _missionAgentSpawnLogic.InitWithSinglePhase(def, atk, def, atk, true, true, in spawnSettings);

            try
            {
                Mission.Scene.SetAtmosphereWithName("sotor_graveyard_night");
            }
            catch (System.Exception ex) { SotorLog.Warn($"[SOTOR] graveyard SetAtmosphereWithName failed: {ex.Message}"); }
        }
    }
}
