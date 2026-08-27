using System;
using SOTOR.AbilitySystem.Missions;
using SOTOR.Quests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorApprenticeDuelBehavior : CampaignBehaviorBase
    {
        public const string PreparationMenu = "sotor_duel_preparation";
        public const string VictoryMenu = "sotor_duel_victory";
        public const string DefeatMenu = "sotor_duel_defeat";

        private Hero _master;
        private string _spellId;
        private string _apprenticeId;

        private CharacterObject _apprentice;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sotorDuelMaster", ref _master);
            dataStore.SyncData("_sotorDuelSpellId", ref _spellId);

            dataStore.SyncData("_sotorDuelChampionId", ref _apprenticeId);
        }

        public bool DeferDuel(Hero master, string spellId, string loreId)
        {
            if (!EnsureChallenge(master, spellId, loreId)) return false;

            var quest = SotorApprenticeDuelQuest.ActiveFor(master);
            SotorLog.Info($"ApprenticeDuel: the player deferred {master.Name}'s challenge; "
                          + $"{RemainingDaysText(quest)} remain.");
            return true;
        }

        public bool BeginDuel(Hero master, string spellId, string loreId)
        {
            if (!EnsureChallenge(master, spellId, loreId)) return false;
            return OpenMenu(PreparationMenu);
        }

        private static bool OpenMenu(string menuId)
        {
            try
            {
                bool hasContext = Campaign.Current?.CurrentMenuContext != null;
                if (hasContext) GameMenu.SwitchToMenu(menuId);
                else GameMenu.ActivateGameMenu(menuId);

                SotorLog.Info($"ApprenticeDuel: opened menu '{menuId}' via "
                              + $"{(hasContext ? "SwitchToMenu (a menu was already open)" : "ActivateGameMenu (no menu context, e.g. met on the map)")}.");
                return true;
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ApprenticeDuel: could not open menu '{menuId}': {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private bool EnsureChallenge(Hero master, string spellId, string loreId)
        {
            if (master == null || string.IsNullOrEmpty(spellId)) return false;

            var apprentice = SotorApprenticePicker.PickFor(master, out _);
            if (apprentice == null)
            {
                SotorLog.Error($"ApprenticeDuel: {master.Name} demanded a duel but no apprentice could be resolved; "
                               + "the challenge was not issued.");
                return false;
            }

            _master = master;
            _spellId = spellId;
            _apprenticeId = apprentice.StringId;
            _apprentice = apprentice;

            var quest = SotorApprenticeDuelQuest.ActiveFor(master);
            if (quest == null)
            {
                quest = new SotorApprenticeDuelQuest(master, spellId, loreId, apprentice.StringId);
                quest.StartQuest();
                SotorLog.Info($"ApprenticeDuel: {master.Name} challenged the player over '{spellId}'; "
                              + $"apprentice '{apprentice.StringId}'. {SotorApprenticeDuelQuest.PreparationDays} day(s) to answer it.");
            }
            else
            {
                SotorLog.Info($"ApprenticeDuel: resuming {master.Name}'s existing challenge over '{quest.SpellId}'; "
                              + $"{RemainingDaysText(quest)} remain.");
            }
            return true;
        }

        private static string RemainingDaysText(SotorApprenticeDuelQuest quest)
        {
            if (quest == null) return "no";
            return quest.QuestDueTime.RemainingDaysFromNow.ToString("0.0") + " day(s)";
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            try
            {
                AddMenus(starter);
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ApprenticeDuel: failed to register the duel menus: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void AddMenus(CampaignGameStarter starter)
        {

            starter.AddGameMenu(PreparationMenu, "{SOTOR_DUEL_PREP}", SetPreparationText,
                GameMenu.MenuOverlayType.None);

            starter.AddGameMenuOption(PreparationMenu, "sotor_duel_fight", "{SOTOR_DUEL_FIGHT}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Mission;
                    return true;
                },
                args => ExecuteDuel(),
                false, 1);

            starter.AddGameMenuOption(PreparationMenu, "sotor_duel_withdraw", "{SOTOR_DUEL_WITHDRAW}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return true;
                },
                args => Withdraw(),
                true, 2);

            starter.AddGameMenu(VictoryMenu, "{SOTOR_DUEL_VICTORY}", SetVictoryText,
                GameMenu.MenuOverlayType.None);
            starter.AddGameMenuOption(VictoryMenu, "sotor_duel_victory_leave", "{SOTOR_DUEL_CONTINUE}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    return true;
                },
                args => FinishMenu(),
                true, 1);

            starter.AddGameMenu(DefeatMenu, "{SOTOR_DUEL_DEFEAT}", SetDefeatText,
                GameMenu.MenuOverlayType.None);
            starter.AddGameMenuOption(DefeatMenu, "sotor_duel_defeat_leave", "{SOTOR_DUEL_CONTINUE}",
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Continue;
                    return true;
                },
                args =>
                {

                    Clear();
                    FinishMenu();
                },
                true, 1);
        }

        private void SetPreparationText(MenuCallbackArgs args)
        {
            var t = SotorText.GetObject("sotor_duel_menu_prepare");
            t.SetTextVariable("MASTER", _master?.Name ?? new TextObject(""));
            t.SetTextVariable("APPRENTICE", ResolveApprentice()?.Name ?? new TextObject(""));
            MBTextManager.SetTextVariable("SOTOR_DUEL_PREP", t, false);
            MBTextManager.SetTextVariable("SOTOR_DUEL_FIGHT", SotorText.GetObject("sotor_duel_menu_fight"), false);
            MBTextManager.SetTextVariable("SOTOR_DUEL_WITHDRAW", SotorText.GetObject("sotor_duel_menu_withdraw"), false);
        }

        private void SetVictoryText(MenuCallbackArgs args)
        {
            var t = SotorText.GetObject("sotor_duel_menu_victory");
            t.SetTextVariable("MASTER", _master?.Name ?? new TextObject(""));
            t.SetTextVariable("APPRENTICE", ResolveApprentice()?.Name ?? new TextObject(""));
            MBTextManager.SetTextVariable("SOTOR_DUEL_VICTORY", t, false);
            MBTextManager.SetTextVariable("SOTOR_DUEL_CONTINUE", SotorText.GetObject("sotor_duel_menu_continue"), false);
        }

        private void SetDefeatText(MenuCallbackArgs args)
        {
            var t = SotorText.GetObject("sotor_duel_menu_defeat");
            t.SetTextVariable("MASTER", _master?.Name ?? new TextObject(""));
            t.SetTextVariable("APPRENTICE", ResolveApprentice()?.Name ?? new TextObject(""));
            MBTextManager.SetTextVariable("SOTOR_DUEL_DEFEAT", t, false);
            MBTextManager.SetTextVariable("SOTOR_DUEL_CONTINUE", SotorText.GetObject("sotor_duel_menu_continue"), false);
        }

        private CharacterObject ResolveApprentice()
        {
            if (_apprentice != null) return _apprentice;
            if (string.IsNullOrEmpty(_apprenticeId)) return null;
            _apprentice = MBObjectManager.Instance?.GetObject<CharacterObject>(_apprenticeId);
            return _apprentice;
        }

        private void ExecuteDuel()
        {
            var apprentice = ResolveApprentice();
            if (apprentice == null)
            {
                SotorLog.Error($"ApprenticeDuel: apprentice '{_apprenticeId}' no longer resolves; aborting the duel.");
                FinishMenu();
                return;
            }

            SotorLog.Info($"ApprenticeDuel: opening the proving ground against '{apprentice.StringId}'.");

            SotorProvingGroundMission.Open(apprentice, _master, EvaluateDuel);
        }

        private void EvaluateDuel(bool playerWon)
        {
            try
            {
                var quest = SotorApprenticeDuelQuest.ActiveFor(_master);
                if (playerWon)
                {
                    quest?.OnApprenticeDefeated();

                    OpenMenu(VictoryMenu);
                }
                else
                {

                    quest?.OnPlayerDefeated();
                    SotorTeachingBehavior.BeginDuelCooldown(_master);
                    OpenMenu(DefeatMenu);

                }
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ApprenticeDuel: evaluating the duel failed: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void Withdraw()
        {
            var quest = SotorApprenticeDuelQuest.ActiveFor(_master);
            SotorLog.Info($"ApprenticeDuel: the player withdrew from {_master?.Name}'s challenge before fighting; "
                          + $"it stands, {RemainingDaysText(quest)} remain.");
            ShowChiding();
            FinishMenu();
        }

        public void ShowChiding()
        {
            try
            {
                var quest = SotorApprenticeDuelQuest.ActiveFor(_master);
                var t = SotorText.GetObject("sotor_duel_chide");
                t.SetTextVariable("MASTER", _master?.Name ?? new TextObject(""));
                t.SetTextVariable("DAYS", quest == null
                    ? SotorApprenticeDuelQuest.PreparationDays
                    : (int)System.Math.Ceiling(System.Math.Max(0.0, quest.QuestDueTime.RemainingDaysFromNow)));
                SotorRibbon.Show(new TextObject(t.ToString()), 4000, _master);
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ApprenticeDuel: the chiding notice failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void Clear()
        {
            _master = null;
            _spellId = null;
            _apprenticeId = null;
            _apprentice = null;
        }

        private void FinishMenu()
        {
            try
            {
                if (Settlement.CurrentSettlement != null)
                {
                    GameMenu.SwitchToMenu(Settlement.CurrentSettlement.IsVillage ? "village" : "town");
                    return;
                }
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Finish(true);
                    return;
                }
                GameMenu.ExitToLast();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"ApprenticeDuel: could not return from the duel menu: {ex.GetType().Name}: {ex.Message}");
                GameMenu.ExitToLast();
            }
        }
    }
}
