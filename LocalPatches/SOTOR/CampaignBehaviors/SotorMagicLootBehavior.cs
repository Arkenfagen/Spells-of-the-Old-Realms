using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.CampaignBehaviors
{

    public class SotorMagicLootBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<CharacterObject, int> _initialEnemyArmy = new Dictionary<CharacterObject, int>();
        private MapEvent _trackedMapEvent;

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventStarted.AddNonSerializedListener(this, OnMapEventStarted);
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnMapEventStarted(MapEvent mapEvent, PartyBase attacker, PartyBase defender)
        {
            if (mapEvent == null || !mapEvent.IsPlayerMapEvent) return;
            _initialEnemyArmy.Clear();
            _trackedMapEvent = mapEvent;
            var enemySide = mapEvent.GetMapEventSide(
                mapEvent.PlayerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            if (enemySide == null) return;
            foreach (var party in enemySide.Parties)
            {
                foreach (var troop in party.Troops)
                {
                    var character = troop.Troop;
                    if (character == null) continue;
                    _initialEnemyArmy[character] = _initialEnemyArmy.TryGetValue(character, out var n) ? n + 1 : 1;
                }
            }
        }

        private void OnPlayerBattleEnded(MapEvent mapEvent)
        {
            try
            {
                if (!SotorSettings.EnableMagicItemLoot) return;
                if (mapEvent == null || mapEvent != _trackedMapEvent) return;
                if (!mapEvent.HasWinner || mapEvent.PlayerSide != mapEvent.WinningSide) return;

#if BL13
                float x = 0f, lootShare = 0f;
                mapEvent.GetBattleRewards(PartyBase.MainParty, out x, out x, out x, out x, out lootShare);
#else
                float lootShare = mapEvent.GetPlayerBattleContributionRate() * 100f;
#endif
                var roster = PlayerEncounter.Current?.RosterToReceiveLootItems ?? PartyBase.MainParty.ItemRoster;
                var lootTraits = SotorItemTraitManager.AllTraits
                    .Where(t => t.ItemTraitStringId.StartsWith("lesser_loot", StringComparison.Ordinal))
                    .ToList();
                if (lootTraits.Count == 0) return;

                foreach (var pair in _initialEnemyArmy)
                {
                    var character = pair.Key;
                    int count = pair.Value;
                    float dropChance = DropChanceForTroop(character) * count * (lootShare / 100f);
                    if (MBRandom.RandomFloatRanged(0f, 1f) > dropChance) continue;
                    int traitCount = TraitCountForTroops(character, count, lootShare);
                    if (traitCount <= 0) continue;

                    var equipment = character.FirstBattleEquipment ?? character.Equipment;
                    if (equipment == null) continue;
                    var candidates = new List<ItemObject>();
                    for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                    {
                        var item = equipment[slot].Item;
                        if (item != null && !item.NotMerchandise && (item.HasArmorComponent || item.HasWeaponComponent)
                            && item.ItemType != ItemObject.ItemTypeEnum.Banner)
                        {
                            candidates.Add(item);
                        }
                    }
                    if (candidates.Count == 0) continue;
                    var baseItem = candidates[MBRandom.RandomInt(candidates.Count)];

                    var picked = new List<string>();
                    for (int i = 0; i < traitCount; i++)
                    {
                        var pool = lootTraits
                            .Where(t => SotorItemTrait.IsValidFor(t, baseItem.ItemType) && !picked.Contains(t.ItemTraitStringId))
                            .ToList();
                        if (pool.Count == 0) break;
                        picked.Add(pool[MBRandom.RandomInt(pool.Count)].ItemTraitStringId);
                    }
                    if (picked.Count == 0) continue;

                    var nameText = SotorText.GetObject("sotor_loot_item_name");
                    nameText.SetTextVariable("ITEM", baseItem.Name);
                    nameText.SetTextVariable("MODIFIER", SotorText.Rendered("sotor_loot_rarity_" + Math.Min(picked.Count, 3)));
                    var created = SotorEnchantmentHelper.CreateEnchantedItem(baseItem, picked, nameText.ToString());
                    if (created == null) continue;
                    roster.AddToCounts(created, 1);
                    SotorLog.Info($"Magic loot: '{created.Name}' [{string.Join(",", picked)}] from {character.StringId} x{count}");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorMagicLootBehavior failed: {ex.Message}");
            }
            finally
            {
                _initialEnemyArmy.Clear();
                _trackedMapEvent = null;
            }
        }

        private static float DropChanceForTroop(CharacterObject troop)
        {
            if (troop.Occupation == Occupation.Bandit) return 0.0001f;
            float baseChance = 0f;
            if (troop.IsHero) baseChance = 0.2f;
            else if (troop.Tier >= 5) baseChance = 0.0005f;
            return baseChance + troop.Level * 0.0005f;
        }

        private static int TraitCountForTroops(CharacterObject character, int count, float lootShare)
        {
            float traitChance = TraitChanceForTroop(character);
            int result = 0;
            if (character.IsHero)
            {
                if (MBRandom.RandomFloatNormal < traitChance) result = MBRandom.RandomInt(0, 3);
                return result;
            }
            int rolls = (int)(count * (lootShare / 100f));
            for (int i = 0; i < rolls; i++)
            {
                if (MBRandom.RandomFloatRanged(0f, 1f) < traitChance) result++;
            }
            return Math.Min(result, 3);
        }

        private static float TraitChanceForTroop(CharacterObject troop)
        {
            if (troop.Occupation == Occupation.Bandit) return 0.0005f;
            float baseChance = 0f;
            if (troop.IsHero) baseChance = 0.25f;
            else if (troop.Tier >= 5) baseChance = 0.05f;
            return baseChance + troop.Level * 0.0005f;
        }
    }
}
