using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorRaiseDeadBehavior : CampaignBehaviorBase
    {

        private const string RaisedTroopId = "sotor_skeleton";

        public override void RegisterEvents()
        {

            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null || PlayerEncounter.Current == null) return;
            if (Hero.MainHero == null) return;

            if (mapEvent.PlayerSide != mapEvent.WinningSide)
            {
                AbilitySystem.SotorMindControlMissionLogic.PendingRecruits.Clear();
                return;
            }

            RecruitMindControlledSurvivors();

            if (!SotorSettings.EnableSkeletonArmies) return;

            var raiser = GetBestRaiser();
            if (raiser == null) return;

            float chance = GetRaiseDeadChance(raiser);
            if (chance <= 0f) return;

            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(RaisedTroopId);
            if (troop == null)
            {
                SotorLog.Warn($"RaiseDead: troop '{RaisedTroopId}' not found — skipping.");
                return;
            }

            int raised = 0;
            var defeatedParties = mapEvent.PartiesOnSide(mapEvent.DefeatedSide);
            foreach (var party in defeatedParties)
            {
                foreach (var element in party.Troops.Where(x => x.IsKilled))
                {
                    if (MBRandom.RandomFloat <= chance)
                    {
                        raised++;
                    }
                }
            }

            if (raised > 0)
            {

                PlayerEncounter.Current.RosterToReceiveLootMembers.AddToCounts(troop, raised, false, 0, 0, true, -1);
                SotorLog.Info($"RaiseDead: raised {raised} skeleton(s) (raiser '{raiser.Name}', chance {chance:P0}).");
            }
        }

        private static void RecruitMindControlledSurvivors()
        {
            var pending = AbilitySystem.SotorMindControlMissionLogic.PendingRecruits;
            if (pending.Count == 0) return;

            try
            {
                if (SotorSettings.EnableMindControlledArmies && PlayerEncounter.Current != null)
                {
                    foreach (var kv in pending)
                    {
                        var troop = kv.Key;
                        int count = kv.Value;
                        if (troop == null || count <= 0) continue;
                        PlayerEncounter.Current.RosterToReceiveLootMembers.AddToCounts(troop, count, false, 0, 0, true, -1);
                        PlayerEncounter.Current.RosterToReceiveLootPrisoners.AddToCounts(troop, -count, false, 0, 0, true, -1);
                        SotorLog.Info($"MindControl recruit: {count}x '{troop.Name}' joined (de-duped from prisoners).");
                    }
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"MindControl recruit failed: {ex.Message}");
            }
            finally
            {
                pending.Clear();
            }
        }

        private static Hero GetBestRaiser()
        {
            var party = Hero.MainHero.PartyBelongedTo;
            if (party == null) return null;

            Hero best = null;
            int bestSkill = -1;
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                var hero = element.Character?.HeroObject;
                if (hero == null) continue;
                if (!CanRaiseDead(hero)) continue;
                int skill = SpellcraftOf(hero);
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    best = hero;
                }
            }
            return best;
        }

        private static bool CanRaiseDead(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null) return false;
            if (!info.HasLore(SotorLores.LoreOfNecromancy)) return false;
            return info.HasSpell("SummonSkeleton") || info.HasSpell("GraveCall");
        }

        private static float GetRaiseDeadChance(Hero hero)
        {
            if (!CanRaiseDead(hero)) return 0f;
            return MBMath.ClampFloat(SpellcraftOf(hero) * 0.005f, 0.05f, 0.70f);
        }

        private static int SpellcraftOf(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            return skill != null ? hero.GetSkillValue(skill) : 0;
        }
    }
}
