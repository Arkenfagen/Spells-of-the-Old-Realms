using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorEnchantShopBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, int> _stock = new Dictionary<string, int>();
        private Dictionary<string, int> _stockWeek = new Dictionary<string, int>();

        public static SotorEnchantShopBehavior Instance =>
            Campaign.Current?.GetCampaignBehavior<SotorEnchantShopBehavior>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_sotorEnchantShopStock", ref _stock);
            dataStore.SyncData("_sotorEnchantShopWeek", ref _stockWeek);
            if (_stock == null) _stock = new Dictionary<string, int>();
            if (_stockWeek == null) _stockWeek = new Dictionary<string, int>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("sotor_enchanter", "sotor_enchanter_shop",
                SotorText.Rendered("sotor_enchanter_shop"),
                args =>
                {
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    if (!ShopSellsAnything())
                    {
                        args.IsEnabled = false;
                        args.Tooltip = SotorText.GetObject("sotor_enchanter_shop_closed");
                    }
                    return true;
                },
                args => OpenShop(),
                false, 1, false);
        }

        private static bool ShopSellsAnything()
        {
            return SotorSettings.EnableEnchantShopBooks || SotorSettings.EnableEnchantShopMaterials;
        }

        private static int CurrentWeek() => (int)CampaignTime.Now.ToWeeks;

        private static string Key(string settlementId, string itemId) => settlementId + "|" + itemId;

        private static List<SotorEnchantStock.IngredientEntry> IngredientEntriesFor(string rulingLore)
        {
            var use = new Dictionary<SotorIngredientType, int>();
            foreach (var type in SotorEnchantingIngredients.AllTypes) use[type] = 0;

            if (!string.IsNullOrEmpty(rulingLore))
            {
                foreach (var trait in SotorItemTraitManager.CraftableTraits)
                {
                    if (trait.RequiredLore != rulingLore) continue;
                    if (trait.IngredientItem == SotorIngredientType.Invalid) continue;
                    if (use.ContainsKey(trait.IngredientItem)) use[trait.IngredientItem]++;
                }
            }

            int best = 0;
            foreach (var pair in use) if (pair.Value > best) best = pair.Value;

            var entries = new List<SotorEnchantStock.IngredientEntry>();
            foreach (var type in SotorEnchantingIngredients.AllTypes)
            {
                entries.Add(new SotorEnchantStock.IngredientEntry
                {
                    Type = type,
                    Rarity = SotorEnchantStock.RarityOf(type),
                    LoreUse = use[type],
                    IsLoreStaple = best > 0 && use[type] == best,
                });
            }
            return entries;
        }

        private static string RulingLoreOf(Town town)
        {
            var clan = town?.OwnerClan;
            var trad = clan != null ? SotorRivalSeeder.DeriveClanTradition(clan) : Trad.None;
            return trad != Trad.None ? SotorTraditions.LoreIdFor(trad) : "";
        }

        private void EnsureStock(Town town)
        {
            if (town?.Settlement == null) return;
            string sid = town.Settlement.StringId;
            int week = CurrentWeek();
            if (_stockWeek.TryGetValue(sid, out int had) && had == week) return;

            foreach (var key in _stock.Keys.Where(k => k.StartsWith(sid + "|", StringComparison.Ordinal)).ToList())
            {
                _stock.Remove(key);
            }

            string seed = SotorRivalSeeder.WorldSeedText();
            string lore = RulingLoreOf(town);

            if (SotorSettings.EnableEnchantShopBooks)
            {
                foreach (var trait in SotorBlueprintBookBehavior.CurrentShelf(town))
                {
                    var book = SotorBlueprintBookBehavior.BookFor(trait.ItemTraitStringId);
                    if (book != null) _stock[Key(sid, book.StringId)] = 1;
                }

                var runeShelf = SotorRuneShelf.ShelfFor(seed, sid, week, town.Prosperity,
                    SotorRuneShelf.LinesFor(town), SotorRuneShelf.RosterEntries());
                foreach (var traitId in runeShelf)
                {
                    var book = SotorBlueprintBookBehavior.BookFor(traitId);
                    if (book != null) _stock[Key(sid, book.StringId)] = 1;
                }
                if (runeShelf.Count > 0)
                {
                    SotorLog.Info($"Rune shelf for {sid} (week {week}): [{string.Join(",", runeShelf)}]");
                }
            }

            if (SotorSettings.EnableEnchantShopMaterials)
            {
                var counts = SotorEnchantStock.IngredientStockFor(
                    seed, sid, week, town.Prosperity, IngredientEntriesFor(lore));
                foreach (var pair in counts)
                {
                    var item = SotorEnchantingIngredients.GetItem(pair.Key);
                    if (item != null) _stock[Key(sid, item.StringId)] = pair.Value;
                }
            }

            _stockWeek[sid] = week;
            int lines = _stock.Count(k => k.Key.StartsWith(sid + "|", StringComparison.Ordinal));
            SotorLog.Info($"Enchanter stock rolled for {sid} (week {week}, lore '{lore}', "
                          + $"prosperity {town.Prosperity:F0}): {lines} line(s)");
        }

        private void OpenShop()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town?.Settlement == null) return;
            EnsureStock(town);

            string sid = town.Settlement.StringId;
            var roster = new ItemRoster();

            var offered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in _stock.Where(k => k.Key.StartsWith(sid + "|", StringComparison.Ordinal)))
            {
                if (pair.Value <= 0) continue;
                var itemId = pair.Key.Substring(sid.Length + 1);

                if (SotorBlueprintBookBehavior.IsBookItemId(itemId) && !SotorSettings.EnableEnchantShopBooks) continue;
                if (SotorEnchantingIngredients.IsIngredientItemId(itemId) && !SotorSettings.EnableEnchantShopMaterials) continue;
                var item = MBObjectManager.Instance?.GetObject<ItemObject>(itemId);
                if (item == null) continue;
                roster.AddToCounts(item, pair.Value);
                offered.Add(itemId);
            }

            InventoryScreenHelper.OpenScreenAsTrade(roster, town,
                InventoryScreenHelper.InventoryCategoryType.None, () => WriteBack(sid, roster, offered));
        }

        private void WriteBack(string settlementId, ItemRoster roster, HashSet<string> offered)
        {
            try
            {

                foreach (var itemId in offered)
                {
                    _stock.Remove(Key(settlementId, itemId));
                }
                foreach (var element in roster)
                {
                    var item = element.EquipmentElement.Item;
                    if (item == null || element.Amount <= 0) continue;
                    if (!IsShopGood(item.StringId)) continue;
                    _stock[Key(settlementId, item.StringId)] = element.Amount;
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Enchanter shop write-back failed: {ex.Message}");
            }
        }

        private static bool IsShopGood(string itemId)
        {
            return SotorBlueprintBookBehavior.IsBookItemId(itemId)
                   || SotorEnchantingIngredients.IsIngredientItemId(itemId);
        }
    }
}
