using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.CampaignBehaviors
{

    public class SotorRivalGearBehavior : CampaignBehaviorBase
    {
        private const float DefeatDropChance = 0.30f;

        private static int PieceCount(int casterLevel) => Math.Max(1, Math.Min(5, casterLevel));

        private static int TierCap(int casterLevel)
        {
            switch (casterLevel)
            {
                case 1: return 50;
                case 2: return 100;
                case 3: return 150;
                case 4: return 175;
                case 5: return 250;
                default: return 250;
            }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => DressAllRivals("session launch"));
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, () => DressAllRivals("weekly"));
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, AwardDefeatDrops);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void DressAllRivals(string reason)
        {
            try
            {
                if (!SotorSettings.EnableEnchanting) return;
                var behavior = SotorEnchantingBehavior.Instance;
                if (behavior == null) return;

                bool rivalsOn = SotorSettings.EnableRivalCasters;
                int dressed = 0, undressed = 0;
                foreach (var hero in Hero.AllAliveHeroes.ToList())
                {
                    if (hero == null || !hero.IsLord || hero == Hero.MainHero || hero.Clan == Clan.PlayerClan) continue;
                    bool isCaster = rivalsOn && SotorRivalSeeder.HeroIsCasterPublic(hero);
                    if (!isCaster)
                    {
                        if (Undress(hero, behavior)) undressed++;
                        continue;
                    }
                    if (Dress(hero, behavior)) dressed++;
                }
                if (dressed > 0 || undressed > 0)
                    SotorLog.Info($"Rival gear ({reason}): dressed {dressed}, undressed {undressed} hero(es)");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"DressAllRivals failed: {ex.Message}");
            }
        }

        private bool Undress(Hero hero, SotorEnchantingBehavior behavior)
        {
            bool changed = false;
            var equipment = hero.BattleEquipment;
            if (equipment == null) return false;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                var item = equipment[slot].Item;
                if (item == null || !behavior.EnchantedItems.TryGetValue(item, out var data)) continue;
                var original = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(data.OriginalItemStringId);
                equipment[slot] = original != null ? new EquipmentElement(original) : EquipmentElement.Invalid;
                behavior.ForgetEnchantedItem(item);
                changed = true;
            }
            return changed;
        }

        private bool Dress(Hero hero, SotorEnchantingBehavior behavior)
        {
            var equipment = hero.BattleEquipment;
            if (equipment == null || hero.Clan == null) return false;
            var trad = SotorRivalSeeder.DeriveClanTradition(hero.Clan);

            if (SotorRivalSeeder.IsSeededMemberOnlyMaster(hero))
            {
                trad = SotorRivalSeeder.MemberOnlyTraditionForHero(hero);
            }
            if (trad == Trad.None) return Undress(hero, behavior);

            string lore = SotorTraditions.LoreIdFor(trad);
            int level = SotorRivalSeeder.HeroCasterLevel(hero, hero.Clan.Tier);
            int pieces = PieceCount(level);
            int cap = TierCap(level);

            bool doubleEverything = level >= 6;

            var line = SotorItemTraitManager.CraftableTraits
                .Where(t => t.RequiredLore == lore && t.LearnThreshold <= cap)
                .OrderBy(t => t.ItemTraitStringId, StringComparer.Ordinal)
                .ToList();
            if (line.Count == 0) return Undress(hero, behavior);

            var plan = new List<(EquipmentIndex slot, SotorItemTraitItemType[] kinds, int traitCount)>();
            var weaponSlot = FindWeaponSlot(equipment);
            if (weaponSlot != EquipmentIndex.None)
            {
                plan.Add((weaponSlot, new[] { SotorItemTraitItemType.Weapon, SotorItemTraitItemType.Melee, SotorItemTraitItemType.Ranged },
                    doubleEverything ? 2 : 1));
            }
            foreach (var slot in new[] { EquipmentIndex.Body, EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Gloves, EquipmentIndex.Leg })
            {
                if (plan.Count >= pieces) break;
                if (equipment[slot].Item != null)
                {
                    plan.Add((slot, new[] { SotorItemTraitItemType.Armor }, doubleEverything ? 2 : 1));
                }
            }

            bool changed = false;
            int applied = 0;
            string seed = SotorRivalSeeder.WorldSeedText();
            foreach (var (slot, kinds, traitCount) in plan)
            {
                if (applied >= pieces) break;
                var current = equipment[slot].Item;
                if (current == null) continue;

                var baseItem = current;
                bool wearsClone = behavior.EnchantedItems.TryGetValue(current, out var cloneData);
                if (wearsClone)
                {
                    baseItem = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(cloneData.OriginalItemStringId);
                    if (baseItem == null) continue;
                }

                var fitting = line.Where(t => kinds.Contains(t.ValidItemType) && t.IsValidForItem(baseItem)).ToList();
                if (fitting.Count == 0) continue;
                var traits = new List<string>();
                for (int i = 0; i < traitCount && fitting.Count > 0; i++)
                {
                    float roll = SotorBookShelf.Roll(seed + "|rivalgear|" + hero.StringId + "|" + (int)slot + "|" + i);
                    var pick = fitting[(int)(roll * fitting.Count) % fitting.Count];
                    traits.Add(pick.ItemTraitStringId);
                    fitting.Remove(pick);
                }
                if (traits.Count == 0) continue;

                if (wearsClone && cloneData.ItemTraits != null && cloneData.ItemTraits.SequenceEqual(traits)
                    && cloneData.OriginalItemStringId == baseItem.StringId)
                {
                    applied++;
                    continue;
                }
                if (wearsClone)
                {
                    behavior.ForgetEnchantedItem(current);
                    changed = true;
                }
                var clone = SotorEnchantmentHelper.CreateEnchantedItem(baseItem, traits);
                if (clone == null) continue;
                equipment[slot] = new EquipmentElement(clone);
                applied++;
                changed = true;
            }
            return changed;
        }

        private static EquipmentIndex FindWeaponSlot(Equipment equipment)
        {
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var item = equipment[slot].Item;
                if (item != null && item.HasWeaponComponent && item.ItemType != ItemObject.ItemTypeEnum.Shield
                    && item.ItemType != ItemObject.ItemTypeEnum.Banner)
                {
                    return slot;
                }
            }
            return EquipmentIndex.None;
        }

        private void AwardDefeatDrops(MapEvent mapEvent)
        {
            try
            {
                if (!SotorSettings.EnableEnchanting || !SotorSettings.EnableRivalCasters) return;
                if (mapEvent == null || !mapEvent.HasWinner || mapEvent.PlayerSide != mapEvent.WinningSide) return;
                var behavior = SotorEnchantingBehavior.Instance;
                if (behavior == null) return;
                var enemySide = mapEvent.GetMapEventSide(
                    mapEvent.PlayerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
                if (enemySide == null) return;
                var roster = PlayerEncounter.Current?.RosterToReceiveLootItems ?? PartyBase.MainParty.ItemRoster;

                foreach (var party in enemySide.Parties)
                {
                    var lord = party.Party?.LeaderHero;
                    if (lord == null || !SotorRivalSeeder.HeroIsCasterPublic(lord)) continue;
                    if (MBRandom.RandomFloat > DefeatDropChance) continue;
                    var equipment = lord.BattleEquipment;
                    if (equipment == null) continue;

                    var worn = new List<(EquipmentIndex slot, ItemObject clone, SotorEnchantedItemData data)>();
                    for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                    {
                        var item = equipment[slot].Item;
                        if (item != null && behavior.EnchantedItems.TryGetValue(item, out var data))
                        {
                            worn.Add((slot, item, data));
                        }
                    }
                    if (worn.Count == 0) continue;
                    var pick = worn[MBRandom.RandomInt(worn.Count)];
                    var original = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<ItemObject>(pick.data.OriginalItemStringId);
                    equipment[pick.slot] = original != null ? new EquipmentElement(original) : EquipmentElement.Invalid;
                    roster.AddToCounts(pick.clone, 1);
                    SotorLog.Info($"Defeat drop: '{pick.clone.Name}' taken from {lord.Name}");
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"AwardDefeatDrops failed: {ex.Message}");
            }
        }
    }
}
