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

            LogCasualtySplit(mapEvent, "player battle");
            int corpses = CorpsesForThisHarvest(mapEvent);
            float share = ShareOfCorpses(mapEvent, MobileParty.MainParty);
            int pool = PoolFor(corpses, share);
            MarkHarvested(mapEvent, pool);

            int raised = 0;
            for (int i = 0; i < pool; i++)
            {
                if (MBRandom.RandomFloat <= chance)
                {
                    raised++;
                }
            }
            if (share < 0.999f)
            {
                SotorLog.Info($"RaiseDead: sharing the field — {corpses} corpse(s), player share {share:P0} → pool {pool}.");
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

        public static void SettleAiConvertClaims(MapEvent mapEvent)
        {
            var claims = AbilitySystem.SotorMindControlMissionLogic.PendingAiClaims;
            if (claims.Count == 0) return;

            int settled = 0, dropped = 0, men = 0;
            var playerLosses = new Dictionary<MobileParty, int>();

            if (mapEvent == null || mapEvent.WinningSide == BattleSideEnum.None)
            {
                SotorLog.Info($"MindControl settle: {claims.Count} claim(s) dropped, the battle ended with no "
                              + "winning side.");
                claims.Clear();
                return;
            }

            try
            {
                foreach (var claim in claims)
                {
                    var controller = claim.ControllerParty;
                    if (claim.Troop == null || controller == null || claim.Count <= 0) continue;
                    if (controller.Party == null || controller.MemberRoster == null) continue;

                    bool won = false, found = false;
                    foreach (var mep in mapEvent.PartiesOnSide(mapEvent.WinningSide))
                    {
                        if (mep.Party == controller.Party) { won = true; found = true; break; }
                    }
                    if (!found)
                    {
                        foreach (var mep in mapEvent.PartiesOnSide(mapEvent.DefeatedSide))
                        {
                            if (mep.Party == controller.Party) { found = true; break; }
                        }
                    }
                    if (!found || !won)
                    {
                        dropped++;
                        continue;
                    }

                    int take = claim.Count;
                    settled++;
                    men += take;

                    controller.MemberRoster.AddToCounts(claim.Troop, take);

                    var origin = claim.OriginParty?.MemberRoster;
                    if (origin != null)
                    {
                        int have = origin.GetTroopCount(claim.Troop);
                        int sub = take < have ? take : have;
                        if (sub > 0) origin.AddToCounts(claim.Troop, -sub);
                    }

                    bool fromPlayer = claim.OriginParty == PartyBase.MainParty;
                    if (fromPlayer)
                    {
                        playerLosses.TryGetValue(controller, out int lost);
                        playerLosses[controller] = lost + take;
                    }
                    SotorLog.Info($"MindControl settle: {take}x '{claim.Troop.Name}' kept by '{controller.Name}' "
                                  + $"(origin {(claim.OriginParty?.Name?.ToString() ?? "?")}{(fromPlayer ? ", THE PLAYER" : "")}).");
                }

                foreach (var kv in playerLosses)
                {
                    var notice = SotorText.GetObject("sotor_mc_lost_men");
                    notice.SetTextVariable("COUNT", kv.Value);
                    notice.SetTextVariable("PARTY", kv.Key.Name);
                    SotorRibbon.Show(notice, 5000);
                }
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"MindControl settle failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                SotorLog.Info($"MindControl settle: {claims.Count} claim(s) — {settled} kept by a winning "
                              + $"controller, {dropped} dropped (controller lost or left the battle), "
                              + $"carrying {men} man(men) off in total.");
                claims.Clear();
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

        public static bool CanRaiseDead(Hero hero)
        {
            var info = hero?.GetExtendedInfo();
            if (info == null) return false;
            if (!info.HasLore(SotorLores.LoreOfNecromancy)) return false;
            return info.HasSpell("SummonSkeleton") || info.HasSpell("GraveCall");
        }

        public static float GetRaiseDeadChance(Hero hero)
        {
            if (!CanRaiseDead(hero)) return 0f;
            return MBMath.ClampFloat(SpellcraftOf(hero) * 0.005f, 0.05f, 0.70f);
        }

        private static int SpellcraftOf(Hero hero)
        {
            var skill = SotorSkills.Spellcraft;
            return skill != null ? hero.GetSkillValue(skill) : 0;
        }

        private static bool HasResolvedSides(MapEvent mapEvent)
        {
            return mapEvent != null
                   && mapEvent.WinningSide != BattleSideEnum.None
                   && mapEvent.DefeatedSide != BattleSideEnum.None;
        }

        public static int CountDefeatedCorpses(MapEvent mapEvent)
        {
            if (!HasResolvedSides(mapEvent)) return 0;
            int corpses = 0;
            foreach (var party in mapEvent.PartiesOnSide(mapEvent.DefeatedSide))
            {
                corpses += party.DiedInBattle?.TotalManCount ?? 0;
            }
            return corpses;
        }

        private static readonly Dictionary<MapEvent, int> _harvested = new Dictionary<MapEvent, int>();

        private static int CorpsesFromRoster(MapEvent mapEvent)
        {
            if (!HasResolvedSides(mapEvent)) return 0;
            int killed = 0;
            foreach (var party in mapEvent.PartiesOnSide(mapEvent.DefeatedSide))
            {
                foreach (var troop in party.Troops)
                {
                    if (troop.IsKilled) killed++;
                }
            }
            return killed;
        }

        public static int CorpsesForThisHarvest(MapEvent mapEvent)
        {
            int available = AvailableCorpses(mapEvent);
            int fromRoster = CorpsesFromRoster(mapEvent);
            if (fromRoster > 0)
            {
                int result = fromRoster < available ? fromRoster : available;

                int whole = CountDefeatedCorpses(mapEvent);
                if (whole > fromRoster)
                {
                    SotorLog.Info($"RaiseDead field: {fromRoster} dead in the engagement fought, of {whole} "
                                  + $"across the whole event -> using {result}.");
                }
                return result;
            }
            return available;
        }

        public static int AiFieldBase(MapEvent mapEvent)
        {
            return AvailableCorpses(mapEvent);
        }

        public static int AvailableCorpses(MapEvent mapEvent)
        {
            int total = CountDefeatedCorpses(mapEvent);
            if (mapEvent != null && _harvested.TryGetValue(mapEvent, out int already))
            {
                total -= already;
            }
            return total < 0 ? 0 : total;
        }

        public static void MarkHarvested(MapEvent mapEvent, int corpses)
        {
            if (mapEvent == null || corpses <= 0) return;
            _harvested.TryGetValue(mapEvent, out int already);
            _harvested[mapEvent] = already + corpses;
        }

        public static void LogCasualtySplit(MapEvent mapEvent, string context)
        {
            try
            {
                if (!HasResolvedSides(mapEvent)) return;
                int killed = 0, wounded = 0;
                foreach (var party in mapEvent.PartiesOnSide(mapEvent.DefeatedSide))
                {
                    killed += party.DiedInBattle?.TotalManCount ?? 0;
                    wounded += party.WoundedInBattle?.TotalManCount ?? 0;
                }
                int casualties = killed + wounded;
                if (casualties == 0) return;
                SotorLog.Info($"RaiseDead casualties ({context}): {killed} killed, {wounded} wounded "
                              + $"of {casualties} — {(float)killed / casualties:P0} of the losing side's "
                              + "casualties were fatal.");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"RaiseDead casualty split failed: {ex.Message}");
            }
        }

        public static void ForgetHarvest(MapEvent mapEvent)
        {
            if (mapEvent != null) _harvested.Remove(mapEvent);
            if (_harvested.Count == 0) return;
            var stale = new List<MapEvent>();
            foreach (var kv in _harvested)
            {
                if (kv.Key == null || kv.Key.HasWinner || kv.Key.DiplomaticallyFinished) stale.Add(kv.Key);
            }
            foreach (var ev in stale) _harvested.Remove(ev);
        }

        public static Hero BestRaiserOf(MobileParty party)
        {
            if (party == null) return null;
            Hero best = null;
            int bestSkill = -1;
            foreach (var element in party.MemberRoster.GetTroopRoster())
            {
                var hero = element.Character?.HeroObject;
                if (hero == null || !CanRaiseDead(hero)) continue;
                int skill = SpellcraftOf(hero);
                if (skill > bestSkill)
                {
                    bestSkill = skill;
                    best = hero;
                }
            }
            return best;
        }

        public static List<MobileParty> FindRaiserParties(MapEvent mapEvent)
        {
            var result = new List<MobileParty>();
            if (!HasResolvedSides(mapEvent)) return result;
            foreach (var mep in mapEvent.PartiesOnSide(mapEvent.WinningSide))
            {
                var mp = mep.Party?.MobileParty;
                if (mp == null || result.Contains(mp)) continue;
                if (BestRaiserOf(mp) != null) result.Add(mp);
            }
            return result;
        }

        public static int PoolFor(int corpses, float share)
        {
            if (corpses <= 0 || share <= 0f) return 0;
            int pool = (int)System.Math.Floor(corpses * share);
            return pool < 0 ? 0 : pool;
        }

        private static object ForceOf(MobileParty party)
        {
            return (object)party?.Army ?? party;
        }

        public static float ShareOfCorpses(MapEvent mapEvent, MobileParty party)
        {
            var raisers = FindRaiserParties(mapEvent);
            if (party == null || !raisers.Contains(party)) return 0f;
            if (raisers.Count == 1) return 1f;

            var myForce = ForceOf(party);

            var raiserForces = new List<object>();
            int raisersInMyForce = 0;
            foreach (var r in raisers)
            {
                var f = ForceOf(r);
                if (!raiserForces.Contains(f)) raiserForces.Add(f);
                if (Equals(f, myForce)) raisersInMyForce++;
            }

            float allForces = 0f, myForceTotal = 0f;
            float myForceRaisers = 0f, mine = 0f;

            foreach (var mep in mapEvent.PartiesOnSide(mapEvent.WinningSide))
            {
                var mp = mep.Party?.MobileParty;
                if (mp == null) continue;
                var f = ForceOf(mp);
                if (!raiserForces.Contains(f)) continue;

                float c = System.Math.Max(0, mep.ContributionToBattle);
                allForces += c;
                if (!Equals(f, myForce)) continue;

                myForceTotal += c;
                if (!raisers.Contains(mp)) continue;
                myForceRaisers += c;
                if (mp == party) mine += c;
            }

            float betweenForces = allForces > 0f ? myForceTotal / allForces : 1f / raiserForces.Count;
            float insideForce = myForceRaisers > 0f ? mine / myForceRaisers : 1f / System.Math.Max(1, raisersInMyForce);
            return betweenForces * insideForce;
        }
    }
}
