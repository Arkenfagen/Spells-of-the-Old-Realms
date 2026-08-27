using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using SOTOR.Quests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    public class SotorTeachingBehavior : CampaignBehaviorBase
    {

        private const float SuccessValue = 1f;
        private const float FailValue = 0f;
        private const float CriticalSuccessValue = 2f;
        private const float CriticalFailValue = 2f;

        private PersuasionTask _task;
        private Trad _offeredLore = Trad.None;
        private string _offeredLoreId;

        private enum AskMode { Lore, Spell }
        private AskMode _askMode = AskMode.Lore;

        private List<SotorArgumentPool.Argument> _offeredArguments = new List<SotorArgumentPool.Argument>();

        private int _critSlot = -1;

        private string _argSummary = "";

        private string _relSummary = "";

        private Dictionary<string, CampaignTime> _teachFailedAt = new Dictionary<string, CampaignTime>();

        public const int FailedAskCooldownDays = 7;

        private string _offeredSpellId;
        private readonly List<SpellSlot> _spellSlots = new List<SpellSlot>();

        private struct SpellSlot
        {
            public string SpellId;
            public string LoreId;
            public Trad Lore;

            public bool KnownByMaster;
        }

        private const int SpellSlotCount = 12;

        private const int RefusalRelationPenaltyStanding = -2;
        private const int RefusalRelationPenaltyIgnorance = -2;
        private const int RefusalRelationPenaltyDoctrine = -3;

        private const int RefusalRelationPenaltyMinor = -1;

        private TeachOutcome _refusalOutcome = TeachOutcome.CanNegotiate;

        private bool _relationPenaltyApplied;

        private TeachOutcome _lastLoggedRefusal = TeachOutcome.CanNegotiate;

        private string _lastLoggedRung;

        private string _lastLoggedSpellList;

        private static SotorTeachingBehavior _instance;

        public override void RegisterEvents()
        {
            _instance = this;
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _relationPenaltyApplied = false;
            _refusalOutcome = TeachOutcome.CanNegotiate;
            _lastLoggedRefusal = TeachOutcome.CanNegotiate;
            _lastLoggedRung = null;
            _lastLoggedSpellList = null;
            _askMode = AskMode.Lore;
            _offeredSpellId = null;
            _spellSlots.Clear();
        }

        private List<string> _acceptedStudentOf = new List<string>();

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("SotorTeachFailedAt", ref _teachFailedAt);
            dataStore.SyncData("SotorTeachAcceptedStudentOf", ref _acceptedStudentOf);
            dataStore.SyncData("SotorTeachForgottenBy", ref _forgottenBy);

            if (_teachFailedAt == null) _teachFailedAt = new Dictionary<string, CampaignTime>();
            if (_acceptedStudentOf == null) _acceptedStudentOf = new List<string>();
            if (_forgottenBy == null) _forgottenBy = new List<string>();
        }

        private bool IsAcceptedStudent(Hero master)
        {
            return master != null && _acceptedStudentOf != null
                   && _acceptedStudentOf.Contains(master.StringId);
        }

        private void AcceptAsStudent(Hero master)
        {
            if (master == null) return;
            if (_acceptedStudentOf == null) _acceptedStudentOf = new List<string>();
            if (_acceptedStudentOf.Contains(master.StringId)) return;

            _acceptedStudentOf.Add(master.StringId);
            SotorLog.Info($"RivalTeach: {master.Name} has accepted the player as a student "
                          + $"({_acceptedStudentOf.Count} master(s) now).");
        }

        private int RetryDaysRemaining(Hero master)
        {
            if (master == null || FailedAskCooldownDays <= 0 || _teachFailedAt == null) return 0;
            if (!_teachFailedAt.TryGetValue(master.StringId, out CampaignTime failedAt)) return 0;
            float elapsed = failedAt.ElapsedDaysUntilNow;
            if (elapsed >= FailedAskCooldownDays) return 0;
            return (int)System.Math.Ceiling(FailedAskCooldownDays - elapsed);
        }

        private static void LeaveEncounterLikeNative()
        {
            try
            {
                if (PlayerEncounter.Current != null
                    && Campaign.Current.ConversationManager.ConversationParty == PlayerEncounter.EncounteredMobileParty
                    && (PlayerEncounter.EncounteredBattle != null
                        || PlayerEncounter.EncounterSettlement?.Party.MapEvent != null
                        || PlayerEncounter.EncounterSettlement?.Party.SiegeEvent != null
                        || (PlayerEncounter.EncounterSettlement == null && Settlement.CurrentSettlement == null)))
                {
                    PlayerEncounter.LeaveEncounter = true;
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"RivalTeach: LeaveEncounterLikeNative failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private void ClearRetryCooldown(Hero master)
        {
            if (master == null || _teachFailedAt == null) return;
            if (_teachFailedAt.Remove(master.StringId))
            {
                SotorLog.Info($"RivalTeach: cleared {master.Name}'s retry cooldown (the persuasion succeeded).");
            }
        }

        private void BeginRetryCooldown(Hero master)
        {
            if (master == null || FailedAskCooldownDays <= 0) return;
            if (_teachFailedAt == null) _teachFailedAt = new Dictionary<string, CampaignTime>();
            _teachFailedAt[master.StringId] = CampaignTime.Now;
            SotorLog.Info($"RivalTeach: {master.Name} will not be asked again for {FailedAskCooldownDays} day(s).");
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);

            if (SotorSettings.SpellbookRequiresMasters && !SotorSettings.EnableRivalCasters)
            {
                SotorLog.Info("Spellbook gate: 'Masters teach the lores' is on but Rival Wizards is off, so the "
                              + "gate is inert and the spellbook still sells everything. It needs Rival Wizards "
                              + "for masters to exist.");
            }
        }

        private void AddDialogs(CampaignGameStarter starter)
        {

            starter.AddPlayerLine("sotor_teach_ask", "hero_main_options", "sotor_teach_gate",
                SotorText.Get("sotor_teach_ask"),
                CanAskToLearn, ResetAskState, 110);

            starter.AddDialogLine("sotor_teach_refuse_hostile", "sotor_teach_gate", "close_window",
                SotorText.Get("sotor_teach_refuse"),
                () => ShouldRefuse() && _refusalOutcome != TeachOutcome.NotTeachable, OnRefused, 210);

            starter.AddDialogLine("sotor_teach_offer", "sotor_teach_gate", "sotor_teach_hub",
                SotorText.Get("sotor_teach_offer"),
                CanProceedWithLore, StartTeachingPersuasion, 150);

            starter.AddDialogLine("sotor_magic_hub_open", "sotor_teach_gate", "sotor_magic_hub",
                SotorText.Get("sotor_magic_hub_open"),
                MagicHubOpen, null, 100);

            starter.AddPlayerLine("sotor_magic_hub_spells", "sotor_magic_hub", "sotor_teach_magic",
                SotorText.Get("sotor_magic_hub_spells"), null, null, 100);
            starter.AddPlayerLine("sotor_magic_hub_never", "sotor_magic_hub", "lord_pretalk",
                SotorText.Get("sotor_magic_hub_never"), null, null, 10);

            starter.AddDialogLine("sotor_teach_practice_pending", "sotor_teach_magic", "lord_pretalk",
                SotorText.Get("sotor_teach_practice_pending"),
                PracticePending, null, 310);

            starter.AddDialogLine("sotor_teach_refuse", "sotor_teach_magic", "lord_pretalk",
                SotorText.Get("sotor_teach_refuse"),
                () => ShouldRefuse() && _refusalOutcome == TeachOutcome.NotTeachable, OnRefused, 200);

            starter.AddDialogLine("sotor_teach_spell_list", "sotor_teach_magic", "sotor_teach_spell_pick",
                SotorText.Get("sotor_teach_spell_list"),
                BuildSpellSlots, null, 100);

            starter.AddDialogLine("sotor_teach_magic_fallback", "sotor_teach_magic", "lord_pretalk",
                SotorText.Get("sotor_teach_gate_fallback"),
                null, OnGateFallback, 1);

            starter.AddDialogLine("sotor_teach_gate_fallback", "sotor_teach_gate", "lord_pretalk",
                SotorText.Get("sotor_teach_gate_fallback"),
                null, OnGateFallback, 1);

            for (int i = 0; i < SpellSlotCount; i++)
            {
                int slot = i;

                starter.AddPlayerLine("sotor_teach_spell_pick_" + slot, "sotor_teach_spell_pick", "sotor_teach_spell_offer",
                    "{SOTOR_TEACH_SPELL_" + slot + "}",
                    () => SpellSlotExists(slot), () => PickSpellSlot(slot), 100 - slot);
            }

            starter.AddPlayerLine("sotor_teach_spell_none", "sotor_teach_spell_pick", "lord_pretalk",
                SotorText.Get("sotor_teach_spell_none"), null, null, 10);

            starter.AddDialogLine("sotor_teach_spell_offer", "sotor_teach_spell_offer", "sotor_teach_duel_choice",
                SotorText.Get("sotor_teach_duel_demand"),
                CanProceedWithSpell, null, 100);

            starter.AddPlayerLine("sotor_teach_duel_accept", "sotor_teach_duel_choice", "close_window",
                SotorText.Get("sotor_teach_duel_accept"),
                null, OnDuelAccepted, 100);

            starter.AddPlayerLine("sotor_teach_duel_decline", "sotor_teach_duel_choice", "sotor_teach_duel_chide",
                SotorText.Get("sotor_teach_duel_decline"), null, OnDuelDeferred, 10);

            starter.AddDialogLine("sotor_teach_duel_chide", "sotor_teach_duel_chide", "lord_pretalk",
                SotorText.Get("sotor_teach_duel_chide_spoken"), null, null, 100);

            starter.AddDialogLine("sotor_teach_duel_reward", "sotor_teach_gate", "sotor_teach_blurb",
                SotorText.Get("sotor_teach_duel_reward"),
                DuelWon, OnDuelRewardPaid, 300);

            starter.AddDialogLine("sotor_teach_practice_done", "sotor_teach_gate", "lord_pretalk",
                SotorText.Get("sotor_teach_practice_done"),
                PracticeComplete, OnPracticeSignedOff, 320);

            starter.AddDialogLine("sotor_teach_forgot_you", "sotor_teach_gate", "sotor_teach_gate",
                SotorText.Get("sotor_teach_forgot_you"),
                MasterForgotYou, ClearForgotten, 330);

            starter.AddDialogLine("sotor_teach_duel_pending", "sotor_teach_gate", "sotor_teach_duel_choice",
                SotorText.Get("sotor_teach_duel_pending"),
                DuelPending, null, 325);

            starter.AddDialogLine("sotor_teach_hub_success", "sotor_teach_hub", "sotor_teach_blurb",
                SotorText.Get("sotor_teach_hub_success"),
                HubSuccess, OnTeachingSucceeded, int.MaxValue);

            starter.AddDialogLine("sotor_teach_blurb", "sotor_teach_blurb", "lord_pretalk",
                "{SOTOR_TEACH_BLURB}", null, null, 100);

            starter.AddDialogLine("sotor_teach_hub_fail", "sotor_teach_hub", "close_window",
                SotorText.Get("sotor_teach_hub_fail"),
                HubFail, OnTeachingFailed, 100);
            starter.AddDialogLine("sotor_teach_hub_continue", "sotor_teach_hub", "sotor_teach_options",
                SotorText.Get("sotor_teach_hub_continue"), null, null, 90);

            starter.AddPlayerLine("sotor_teach_arg_1", "sotor_teach_options", "sotor_teach_reaction",
                SotorText.Get("sotor_teach_arg_1"), () => ArgExists(0), null, 100,
                (out TextObject e) => ArgClickableCond(0, out e), () => Option(0));
            starter.AddPlayerLine("sotor_teach_arg_2", "sotor_teach_options", "sotor_teach_reaction",
                SotorText.Get("sotor_teach_arg_2"), () => ArgExists(1), null, 100,
                (out TextObject e) => ArgClickableCond(1, out e), () => Option(1));
            starter.AddPlayerLine("sotor_teach_arg_3", "sotor_teach_options", "sotor_teach_reaction",
                SotorText.Get("sotor_teach_arg_3"), () => ArgExists(2), null, 100,
                (out TextObject e) => ArgClickableCond(2, out e), () => Option(2));

            starter.AddPlayerLine("sotor_teach_arg_bail", "sotor_teach_options", "lord_pretalk",
                SotorText.Get("sotor_teach_arg_bail"), null, OnTeachingAbandoned, 50);

            starter.AddDialogLine("sotor_teach_reaction", "sotor_teach_reaction", "sotor_teach_hub",
                SotorText.Get("sotor_teach_reaction"), null, MarkArgumentSpoken, 100);
        }

        private bool CanAskToLearn()
        {
            if (!SotorSettings.EnableRivalCasters) return false;
            var master = Hero.OneToOneConversationHero;
            if (master == null || master == Hero.MainHero) return false;
            if (!master.IsLord || !master.IsAbilityUser()) return false;

            var captiveChar = CharacterObject.OneToOneConversationCharacter;
            if (captiveChar != null && MobileParty.MainParty.PrisonRoster.Contains(captiveChar))
            {
                return false;
            }

            if (SotorRivalSeeder.IsHiddenMaster(master)
                && (!SotorRivalReveal.IsReady || !SotorRivalReveal.IsRevealed(master)))
            {
                return false;
            }

            SetLineVariables(master);
            return true;
        }

        private bool MagicHubOpen()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);
            return true;
        }

        private void ResetAskState()
        {
            _askMode = AskMode.Lore;
            _offeredSpellId = null;
            _spellSlots.Clear();
        }

        private void OnGateFallback()
        {
            var master = Hero.OneToOneConversationHero;
            try
            {
                var info = Hero.MainHero.GetExtendedInfo();
                var masterInfo = master?.GetExtendedInfo();
                SotorLog.Warn("RivalTeach GATE FALLBACK: no branch matched, so the weave-slipped line ran. "
                              + $"master={master?.Name} student={(master != null && IsAcceptedStudent(master))} "
                              + $"refuse={ShouldRefuse()} outcome={_refusalOutcome} "
                              + $"offeredLore={_offeredLoreId ?? "none"} askMode={_askMode} "
                              + $"slots={_spellSlots.Count} "
                              + $"masterLores=[{string.Join(",", masterInfo?.AcquiredLores ?? new List<string>())}] "
                              + $"playerLores=[{string.Join(",", info?.AcquiredLores ?? new List<string>())}]");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"RivalTeach GATE FALLBACK (state dump failed: {ex.Message}).");
            }
            ResetAskState();
            _offeredLore = Trad.None;
            _offeredLoreId = null;
        }

        private bool CanProceedWithLore()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);

            if (IsAcceptedStudent(master))
            {
                var studentOutcome = ResolveOutcome(master, out _offeredLore, out _offeredLoreId);
                if (studentOutcome != TeachOutcome.CanNegotiate || _offeredLoreId == null)
                {
                    return false;
                }
                _askMode = AskMode.Lore;
                _offeredSpellId = null;
                var studentLine = LoremasterFlavour(master)
                    ? SotorText.GetObject("sotor_teach_offer_lore_loremaster")
                    : SotorText.GetObject("sotor_teach_offer_lore");
                studentLine.SetTextVariable("LORE", LoreTitle(_offeredLoreId));
                MBTextManager.SetTextVariable("SOTOR_TEACH_OFFER", studentLine.ToString());
                SotorLog.Info($"RivalTeach: existing student is being offered a NEW lore "
                              + $"'{_offeredLoreId}' by {master.Name}.");
                return true;
            }

            var outcome = ResolveOutcome(master, out _offeredLore, out _offeredLoreId);
            if (outcome != TeachOutcome.CanNegotiate && outcome != TeachOutcome.NotTeachable)
            {
                return false;
            }

            if (_offeredLoreId == null && !PickAlreadyHeldLore(master, out _offeredLore, out _offeredLoreId))
            {
                return false;
            }

            _askMode = AskMode.Lore;
            _offeredSpellId = null;

            var line = LoremasterFlavour(master)
                ? SotorText.GetObject("sotor_teach_offer_lore_loremaster")
                : SotorText.GetObject("sotor_teach_offer_lore");
            line.SetTextVariable("LORE", LoreTitle(_offeredLoreId));
            MBTextManager.SetTextVariable("SOTOR_TEACH_OFFER", line.ToString());
            return true;
        }

        private bool CanProceedWithSpell()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _offeredSpellId == null) return false;
            SetLineVariables(master);

            var line = LoremasterFlavour(master)
                ? SotorText.GetObject("sotor_teach_offer_spell_loremaster")
                : SotorText.GetObject("sotor_teach_offer_spell");
            line.SetTextVariable("LORE", LoreTitle(_offeredLoreId));
            line.SetTextVariable("SPELL", SpellTitle(_offeredSpellId));
            MBTextManager.SetTextVariable("SOTOR_TEACH_OFFER", line.ToString());

            MBTextManager.SetTextVariable("SPELL", SpellTitle(_offeredSpellId));
            return true;
        }

        private bool PracticeComplete()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;

            var quest = SotorPracticeQuest.ActiveFor(master);
            if (quest == null || !quest.IsComplete) return false;

            SetLineVariables(master);
            MBTextManager.SetTextVariable("SPELL", SpellTitle(quest.SpellId));
            return true;
        }

        private bool PracticePending()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;

            var quest = SotorPracticeQuest.ActiveFor(master);
            if (quest == null || quest.IsComplete) return false;

            SetLineVariables(master);
            MBTextManager.SetTextVariable("SPELL", SpellTitle(quest.SpellId));
            return true;
        }

        private void OnPracticeSignedOff()
        {
            var master = Hero.OneToOneConversationHero;
            var quest = master == null ? null : SotorPracticeQuest.ActiveFor(master);
            if (quest == null) return;

            if (SotorRivalStanding.IsReady)
            {
                SotorRivalStanding.ApplyLearningInfluence(SotorTraditions.TradForLore(quest.LoreId), isLore: false);
            }

            SotorLog.Info($"RivalTeach: {master.Name} signed off the player's practice of '{quest.SpellId}'.");
            quest.OnAccepted();
        }

        private List<string> _forgottenBy = new List<string>();

        public static void NoteForgottenApprentice(Hero master)
        {
            if (master == null || _instance == null) return;
            if (_instance._forgottenBy == null) _instance._forgottenBy = new List<string>();
            if (_instance._forgottenBy.Contains(master.StringId)) return;

            _instance._forgottenBy.Add(master.StringId);
            SotorLog.Info($"RivalTeach: {master.Name} has forgotten the player; he owes him a greeting.");
        }

        private bool MasterForgotYou()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _forgottenBy == null) return false;
            if (!_forgottenBy.Contains(master.StringId)) return false;

            SetLineVariables(master);
            return true;
        }

        private void ClearForgotten()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _forgottenBy == null) return;
            _forgottenBy.Remove(master.StringId);
        }

        private void OnDuelAccepted()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _offeredSpellId == null)
            {
                SotorLog.Error("RivalTeach: the duel was accepted with no master or no chosen spell; ignoring.");
                return;
            }

            var duels = Campaign.Current?.GetCampaignBehavior<SotorApprenticeDuelBehavior>();
            if (duels == null)
            {
                SotorLog.Error("RivalTeach: SotorApprenticeDuelBehavior is not registered; the duel cannot start.");
                return;
            }

            if (!duels.BeginDuel(master, _offeredSpellId, _offeredLoreId))
            {
                SotorLog.Error($"RivalTeach: {master.Name}'s challenge over '{_offeredSpellId}' could not be issued.");
            }
        }

        private void OnDuelDeferred()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null || _offeredSpellId == null) return;

            var duels = Campaign.Current?.GetCampaignBehavior<SotorApprenticeDuelBehavior>();
            if (duels == null)
            {
                SotorLog.Error("RivalTeach: SotorApprenticeDuelBehavior is not registered; the challenge cannot stand.");
                return;
            }

            if (!duels.DeferDuel(master, _offeredSpellId, _offeredLoreId))
            {
                SotorLog.Error($"RivalTeach: {master.Name}'s deferred challenge over '{_offeredSpellId}' "
                               + "could not be issued.");
                return;
            }
            PublishRemainingDays(master);
        }

        private static void PublishRemainingDays(Hero master)
        {
            var quest = SotorApprenticeDuelQuest.ActiveFor(master);
            int days = quest == null
                ? SotorApprenticeDuelQuest.PreparationDays
                : (int)System.Math.Ceiling(System.Math.Max(0.0, quest.QuestDueTime.RemainingDaysFromNow));
            MBTextManager.SetTextVariable("DAYS", days);
        }

        private bool DuelWon()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;

            var quest = SotorApprenticeDuelQuest.ActiveFor(master);
            if (quest == null || !quest.ApprenticeDefeated) return false;

            SetLineVariables(master);
            return true;
        }

        private bool DuelPending()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;

            var quest = SotorApprenticeDuelQuest.ActiveFor(master);
            if (quest == null || quest.ApprenticeDefeated) return false;

            SetLineVariables(master);
            MBTextManager.SetTextVariable("SPELL", SpellTitle(quest.SpellId));
            PublishRemainingDays(master);

            _askMode = AskMode.Spell;
            _offeredSpellId = quest.SpellId;
            _offeredLoreId = quest.LoreId;
            return true;
        }

        private void OnDuelRewardPaid()
        {
            var master = Hero.OneToOneConversationHero;
            var quest = master == null ? null : SotorApprenticeDuelQuest.ActiveFor(master);
            if (quest == null) return;

            string spellId = quest.SpellId;
            GrantSingleSpell(Hero.MainHero, spellId);

            ChangeRelationAction.ApplyPlayerRelation(master, 2, affectRelatives: false, showQuickNotification: true);
            if (SotorRivalStanding.IsReady)
            {
                SotorRivalStanding.ApplyLearningInfluence(SotorTraditions.TradForLore(quest.LoreId), isLore: false);
            }

            SetUnlockBlurbText("sotor_unlock_spell_" + spellId, master, quest.LoreId, spellId);

            var pi = Hero.MainHero.GetExtendedInfo();
            SotorLog.Info($"RivalTeach: {master.Name} paid out the duel reward SPELL '{spellId}'. "
                          + $"owned={pi?.HasSpell(spellId)} castable={Hero.MainHero.HasAbility(spellId)} "
                          + $"EQUIPPED={pi?.IsAbilitySelected(spellId)}");

            quest.OnRewardPaid();
            IssuePracticeQuest(master, spellId, quest.LoreId);

            _offeredSpellId = null;
            _offeredLoreId = null;
            _askMode = AskMode.Lore;
        }

        private void IssuePracticeQuest(Hero master, string spellId, string loreId)
        {
            if (master == null || string.IsNullOrEmpty(spellId)) return;
            if (SotorPracticeQuest.ActiveFor(master) != null) return;

            try
            {
                var template = AbilitySystem.AbilityFactory.GetTemplate(spellId);
                var objective = SotorPracticeTracker.ObjectiveFor(template);
                int target = SotorPracticeTracker.TargetFor(template);

                var quest = new SotorPracticeQuest(master, spellId, loreId, (int)objective, target);
                quest.StartQuest();
            }
            catch (System.Exception ex)
            {

                SotorLog.Error($"RivalTeach: could not issue the practice quest for '{spellId}': "
                               + $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        public static void BeginDuelCooldown(Hero master)
        {
            _instance?.BeginRetryCooldown(master);
        }

        private List<SpellSlot> CollectTeachableSpells(Hero master)
        {
            var result = new List<SpellSlot>();
            if (master == null) return result;

            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return result;

            var houseTrad = SotorRivalSeeder.SocialTradition(master);
            int relation = (int)master.GetRelationWithPlayer();

            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string loreId = SotorTraditions.LoreIdFor(t);
                if (loreId == null) continue;
                if (!info.HasLore(loreId)) continue;

                if (SotorTraditions.IsMemberOnly(t) && SotorRivalReveal.IsReady && !SotorRivalReveal.IsRevealed(master)) continue;
                if (!SotorTeachingLogic.PassesDispositionGate(houseTrad, t, relation)) continue;

                var masterInfo = master.GetExtendedInfo();
                foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
                {
                    string spellId = template?.StringID;
                    if (spellId == null) continue;

                    if (info.HasSpell(spellId) || Hero.MainHero.HasAbility(spellId)) continue;

                    if (masterInfo == null || !masterInfo.HasSpell(spellId)) continue;

                    result.Add(new SpellSlot
                    {
                        SpellId = spellId, LoreId = loreId, Lore = t, KnownByMaster = true,
                    });
                }
            }
            return result;
        }

        private bool BuildSpellSlots()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);

            if (!IsAcceptedStudent(master)) return false;

            _spellSlots.Clear();
            var all = CollectTeachableSpells(master);
            if (all.Count == 0) return false;

            if (all.Count > SpellSlotCount)
            {
                SotorLog.Info($"RivalTeach: {master.Name} could teach {all.Count} spells, showing the first {SpellSlotCount}.");
            }

            var mi = master.GetExtendedInfo();

            string signature = master.StringId + "|" + string.Join(",", all.ConvertAll(x => x.SpellId));
            if (signature != _lastLoggedSpellList)
            {
                _lastLoggedSpellList = signature;
                SotorLog.Info($"RivalTeach: spell list for {master.Name} = [{string.Join(",", all.ConvertAll(x => x.SpellId))}]. "
                              + $"hisAcquiredSpells=[{string.Join(",", mi?.AcquiredSpells ?? new List<string>())}] "
                              + $"hisAllAbilities=[{string.Join(",", mi?.AllAbilities ?? new List<string>())}].");
            }

            for (int i = 0; i < all.Count && i < SpellSlotCount; i++)
            {
                _spellSlots.Add(all[i]);

                var label = SotorText.GetObject("sotor_teach_spell_option");
                label.SetTextVariable("SPELL", SpellTitle(all[i].SpellId));
                label.SetTextVariable("LORE", LoreTitle(all[i].LoreId));
                MBTextManager.SetTextVariable("SOTOR_TEACH_SPELL_" + i, label.ToString());
            }
            return true;
        }

        private int CountTeachableSpells(Hero master)
        {
            int n = 0;
            foreach (var c in CollectTeachableSpells(master)) if (c.KnownByMaster) n++;
            return n;
        }

        private bool SpellSlotExists(int index) => index >= 0 && index < _spellSlots.Count;

        private void PickSpellSlot(int index)
        {
            if (!SpellSlotExists(index)) return;
            var slot = _spellSlots[index];

            if (!slot.KnownByMaster) return;
            _askMode = AskMode.Spell;
            _offeredSpellId = slot.SpellId;
            _offeredLore = slot.Lore;
            _offeredLoreId = slot.LoreId;
        }

        private bool PickAlreadyHeldLore(Hero master, out Trad lore, out string loreId)
        {
            lore = Trad.None;
            loreId = null;

            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return false;

            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string id = SotorTraditions.LoreIdFor(t);
                if (id == null || !info.HasLore(id)) continue;

                lore = t;
                loreId = id;
                return true;
            }
            return false;
        }

        private bool PickTeachableLore(Hero master, out Trad lore, out string loreId)
        {
            lore = Trad.None;
            loreId = null;
            var houseTrad = SotorRivalSeeder.SocialTradition(master);
            int relation = (int)master.GetRelationWithPlayer();

            Trad best = Trad.None;
            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string id = SotorTraditions.LoreIdFor(t);
                if (id == null) continue;

                var pInfo = Hero.MainHero.GetExtendedInfo();
                if (pInfo != null && pInfo.HasLore(id)) continue;

                if (SotorTraditions.IsMemberOnly(t) && SotorRivalReveal.IsReady && !SotorRivalReveal.IsRevealed(master)) continue;
                if (!SotorTeachingLogic.PassesDispositionGate(houseTrad, t, relation)) continue;

                if (best == Trad.None || Prefer(houseTrad, t, best))
                {
                    best = t;
                    lore = t;
                    loreId = id;
                }
            }
            return best != Trad.None;
        }

        private static bool HasAnythingLeftToTeach(string loreId)
        {
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return true;
            if (!info.HasLore(loreId)) return true;
            return NextMissingSpell(loreId) != null;
        }

        private static string NextMissingSpell(string loreId)
        {
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return null;
            foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null) continue;
                if (!info.HasSpell(id)) return id;
            }
            return null;
        }

        private static void GrantLoreOnly(Hero hero, string loreId)
        {
            if (hero == null || loreId == null) return;
            hero.AddAttribute("AbilityUser");
            hero.AddAttribute("SpellCaster");
            var info = hero.GetExtendedInfo();
            if (info == null || info.HasLore(loreId)) return;
            info.AddLore(loreId);
        }

        private static void GrantSingleSpell(Hero hero, string abilityId)
        {
            if (hero == null || abilityId == null) return;
            var info = hero.GetExtendedInfo();
            if (info == null) return;
            if (!info.HasSpell(abilityId)) info.AddSpell(abilityId);
            if (!hero.HasAbility(abilityId)) hero.AddAbility(abilityId);
        }

        private static TextObject SpellTitle(string abilityId)
        {
            var template = AbilitySystem.AbilityFactory.GetTemplate(abilityId);
            return new TextObject(string.IsNullOrEmpty(template?.Name) ? abilityId : template.Name);
        }

        private static bool Prefer(Trad houseTrad, Trad candidate, Trad current)
        {
            return SotorTraditions.Affinity(houseTrad, candidate) > SotorTraditions.Affinity(houseTrad, current);
        }

        private bool ShouldRefuse()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;
            SetLineVariables(master);

            int retryDays = RetryDaysRemaining(master);
            TeachOutcome outcome;
            if (SotorCoercionRecord.IsReady && SotorCoercionRecord.WasCoerced(master))
            {
                outcome = TeachOutcome.VetoCoerced;
            }
            else if (retryDays > 0)
            {
                outcome = TeachOutcome.VetoRecentFailure;
                MBTextManager.SetTextVariable("SOTOR_RETRY_DAYS", retryDays);
            }
            else
            {
                bool hasSchool = PickTeachableLore(master, out _, out _);
                bool hasSpell = CountTeachableSpells(master) > 0;
                outcome = ResolveVetoes(master, hasSchool || hasSpell);
            }

            SetRefusalText(master, outcome);

            _refusalOutcome = outcome;

            if (outcome != TeachOutcome.CanNegotiate && outcome != _lastLoggedRefusal)
            {
                _lastLoggedRefusal = outcome;
                var mInfo = master.GetExtendedInfo();
                SotorLog.Info($"RivalTeach: {master.Name} refuses ({outcome}). ask={_askMode} "
                              + $"hisLores=[{string.Join(",", mInfo?.AcquiredLores ?? new List<string>())}] "
                              + $"hisSpells={mInfo?.AcquiredSpells?.Count ?? 0} "
                              + $"spellsHeCouldTeach={CountTeachableSpells(master)} "
                              + $"spellcraft={master.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft)}.");
            }
            return outcome != TeachOutcome.CanNegotiate;
        }

        private void OnRefused()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;
            if (_relationPenaltyApplied) return;

            int penalty;
            switch (_refusalOutcome)
            {
                case TeachOutcome.VetoStanding:  penalty = RefusalRelationPenaltyStanding; break;
                case TeachOutcome.VetoDoctrine:  penalty = RefusalRelationPenaltyDoctrine; break;
                case TeachOutcome.VetoIgnorance: penalty = RefusalRelationPenaltyIgnorance; break;
                case TeachOutcome.VetoWar:
                case TeachOutcome.VetoRecentFailure:
                case TeachOutcome.VetoCoerced:
                    penalty = RefusalRelationPenaltyMinor;
                    break;

                default: return;
            }

            _relationPenaltyApplied = true;
            ChangeRelationAction.ApplyPlayerRelation(master, penalty, affectRelatives: false,
                showQuickNotification: true);

            if (_refusalOutcome != TeachOutcome.NotTeachable) LeaveEncounterLikeNative();

            string exit = _refusalOutcome == TeachOutcome.NotTeachable ? "stays in dialogue" : "ENDS the conversation";
            SotorLog.Info($"RivalTeach: {master.Name} refused the player ({_refusalOutcome}), relation {penalty}, {exit}.");
        }

        private static void SetLineVariables(Hero master)
        {
            SotorText.SetPlayerVariables();
            if (master != null) MBTextManager.SetTextVariable("MASTER", master.Name);
        }

        private static void SetUnlockBlurbText(string blurbId, Hero master, string loreId, string spellId)
        {
            SotorUnlockBlurb.Publish("SOTOR_TEACH_BLURB", blurbId, master, loreId, spellId);
        }

        private TeachOutcome ResolveOutcome(Hero master, out Trad lore, out string loreId)
        {
            lore = Trad.None;
            loreId = null;
            bool hasSomethingToTeach = PickTeachableLore(master, out lore, out loreId);

            return ResolveVetoes(master, hasSomethingToTeach);
        }

        private TeachOutcome ResolveVetoes(Hero master, bool hasSomethingToTeach = true)
        {

            var masterFaction = master.MapFaction;
            bool outlawFaction = masterFaction != null
                && (masterFaction.IsOutlaw || masterFaction.IsBanditFaction);
            bool atWar = !outlawFaction && masterFaction != null && Hero.MainHero.MapFaction != null
                && masterFaction.IsAtWarWith(Hero.MainHero.MapFaction);
            var houseTrad = SotorRivalSeeder.SocialTradition(master);

            var playerHostile = SotorTeachingLogic.OffendingTradition(
                houseTrad, SotorRivalSeeder.TeachableTraditions(Hero.MainHero));

            int masterTier = master.Clan?.Tier ?? 0;

            return SotorTeachingLogic.Resolve(
                atWar, houseTrad, playerHostile, masterKnowsLore: hasSomethingToTeach,
                masterClanTier: masterTier,
                playerClanTier: Clan.PlayerClan?.Tier ?? 0,
                masterCasterLevel: SotorRivalSeeder.HeroCasterLevel(master, masterTier),
                playerCasterLevel: PlayerCasterLevel(),
                playerMasteredHighMagic: PlayerIsFullLoremaster());
        }

        private static string LoremasterRefusalOrNull(Hero master)
        {
            return LoremasterFlavour(master)
                ? SotorText.Get("sotor_teach_refuse_knows_all_loremaster")
                : null;
        }

        private static bool LoremasterFlavour(Hero master)
        {
            if (master == null) return false;
            return SotorTeachingLogic.LoremasterExemptionApplies(
                PlayerIsFullLoremaster(), SotorRivalSeeder.SocialTradition(master));
        }

        private static bool PlayerIsFullLoremaster()
        {
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null || !info.HasLore(AbilitySystem.SotorLores.HighMagic)) return false;
            return PlayerHasWholeSchool(AbilitySystem.SotorLores.HighMagic);
        }

        private static int PlayerCasterLevel()
        {
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null || info.AcquiredLores == null) return 1;

            int level = 1;
            bool hasMemberOnly = false;
            foreach (var loreId in info.AcquiredLores)
            {
                var t = SotorTraditions.TradForLore(loreId);
                if (t == Trad.None) continue;
                level += 1;
                if (SotorTraditions.IsMemberOnly(t)) hasMemberOnly = true;
            }
            if (hasMemberOnly) level += 1;

            if (level > 6) level = 6;
            return level;
        }

        private void StartTeachingPersuasion()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;

            bool spellAsk = _askMode == AskMode.Spell && _offeredSpellId != null;
            if (_offeredLoreId == null)
            {

                SotorLog.Warn($"RivalTeach: StartTeachingPersuasion with no target for {master.Name}, aborting.");
                return;
            }

            var houseTrad = SotorRivalSeeder.SocialTradition(master);
            int tier = master.Clan?.Tier ?? 0;

            var diff = SotorTeachingLogic.AdjustDifficulty(
                SotorTeachingLogic.BaseDifficulty(tier), houseTrad, _offeredLore);

            _task = BuildTask(master);

            int successes = SotorTeachingLogic.SuccessesRequired(
                lore: _offeredLore,
                masterClanTier: tier,
                playerClanTier: Clan.PlayerClan?.Tier ?? 0,
                relation: (int)master.GetRelationWithPlayer(),
                traditionStanding: SotorRivalStanding.IsReady ? SotorRivalStanding.GetTradition(_offeredLore) : 0,
                playerSpellcraft: GetPlayerSpellcraft(),
                masterSpellcraft: master.GetSkillValue(AbilitySystem.SotorSkills.Spellcraft));

            ConversationManager.StartPersuasion(
                successes, SuccessValue, FailValue, CriticalSuccessValue, CriticalFailValue,
                initialProgress: 0f, difficulty: (PersuasionDifficulty)(int)diff);

            SotorLog.Info(
                $"RivalTeach: arguments for {master.Name}/{_offeredLoreId} = "
                + $"[{string.Join(",", _offeredArguments.ConvertAll(a => a.Skill.ToString()))}] "
                + $"{_relSummary} [{_argSummary}] crit=slot{_critSlot} loremaster={LoremasterFlavour(master)} "
                + $"(fullLoremaster={PlayerIsFullLoremaster()}, house={SotorRivalSeeder.SocialTradition(master)}).");

            SotorLog.Info(
                $"RivalTeach: {master.Name} offered {_offeredLore} at difficulty {diff}, " +
                $"needs {successes} success(es) (masterTier={tier}, playerTier={Clan.PlayerClan?.Tier ?? 0}, " +
                $"relation={(int)master.GetRelationWithPlayer()}, spellcraft={GetPlayerSpellcraft()}).");

            SetArgumentTexts(master);
        }

        private static int GetPlayerSpellcraft()
        {
            var skill = AbilitySystem.SotorSkills.Spellcraft;
            return skill == null ? 0 : Hero.MainHero.GetSkillValue(skill);
        }

        private float ArgumentPoolRoll(Hero master)
        {
            if (master == null) return 0f;
            uint loreSalt = (uint)((_offeredLoreId ?? "").GetDeterministicHashCode());
            return master.RandomFloatWithSeed(SotorTraditions.SaltArgumentPool ^ loreSalt);
        }

        private static SkillObject SkillFor(SotorArgumentPool.ArgSkill skill)
        {
            switch (skill)
            {
                case SotorArgumentPool.ArgSkill.Leadership: return DefaultSkills.Leadership;
                case SotorArgumentPool.ArgSkill.Spellcraft: return AbilitySystem.SotorSkills.Spellcraft ?? DefaultSkills.Charm;
                case SotorArgumentPool.ArgSkill.Trade: return DefaultSkills.Trade;
                case SotorArgumentPool.ArgSkill.Roguery: return DefaultSkills.Roguery;
                case SotorArgumentPool.ArgSkill.Medicine: return DefaultSkills.Medicine;
                case SotorArgumentPool.ArgSkill.Tactics: return DefaultSkills.Tactics;
                case SotorArgumentPool.ArgSkill.Steward: return DefaultSkills.Steward;
                default: return DefaultSkills.Charm;
            }
        }

        private PersuasionTask BuildTask(Hero master)
        {
            var task = new PersuasionTask(0);

            var houseTrad = SotorRivalSeeder.SocialTradition(master);
            var worstLore = SotorTeachingLogic.OffendingTradition(
                houseTrad, SotorRivalSeeder.TeachableTraditions(Hero.MainHero));
            int affinityShift = SotorTeachingLogic.AffinityRelationShift(houseTrad, worstLore);

            float rel = master.GetRelationWithPlayer() + affinityShift;
            PersuasionArgumentStrength strength =
                rel >= 20f ? PersuasionArgumentStrength.Easy :
                rel >= 0f ? PersuasionArgumentStrength.Normal :
                rel <= -30f ? PersuasionArgumentStrength.VeryHard :
                PersuasionArgumentStrength.Hard;

            _relSummary = $"rel={master.GetRelationWithPlayer():0}{(affinityShift >= 0 ? "+" : "")}{affinityShift}"
                          + $"(aff:{worstLore})={rel:0}->{strength}";

            float poolRoll = ArgumentPoolRoll(master);
            _offeredArguments = SotorArgumentPool.Offered(poolRoll);
            var traits = new[] { DefaultTraits.Honor, DefaultTraits.Mercy, DefaultTraits.Valor };

            int critIndex = SotorArgumentPool.CritIndex(poolRoll);
            _critSlot = critIndex;
            var argLog = new List<string>();

            for (int i = 0; i < _offeredArguments.Count; i++)
            {
                var arg = _offeredArguments[i];

                bool canCrit = i == critIndex;

                var optionSkill = SkillFor(arg.Skill);
                int skillValue = optionSkill == null ? 0 : Hero.MainHero.GetSkillValue(optionSkill);
                var optionStrength = ShiftStrength(strength, SotorArgumentPool.SkillStrengthShift(skillValue));

                argLog.Add($"{arg.Skill}({skillValue})->{optionStrength}{(canCrit ? " CRIT" : "")}");

                task.AddOptionToTask(new PersuasionOptionArgs(
                    optionSkill,
                    traits[i % traits.Length], TraitEffect.Positive, optionStrength,
                    givesCriticalSuccess: canCrit,
                    SotorText.GetObject("sotor_teach_arg_" + (i + 1)),
                    null, canBlockOtherOption: false, canMoveToTheNextReservation: true));
            }

            _argSummary = string.Join(", ", argLog);

            return task;
        }

        private static PersuasionArgumentStrength ShiftStrength(PersuasionArgumentStrength baseStrength, int steps)
        {
            int v = (int)baseStrength + steps;
            if (v < (int)PersuasionArgumentStrength.ExtremelyHard) v = (int)PersuasionArgumentStrength.ExtremelyHard;
            if (v > (int)PersuasionArgumentStrength.ExtremelyEasy) v = (int)PersuasionArgumentStrength.ExtremelyEasy;
            return (PersuasionArgumentStrength)v;
        }

        private PersuasionOptionArgs Option(int i)
        {
            return _task != null && i < _task.Options.Count ? _task.Options[i] : null;
        }
        private bool ArgExists(int i) => _task != null && i < _task.Options.Count;
        private bool ArgClickableCond(int i, out TextObject explanation)
        {
            explanation = TextObject.GetEmpty();
            var o = Option(i);
            return o != null && !o.IsBlocked;
        }

        private bool HubSuccess()
        {
            if (_task == null) return false;
            SetLineVariables(Hero.OneToOneConversationHero);
            return ConversationManager.GetPersuasionProgressSatisfied();
        }

        private bool HubFail()
        {

            if (_task == null) return false;
            SetLineVariables(Hero.OneToOneConversationHero);
            return _task.Options.All(x => x.IsBlocked) && !ConversationManager.GetPersuasionProgressSatisfied();
        }

        private void OnTeachingSucceeded()
        {

            ClearRetryCooldown(Hero.OneToOneConversationHero);

            var master = Hero.OneToOneConversationHero;
            EndPersuasionConsequence();
            if (master == null || _offeredLoreId == null) return;

            AcceptAsStudent(master);

            var info = Hero.MainHero.GetExtendedInfo();
            bool taughtLore = false;
            string taughtSpellId = null;

            if (_askMode == AskMode.Spell && _offeredSpellId != null)
            {
                taughtSpellId = _offeredSpellId;
                GrantSingleSpell(Hero.MainHero, taughtSpellId);
            }
            else if (info != null && !info.HasLore(_offeredLoreId))
            {
                taughtLore = true;
                GrantLoreOnly(Hero.MainHero, _offeredLoreId);
            }
            else
            {

                SotorLog.Info($"RivalTeach: {master.Name} accepted the player as a student; he already holds "
                              + $"{_offeredLoreId}, so no school was granted.");
            }

            ChangeRelationAction.ApplyPlayerRelation(master, 2, affectRelatives: false, showQuickNotification: true);

            if (SotorRivalStanding.IsReady && (taughtLore || taughtSpellId != null))
            {
                SotorRivalStanding.ApplyLearningInfluence(_offeredLore, isLore: taughtLore);
            }

            if (!taughtLore && taughtSpellId == null)
            {

                var accepted = SotorText.GetObject("sotor_teach_accepted_student");
                accepted.SetTextVariable("LORE", LoreTitle(_offeredLoreId));
                MBTextManager.SetTextVariable("SOTOR_TEACH_BLURB", accepted.ToString());
            }
            else if (taughtLore)
            {
                SetUnlockBlurbText("sotor_unlock_lore_" + _offeredLoreId, master, _offeredLoreId, null);
                SotorLog.Info($"RivalTeach: {master.Name} unlocked the LORE {_offeredLore} ({_offeredLoreId}) for the player (no spells).");
            }
            else if (taughtSpellId != null)
            {
                SetUnlockBlurbText("sotor_unlock_spell_" + taughtSpellId, master, _offeredLoreId, taughtSpellId);

                var pi = Hero.MainHero.GetExtendedInfo();
                SotorLog.Info(
                    $"RivalTeach: {master.Name} taught the player the SPELL '{taughtSpellId}' ({_offeredLore}). " +
                    $"owned={pi?.HasSpell(taughtSpellId)} castable={Hero.MainHero.HasAbility(taughtSpellId)} " +
                    $"EQUIPPED={pi?.IsAbilitySelected(taughtSpellId)} " +
                    $"selected=[{string.Join(",", pi?.SelectedAbilities ?? new System.Collections.Generic.List<string>())}]");
            }

            _offeredLore = Trad.None;
            _offeredLoreId = null;
            _offeredSpellId = null;
            _askMode = AskMode.Lore;
        }

        private void OnTeachingFailed()
        {
            LeaveEncounterLikeNative();
            BeginRetryCooldown(Hero.OneToOneConversationHero);
            EndPersuasionConsequence();
        }

        private void OnTeachingAbandoned()
        {
            EndPersuasionConsequence();
        }

        private void MarkArgumentSpoken()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;
            BeginRetryCooldown(master);
        }

        private void EndPersuasionConsequence()
        {
            if (ConversationManager.GetPersuasionIsActive())
            {
                ConversationManager.EndPersuasion();
            }
            _task = null;
        }

        private static void SetOffendingTraditionVariables(Hero master)
        {
            var houseTrad = SotorRivalSeeder.SocialTradition(master);
            var offending = SotorTeachingLogic.OffendingTradition(
                houseTrad, SotorRivalSeeder.TeachableTraditions(Hero.MainHero));

            string loreId = SotorTraditions.LoreIdFor(offending);
            MBTextManager.SetTextVariable("LORE", loreId != null
                ? SotorText.Get("sotor_lore_short_" + loreId)
                : SotorText.Get("sotor_rumour_unknown_tradition"));

            var obj = SotorTraditionObject.For(offending);
            string plural = obj != null ? obj.Name.ToString() : null;
            string singular = string.IsNullOrEmpty(plural)
                ? SotorText.Get("sotor_rumour_unknown_tradition")
                : (plural.EndsWith("s") ? plural.Substring(0, plural.Length - 1) : plural);
            MBTextManager.SetTextVariable("PRACTITIONER", singular);

            SotorLog.Info($"RivalTeach: doctrine refusal names lore='{SotorText.GetObject("sotor_lore_short_" + (loreId ?? "?")).ToString()}' "
                          + $"practitioner='{singular}' (offending={offending}, notoriety={SotorTraditions.Notoriety(offending)}, "
                          + $"house={houseTrad}).");
        }

        private void SetRefusalText(Hero master, TeachOutcome outcome)
        {
            string line;
            switch (outcome)
            {
                case TeachOutcome.VetoWar:
                    line = SotorText.Get("sotor_teach_refuse_war");
                    break;
                case TeachOutcome.VetoDoctrine:

                    line = SotorText.Get("sotor_teach_refuse_doctrine");
                    SetOffendingTraditionVariables(master);
                    break;
                case TeachOutcome.VetoStanding:

                    line = SotorText.Get("sotor_teach_refuse_standing");
                    break;
                case TeachOutcome.VetoCoerced:

                    line = CoercionRefusalLine(master);
                    break;
                case TeachOutcome.VetoRecentFailure:

                    line = SotorText.Get("sotor_teach_refuse_recent_failure");
                    break;
                case TeachOutcome.VetoIgnorance:

                    line = SotorText.Get("sotor_teach_refuse_ignorance");
                    break;
                default:

                    string mastery = MasteryLineOrNull(master);
                    string loreDefer = mastery == null ? LoremasterRefusalOrNull(master) : null;
                    line = mastery ?? loreDefer ?? SotorText.Get("sotor_teach_refuse_knows_all");

                    string rung = mastery != null ? "1 MASTERY" : loreDefer != null ? "2 LOREMASTER" : "3 STANDARD";
                    if (rung != _lastLoggedRung)
                    {
                        _lastLoggedRung = rung;
                        SotorLog.Info($"RivalTeach: knows-all refusal for {master.Name} took rung {rung} "
                                      + $"(loremaster={LoremasterFlavour(master)}).");
                    }
                    break;
            }
            MBTextManager.SetTextVariable("SOTOR_TEACH_REFUSE", line);
        }

        private string CoercionRefusalLine(Hero master)
        {
            const string NoBlurb = "\u0000_sotor_no_blurb";
            string best = null;
            Trad bestTrad = Trad.None;

            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string loreId = SotorTraditions.LoreIdFor(t);
                if (loreId == null) continue;
                string custom = SotorText.Get("sotor_refuse_lore_" + loreId, NoBlurb);
                if (custom == NoBlurb || string.IsNullOrWhiteSpace(custom)) continue;

                if (best == null || SotorTraditions.Rarity(t) > SotorTraditions.Rarity(bestTrad))
                {
                    best = custom;
                    bestTrad = t;
                }
            }

            if (best == null)
            {
                SotorLog.Info($"RivalTeach: {master.Name} refuses the man who coerced him, but no refusal blurb "
                              + "is written for his lore; using the generic line.");
                return SotorText.Get("sotor_teach_refuse_knows_all");
            }

            SotorLog.Info($"RivalTeach: {master.Name} speaks the COERCION refusal for {bestTrad}.");
            return best;
        }

        private string MasteryLineOrNull(Hero master)
        {
            if (master == null) return null;

            var playerInfo = Hero.MainHero.GetExtendedInfo();
            if (playerInfo == null) return null;

            foreach (var t in SotorRivalSeeder.CoercibleTraditions(master))
            {
                string loreId = SotorTraditions.LoreIdFor(t);
                if (loreId == null || !playerInfo.HasLore(loreId)) continue;

                if (!MasterHasWholeSchool(master, loreId)) continue;

                if (!PlayerHasWholeSchool(loreId)) continue;

                const string NoBlurb = "\u0000_sotor_no_blurb";
                string custom = SotorText.Get("sotor_master_lore_" + loreId, NoBlurb);
                if (custom == NoBlurb || string.IsNullOrWhiteSpace(custom)) continue;

                SotorLog.Info($"RivalTeach: {master.Name} speaks the MASTERY line for {loreId}: he holds the whole "
                              + "school and so does the player.");
                return custom;
            }
            return null;
        }

        private static bool MasterHasWholeSchool(Hero master, string loreId)
        {
            var info = master?.GetExtendedInfo();
            if (info == null) return false;
            bool any = false;
            foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null || !SotorRivalSeeder.IsAiSafeSpell(id)) continue;
                any = true;
                if (!info.HasSpell(id) && !master.HasAbility(id)) return false;
            }
            return any;
        }

        private static bool PlayerHasWholeSchool(string loreId)
        {
            var info = Hero.MainHero.GetExtendedInfo();
            if (info == null) return false;
            bool any = false;
            foreach (var template in AbilitySystem.AbilityFactory.GetTemplatesByLore(loreId))
            {
                string id = template?.StringID;
                if (id == null) continue;
                any = true;
                if (!info.HasSpell(id) && !Hero.MainHero.HasAbility(id)) return false;
            }
            return any;
        }

        private const string LoremasterSuffix = "_loremaster";

        private const string NoVariant = "\u0000_sotor_no_variant";

        private void SetArgumentTexts(Hero master)
        {

            bool lm = LoremasterFlavour(master);

            for (int i = 0; i < SotorArgumentPool.OfferedCount; i++)
            {
                string text;
                if (i < _offeredArguments.Count)
                {
                    string id = _offeredArguments[i].StringId;
                    text = null;
                    if (lm)
                    {

                        string variant = SotorText.Get(id + LoremasterSuffix, NoVariant);
                        if (variant != NoVariant && !string.IsNullOrWhiteSpace(variant)) text = variant;
                    }
                    if (text == null) text = SotorText.Get(id);
                }
                else
                {
                    text = string.Empty;
                }
                MBTextManager.SetTextVariable("SOTOR_TEACH_ARG_" + (i + 1), text);
            }

            MBTextManager.SetTextVariable("SOTOR_TEACH_REACTION",
                lm ? SotorText.Get("sotor_teach_reaction_text_loremaster") : SotorText.Get("sotor_teach_reaction_text"));
        }

        private static TextObject LoreTitle(string loreId)
        {
            return new TextObject(AbilitySystem.SotorLores.TitleFor(loreId));
        }
    }
}
