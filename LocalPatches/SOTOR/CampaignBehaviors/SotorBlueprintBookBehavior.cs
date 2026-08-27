using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using SOTOR.Items;
using SOTOR.Quests;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorBlueprintBookBehavior : CampaignBehaviorBase
    {
        private const string BookIdPrefix = "sotor_book_";
        private const float LordBookDropChance = 0.08f;
        private const float LordRestrictedBookChance = 0.02f;
        private const float MasterDiscount = 0.75f;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, AwardLordBookDrops);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddMasterDialog(starter);
            RegisterBooksAsMagicCarriers();
            InstallInventoryUseHook();

            SotorRuneLines.LogWorldCoverage();
        }

        private static void RegisterBooksAsMagicCarriers()
        {
            int count = 0;
            foreach (var trait in SotorItemTraitManager.CraftableTraits)
            {
                var book = BookFor(trait.ItemTraitStringId);
                if (book == null) continue;
                SotorExtendedItemManager.RegisterItemTraits(book.StringId,
                    new List<string> { trait.ItemTraitStringId });
                count++;
            }
            SotorLog.Info($"Registered {count} blueprint book(s) as magic-item carriers");
        }

        public static bool IsBookItemId(string itemId)
        {
            return itemId != null && itemId.StartsWith(BookIdPrefix, StringComparison.Ordinal);
        }

        private void InstallInventoryUseHook()
        {
            SotorInventoryUse.HintText = SotorText.Rendered("sotor_inventory_use_item");
            SotorInventoryUse.IsUsable = id =>
            {

                try { return IsBookItemId(id) && IsPlainInventoryOpen(); }
                catch { return false; }
            };
            SotorInventoryUse.Use = UseBookFromInventory;
        }

        private static bool IsPlainInventoryOpen()
        {
            return TaleWorlds.Core.GameStateManager.Current?.ActiveState
                is TaleWorlds.CampaignSystem.GameState.InventoryState state
                && state.InventoryMode == Helpers.InventoryScreenHelper.InventoryMode.Default;
        }

        private void UseBookFromInventory(string itemId)
        {
            if (Campaign.Current == null || !IsBookItemId(itemId)) return;
            var traitId = itemId.Substring(BookIdPrefix.Length);
            if (SotorItemTraitManager.GetTrait(traitId) == null) return;

            var book = BookFor(traitId);
            if (book == null || !(MobileParty.MainParty?.ItemRoster?.GetItemNumber(book) > 0)) return;
            PickHeroForBook(traitId);
        }

        public static bool HeroMeetsGate(Hero hero, SotorItemTrait trait, bool ignoreLevel = false)
        {
            if (hero == null || trait == null) return false;

            if (trait.HasSkillRequirement)
            {
                return SotorSkillGate.Meets(hero, trait);
            }
            var level = SotorSpellcraftHelper.GetCastingLevel(hero);
            if (level == SpellCastingLevel.None) return false;
            if (!ignoreLevel && level < trait.RequiredCastingLevel) return false;
            if (trait.HasLoreRequirement)
            {
                var info = hero.GetExtendedInfo();
                if (info == null || !info.HasLore(trait.RequiredLore)) return false;
            }
            return true;
        }

        public static bool TryLearn(Hero hero, SotorItemTrait trait, bool ignoreLevel = false)
        {
            if (!HeroMeetsGate(hero, trait, ignoreLevel)) return false;
            var info = hero.GetExtendedInfo();
            if (info == null || info.HasBlueprint(trait.ItemTraitStringId)) return false;
            info.AddBlueprint(trait.ItemTraitStringId);
            var msg = SotorText.GetObject("sotor_blueprint_learned");
            msg.SetTextVariable("HERO", hero.Name);
            msg.SetTextVariable("TRAIT", trait.ItemTraitName);
            SotorRibbon.Show(msg, SotorRibbon.DefaultMs, hero);
            SotorLog.Info($"Blueprint learned: {hero.Name} -> {trait.ItemTraitStringId}");
            return true;
        }

        public static ItemObject BookFor(string traitId)
        {
            return MBObjectManager.Instance?.GetObject<ItemObject>(BookIdPrefix + traitId);
        }

        public static List<SotorItemTrait> CurrentShelf(Town town)
        {
            if (town == null) return new List<SotorItemTrait>();
            var clan = town.OwnerClan;
            var trad = clan != null ? SotorRivalSeeder.DeriveClanTradition(clan) : Trad.None;
            string rulingLore = trad != Trad.None ? SotorTraditions.LoreIdFor(trad) : "";
            int week = (int)(CampaignTime.Now.ToWeeks);
            var ids = SotorBookShelf.ShelfFor(
                SotorRivalSeeder.WorldSeedText(), town.Settlement.StringId, week,
                town.Prosperity, rulingLore, SotorBookShelf.LiveRoster());
            return SotorItemTraitManager.GetTraits(ids);
        }

        private void PickHeroForBook(string traitId)
        {
            var trait = SotorItemTraitManager.GetTrait(traitId);
            if (trait == null) return;

            string requirement = SotorSkillGate.RequirementText(trait);
            string needsLine = SotorSkillGate.NeedsLine(trait);
            string blockedHint = string.IsNullOrEmpty(needsLine)
                ? SotorText.Rendered("sotor_read_hero_blocked")
                : needsLine;

            string bodyText = SotorText.Rendered("sotor_read_hero_text");
            if (!string.IsNullOrEmpty(needsLine)) bodyText = bodyText + "\n" + needsLine;

            var candidates = new List<InquiryElement>();
            foreach (var hero in PartyHeroes())
            {
                if (hero == null) continue;
                var info = hero.GetExtendedInfo();
                bool known = info?.HasBlueprint(traitId) == true;
                bool eligible = !known && HeroMeetsGate(hero, trait);
                string hint =
                    known ? SotorText.Rendered("sotor_shelf_known_hint") :
                    eligible ? SotorText.Rendered("sotor_read_hero_hint") :
                    blockedHint;
                candidates.Add(new InquiryElement(hero, hero.Name.ToString(),
                    new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject)), eligible, hint));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                SotorText.Rendered("sotor_read_hero_title"),
                bodyText,
                candidates, true, 1, 1,
                SotorText.Rendered("sotor_str_accept"), SotorText.Rendered("sotor_str_cancel"),
                selected =>
                {
                    var pick = selected?.FirstOrDefault();
                    if (!(pick?.Identifier is Hero hero)) return;

                    StageRead(hero, trait);
                }, null), true, false);
        }

        private static readonly List<(Hero hero, SotorItemTrait trait)> _stagedReads =
            new List<(Hero, SotorItemTrait)>();

        private static void StageRead(Hero hero, SotorItemTrait trait)
        {
            if (hero == null || trait == null) return;
            if (_stagedReads.Any(s => s.hero == hero && s.trait == trait)) return;
            _stagedReads.Add((hero, trait));
            SotorLog.Info($"Staged read: {hero.Name} -> {trait.ItemTraitStringId}");
        }

        public static void CommitStagedReads()
        {
            if (_stagedReads.Count == 0) return;
            var staged = _stagedReads.ToList();
            _stagedReads.Clear();
            foreach (var (hero, trait) in staged)
            {
                var book = BookFor(trait.ItemTraitStringId);
                if (book == null) continue;
                if (MobileParty.MainParty?.ItemRoster?.GetItemNumber(book) > 0)
                {
                    TryLearn(hero, trait);
                }
                else
                {
                    var msg = SotorText.GetObject("sotor_blueprint_not_kept");
                    msg.SetTextVariable("TRAIT", trait.ItemTraitName);
                    SotorRibbon.Show(msg, SotorRibbon.DefaultMs, hero);
                    SotorLog.Info($"Staged read dropped (book not kept): {trait.ItemTraitStringId} for {hero?.Name}");
                }
            }
        }

        public static void DiscardStagedReads()
        {
            if (_stagedReads.Count > 0)
            {
                SotorLog.Info($"Discarded {_stagedReads.Count} staged read(s) (inventory cancelled)");
                _stagedReads.Clear();
            }
        }

        private static IEnumerable<Hero> PartyHeroes()
        {
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null) yield break;
            for (int i = 0; i < roster.Count; i++)
            {
                var character = roster.GetCharacterAtIndex(i);
                if (character != null && character.IsHero) yield return character.HeroObject;
            }
        }

        private void AwardLordBookDrops(MapEvent mapEvent)
        {
            try
            {
                if (!SotorSettings.EnableEnchanting) return;
                if (mapEvent == null || !mapEvent.HasWinner || mapEvent.PlayerSide != mapEvent.WinningSide) return;
                var enemySide = mapEvent.GetMapEventSide(
                    mapEvent.PlayerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                if (enemySide == null) return;

                var playerInfo = Hero.MainHero.GetExtendedInfo();
                var roster = TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.Current?.RosterToReceiveLootItems
                             ?? PartyBase.MainParty.ItemRoster;

                foreach (var party in enemySide.Parties)
                {
                    var lord = party.Party?.LeaderHero;
                    if (lord == null || !lord.IsLord || lord.Clan == null) continue;

                    if (MBRandom.RandomFloat < LordBookDropChance)
                    {
                        var runePool = SotorItemTraitManager.CraftableTraits
                            .Where(t => t.HasSkillRequirement && SotorSkillGate.Meets(lord, t))
                            .ToList();
                        if (runePool.Count > 0)
                        {
                            var runeTrait = runePool[MBRandom.RandomInt(runePool.Count)];
                            var runeBook = BookFor(runeTrait.ItemTraitStringId);
                            if (runeBook != null)
                            {
                                roster.AddToCounts(runeBook, 1);
                                SotorLog.Info($"Lord book drop (skill lane): {runeBook.Name} from {lord.Name} "
                                              + $"({runeTrait.ItemTraitStringId}, threshold {runeTrait.SkillThreshold})");
                            }
                        }
                    }

                    var trad = SotorRivalSeeder.DeriveClanTradition(lord.Clan);
                    if (trad == Trad.None) continue;

                    string lore = SotorTraditions.LoreIdFor(trad);
                    string pickedLore = null;
                    float roll = MBRandom.RandomFloat;
                    if (roll < LordRestrictedBookChance)
                    {

                        if (playerInfo?.HasLore(SotorLores.DarkMagic) == true) pickedLore = SotorLores.DarkMagic;
                        else if (playerInfo?.HasLore(SotorLores.HighMagic) == true) pickedLore = SotorLores.HighMagic;
                        else pickedLore = lore;
                    }
                    else if (roll < LordBookDropChance)
                    {
                        pickedLore = lore;
                    }
                    if (pickedLore == null) continue;

                    var pool = SotorItemTraitManager.CraftableTraits.Where(t => t.RequiredLore == pickedLore).ToList();
                    if (pool.Count == 0) continue;
                    var trait = pool[MBRandom.RandomInt(pool.Count)];
                    var book = BookFor(trait.ItemTraitStringId);
                    if (book == null) continue;
                    roster.AddToCounts(book, 1);
                    SotorLog.Info($"Lord book drop: {book.Name} from {lord.Name} ({pickedLore})");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AwardLordBookDrops failed: {ex.Message}");
            }
        }

        private void AddMasterDialog(CampaignGameStarter starter)
        {
            starter.AddPlayerLine("sotor_enchant_master_ask", "sotor_magic_hub", "sotor_enchant_master_answer",
                SotorText.Get("sotor_enchant_master_ask"),
                CanAskMasterForEnchants, null, 50);

            starter.AddDialogLine("sotor_commission_due", "sotor_enchant_master_answer", "sotor_commission_fork",
                SotorText.Get("sotor_commission_due"),
                CommissionDueWithItem, null, 300);

            starter.AddDialogLine("sotor_commission_empty_handed", "sotor_enchant_master_answer", "lord_pretalk",
                SotorText.Get("sotor_commission_empty_handed"),
                CommissionDueEmptyHanded, null, 290);

            starter.AddDialogLine("sotor_enchant_master_empty", "sotor_enchant_master_answer", "lord_pretalk",
                SotorText.Get("sotor_enchant_master_empty"),
                MasterShelfEmpty, null, 200);

            starter.AddDialogLine("sotor_enchant_master_answer", "sotor_enchant_master_answer", "lord_pretalk",
                SotorText.Get("sotor_enchant_master_answer"),
                () => true, OpenMasterShop, 100);

            starter.AddPlayerLine("sotor_commission_hand_over", "sotor_commission_fork", "sotor_commission_settle",
                SotorText.Get("sotor_commission_hand_over"),
                null, OpenDeliveryPicker, 100);
            starter.AddPlayerLine("sotor_commission_not_yet", "sotor_commission_fork", "lord_pretalk",
                SotorText.Get("sotor_commission_not_yet"), null, null, 10);

            starter.AddDialogLine("sotor_commission_settled", "sotor_commission_settle", "lord_pretalk",
                SotorText.Get("sotor_commission_settled"),
                () => _deliveryAnswer == DeliveryAnswer.Delivered, null, 100);
            starter.AddDialogLine("sotor_commission_kept", "sotor_commission_settle", "lord_pretalk",
                SotorText.Get("sotor_commission_kept"),
                () => _deliveryAnswer == DeliveryAnswer.Cancelled, null, 90);
        }

        private static bool CommissionDueWithItem()
        {
            var master = Hero.OneToOneConversationHero;
            var quest = master == null ? null : SotorCommissionQuest.ActiveFor(master);
            if (quest == null) return false;
            if (SotorCommissionQuest.DeliverableItems(quest.TraitId).Count == 0) return false;
            PublishCommissionVariables(master, quest);
            return true;
        }

        private static bool CommissionDueEmptyHanded()
        {
            var master = Hero.OneToOneConversationHero;
            var quest = master == null ? null : SotorCommissionQuest.ActiveFor(master);
            if (quest == null) return false;
            if (SotorCommissionQuest.DeliverableItems(quest.TraitId).Count > 0) return false;
            PublishCommissionVariables(master, quest);
            return true;
        }

        private static void PublishCommissionVariables(Hero master, SotorCommissionQuest quest)
        {
            SotorText.SetPlayerVariables();
            if (master != null) MBTextManager.SetTextVariable("MASTER", master.Name);
            if (quest == null) return;
            var trait = SotorItemTraitManager.GetTrait(quest.TraitId);
            MBTextManager.SetTextVariable("TRAIT",
                trait != null ? trait.ItemTraitName : new TextObject(quest.TraitId));
            MBTextManager.SetTextVariable("DAYS",
                (int)Math.Ceiling(Math.Max(0.0, quest.QuestDueTime.RemainingDaysFromNow)));
        }

        private bool CanAskMasterForEnchants()
        {
            return SotorSettings.EnableEnchanting && Hero.OneToOneConversationHero != null;
        }

        private bool MasterShelfEmpty()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return false;

            SotorText.SetPlayerVariables();
            return !MasterSellableTraits(master).Any();
        }

        private static List<SotorItemTrait> MasterSellableTraits(Hero master)
        {

            var lores = SotorRivalSeeder.CoercibleTraditions(master)
                .Where(t => t != Trad.None)
                .Select(SotorTraditions.LoreIdFor)
                .ToList();
            var info = Hero.MainHero.GetExtendedInfo();
            return SotorItemTraitManager.CraftableTraits
                .Where(t => t.HasLoreRequirement && lores.Contains(t.RequiredLore)

                         && HeroMeetsGate(master, t)
                         && info?.HasBlueprint(t.ItemTraitStringId) != true)
                .ToList();
        }

        private void OpenMasterShop()
        {
            var master = Hero.OneToOneConversationHero;
            if (master == null) return;

            MBTextManager.SetTextVariable("MASTER", master.Name);
            var elements = new List<InquiryElement>();
            foreach (var trait in MasterSellableTraits(master).OrderBy(t => t.LearnThreshold))
            {
                var book = BookFor(trait.ItemTraitStringId);
                if (book == null) continue;

                bool loreOk = HeroMeetsGate(Hero.MainHero, trait, ignoreLevel: true);
                elements.Add(new InquiryElement(trait.ItemTraitStringId, trait.ItemTraitName.ToString(),
                    new ItemImageIdentifier(book), loreOk,
                    loreOk ? trait.ItemTraitDescription : SotorText.Rendered("sotor_read_hero_blocked")));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                SotorText.Rendered("sotor_master_shop_title"),
                SotorText.Rendered("sotor_master_shop_text"),
                elements, true, 1, 1,
                SotorText.Rendered("sotor_str_accept"), SotorText.Rendered("sotor_str_cancel"),
                selected =>
                {
                    var pick = selected?.FirstOrDefault();
                    if (!(pick?.Identifier is string traitId)) return;
                    var trait = SotorItemTraitManager.GetTrait(traitId);
                    if (trait == null) return;
                    if (!TryLearn(Hero.MainHero, trait, ignoreLevel: true)) return;
                    BeginCommission(master, traitId);
                }, null), true, false);
        }

        private static void BeginCommission(Hero master, string traitId)
        {
            try
            {
                if (SotorCommissionQuest.ActiveFor(master) != null) return;
                new SotorCommissionQuest(master, traitId).StartQuest();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"Master shop: could not open the commission for '{traitId}' with "
                               + $"{master?.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private enum DeliveryAnswer { None, Delivered, Cancelled }
        private static DeliveryAnswer _deliveryAnswer;

        private void OpenDeliveryPicker()
        {
            var master = Hero.OneToOneConversationHero;
            var quest = master == null ? null : SotorCommissionQuest.ActiveFor(master);
            if (quest == null)
            {

                CancelDelivery();
                return;
            }

            _deliveryAnswer = DeliveryAnswer.None;
            var candidates = SotorCommissionQuest.DeliverableItems(quest.TraitId);
            var elements = new List<InquiryElement>();
            foreach (var entry in candidates)
            {
                var item = entry.EquipmentElement.Item;
                elements.Add(new InquiryElement(item.StringId, item.Name.ToString(),
                    new ItemImageIdentifier(item), true, item.Name.ToString()));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                SotorText.Rendered("sotor_commission_deliver_title"),
                SotorText.Rendered("sotor_commission_deliver_text"),
                elements, true, 1, 1,
                SotorText.Rendered("sotor_str_accept"), SotorText.Rendered("sotor_str_cancel"),
                selected =>
                {
                    var pick = selected?.FirstOrDefault();
                    if (pick?.Identifier is string itemId) DeliverCommissionItem(master, quest, itemId);
                    else CancelDelivery();
                },
                _ => CancelDelivery()), true, false);
        }

        private static void DeliverCommissionItem(Hero master, SotorCommissionQuest quest, string itemId)
        {
            try
            {

                var roster = MobileParty.MainParty?.ItemRoster;
                var match = SotorCommissionQuest.DeliverableItems(quest.TraitId)
                    .FirstOrDefault(e => e.EquipmentElement.Item?.StringId == itemId);
                if (roster == null || match.EquipmentElement.Item == null)
                {
                    CancelDelivery();
                    return;
                }
                roster.AddToCounts(match.EquipmentElement, -1);
                quest.OnDelivered();

                var msg = SotorText.GetObject("sotor_commission_delivered_msg");
                msg.SetTextVariable("MASTER", master?.Name ?? new TextObject(""));
                msg.SetTextVariable("ITEM", match.EquipmentElement.Item.Name);
                SotorRibbon.Show(msg, SotorRibbon.DefaultMs, master);

                _deliveryAnswer = DeliveryAnswer.Delivered;
            }
            catch (Exception ex)
            {
                SotorLog.Error($"Master shop: delivering '{itemId}' failed: {ex.GetType().Name}: {ex.Message}");
                _deliveryAnswer = DeliveryAnswer.Cancelled;
            }
            ResumeConversation();
        }

        private static void CancelDelivery()
        {
            _deliveryAnswer = DeliveryAnswer.Cancelled;
            ResumeConversation();
        }

        private static void ResumeConversation()
        {
            try
            {
                Campaign.Current?.ConversationManager?.ContinueConversation();
            }
            catch (Exception ex)
            {
                SotorLog.Error($"Master shop: could not resume the conversation after the delivery "
                               + $"picker: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
