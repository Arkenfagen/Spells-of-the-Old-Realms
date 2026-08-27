using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace SOTOR.Quests
{

    public class SotorApprenticeDuelQuest : QuestBase
    {

        public const int PreparationDays = 7;

        [SaveableField(1)] private readonly string _spellId;
        [SaveableField(2)] private readonly string _loreId;
        [SaveableField(3)] private readonly string _apprenticeId;
        [SaveableField(4)] private bool _apprenticeDefeated;

        public SotorApprenticeDuelQuest(Hero master, string spellId, string loreId, string apprenticeId)

            : base("sotor_champion_duel_" + master.StringId + "_" + spellId, master,
                   CampaignTime.DaysFromNow(PreparationDays), 0)
        {
            _spellId = spellId;
            _loreId = loreId;
            _apprenticeId = apprenticeId;
        }

        public string SpellId => _spellId;
        public string LoreId => _loreId;
        public bool ApprenticeDefeated => _apprenticeDefeated;

        public override TextObject Title
        {
            get
            {
                var t = SotorText.GetObject("sotor_duel_quest_title");
                t.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
                return t;
            }
        }

        public override bool IsRemainingTimeHidden => false;

        public override string SpecialQuestType => "SotorApprenticeDuel";

        protected override void SetDialogs()
        {
        }

        protected override void InitializeQuestOnGameLoad()
        {

            SotorLog.Info($"ApprenticeDuel: quest survived the load - master={QuestGiver?.Name}, spell='{_spellId}', "
                          + $"apprenticeDefeated={_apprenticeDefeated}, "
                          + $"{System.Math.Max(0.0, QuestDueTime.RemainingDaysFromNow):0.0} day(s) left.");
        }

        protected override void RegisterEvents()
        {

            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim != QuestGiver) return;

            var log = SotorText.GetObject("sotor_duel_quest_giver_died");
            log.SetTextVariable("MASTER", QuestGiver.Name);
            CompleteQuestWithFail(log);
        }

        protected override void OnStartQuest()
        {
            SetLogs();
        }

        private void SetLogs()
        {
            var log = SotorText.GetObject(_apprenticeDefeated
                ? "sotor_duel_quest_log_return"
                : "sotor_duel_quest_log_fight");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            log.SetTextVariable("SPELL", SpellTitle(_spellId));
            AddLog(log);
        }

        private static TextObject SpellTitle(string abilityId)
        {
            var template = AbilitySystem.AbilityFactory.GetTemplate(abilityId);
            return new TextObject(string.IsNullOrEmpty(template?.Name) ? abilityId : template.Name);
        }

        public void OnApprenticeDefeated()
        {
            if (_apprenticeDefeated) return;
            _apprenticeDefeated = true;
            SetLogs();
            SotorLog.Info($"ApprenticeDuel: player beat {QuestGiver?.Name}'s apprentice '{_apprenticeId}'. "
                          + $"'{_spellId}' is owed on his return.");
        }

        public void OnRewardPaid()
        {
            SotorLog.Info($"ApprenticeDuel: {QuestGiver?.Name} taught '{_spellId}'; quest complete.");
            CompleteQuestWithSuccess();
        }

        protected override void OnTimedOut()
        {
            var log = SotorText.GetObject("sotor_duel_quest_expired");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            AddLog(log);

            SotorLog.Info($"ApprenticeDuel: the player let {QuestGiver?.Name}'s challenge expire after "
                          + $"{PreparationDays} day(s); the quest failed and his cooldown is armed.");
            CampaignBehaviors.SotorTeachingBehavior.BeginDuelCooldown(QuestGiver);
        }

        public void OnPlayerDefeated()
        {
            var log = SotorText.GetObject("sotor_duel_quest_lost");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            SotorLog.Info($"ApprenticeDuel: player lost to {QuestGiver?.Name}'s apprentice; quest failed.");
            CompleteQuestWithFail(log);
        }

        public static SotorApprenticeDuelQuest ActiveFor(Hero master)
        {
            if (master == null || Campaign.Current?.QuestManager == null) return null;
            try
            {
                foreach (var q in Campaign.Current.QuestManager.Quests)
                {
                    if (q is SotorApprenticeDuelQuest duel && !duel.IsFinalized && duel.QuestGiver == master)
                    {
                        return duel;
                    }
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"ApprenticeDuel: quest lookup failed: {ex.GetType().Name}: {ex.Message}");
            }
            return null;
        }
    }
}
