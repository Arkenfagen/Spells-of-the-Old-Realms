using System;
using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using SOTOR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;

namespace SOTOR.AbilitySystem.Missions
{

    public class SotorProvingGroundMission : MissionLogic
    {

        private static readonly Vec2 PlayerSpawn = new Vec2(92.863f, 107.609f);
        private static readonly Vec2 ApprenticeSpawn = new Vec2(104.723f, 107.557f);

        private readonly CharacterObject _apprentice;
        private readonly Hero _master;
        private readonly Action<bool> _onFinished;

        private Agent _playerAgent;
        private Agent _apprenticeAgent;
        private bool _finished;
        private float _endDelay;

        public SotorProvingGroundMission(CharacterObject apprentice, Hero master = null, Action<bool> onFinished = null)
        {
            _apprentice = apprentice;
            _master = master;
            _onFinished = onFinished;
        }

        public override void AfterStart()
        {
            base.AfterStart();

            Mission.SetMissionMode(MissionMode.Battle, true);
            Mission.IsInventoryAccessible = false;

            Mission.IsFriendlyMission = false;

            LogSceneDiagnostics();

            try
            {

                SotorApprenticeCaster.Clear();
                SotorApprenticeWinds.Clear();

                InitializeTeams();
                SpawnCombatants();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ProvingGround: setup failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void LogSceneDiagnostics()
        {
            try
            {
                var scene = Mission.Scene;
                SotorLog.Info($"ProvingGround scene: name='{scene?.GetName()}'");

                string cache = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
                    "Mount and Blade II Bannerlord", "Shaders", "TerrainShaders", "SOTOR", "sotor_proving_ground_01");
                if (System.IO.Directory.Exists(cache))
                {
                    var built = System.IO.Directory.GetCreationTime(cache);
                    SotorLog.Info($"ProvingGround terrain shader cache: EXISTS, built {built:yyyy-MM-dd HH:mm:ss}. "
                                  + "If that is OLDER than ModuleData/terrain_materials.xml it is STALE and the "
                                  + "ground will render black: delete this folder to force a rebuild.");
                }
                else
                {
                    SotorLog.Info("ProvingGround terrain shader cache: absent, so the engine will compile it now "
                                  + "(expect a pause on first entry). This is the healthy state after a materials change.");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"ProvingGround: scene diagnostics failed harmlessly: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void InitializeTeams()
        {
            var player = Hero.MainHero?.MapFaction;
            var c1 = player?.Color ?? uint.MaxValue;
            var c2 = player?.Color2 ?? uint.MaxValue;

            Mission.Teams.Add(BattleSideEnum.Defender, c1, c2, null, true, false, true);
            Mission.Teams.Add(BattleSideEnum.Attacker, c2, c1, null, true, false, true);
            Mission.PlayerTeam = Mission.DefenderTeam;
        }

        private void SpawnCombatants()
        {
            var playerPos = OnGround(PlayerSpawn);
            var apprenticePos = OnGround(ApprenticeSpawn);

            var toApprentice = (ApprenticeSpawn - PlayerSpawn).Normalized();

            var playerFormation = Mission.DefenderTeam?.GetFormation(FormationClass.Infantry);
            var apprenticeFormation = Mission.AttackerTeam?.GetFormation(FormationClass.Infantry);
            playerFormation?.BeginSpawn(1, false);
            apprenticeFormation?.BeginSpawn(1, false);

            _playerAgent = SpawnOne(CharacterObject.PlayerCharacter, Mission.DefenderTeam, playerPos, toApprentice, true, playerFormation);
            _apprenticeAgent = SpawnOne(_apprentice, Mission.AttackerTeam, apprenticePos, -toApprentice, false, apprenticeFormation);

            playerFormation?.EndSpawn();
            apprenticeFormation?.EndSpawn();

            try
            {
                if (apprenticeFormation != null && apprenticeFormation.CountOfUnits > 0)
                {
                    apprenticeFormation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    apprenticeFormation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
                    SotorLog.Info("ProvingGround: apprentice ordered to charge from the start (hybrid fight - "
                                  + "he closes and casts at the same time).");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ProvingGround: could not order the charge: {ex.GetType().Name}: {ex.Message}");
            }

            ReportFormations(playerFormation, apprenticeFormation);

            if (_apprenticeAgent != null && _master != null)
            {
                SotorApprenticeCaster.Equip(_apprenticeAgent, _master);
            }

            SotorLog.Info($"ProvingGround: spawned player at {playerPos} and apprentice "
                          + $"'{_apprentice?.Name}' at {apprenticePos}. mode={(int)Mission.Mode} "
                          + $"friendly={Mission.IsFriendlyMission} combatType={(int)Mission.CombatType}");
        }

        private void ReportFormations(Formation playerFormation, Formation apprenticeFormation)
        {
            try
            {

                SotorLog.Info($"ProvingGround: formations built - player={playerFormation?.CountOfUnits ?? 0} "
                              + $"unit(s), apprentice={apprenticeFormation?.CountOfUnits ?? 0} unit(s). "
                              + $"agentFormationsSet player={_playerAgent?.Formation != null} "
                              + $"apprentice={_apprenticeAgent?.Formation != null}. "
                              + "All must be non-zero/true or the AI has nothing to target.");
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ProvingGround: could not assign formations, so the apprentice will only be able to "
                               + $"cast summons: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private Vec3 OnGround(Vec2 flat)
        {
            var p = new Vec3(flat.x, flat.y, 0f, -1f);
            try
            {
                p.z = Mission.Scene.GetGroundHeightAtPosition(p);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"ProvingGround: ground height lookup failed ({ex.GetType().Name}); using 0.");
            }
            return p;
        }

        private Agent SpawnOne(CharacterObject character, Team team, Vec3 position, Vec2 facing, bool isPlayer,
            Formation formation = null)
        {
            if (character == null)
            {
                SotorLog.Error($"ProvingGround: cannot spawn a null character ({(isPlayer ? "player" : "apprentice")}).");
                return null;
            }

            var build = new AgentBuildData(new SimpleAgentOrigin(character))
                .Team(team)
                .InitialPosition(position)
                .NoHorses(true)
                .CivilianEquipment(false)
                .Controller(isPlayer ? AgentControllerType.Player : AgentControllerType.AI);

            if (formation != null)
            {
                build = build.Formation(formation);
            }

            build = build.InitialDirection(facing);

            var agent = Mission.SpawnAgent(build);
            agent.FadeIn();
            if (!isPlayer)
            {

                agent.SetWatchState(Agent.WatchState.Alarmed);
            }
            return agent;
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (_finished || affectedAgent == null) return;

            if (affectedAgent == _apprenticeAgent)
            {
                Finish(true);
            }
            else if (affectedAgent == _playerAgent)
            {
                Finish(false);
            }
        }

        private static void ApplyKnockoutToPlayer()
        {
            try
            {
                var hero = Hero.MainHero;
                if (hero == null) return;

                int before = hero.HitPoints;
                hero.HitPoints = 1;
                SotorLog.Info($"ProvingGround: player knocked out; hit points {before} -> {hero.HitPoints}.");
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ProvingGround: could not apply the knockout: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void Finish(bool playerWon)
        {
            _finished = true;
            _endDelay = 0f;
            SotorLog.Info($"ProvingGround: duel over, player {(playerWon ? "WON" : "LOST")}.");

            if (!playerWon) ApplyKnockoutToPlayer();

            try
            {
                _onFinished?.Invoke(playerWon);
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ProvingGround: end callback threw: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private float _rangeLogTimer;

        private void LogDuelRange(float dt)
        {
            if (_playerAgent == null || _apprenticeAgent == null) return;
            if (!_playerAgent.IsActive() || !_apprenticeAgent.IsActive()) return;

            _rangeLogTimer += dt;
            if (_rangeLogTimer < 2f) return;
            _rangeLogTimer = 0f;

            float range = _playerAgent.Position.Distance(_apprenticeAgent.Position);
            var behavior = _apprenticeAgent.GetComponent<AI.WizardAIComponent>()?.CurrentCastingBehavior;

            int targets = -1;
            string firstAbility = "none";
            try
            {
                var component = _apprenticeAgent.GetComponent<AbilityComponent>();
                var ability = component != null && component.KnownAbilitySystem.Count > 0
                    ? component.KnownAbilitySystem[0] : null;
                if (ability?.Template != null)
                {
                    firstAbility = ability.Template.StringID;
                    targets = AI.AgentCastingBehaviorConfiguration.FindTargets(_apprenticeAgent, ability.Template).Count;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"ProvingGround: target probe failed harmlessly: {ex.GetType().Name}: {ex.Message}");
            }

            int enemyFormations = 0;
            try
            {
                foreach (var team in Mission.Teams)
                {
                    if (team == null || !_apprenticeAgent.Team.IsEnemyOf(team)) continue;
                    foreach (var f in team.FormationsIncludingEmpty) if (f.CountOfUnits > 0) enemyFormations++;
                }
            }
            catch (Exception) { }

            SotorLog.Info($"ProvingGround range: {range:0.0}m | apprentice winds="
                          + $"{SotorApprenticeWinds.Get(_apprenticeAgent):0}/{SotorApprenticeCaster.MaxWindsFor(_apprenticeAgent):0} "
                          + $"behavior={behavior?.GetType().Name ?? "none"} "
                          + $"apprenticeMoving={_apprenticeAgent.MovementVelocity.Length > 0.1f} "
                          + $"| targetsFor'{firstAbility}'={targets} enemyFormationsWithUnits={enemyFormations} "
                          + $"playerFormationUnits={_playerAgent.Formation?.CountOfUnits ?? -1} "
                          + $"apprenticeAlarmed={_apprenticeAgent.IsAlarmed()}");
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            LogDuelRange(dt);
            if (!_finished) return;

            _endDelay += dt;
            if (_endDelay > 3f)
            {
                _finished = false;
                Mission.EndMission();
            }
        }

        public override void OnRemoveBehavior()
        {
            SotorApprenticeCaster.Clear();
            SotorApprenticeWinds.Clear();
            base.OnRemoveBehavior();
        }

        public static Mission Open(CharacterObject apprentice, Hero master = null, Action<bool> onFinished = null)
        {
            return MissionState.OpenNew(
                "SotorProvingGround",

                SandBoxMissions.CreateSandBoxMissionInitializerRecord("sotor_proving_ground_01", "", false, DecalAtlasGroup.All),
                mission => new MissionBehavior[]
                {
                    new MissionOptionsComponent(),
                    new CampaignMissionComponent(),
                    new MissionBasicTeamLogic(),
                    new MissionAgentLookHandler(),
                    new BasicLeaveMissionLogic(),
                    new AgentHumanAILogic(),
                    new SotorProvingGroundMission(apprentice, master, onFinished),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(),
                    new MissionFacialAnimationHandler(),
                    new HeroSkillHandler(),
                    new EquipmentControllerLeaveLogic(),
                },
                true, true);
        }
    }
}
