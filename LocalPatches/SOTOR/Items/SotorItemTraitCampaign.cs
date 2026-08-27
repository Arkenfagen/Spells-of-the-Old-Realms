using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.Items
{

    public static class SotorItemTraitCampaign
    {

        public static float SumEquipmentStat(Hero hero, SotorItemTraitStatType stat, string skillId = null)
        {
            var equipment = hero?.BattleEquipment;
            if (equipment == null) return 0f;
            float sum = 0f;
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var item = equipment[i].Item;
                if (item == null || !SotorExtendedItemManager.HasTraits(item)) continue;
                foreach (var t in SotorExtendedItemManager.GetTraitsOfItem(item))
                {
                    if (t.StatsTuple == null || t.StatsTuple.StatType != stat) continue;
                    if (skillId != null && !string.Equals(t.StatsTuple.SkillId, skillId, StringComparison.OrdinalIgnoreCase)) continue;
                    sum += t.StatsTuple.Value;
                }
            }
            return sum;
        }

        public static float GetSpellRadiusFactor(Hero hero)
        {
            float pct = SumEquipmentStat(hero, SotorItemTraitStatType.SpellRadius);
            return pct != 0f ? Math.Max(0.1f, 1f + pct / 100f) : 1f;
        }

        public static string DescribeSpellRadiusSources(Hero hero)
        {
            var equipment = hero?.BattleEquipment;
            if (equipment == null) return "no equipment";
            var parts = new System.Collections.Generic.List<string>();
            for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumEquipmentSetSlots; i++)
            {
                var item = equipment[i].Item;
                if (item == null || !SotorExtendedItemManager.HasTraits(item)) continue;
                foreach (var t in SotorExtendedItemManager.GetTraitsOfItem(item))
                {
                    if (t.StatsTuple == null || t.StatsTuple.StatType != SotorItemTraitStatType.SpellRadius) continue;
                    parts.Add($"{item.Name}[{t.ItemTraitStringId}]+{t.StatsTuple.Value}%");
                }
            }
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }
    }

    [HarmonyPatch(typeof(DefaultCharacterStatsModel), "MaxHitpoints")]
    public static class SotorItemTraitMaxHealthPatch
    {
        public static void Postfix(ref ExplainedNumber __result, CharacterObject character)
        {
            try
            {
                var hero = character?.HeroObject;
                if (hero == null) return;
                float bonus = SotorItemTraitCampaign.SumEquipmentStat(hero, SotorItemTraitStatType.HealthMax);
                if (bonus != 0f)
                {
                    __result.Add(bonus, SotorText.GetObject("sotor_stat_enchanted_gear"));
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitMaxHealthPatch failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPartyHealingModel), "GetDailyHealingHpForHeroes")]
    public static class SotorItemTraitHealingPatch
    {
        public static void Postfix(ref ExplainedNumber __result, PartyBase party, bool isPrisoners)
        {
            try
            {

                if (isPrisoners) return;
                var hero = party?.LeaderHero;
                if (hero == null) return;
                float pct = SotorItemTraitCampaign.SumEquipmentStat(hero, SotorItemTraitStatType.HealthRegen);
                if (pct != 0f)
                {
                    __result.AddFactor(pct / 100f, SotorText.GetObject("sotor_stat_enchanted_gear"));
                }
            }
            catch (AccessViolationException ex)
            {

                SotorLog.Warn($"SotorItemTraitHealingPatch failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitHealingPatch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPartySpeedCalculatingModel), "CalculateFinalSpeed")]
    public static class SotorItemTraitPartySpeedPatch
    {
        public static void Postfix(ref ExplainedNumber __result, MobileParty mobileParty)
        {
            try
            {
                var hero = mobileParty?.LeaderHero;
                if (hero == null) return;
                float pct = SotorItemTraitCampaign.SumEquipmentStat(hero, SotorItemTraitStatType.PartySpeed);
                if (pct != 0f)
                {
                    __result.AddFactor(pct / 100f, SotorText.GetObject("sotor_stat_enchanted_gear"));
                }
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorItemTraitPartySpeedPatch failed: {ex.Message}");
            }
        }
    }
}
