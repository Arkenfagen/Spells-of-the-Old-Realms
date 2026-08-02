using System;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using Helpers;
#if BL13
using SotorSpawnLogic = TaleWorlds.MountAndBlade.MissionAgentSpawnLogic;
#else
using SotorSpawnLogic = TaleWorlds.MountAndBlade.DefaultBattleMissionAgentSpawnLogic;
#endif
using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TroopSuppliers;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorGraveyardBehavior : CampaignBehaviorBase
    {
        public const string GraveyardScene = "sotor_graveyard_01_atmo_w_night";
        private const string RaisedTroopId = "sotor_skeleton";

        private const int MaxDefenderTroops = 5;

        private CharacterObject _skeleton;
        private CampaignTime _startWaitTime = CampaignTime.Zero;
        private Settlement _currentSettlement;
        private MobileParty _watchParty;
        private bool _isMissionStarted;

        private System.Collections.Generic.HashSet<CharacterObject> _selectedDefenders;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, Initialize);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, HourlyPartyTick);
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnBattleEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void Initialize(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "sotor_graveyard",
                new TextObject("Go to the graveyard").ToString(),
                GraveyardAccessCondition,
                args => GameMenu.SwitchToMenu("sotor_graveyard"),
                false, 4, false);

            starter.AddGameMenuOption("village", "sotor_graveyard",
                new TextObject("Go to the graveyard").ToString(),
                GraveyardAccessCondition,
                args => GameMenu.SwitchToMenu("sotor_graveyard"),
                false, 4, false);

            starter.AddGameMenu("sotor_graveyard", "{SOTOR_GRAVEYARD_INTRODUCTION}",
                args =>
                {
                    args.MenuTitle = new TextObject("Graveyard");
                    var intro = new TextObject("You have arrived at {SETTLEMENT_NAME}'s graveyard. Graves, tombstones and family crypts litter the peaceful hillside.");
                    intro.SetTextVariable("SETTLEMENT_NAME", Settlement.CurrentSettlement?.Name ?? new TextObject("the town"));
                    MBTextManager.SetTextVariable("SOTOR_GRAVEYARD_INTRODUCTION", intro, false);
                },
                GameMenu.MenuOverlayType.SettlementWithCharacters);

            starter.AddGameMenuOption("sotor_graveyard", "sotor_raise_dead_attempt",
                new TextObject("Raise dead from the corpses in the ground (wait 8 hours).").ToString(),
                RaiseDeadAttemptCondition,
                args => GameMenu.SwitchToMenu("sotor_raising_dead"),
                false, -1, false);

            starter.AddGameMenuOption("sotor_graveyard", "sotor_graveyard_leave",
                new TextObject("Leave").ToString(),
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => GameMenu.SwitchToMenu(ParentSettlementMenu()),
                true, -1, false);

            starter.AddWaitGameMenu("sotor_raising_dead",
                new TextObject("The common folk's graves are ripe for the taking. You spend the night hours dragging corpses from the ground and binding them to your will.").ToString(),
                args =>
                {
                    _startWaitTime = CampaignTime.Now;
                    PlayerEncounter.Current.IsPlayerWaiting = true;
                    args.MenuContext.GameMenu.StartWait();
                },
                null,
                RaisingDeadConsequence,
                RaisingDeadTick,
                GameMenu.MenuAndOptionType.WaitMenuShowProgressAndHoursOption,
                GameMenu.MenuOverlayType.SettlementWithCharacters,
                8f);

            starter.AddGameMenuOption("sotor_raising_dead", "sotor_raising_dead_leave",
                new TextObject("Leave").ToString(),
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args =>
                {
                    PlayerEncounter.Current.IsPlayerWaiting = false;
                    SwitchToMenuIfThereIsAnInterrupt(args.MenuContext.GameMenu.StringId);
                },
                true, -1, false);

            starter.AddGameMenu("sotor_graveyard_interrupt", "{SOTOR_GRAVEYARD_INTERRUPT}",
                args =>
                {
                    args.MenuTitle = new TextObject("Caught in the act");
                    MBTextManager.SetTextVariable("SOTOR_GRAVEYARD_INTERRUPT",
                        new TextObject("The local nightwatch is onto you. Face the consequences of your vile actions."), false);
                    CalculateAndApplyCrimeRatingChange();
                },
                GameMenu.MenuOverlayType.SettlementWithCharacters);

            starter.AddGameMenuOption("sotor_graveyard_interrupt", "sotor_interrupt_battle",
                new TextObject("Defend yourself").ToString(),
                args =>
                {
                    if (!Hero.MainHero.IsWounded) { args.optionLeaveType = GameMenuOption.LeaveType.DefendAction; return true; }
                    return false;
                },
                args => OpenCompanionSelection(args),
                false, -1, false);

            starter.AddGameMenuOption("sotor_graveyard_interrupt", "sotor_interrupt_surrender",
                new TextObject("Surrender").ToString(),
                args => { args.optionLeaveType = GameMenuOption.LeaveType.LeaveTroopsAndFlee; return true; },
                args =>
                {
                    PlayerEncounter.Current.IsPlayerWaiting = false;
                    PlayerEncounter.Finish(false);

                    var prison = PrisonSettlementFor(Settlement.CurrentSettlement);
                    if (prison != null) TakePrisonerAction.Apply(prison.Party, Hero.MainHero);
                },
                true, -1, false);

            _skeleton = MBObjectManager.Instance.GetObject<CharacterObject>(RaisedTroopId);
        }

        private bool GraveyardAccessCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!SotorSettings.EnableSkeletonArmies) return false;
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null) return false;
            if (!settlement.IsTown && !settlement.IsVillage) return false;

            bool disabled = false;
            TextObject disabledText = new TextObject("The graveyard's massive iron gates are closed shut.");

            bool canAccess = settlement.IsVillage
                || Campaign.Current.Models.SettlementAccessModel.CanMainHeroAccessLocation(
                    settlement, "center", out disabled, out disabledText);
            if (canAccess)
                canAccess = GetBestRaiser() != null && !settlement.IsUnderSiege;
            return MenuHelper.SetOptionProperties(args, canAccess, disabled, disabledText);
        }

        private bool RaiseDeadAttemptCondition(MenuCallbackArgs args)
        {
            if (GetBestRaiser() != null && !Settlement.CurrentSettlement.IsUnderSiege)
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Wait;
                return true;
            }
            return false;
        }

        private void RaisingDeadConsequence(MenuCallbackArgs args)
        {
            PlayerEncounter.Current.IsPlayerWaiting = false;
            args.MenuContext.GameMenu.EndWait();
            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(0f);
            GameMenu.SwitchToMenu("sotor_graveyard");
        }

        private void RaisingDeadTick(MenuCallbackArgs args, CampaignTime dt)
        {
            if (Settlement.CurrentSettlement.IsUnderSiege) { InterruptWaitSiege(args); return; }

            float progress = args.MenuContext.GameMenu.Progress;
            int elapsedHours = (int)_startWaitTime.ElapsedHoursUntilNow;
            if (elapsedHours <= 0) return;

            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(elapsedHours * 0.125f);
            if (args.MenuContext.GameMenu.Progress != progress)
            {
                if (_skeleton != null && MobileParty.MainParty.MemberRoster.TotalManCount <= MobileParty.MainParty.Party.PartySizeLimit)
                {
                    Hero raiser = GetBestRaiser() ?? Hero.MainHero;
                    int tier = Math.Max(1, (int)SotorSpellcraftHelper.GetCastingLevel(raiser));

                    int perLevel = Settlement.CurrentSettlement.IsVillage ? 1 : 2;
                    int raised = perLevel * tier;
                    MobileParty.MainParty.MemberRoster.AddToCounts(_skeleton, raised, false, 0, 0, true, -1);
                    NotifyRaised(raiser, raised);
                }

                if (MBRandom.RandomFloatRanged(0f, 1f) > 0.95f
                    && Settlement.CurrentSettlement != null
                    && Settlement.CurrentSettlement.OwnerClan != Clan.PlayerClan)
                {
                    InterruptWait(args);
                }
            }
        }

        private void OpenCompanionSelection(MenuCallbackArgs args)
        {

            var heroesRoster = TroopRoster.CreateDummyTroopRoster();
            heroesRoster.AddToCounts(CharacterObject.PlayerCharacter, 1);
            foreach (var el in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
                var c = el.Character;
                if (c != null && !c.IsPlayerCharacter && c.IsHero) heroesRoster.AddToCounts(c, 1);
            }

            var initial = TroopRoster.CreateDummyTroopRoster();
            initial.AddToCounts(CharacterObject.PlayerCharacter, 1);
            int companions = 0;
            foreach (var el in heroesRoster.GetTroopRoster())
            {
                var c = el.Character;
                if (c == null || c.IsPlayerCharacter) continue;
                if (1 + companions >= MaxDefenderTroops) break;
                initial.AddToCounts(c, 1);
                companions++;
            }

            Func<CharacterObject, bool> canChange = c => !c.IsPlayerCharacter && c.IsHero;
            Action<TroopRoster> onDone = OnCompanionSelectionDone;
            InvokeOpenTroopSelection(args.MenuContext, heroesRoster, initial, canChange, onDone, MaxDefenderTroops, 1);
        }

        private static void InvokeOpenTroopSelection(object menuContext, TroopRoster fullRoster,
            TroopRoster initial, Func<CharacterObject, bool> canChange, Action<TroopRoster> onDone,
            int maxSel, int minSel)
        {
            try
            {
                var mi = menuContext.GetType().GetMethod("OpenTroopSelection");
                if (mi == null)
                {
                    SotorLog.Warn("OpenTroopSelection method not found — graveyard companion-select unavailable.");
                    return;
                }
                var ps = mi.GetParameters();
                var args = new object[ps.Length];

                args[0] = fullRoster;
                args[1] = initial;
                int idx = 2;

                if (ps[idx].ParameterType != typeof(Func<CharacterObject, bool>))
                {
                    args[idx] = null;
                    idx++;
                }
                args[idx++] = canChange;
                args[idx++] = onDone;
                args[idx++] = maxSel;
                if (idx < ps.Length) args[idx++] = minSel;

                for (int k = idx; k < ps.Length; k++)
                    args[k] = ps[k].HasDefaultValue ? ps[k].DefaultValue : (ps[k].ParameterType.IsValueType ? Activator.CreateInstance(ps[k].ParameterType) : null);
                mi.Invoke(menuContext, args);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"InvokeOpenTroopSelection failed: {ex.Message}");
            }
        }

        private void OnCompanionSelectionDone(TroopRoster selected)
        {
            _selectedDefenders = new System.Collections.Generic.HashSet<CharacterObject>();
            _selectedDefenders.Add(CharacterObject.PlayerCharacter);
            foreach (var el in selected.GetTroopRoster())
            {
                if (el.Character != null && el.Character.IsHero)
                    _selectedDefenders.Add(el.Character);
            }
            SetupBattle();
        }

        private void SetupBattle()
        {
            PlayerEncounter.Current.IsPlayerWaiting = false;
            _watchParty = SotorGraveyardNightWatchPartyComponent.CreateParty(Settlement.CurrentSettlement);
            _currentSettlement = Settlement.CurrentSettlement;
            PlayerEncounter.RestartPlayerEncounter(PartyBase.MainParty, _watchParty.Party, true);
            if (PlayerEncounter.Battle == null)
            {
                PlayerEncounter.StartBattle();
                PlayerEncounter.Update();
            }
            _isMissionStarted = true;
            OpenGraveyardBattleMission();
        }

        private Mission OpenGraveyardBattleMission()
        {
            var rec = SandBoxMissions.CreateSandBoxMissionInitializerRecord(GraveyardScene, "", false, (DecalAtlasGroup)0);
            rec.PlayingInCampaignMode = false;
            rec.AtmosphereOnCampaign = AtmosphereInfo.GetInvalidAtmosphereInfo();

            var mapEvent = MobileParty.MainParty.MapEvent;

            var defenderPriors = BuildDefenderPriorTroops(out int defenderPriorCount);

            SotorGraveyardFightMissionController.DefenderSpawnCap = defenderPriorCount;
            Hero atkLeader = mapEvent.AttackerSide.LeaderParty.LeaderHero;
            Hero defLeader = mapEvent.DefenderSide.LeaderParty.LeaderHero;
            var attackerLeaderName = atkLeader?.Name;
            var defenderLeaderName = defLeader?.Name;

            return MissionState.OpenNew("Battle", rec, mission =>
            {

                var suppliers = new IMissionTroopSupplier[]
                {
                    new PartyGroupTroopSupplier(mapEvent, BattleSideEnum.Defender, defenderPriors, null),
                    new PartyGroupTroopSupplier(mapEvent, BattleSideEnum.Attacker, null, null),
                };
                var list = new System.Collections.Generic.List<MissionBehavior>
                {
                    new SotorSpawnLogic(suppliers, BattleSideEnum.Defender, Mission.BattleSizeType.Battle),
                    new BattlePowerCalculationLogic(),
                    new BattleSpawnLogic("battle_set"),
                    new SotorGraveyardFightMissionController(),

                    new CampaignMissionComponent(),
                    new BattleAgentLogic(),
                    new MountAgentLogic(),
                    new MissionOptionsComponent(),
                    new BattleEndLogic(),
                    new MissionCombatantsLogic(
                        mapEvent.InvolvedParties, PartyBase.MainParty,
                        mapEvent.GetLeaderParty(BattleSideEnum.Defender),
                        mapEvent.GetLeaderParty(BattleSideEnum.Attacker),
                        Mission.MissionTeamAITypeEnum.FieldBattle, false),
                    new BattleObserverMissionLogic(),
                    new AgentHumanAILogic(),
                    new AgentVictoryLogic(),
                    new MissionAgentPanicHandler(),
                    new BattleMissionAgentInteractionLogic(),
                    new AgentMoraleInteractionLogic(),
                    new AssignPlayerRoleInTeamMissionController(true, false, false, null),
                    new BannerBearerLogic(),
                    new SandboxGeneralsAndCaptainsAssignmentLogic(attackerLeaderName, defenderLeaderName, null, null, false),
                    new EquipmentControllerLeaveLogic(),
                    new MissionHardBorderPlacer(),
                    new MissionBoundaryPlacer(),
                    new MissionBoundaryCrossingHandler(10f),
                    new HighlightsController(),
                    new BattleHighlightsController(),
                    new BattleDeploymentMissionController(false),
                    new BattleDeploymentHandler(false),
                };
                return list.ToArray();
            }, true, true);
        }

        private FlattenedTroopRoster BuildDefenderPriorTroops(out int count)
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(CharacterObject.PlayerCharacter, 1);
            count = 1;
            if (_selectedDefenders != null)
            {
                foreach (var c in _selectedDefenders)
                    if (c != null && !c.IsPlayerCharacter && c.IsHero) { roster.AddToCounts(c, 1); count++; }
            }
            return roster.ToFlattenedRoster();
        }

        private void OnBattleEnded(MapEvent mapevent)
        {
            if (_isMissionStarted && mapevent.WinningSide == mapevent.PlayerSide)
            {
                _isMissionStarted = false;
                _watchParty = null;
                _currentSettlement = null;
            }
        }

        private void HourlyPartyTick(MobileParty party)
        {
            if (party == _watchParty && _isMissionStarted)
            {
                _isMissionStarted = false;
                Settlement settlement = _currentSettlement ?? Town.AllTowns.GetRandomElementInefficiently().Settlement;

                if (Hero.MainHero.IsPrisoner && _watchParty.PrisonRoster.Contains(CharacterObject.PlayerCharacter))
                {
                    var prison = PrisonSettlementFor(settlement);
                    if (prison != null)
                        TransferPrisonerAction.Apply(CharacterObject.PlayerCharacter, _watchParty.Party, prison.Party);
                    else
                        EndCaptivityAction.ApplyByEscape(Hero.MainHero);
                }
                DestroyPartyAction.ApplyForDisbanding(_watchParty, settlement);
                _watchParty = null;
                _currentSettlement = null;
            }
        }

        private void NotifyRaised(Hero raiser, int count)
        {
            if (count <= 0) return;
            var msg = new TextObject("You drag {COUNT} more from the earth to serve you.");
            msg.SetTextVariable("COUNT", count);
            InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));

            var settlement = Settlement.CurrentSettlement;
            if (settlement != null && _skeleton != null)
            {
                using (SotorGraveyardRecruitTextPatch.Scope())
                    CampaignEventDispatcher.Instance.OnTroopRecruited(raiser, settlement, null, _skeleton, count);
            }
        }

        private void SwitchToMenuIfThereIsAnInterrupt(string currentMenuId)
        {
            string genericStateMenu = Campaign.Current.Models.EncounterGameMenuModel.GetGenericStateMenu();
            if (genericStateMenu != currentMenuId)
            {
                if (!string.IsNullOrEmpty(genericStateMenu)) GameMenu.SwitchToMenu(genericStateMenu);
                else GameMenu.ExitToLast();
            }
        }

        private static string ParentSettlementMenu()
        {
            return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsVillage ? "village" : "town";
        }

        private static Settlement PrisonSettlementFor(Settlement settlement)
        {
            if (settlement == null) return null;
            if (settlement.IsTown || settlement.IsCastle) return settlement;
            if (settlement.IsVillage)
            {
                var bound = settlement.Village?.Bound;
                if (bound != null && (bound.IsTown || bound.IsCastle)) return bound;
                return null;
            }
            return null;
        }

        private void InterruptWait(MenuCallbackArgs args)
        {
            PlayerEncounter.Current.IsPlayerWaiting = false;
            args.MenuContext.GameMenu.EndWait();
            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(0f);
            GameMenu.SwitchToMenu("sotor_graveyard_interrupt");
        }

        private void InterruptWaitSiege(MenuCallbackArgs args)
        {
            PlayerEncounter.Current.IsPlayerWaiting = false;
            args.MenuContext.GameMenu.EndWait();
            args.MenuContext.GameMenu.SetProgressOfWaitingInMenu(0f);
            GameMenu.SwitchToMenu(ParentSettlementMenu());
        }

        private void CalculateAndApplyCrimeRatingChange()
        {
            float delta = 0f;
            float crime = Settlement.CurrentSettlement.MapFaction.MainHeroCrimeRating;
            if (crime < 30f) delta = 30f - crime + 5f;
            else if (crime < 65f) delta = Math.Min(20f, 65f - crime - 5f);
            else delta = 20f;
            if (delta > 0f)
                ChangeCrimeRatingAction.Apply(Settlement.CurrentSettlement.MapFaction, delta, true);
        }

        private static Hero GetBestRaiser()
        {
            var party = Hero.MainHero?.PartyBelongedTo;
            if (party == null) return null;
            Hero best = null; int bestSkill = -1;
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                var hero = element.Character?.HeroObject;
                if (hero == null || !CanRaiseDead(hero)) continue;
                int skill = SpellcraftOf(hero);
                if (skill > bestSkill) { bestSkill = skill; best = hero; }
            }
            return best;
        }

        private static bool CanRaiseDead(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null || !info.HasLore(SotorLores.LoreOfNecromancy)) return false;
            return info.HasSpell("SummonSkeleton") || info.HasSpell("GraveCall");
        }

        private static int SpellcraftOf(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            return skill != null ? hero.GetSkillValue(skill) : 0;
        }
    }
}
