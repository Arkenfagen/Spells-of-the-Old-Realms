using System;
using HarmonyLib;
using SOTOR.AbilitySystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR.CampaignBehaviors
{

    public class SotorStorytellerBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            try
            {
                if (SotorPerks.StoryTeller == null) return;
                var leader = Hero.MainHero;
                if (leader == null || !leader.GetPerkValue(SotorPerks.StoryTeller)) return;

                var roster = MobileParty.MainParty?.MemberRoster;
                if (roster == null) return;
                var skills = Skills.All;
                if (skills == null || skills.Count == 0) return;

                int taught = 0;
                for (int i = 0; i < roster.Count; i++)
                {
                    var character = roster.GetCharacterAtIndex(i);
                    if (character == null || !character.IsHero) continue;
                    var hero = character.HeroObject;
                    if (hero == null || hero == leader) continue;
                    var skill = skills[MBRandom.RandomInt(skills.Count)];
                    hero.AddSkillXp(skill, 1000f);
                    taught++;
                }
                if (taught > 0)
                    SotorLog.Info($"Storyteller: 1000 XP to {taught} companion(s), each in a random skill");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorStorytellerBehavior.OnDailyTick failed: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DefaultPartyMoraleModel), nameof(DefaultPartyMoraleModel.GetEffectivePartyMorale))]
    public static class SotorStorytellerMoralePatch
    {
        public static void Postfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            try
            {
                if (SotorPerks.StoryTeller == null) return;
                var leader = mobileParty?.LeaderHero;
                if (leader == null || !leader.GetPerkValue(SotorPerks.StoryTeller)) return;
                __result.Add(5f, SotorPerks.StoryTeller.Name);
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorStorytellerMoralePatch failed: {ex.Message}");
            }
        }
    }
}
