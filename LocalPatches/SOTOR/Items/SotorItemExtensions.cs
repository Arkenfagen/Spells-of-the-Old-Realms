using System.Collections.Generic;
using SOTOR.AbilitySystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace SOTOR.Items
{

    public static class SotorItemExtensions
    {
        public static List<SotorItemTrait> GetTraits(this ItemObject item, Agent agent = null)
        {
            var list = SotorExtendedItemManager.GetTraitsOfItem(item);
            if (agent != null)
            {
                var comp = agent.GetComponent<SotorItemTraitAgentComponent>();
                if (comp != null) list.AddRange(comp.GetDynamicTraits(item));
            }
            return list;
        }

        public static bool HasAnyTrait(this ItemObject item, Agent agent = null)
        {
            if (item == null) return false;
            if (SotorExtendedItemManager.HasTraits(item)) return true;
            if (agent != null)
            {
                var comp = agent.GetComponent<SotorItemTraitAgentComponent>();
                if (comp != null && comp.HasDynamicTraits(item)) return true;
            }
            return false;
        }

        public static List<ItemObject> GetArmorItems(Agent agent)
        {
            var list = new List<ItemObject>();
            var eq = agent?.SpawnEquipment;
            if (eq == null) return list;
            for (var i = EquipmentIndex.NumAllWeaponSlots; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var item = eq[i].Item;
                if (item != null) list.Add(item);
            }
            return list;
        }

        public static float SumWornStat(Agent agent, SotorItemTraitStatType stat)
        {
            var equipment = agent?.SpawnEquipment;
            if (equipment == null) return 0f;
            float sum = 0f;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var item = equipment[i].Item;
                if (item == null || !SotorExtendedItemManager.HasTraits(item)) continue;
                foreach (var t in SotorExtendedItemManager.GetTraitsOfItem(item))
                {
                    if (t.StatsTuple == null || t.StatsTuple.StatType != stat) continue;
                    sum += t.StatsTuple.Value;
                }
            }
            return sum;
        }

        public static float SumArmorDamageBonus(Agent agent, DamageType damageType)
        {
            float sum = 0f;
            foreach (var t in GetArmorTraits(agent))
            {
                if (t.AmplifierTuple != null && t.AmplifierTuple.AmplifiedDamageType == damageType)
                    sum += t.AmplifierTuple.DamageAmplifier;
                if (t.AdditionalDamageTuple != null && t.AdditionalDamageTuple.DamageType == damageType)
                    sum += t.AdditionalDamageTuple.Percent;
            }
            return sum;
        }

        public static float SumItemResist(Agent victim, DamageType damageType)
        {
            int n = System.Enum.GetValues(typeof(DamageType)).Length;
            var resist = new float[n];
            SumResistTuples(victim, resist);
            return resist[(int)damageType];
        }

        public static string DescribeWornStat(Agent agent, SotorItemTraitStatType stat)
        {
            var equipment = agent?.SpawnEquipment;
            if (equipment == null) return "no spawn equipment";
            var parts = new List<string>();
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var item = equipment[i].Item;
                if (item == null || !SotorExtendedItemManager.HasTraits(item)) continue;
                foreach (var t in SotorExtendedItemManager.GetTraitsOfItem(item))
                {
                    if (t.StatsTuple == null || t.StatsTuple.StatType != stat) continue;
                    parts.Add($"{item.Name}[{t.ItemTraitStringId}]+{t.StatsTuple.Value}%");
                }
            }
            return parts.Count == 0 ? "nothing worn carries it" : string.Join(", ", parts);
        }

        public static List<SotorItemTrait> GetArmorTraits(Agent agent)
        {
            var traits = new List<SotorItemTrait>();
            foreach (var item in GetArmorItems(agent))
            {
                if (item.HasAnyTrait(agent)) traits.AddRange(item.GetTraits(agent));
            }
            return traits;
        }

        public static void SumAttackTuples(Agent attacker, bool isMissile, float[] ampByType, float[] addByType)
        {
            var traits = GetArmorTraits(attacker);
            var wielded = attacker.WieldedWeapon;
            if (!wielded.IsEmpty && wielded.Item != null)
            {
                traits.AddRange(wielded.Item.GetTraits(attacker));
                if (isMissile && !wielded.AmmoWeapon.IsEmpty && wielded.AmmoWeapon.Item != null)
                {
                    traits.AddRange(wielded.AmmoWeapon.Item.GetTraits(attacker));
                }
            }
            var offhand = attacker.WieldedOffhandWeapon;
            if (!isMissile && !offhand.IsEmpty && offhand.Item != null)
            {
                traits.AddRange(offhand.Item.GetTraits(attacker));
            }
            foreach (var t in traits)
            {
                if (t.AmplifierTuple != null && t.AmplifierTuple.AmplifiedDamageType != DamageType.Invalid)
                    ampByType[(int)t.AmplifierTuple.AmplifiedDamageType] += t.AmplifierTuple.DamageAmplifier;
                if (t.AdditionalDamageTuple != null && t.AdditionalDamageTuple.DamageType != DamageType.Invalid)
                    addByType[(int)t.AdditionalDamageTuple.DamageType] += t.AdditionalDamageTuple.Percent;
            }
        }

        public static void SumResistTuples(Agent victim, float[] resistByType)
        {
            var traits = GetArmorTraits(victim);
            var offhand = victim.WieldedOffhandWeapon;
            if (!offhand.IsEmpty && offhand.Item != null)
            {
                traits.AddRange(offhand.Item.GetTraits(victim));
            }
            foreach (var t in traits)
            {
                if (t.ResistanceTuple != null && t.ResistanceTuple.ResistedDamageType != DamageType.Invalid)
                    resistByType[(int)t.ResistanceTuple.ResistedDamageType] += t.ResistanceTuple.ReductionPercent;
            }
        }

        public static float SumWieldedStat(Agent agent, SotorItemTraitStatType stat, bool rangedOnly = false, bool meleeOnly = false)
        {
            var wielded = agent.WieldedWeapon;
            if (wielded.IsEmpty || wielded.Item == null || wielded.CurrentUsageItem == null) return 0f;
            if (rangedOnly && !wielded.CurrentUsageItem.IsRangedWeapon) return 0f;
            if (meleeOnly && !wielded.CurrentUsageItem.IsMeleeWeapon) return 0f;
            float sum = 0f;
            foreach (var t in wielded.Item.GetTraits(agent))
            {
                if (t.StatsTuple != null && t.StatsTuple.StatType == stat) sum += t.StatsTuple.Value;
            }
            return sum;
        }

        public static float SumArmorStat(Agent agent, SotorItemTraitStatType stat)
        {
            float sum = 0f;
            foreach (var t in GetArmorTraits(agent))
            {
                if (t.StatsTuple != null && t.StatsTuple.StatType == stat) sum += t.StatsTuple.Value;
            }
            return sum;
        }
    }
}
