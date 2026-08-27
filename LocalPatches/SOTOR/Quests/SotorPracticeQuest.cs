using System;
using SOTOR.AbilitySystem.Rivals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace SOTOR.Quests
{

    public class SotorPracticeQuest : QuestBase
    {
        public const int PracticeDays = 30;

        [SaveableField(1)] private readonly string _spellId;
        [SaveableField(2)] private readonly string _loreId;
        [SaveableField(3)] private readonly int _target;
        [SaveableField(4)] private readonly int _objective;
        [SaveableField(5)] private int _progress;

        [SaveableField(6)] private JournalLog _task;
        [SaveableField(7)] private JournalLog _returnTask;

        public SotorPracticeQuest(Hero master, string spellId, string loreId, int objective, int target)
            : base("sotor_practice_" + master.StringId + "_" + spellId, master,
                   CampaignTime.DaysFromNow(PracticeDays), 0)
        {
            _spellId = spellId;
            _loreId = loreId;
            _objective = objective;
            _target = target < 1 ? 1 : target;
        }

        public string SpellId => _spellId;
        public string LoreId => _loreId;
        public SotorPracticeTracker.Objective Objective => (SotorPracticeTracker.Objective)_objective;
        public bool IsComplete => _progress >= _target;

        public override TextObject Title
        {
            get
            {
                var t = SotorText.GetObject("sotor_practice_quest_title");
                t.SetTextVariable("SPELL", SpellTitle(_spellId));
                return t;
            }
        }

        public override bool IsRemainingTimeHidden => false;

        public override string SpecialQuestType => "SotorPracticeQuest";

        protected override void SetDialogs()
        {
        }

        protected override void InitializeQuestOnGameLoad()
        {
            SotorLog.Info($"Practice: quest survived the load - master={QuestGiver?.Name}, spell='{_spellId}', "
                          + $"{_progress}/{_target}, {Math.Max(0.0, QuestDueTime.RemainingDaysFromNow):0.0} day(s) left.");
        }

        protected override void OnStartQuest()
        {
            var task = SotorText.GetObject(SotorPracticeTracker.TaskStringId(Objective));
            task.SetTextVariable("SPELL", SpellTitle(_spellId));
            task.SetTextVariable("TARGET", _target);
            task.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));

            var counter = SotorText.GetObject(SotorPracticeTracker.CounterStringId(Objective));

            _task = AddDiscreteLog(task, counter, _progress, _target);

            SotorLog.Info($"Practice: {QuestGiver?.Name} set the player to practise '{_spellId}' "
                          + $"({Objective}, target {_target}, {PracticeDays} days).");
        }

        protected override void RegisterEvents()
        {
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim != QuestGiver) return;

            var log = SotorText.GetObject("sotor_practice_quest_giver_died");
            log.SetTextVariable("MASTER", QuestGiver.Name);
            CompleteQuestWithFail(log);
        }

        public void Advance(int amount)
        {
            if (amount <= 0 || IsComplete) return;

            _progress += amount;
            if (_progress > _target) _progress = _target;

            _task?.UpdateCurrentProgress(_progress);

            SotorLog.Info($"Practice: '{_spellId}' {_progress}/{_target} ({Objective}, +{amount}).");

            if (IsComplete && _returnTask == null)
            {
                var log = SotorText.GetObject("sotor_practice_task_return");
                log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
                _returnTask = AddLog(log);
                SotorLog.Info($"Practice: '{_spellId}' drilled to {_progress}/{_target}; "
                              + $"return to {QuestGiver?.Name}.");
            }
        }

        public void OnAccepted()
        {
            SotorLog.Info($"Practice: {QuestGiver?.Name} signed off '{_spellId}'; quest complete.");
            CompleteQuestWithSuccess();
        }

        protected override void OnTimedOut()
        {
            var log = SotorText.GetObject("sotor_practice_quest_expired");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            log.SetTextVariable("SPELL", SpellTitle(_spellId));
            AddLog(log);

            CampaignBehaviors.SotorTeachingBehavior.NoteForgottenApprentice(QuestGiver);
            SotorLog.Info($"Practice: the player let '{_spellId}' lapse; {QuestGiver?.Name} has forgotten him. "
                          + "No penalty applied, by design.");
        }

        private static TextObject SpellTitle(string abilityId)
        {
            var template = AbilitySystem.AbilityFactory.GetTemplate(abilityId);
            return new TextObject(string.IsNullOrEmpty(template?.Name) ? abilityId : template.Name);
        }

        public static bool AnyActive()
        {
            foreach (var q in All()) return true;
            return false;
        }

        public static SotorPracticeQuest ActiveFor(Hero master)
        {
            if (master == null) return null;
            foreach (var q in All())
            {
                if (q.QuestGiver == master) return q;
            }
            return null;
        }

        public static SotorPracticeQuest ActiveForSpell(string spellIdOrName)
        {
            if (string.IsNullOrEmpty(spellIdOrName)) return null;
            foreach (var q in All())
            {
                if (q.MatchesSpell(spellIdOrName)) return q;
            }
            return null;
        }

        private bool MatchesSpell(string spellIdOrName)
        {
            if (string.IsNullOrEmpty(spellIdOrName)) return false;
            if (_spellId == spellIdOrName) return true;

            var template = AbilitySystem.AbilityFactory.GetTemplate(_spellId);
            return template != null && !string.IsNullOrEmpty(template.Name)
                   && template.Name == spellIdOrName;
        }

        private static System.Collections.Generic.IEnumerable<SotorPracticeQuest> All()
        {
            var manager = Campaign.Current?.QuestManager;
            if (manager == null) yield break;

            foreach (var q in manager.Quests)
            {
                if (q is SotorPracticeQuest practice && !practice.IsFinalized) yield return practice;
            }
        }
    }
}
