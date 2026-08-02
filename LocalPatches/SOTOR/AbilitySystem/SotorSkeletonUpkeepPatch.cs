using System;
using HarmonyLib;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    internal static class SkeletonUpkeep
    {
        public const string SkeletonCultureId = "sotor_skeleton";

        public static bool IsSkeletonChar(CharacterObject c)
            => c?.Culture != null && c.Culture.StringId == SkeletonCultureId;
    }

    [HarmonyPatch(typeof(DefaultPartyWageModel), nameof(DefaultPartyWageModel.GetCharacterWage))]
    public static class SotorSkeletonWagePatch
    {
        private static void Postfix(CharacterObject character, ref int __result)
        {
            if (__result != 0 && SkeletonUpkeep.IsSkeletonChar(character))
            {
                __result = 0;
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPartySizeLimitModel), nameof(DefaultPartySizeLimitModel.GetPartyMemberSizeLimit))]
    public static class SotorSkeletonPartyWeightPatch
    {
        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            try
            {
                if (party?.MemberRoster == null || party.MobileParty == null) return;

                float addBackPerSkeleton = BestNecromancerAddBack(party);
                if (addBackPerSkeleton <= 0f) return;

                int skeletonCount = 0;
                var roster = party.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var e = roster.GetElementCopyAtIndex(i);
                    if (SkeletonUpkeep.IsSkeletonChar(e.Character))
                    {
                        skeletonCount += e.Number;
                    }
                }
                if (skeletonCount <= 0) return;

                __result.Add(skeletonCount * addBackPerSkeleton,
                    new TaleWorlds.Localization.TextObject("Necromancy (undead retinue)"));
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SkeletonPartyWeightPatch failed: {ex.Message}");
            }
        }

        private static float BestNecromancerAddBack(PartyBase party)
        {
            float best = 0f;
            var roster = party.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var hero = roster.GetElementCopyAtIndex(i).Character?.HeroObject;
                if (hero == null) continue;
                var info = hero.GetExtendedInfo();
                if (info == null || !info.HasLore(SotorLores.LoreOfNecromancy)) continue;

                int level = (int)SotorSpellcraftHelper.GetCastingLevel(hero);
                int levelsAboveEntry = level - (int)SpellCastingLevel.Entry;
                if (levelsAboveEntry <= 0) continue;

                float addBack = 0.2f * levelsAboveEntry;
                if (addBack > best) best = addBack;
            }
            return best;
        }
    }

    [HarmonyPatch(typeof(DefaultMobilePartyFoodConsumptionModel), nameof(DefaultMobilePartyFoodConsumptionModel.CalculateDailyBaseFoodConsumptionf))]
    public static class SotorSkeletonFoodPatch
    {
        private static void Postfix(MobileParty party, ref ExplainedNumber __result)
        {
            try
            {
                if (party?.Party?.MemberRoster == null) return;

                int skeletonCount = 0;
                var roster = party.Party.MemberRoster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var e = roster.GetElementCopyAtIndex(i);
                    if (SkeletonUpkeep.IsSkeletonChar(e.Character))
                    {
                        skeletonCount += e.Number;
                    }
                }
                if (skeletonCount <= 0) return;

                float perMan = 1f / 20f;
                __result.Add(skeletonCount * perMan, new TaleWorlds.Localization.TextObject("Undead (no food)"));
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SkeletonFoodPatch failed: {ex.Message}");
            }
        }
    }
}
