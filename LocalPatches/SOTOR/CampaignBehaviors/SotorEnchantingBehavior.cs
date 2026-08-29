using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.Inquiries;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorEnchantingBehavior : CampaignBehaviorBase
    {
        private Dictionary<ItemObject, SotorEnchantedItemData> _enchantedItems =
            new Dictionary<ItemObject, SotorEnchantedItemData>();

        public static SotorEnchantingBehavior Instance =>
            Campaign.Current?.GetCampaignBehavior<SotorEnchantingBehavior>();

        public override void RegisterEvents()
        {

            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddEnchanterQuarterMenu);

            CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, AddEnchanterEntryOption);
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, AwardIngredientLoot);
            CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, AwardRaidScrolls);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, TickRaidScrolls);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, WeeklySweep);
        }

        private void WeeklySweep()
        {
            try
            {
                if (_enchantedItems.Count == 0) return;
                var owned = new HashSet<ItemObject>();

                foreach (var settlement in Settlement.All)
                {

                    CollectRoster(settlement.ItemRoster, owned);
                    CollectRoster(settlement.Stash, owned);
                }
                foreach (var party in MobileParty.All)
                {
                    CollectRoster(party.ItemRoster, owned);
                }
                foreach (var hero in Hero.AllAliveHeroes)
                {

                    CollectEquipment(hero.BattleEquipment, owned);
                    CollectEquipment(hero.CivilianEquipment, owned);
                    CollectEquipment(hero.StealthEquipment, owned);
                }

                int removed = 0;
                int keptCrafted = 0;
                foreach (var pair in _enchantedItems.ToList())
                {
                    if (owned.Contains(pair.Key)) continue;
                    if (pair.Value != null && pair.Value.IsPlayerCrafted) { keptCrafted++; continue; }
                    ForgetEnchantedItem(pair.Key);
                    MBObjectManager.Instance.UnregisterObject(pair.Key);
                    removed++;
                }
                if (removed > 0 || keptCrafted > 0)
                    SotorLog.Info($"Weekly enchant sweep: unregistered {removed} unowned clone(s), "
                                  + $"kept {keptCrafted} unowned player-crafted; {_enchantedItems.Count} live");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"WeeklySweep failed: {ex.Message}");
            }
        }

        private void CollectRoster(ItemRoster roster, HashSet<ItemObject> owned)
        {
            if (roster == null) return;
            foreach (var element in roster)
            {
                var item = element.EquipmentElement.Item;
                if (item != null && _enchantedItems.ContainsKey(item)) owned.Add(item);
            }
        }

        private void CollectEquipment(Equipment equipment, HashSet<ItemObject> owned)
        {
            if (equipment == null) return;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                var item = equipment[slot].Item;
                if (item != null && _enchantedItems.ContainsKey(item)) owned.Add(item);
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sotorEnchantedItems", ref _enchantedItems);
            if (_enchantedItems == null) _enchantedItems = new Dictionary<ItemObject, SotorEnchantedItemData>();
        }

        public void RecordEnchantedItem(ItemObject newItem, ItemObject original, List<string> traitIds, bool playerCrafted)
        {
            if (newItem == null || original == null) return;
            if (_enchantedItems.ContainsKey(newItem)) return;
            _enchantedItems.Add(newItem, new SotorEnchantedItemData
            {
                OriginalItemStringId = original.StringId,
                NewItemName = newItem.Name?.ToString() ?? "",
                ItemTraits = new List<string>(traitIds ?? new List<string>()),
                IsPlayerCrafted = playerCrafted,
            });
        }

        public IReadOnlyDictionary<ItemObject, SotorEnchantedItemData> EnchantedItems => _enchantedItems;

        public void ForgetEnchantedItem(ItemObject item)
        {
            if (item == null) return;
            _enchantedItems.Remove(item);
            SotorExtendedItemManager.UnregisterItem(item.StringId);
        }

        public void InitializeSavedEnchantedItems()
        {
            int ok = 0, orphaned = 0;
            foreach (var pair in _enchantedItems.ToList())
            {
                var item = pair.Key;
                var data = pair.Value;
                var original = MBObjectManager.Instance.GetObject<ItemObject>(data.OriginalItemStringId);
                if (original == null)
                {

                    SotorLog.Warn($"Enchanted item {item.StringId}: original '{data.OriginalItemStringId}' no longer exists; dropping");
                    _enchantedItems.Remove(item);
                    orphaned++;
                    continue;
                }
                SotorEnchantmentHelper.CopyItemProperties(item, original);
                SotorEnchantmentHelper.SetItemName(item, data.NewItemName);
                item.Initialize();
                if (data.IsPlayerCrafted)
                {
                    var reference = item;
                    ItemObject.InitAsPlayerCraftedItem(ref reference);
                }
                item.DetermineItemCategoryForItem();
                item.IsReady = true;
                SotorExtendedItemManager.RegisterItemTraits(item.StringId, data.ItemTraits);
                ok++;
            }
            if (ok > 0 || orphaned > 0)
                SotorLog.Info($"Rehydrated {ok} enchanted item(s), dropped {orphaned} orphan(s)");
        }

        private void AwardIngredientLoot(MapEvent mapEvent)
        {
            try
            {
                if (!SotorSettings.EnableEnchanting) return;
                if (mapEvent == null || !mapEvent.HasWinner || mapEvent.PlayerSide != mapEvent.WinningSide) return;

                var enemySide = mapEvent.GetMapEventSide(
                    mapEvent.PlayerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                if (enemySide == null) return;

                var scores = new Dictionary<SotorIngredientType, float>();
                foreach (var t in SotorEnchantingIngredients.AllTypes) scores[t] = 0f;

                var battleTerrain = SotorIngredientDropModel.BattleTerrain(mapEvent);
                float battleMultiplier = SotorIngredientDropModel.BattleMultiplier(mapEvent);

                var battlePos = MobileParty.MainParty.Position.ToVec2();
                float snow = SotorIngredientDropModel.SnowAt(battlePos);

                if (mapEvent.MapEventSettlement != null)
                    scores[SotorIngredientType.ArcaneScroll] +=
                        SotorIngredientDropModel.SettlementScrollScore(mapEvent.MapEventSettlement);

                foreach (var party in enemySide.Parties)
                {

                    var primaryLane = SotorIngredientDropModel.PrimaryLaneFor(
                        party.Party, battleTerrain, battlePos, out float primaryYield);

                    if (mapEvent.MapEventSettlement != null) primaryYield = 1f;

                    var homeLore = SotorIngredientDropModel.PartyLore(party.Party);
                    foreach (var troop in party.Troops)
                    {
                        var character = troop.Troop;
                        if (character == null) continue;
                        foreach (var type in SotorEnchantingIngredients.AllTypes)
                        {
                            scores[type] += SotorIngredientDropModel.GetDropFactor(
                                                character, type, primaryLane, homeLore, primaryYield)
                                            * battleMultiplier;
                        }
                    }
                }

                if (mapEvent.MapEventSettlement != null)
                    SotorIngredientDropModel.ShiftBodyValueToScrolls(
                        scores, SotorIngredientDropModel.SettlementBodyKeep);

#if BL13
                float x = 0f, lootShare = 0f;
                mapEvent.GetBattleRewards(PartyBase.MainParty, out x, out x, out x, out x, out lootShare);
#else
                float lootShare = mapEvent.GetPlayerBattleContributionRate() * 100f;
#endif

                var roster = PlayerEncounter.Current?.RosterToReceiveLootItems ?? PartyBase.MainParty.ItemRoster;
                var gained = new List<string>();
                foreach (var type in SotorEnchantingIngredients.AllTypes)
                {
                    int amount = SotorIngredientDropModel.CalculateResultAmount(
                        scores[type], type, lootShare, SotorSettings.ReagentDropRatePercent);
                    if (amount <= 0) continue;
                    var item = SotorEnchantingIngredients.GetItem(type);
                    if (item == null) continue;
                    roster.AddToCounts(item, amount);
                    gained.Add($"{amount}x {item.Name}");
                }
                if (gained.Count > 0)
                {
                    SotorLog.Info($"Ingredient loot: {string.Join(", ", gained)} (lootShare={lootShare:0.#}%, "
                                  + $"terrain={battleTerrain}, battleX{battleMultiplier:0.#}, "
                                  + $"snow={snow:0.00}, rate={SotorSettings.ReagentDropRatePercent}%)");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AwardIngredientLoot failed: {ex.Message}");
            }
        }

        private Settlement _raidScrollsAwardedFor;

        private void TickRaidScrolls()
        {
            try
            {
                if (!SotorSettings.EnableEnchanting) return;
                var mapEvent = MobileParty.MainParty?.MapEvent;
                if (mapEvent == null || !mapEvent.IsRaid) return;
                if (mapEvent.PlayerSide != BattleSideEnum.Attacker) return;

                var settlement = mapEvent.MapEventSettlement;
                if (settlement == null || settlement == _raidScrollsAwardedFor) return;

                if (settlement.SettlementHitPoints > 0.5f) return;

                if (GrantRaidScrolls(settlement, "mid-raid")) _raidScrollsAwardedFor = settlement;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"TickRaidScrolls failed: {ex.Message}");
            }
        }

        private void AwardRaidScrolls(BattleSideEnum winnerSide, RaidEventComponent raidEvent)
        {
            try
            {
                if (!SotorSettings.EnableEnchanting) return;
                if (raidEvent == null || !raidEvent.IsPlayerMapEvent) return;
                if (winnerSide != BattleSideEnum.Attacker) return;
                if (raidEvent.MapEvent == null || raidEvent.MapEvent.PlayerSide != BattleSideEnum.Attacker) return;

                var settlement = raidEvent.MapEventSettlement;
                if (settlement == null) return;

                if (settlement == _raidScrollsAwardedFor)
                {
                    _raidScrollsAwardedFor = null;
                    return;
                }

                if (settlement.SettlementHitPoints > 0.0001f) return;

                GrantRaidScrolls(settlement, "raid end");
                _raidScrollsAwardedFor = null;
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AwardRaidScrolls failed: {ex.Message}");
            }
        }

        private bool GrantRaidScrolls(Settlement settlement, string when)
        {
            float score = SotorIngredientDropModel.SettlementScrollScore(settlement);
            if (score <= 0f) return false;

            int amount = SotorIngredientDropModel.CalculateResultAmount(
                score, SotorIngredientType.ArcaneScroll, 100f, SotorSettings.ReagentDropRatePercent);
            if (amount <= 0) return true;

            var item = SotorEnchantingIngredients.GetItem(SotorIngredientType.ArcaneScroll);
            if (item == null) return false;
            var roster = PlayerEncounter.Current?.RosterToReceiveLootItems ?? PartyBase.MainParty.ItemRoster;
            roster.AddToCounts(item, amount);

            try
            {

                var looted = new ItemRoster();
                looted.AddToCounts(item, amount);
                CampaignEventDispatcher.Instance.OnItemsLooted(MobileParty.MainParty, looted);

                var banner = SotorText.GetObject("sotor_raid_scroll_loot", "You plundered {COUNT} {ITEM}.");
                banner.SetTextVariable("COUNT", amount);
                banner.SetTextVariable("ITEM", item.Name);
                MBInformationManager.AddQuickInformation(banner);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Raid scroll notification failed (the scrolls were still granted): {ex.Message}");
            }

            SotorLog.Info($"Raid loot ({when}): {amount}x {item.Name} from {settlement?.Name} "
                          + $"(score={score:0.#}, rate={SotorSettings.ReagentDropRatePercent}%)");
            return true;
        }

        private void AddEnchanterEntryOption(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "sotor_enchanter_quarter",
                SotorText.Rendered("sotor_enchanter_menu_goto"),
                args =>
                {

                    args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                    return SotorSettings.EnableEnchanting
                        && Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown;
                },
                args => GameMenu.SwitchToMenu("sotor_enchanter"),

                false, SotorMenuOrder.After("town", SotorMenuOrder.TownSmithy), false);
        }

        private void AddEnchanterQuarterMenu(CampaignGameStarter starter)
        {
            starter.AddGameMenu("sotor_enchanter", "{SOTOR_ENCHANTER_INTRO}",
                args =>
                {
                    args.MenuTitle = SotorText.GetObject("sotor_enchanter_title");
                    var intro = SotorText.GetObject("sotor_enchanter_intro");
                    intro.SetTextVariable("SETTLEMENT_NAME", Settlement.CurrentSettlement?.Name);
                    MBTextManager.SetTextVariable("SOTOR_ENCHANTER_INTRO", intro, false);
                },
                GameMenu.MenuOverlayType.SettlementWithBoth);

            starter.AddGameMenuOption("sotor_enchanter", "sotor_enchanter_screen",
                SotorText.Rendered("sotor_enchanter_open_screen"),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                    if (!AnyPartyHeroKnowsBlueprint())
                    {
                        args.IsEnabled = false;
                        args.Tooltip = SotorText.GetObject("sotor_enchanter_screen_locked");
                    }
                    return true;
                },
                args => PickEnchanter(),
                false, 0, false);

            starter.AddGameMenuOption("sotor_enchanter", "sotor_enchanter_disenchant",
                SotorText.Rendered("sotor_enchanter_disenchant"),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Craft;
                    if (!GetDisenchantCandidates().Any())
                    {
                        args.IsEnabled = false;
                        args.Tooltip = SotorText.GetObject("sotor_enchanter_disenchant_none");
                    }
                    return true;
                },
                args => OpenDisenchantList(),
                false, -1, false);

            starter.AddGameMenuOption("sotor_enchanter", "sotor_enchanter_leave",
                SotorText.Rendered("sotor_enchanter_leave"),
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => GameMenu.SwitchToMenu("town"),
                true, -1, false);
        }

        private static void PickEnchanter()
        {
            var candidates = new List<InquiryElement>();
            foreach (var hero in PartyHeroes())
            {
                if (hero == null) continue;
                var info = Extensions.HeroExtensions.GetExtendedInfo(hero);
                var known = info?.AllKnownBlueprints ?? new List<string>();
                int workable = known.Count(id =>
                {
                    var trait = SotorItemTraitManager.GetTrait(id);
                    return trait != null && trait.IsCraftable
                        && SotorBlueprintBookBehavior.HeroMeetsGate(hero, trait);
                });

                TextObject hint;
                if (known.Count == 0)
                {
                    hint = SotorText.GetObject("sotor_enchanter_pick_none");
                }
                else if (workable == 0)
                {
                    hint = SotorText.GetObject("sotor_enchanter_pick_blocked");
                    hint.SetTextVariable("COUNT", known.Count);
                }
                else
                {
                    hint = SotorText.GetObject("sotor_enchanter_pick_ready");
                    hint.SetTextVariable("COUNT", workable);
                }

                candidates.Add(new InquiryElement(hero, hero.Name.ToString(),
                    new CharacterImageIdentifier(CharacterCode.CreateFrom(hero.CharacterObject)),
                    workable > 0, hint.ToString()));
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                SotorText.Rendered("sotor_enchanter_pick_title"),
                SotorText.Rendered("sotor_enchanter_pick_text"),
                candidates, true, 1, 1,
                SotorText.Rendered("sotor_str_accept"), SotorText.Rendered("sotor_str_cancel"),
                selected =>
                {
                    var pick = selected?.FirstOrDefault();
                    if (pick?.Identifier is Hero hero) SotorEnchantingScreen.Open(hero);
                }, null), true, false);
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

        private static bool AnyPartyHeroKnowsBlueprint()
        {
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null) return false;
            for (int i = 0; i < roster.Count; i++)
            {
                var character = roster.GetCharacterAtIndex(i);
                if (character == null || !character.IsHero) continue;
                var info = Extensions.HeroExtensions.GetExtendedInfo(character.HeroObject);
                if (info != null && info.AllKnownBlueprints.Count > 0) return true;
            }
            return false;
        }

        internal static IEnumerable<ItemRosterElement> GetDisenchantCandidates()
        {
            return MobileParty.MainParty.ItemRoster
                .Where(e => e.EquipmentElement.Item != null
                         && SotorExtendedItemManager.HasTraits(e.EquipmentElement.Item)
                         && !SotorBlueprintBookBehavior.IsBookItemId(e.EquipmentElement.Item.StringId));
        }

        internal static (Dictionary<SotorIngredientType, int> amounts, string text) GetRefund(ItemObject item)
        {
            return ComputeRefund(item);
        }

        private void OpenDisenchantList()
        {

            SotorDisenchantPopup.Open(ApplyDisenchant);
        }

        private void ApplyDisenchant(List<EquipmentElement> selected)
        {
            if (selected == null || selected.Count == 0) return;
            var roster = MobileParty.MainParty.ItemRoster;
            var totals = new Dictionary<SotorIngredientType, int>();
            var unmade = new List<string>();

            foreach (var equipment in selected)
            {
                var item = equipment.Item;
                if (item == null || roster.GetItemNumber(item) <= 0) continue;

                unmade.Add(item.Name?.ToString() ?? "");
                roster.AddToCounts(equipment, -1);
                foreach (var kv in ComputeRefund(item).amounts)
                {
                    if (kv.Value <= 0) continue;
                    totals[kv.Key] = totals.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
                }

                if (roster.GetItemNumber(item) <= 0 && _enchantedItems.ContainsKey(item))
                {
                    ForgetEnchantedItem(item);
                }
            }
            if (unmade.Count == 0) return;

            var gained = new List<string>();
            foreach (var type in SotorEnchantingIngredients.AllTypes)
            {
                if (!totals.TryGetValue(type, out var amount) || amount <= 0) continue;
                var ingredient = SotorEnchantingIngredients.GetItem(type);
                if (ingredient == null) continue;
                roster.AddToCounts(ingredient, amount);
                gained.Add(amount + SotorEnchantingIngredients.IconAsText(type));
            }

            var done = SotorText.GetObject("sotor_enchanter_disenchant_done");
            done.SetTextVariable("ITEMS", gained.Count > 0
                ? string.Join("  ", gained)
                : SotorText.Rendered("sotor_enchanter_refund_nothing"));
            SotorRibbon.Show(done, 2000);
        }

        private static (Dictionary<SotorIngredientType, int> amounts, string text) ComputeRefund(ItemObject item)
        {
            var amounts = new Dictionary<SotorIngredientType, int>();
            float fraction = SotorIngredientDropModel.RecycleFraction(item);
            foreach (var trait in SotorExtendedItemManager.GetTraitsOfItem(item))
            {
                if (trait.IngredientItem == SotorIngredientType.Invalid || trait.IngredientAmount <= 0) continue;
                int back = Math.Max(1, (int)(trait.IngredientAmount * fraction));
                amounts[trait.IngredientItem] = amounts.TryGetValue(trait.IngredientItem, out var v) ? v + back : back;
            }
            var parts = new List<string>();
            foreach (var kv in amounts)
            {
                var ing = SotorEnchantingIngredients.GetItem(kv.Key);
                if (ing != null) parts.Add($"{kv.Value} {ing.Name}");
            }
            return (amounts, parts.Count > 0 ? string.Join(", ", parts) : SotorText.Rendered("sotor_enchanter_refund_nothing"));
        }
    }

    [HarmonyPatch(typeof(CampaignGameStarter), "UnregisterNonReadyObjects")]
    public static class SotorEnchantRehydratePatch
    {
        [HarmonyPrefix]
        public static void BeforeUnregisterNonReadyObjects()
        {
            SotorEnchantingBehavior.Instance?.InitializeSavedEnchantedItems();
        }
    }
}
