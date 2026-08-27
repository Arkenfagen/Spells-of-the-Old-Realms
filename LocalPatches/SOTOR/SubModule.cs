using Bannerlord.UIExtenderEx;
using HarmonyLib;
using SOTOR.AbilitySystem;
using SOTOR.AbilitySystem.StatusEffects;
using SOTOR.Extensions.ExtendedInfoSystem;
using SOTOR.GameManagers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR
{
    public class SubModule : MBSubModuleBase
    {
        private const string HarmonyId = "sotor.harmony";
        private UIExtender _uiExtender;
        private static Harmony _harmony;

        public static Harmony HarmonyInstance => _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            SotorLog.Info($"SubModule load. Log file: {SotorLog.LogFilePath}");
            SotorSettings.Load();
            SotorPriceTable.Load();

            AbilitySystem.Rivals.SotorRivalOverrides.Load();
            AbilityFactory.LoadTemplates();
            TriggeredEffectManager.LoadTemplates();
            StatusEffectManager.LoadTemplates();

            Items.SotorItemTraitManager.LoadTemplates();
            SotorKeyInputManager.Initialize();

            try
            {
                _harmony = new Harmony(HarmonyId);

                _harmony.PatchAllUncategorized(typeof(SubModule).Assembly);
                SotorLog.Info("Harmony uncategorized patches applied (mission-only category deferred).");
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"Harmony PatchAll failed: {ex.GetType().Name}: {ex.Message}");
            }

            _uiExtender = UIExtender.Create("SOTOR");
            _uiExtender.Register(typeof(SubModule).Assembly);
            _uiExtender.Enable();
            SotorLog.Info("UIExtenderEx enabled.");
        }

        private bool _mcmSynced;

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (_mcmSynced) return;
            _mcmSynced = true;
            try
            {
                SotorMcmBridge.Initialize();
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"MCM bridge init skipped ({ex.GetType().Name}): {ex.Message}. Using SotorSettings defaults.");
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            SotorSpellbookHotkey.Tick();
        }

        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            if (game.GameType is Campaign && starterObject is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddBehavior(new ExtendedInfoManager());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorRaiseDeadBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorGraveyardBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorRivalBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorTeachingBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorPrisonerTeachBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorRumourBehavior());

                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorApprenticeDuelBehavior());

                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorAiRaiseDeadBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorHiddenMasterHuntBehavior());

                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorEnchantingBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorBlueprintBookBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorEnchantShopBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorMagicLootBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorRivalGearBehavior());
                campaignStarter.AddBehavior(new SOTOR.CampaignBehaviors.SotorStorytellerBehavior());
                SotorLog.Info("ExtendedInfoManager + SotorRaiseDeadBehavior + SotorGraveyardBehavior + SotorRivalBehavior + SotorTeachingBehavior + SotorPrisonerTeachBehavior + SotorRumourBehavior + SotorAiRaiseDeadBehavior + SotorHiddenMasterHuntBehavior registered.");
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            SotorLog.Info(
                $"Mission behaviors init. friendly={mission.IsFriendlyMission} combatType={(int)mission.CombatType} mode={(int)mission.Mode}");
            mission.AddMissionBehavior(new AbilityManagerMissionLogic());
            mission.AddMissionBehavior(new AbilityHUDMissionView());
            mission.AddMissionBehavior(new StatusEffectMissionLogic());

            mission.AddMissionBehavior(new Items.SotorWeaponHitMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorUndeadMoraleMissionLogic());

            mission.AddMissionBehavior(new AbilitySystem.SotorSpellGoreMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorThrownJavelinMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorMindControlMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorArcaneConduitMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.AI.SotorCastingAIMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorBurningDeckMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorAbandonShipMissionLogic());
            mission.AddMissionBehavior(new AbilitySystem.SotorSummonNavalGuardMissionLogic());
        }
    }
}