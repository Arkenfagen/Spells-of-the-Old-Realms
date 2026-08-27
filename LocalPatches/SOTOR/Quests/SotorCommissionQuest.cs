using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace SOTOR.Quests
{

    public class SotorCommissionQuest : QuestBase
    {
        public const int CommissionDays = 30;

        [SaveableField(1)] private readonly string _traitId;
        [SaveableField(2)] private JournalLog _task;

        public SotorCommissionQuest(Hero master, string traitId)
            : base("sotor_commission_" + master.StringId + "_" + traitId, master,
                   CampaignTime.DaysFromNow(CommissionDays), 0)
        {
            _traitId = traitId;
        }

        public string TraitId => _traitId;

        public override TextObject Title
        {
            get
            {
                var t = SotorText.GetObject("sotor_commission_quest_title");
                t.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
                return t;
            }
        }

        public override bool IsRemainingTimeHidden => false;

        public override string SpecialQuestType => "SotorCommissionQuest";

        protected override void SetDialogs() { }

        protected override void InitializeQuestOnGameLoad()
        {
            SotorLog.Info($"Commission: quest survived the load - master={QuestGiver?.Name}, trait='{_traitId}', "
                          + $"{Math.Max(0.0, QuestDueTime.RemainingDaysFromNow):0.0} day(s) left.");
        }

        protected override void OnStartQuest()
        {
            var log = SotorText.GetObject("sotor_commission_quest_log");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            log.SetTextVariable("TRAIT", TraitName(_traitId));
            _task = AddLog(log);

            SotorLog.Info($"Commission: {QuestGiver?.Name} gave the player '{_traitId}' against an item "
                          + $"bearing it, due in {CommissionDays} days.");
        }

        protected override void RegisterEvents()
        {
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            if (victim != QuestGiver) return;

            var log = SotorText.GetObject("sotor_commission_quest_giver_died");
            log.SetTextVariable("MASTER", QuestGiver.Name);
            CompleteQuestWithFail(log);
        }

        public void OnDelivered()
        {
            SotorLog.Info($"Commission: the player settled '{_traitId}' with {QuestGiver?.Name}; quest complete.");
            CompleteQuestWithSuccess();
        }

        protected override void OnTimedOut()
        {
            var log = SotorText.GetObject("sotor_commission_quest_expired");
            log.SetTextVariable("MASTER", QuestGiver?.Name ?? new TextObject(""));
            log.SetTextVariable("TRAIT", TraitName(_traitId));
            AddLog(log);

            CampaignBehaviors.SotorTeachingBehavior.NoteForgottenApprentice(QuestGiver);
            SotorLog.Info($"Commission: the player let '{_traitId}' lapse; {QuestGiver?.Name} has forgotten him. "
                          + "No penalty applied, by design.");
        }

        private static TextObject TraitName(string traitId)
        {
            var trait = SotorItemTraitManager.GetTrait(traitId);
            return new TextObject(trait != null ? trait.ItemTraitName : traitId);
        }

        public static SotorCommissionQuest ActiveFor(Hero master)
        {
            if (master == null) return null;
            foreach (var q in All())
            {
                if (q.QuestGiver == master) return q;
            }
            return null;
        }

        private static IEnumerable<SotorCommissionQuest> All()
        {
            var manager = Campaign.Current?.QuestManager;
            if (manager == null) yield break;

            foreach (var q in manager.Quests)
            {
                if (q is SotorCommissionQuest commission && !commission.IsFinalized) yield return commission;
            }
        }

        public static List<ItemRosterElement> DeliverableItems(string traitId)
        {
            var result = new List<ItemRosterElement>();
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null || string.IsNullOrEmpty(traitId)) return result;

            foreach (var entry in roster)
            {
                var item = entry.EquipmentElement.Item;
                if (item == null) continue;
                if (CampaignBehaviors.SotorBlueprintBookBehavior.IsBookItemId(item.StringId)) continue;
                if (!SotorExtendedItemManager.GetTraitIdsOfItem(item.StringId).Contains(traitId)) continue;
                result.Add(entry);
            }
            return result;
        }
    }
}
