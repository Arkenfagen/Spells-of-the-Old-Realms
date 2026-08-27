using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorAiRaiseDeadBehavior : CampaignBehaviorBase
    {
        private const string RaisedTroopId = "sotor_skeleton";

        public override void RegisterEvents()
        {
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => LogQualifyingRaisers());
        }

        private void LogQualifyingRaisers()
        {
            try
            {
                if (!SotorSettings.EnableRivalCasters || !SotorSettings.EnableSkeletonArmies)
                {
                    SotorLog.Warn("AiRaiseDead: DISABLED by MCM (Rival Casters or Skeleton Armies is off). "
                                  + "No AI necromancer will raise anything.");
                    return;
                }

                var qualified = new List<string>();
                int necromancersWithoutSummons = 0;
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || hero == Hero.MainHero || !hero.IsLord) continue;
                    var info = hero.GetExtendedInfo();
                    if (info == null || !info.HasLore(SotorLores.LoreOfNecromancy)) continue;

                    if (SotorRaiseDeadBehavior.CanRaiseDead(hero))
                    {
                        float chance = SotorRaiseDeadBehavior.GetRaiseDeadChance(hero);
                        string party = hero.PartyBelongedTo?.Name?.ToString() ?? "no party";
                        qualified.Add($"{hero.Name} ({chance:P0}/corpse, {party})");
                    }
                    else
                    {
                        necromancersWithoutSummons++;
                    }
                }

                if (qualified.Count == 0)
                {
                    SotorLog.Warn($"AiRaiseDead: NOBODY qualifies. {necromancersWithoutSummons} necromancer "
                                  + "lord(s) hold the school but none has Raise the Dead or Grave Call, so no "
                                  + "AiRaiseDead line will ever appear no matter how long you wait.");
                    return;
                }

                var playerRaiser = SotorRaiseDeadBehavior.BestRaiserOf(MobileParty.MainParty);
                SotorLog.Info($"AiRaiseDead: {qualified.Count} qualifying AI raiser(s) in the world"
                              + $"{(necromancersWithoutSummons > 0 ? $", {necromancersWithoutSummons} more hold Necromancy but lack the summons" : "")}; "
                              + $"the player's party {(playerRaiser != null ? "also qualifies via " + playerRaiser.Name : "does NOT qualify")}.");
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"AiRaiseDead raiser survey failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            try
            {

                SotorRaiseDeadBehavior.SettleAiConvertClaims(mapEvent);

                RaiseForAiParties(mapEvent);
            }
            catch (System.Exception ex)
            {
                SotorLog.Error($"AiRaiseDead failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {

                SotorRaiseDeadBehavior.ForgetHarvest(mapEvent);
            }
        }

        private void RaiseForAiParties(MapEvent mapEvent)
        {
            if (mapEvent == null) return;
            if (!SotorSettings.EnableRivalCasters || !SotorSettings.EnableSkeletonArmies) return;
            if (mapEvent.WinningSide == TaleWorlds.Core.BattleSideEnum.None) return;

            var troop = MBObjectManager.Instance.GetObject<CharacterObject>(RaisedTroopId);
            if (troop == null) return;

            var raiserParties = SotorRaiseDeadBehavior.FindRaiserParties(mapEvent);
            if (raiserParties.Count == 0) return;

            SotorRaiseDeadBehavior.LogCasualtySplit(mapEvent, "AI battle");

            if (raiserParties.Count > 1)
            {
                try
                {
                    int corpseCount = SotorRaiseDeadBehavior.AiFieldBase(mapEvent);
                    var shares = new List<string>();
                    foreach (var rp in raiserParties)
                    {
                        float s = SotorRaiseDeadBehavior.ShareOfCorpses(mapEvent, rp);
                        shares.Add($"{(rp == MobileParty.MainParty ? "PLAYER" : rp.Name.ToString())}={s:P0}"
                                   + $"({SotorRaiseDeadBehavior.PoolFor(corpseCount, s)})");
                    }
                    SotorLog.Info($"AiRaiseDead: {corpseCount} corpse(s) shared by {raiserParties.Count} "
                                  + "raiser(s): " + string.Join(" ", shares));
                }
                catch (System.Exception ex)
                {
                    SotorLog.Warn($"AiRaiseDead share report failed: {ex.Message}");
                }
            }

            int corpses = -1;
            foreach (var party in raiserParties)
            {

                if (party == MobileParty.MainParty) continue;

                var raiser = SotorRaiseDeadBehavior.BestRaiserOf(party);
                if (raiser == null) continue;

                if (corpses < 0) corpses = SotorRaiseDeadBehavior.AiFieldBase(mapEvent);
                if (corpses == 0) break;

                float share = SotorRaiseDeadBehavior.ShareOfCorpses(mapEvent, party);
                int pool = SotorRaiseDeadBehavior.PoolFor(corpses, share);

                int left = SotorRaiseDeadBehavior.AvailableCorpses(mapEvent);
                if (pool > left) pool = left;
                SotorRaiseDeadBehavior.MarkHarvested(mapEvent, pool);
                float chance = SotorRaiseDeadBehavior.GetRaiseDeadChance(raiser);

                int raised = 0;
                for (int i = 0; i < pool; i++)
                {
                    if (MBRandom.RandomFloat <= chance) raised++;
                }
                if (raised <= 0) continue;

                int capPercent = SotorSettings.RivalRaiseDeadPartyCapPercent;
                if (capPercent <= 0)
                {
                    continue;
                }
                int perBattleCap = System.Math.Max(1, party.Party.PartySizeLimit * capPercent / 100);
                if (raised > perBattleCap)
                {
                    SotorLog.Info($"AiRaiseDead: '{raiser.Name}' ({party.Name}) raised {raised} but is capped at "
                                  + $"{perBattleCap} for one battle ({capPercent}% of {party.Party.PartySizeLimit}).");
                    raised = perBattleCap;
                }

                party.MemberRoster.AddToCounts(troop, raised);
                SotorLog.Info($"AiRaiseDead: '{raiser.Name}' ({party.Name}) raised {raised} skeleton(s) "
                              + $"from {pool} corpse(s) (share {share:P0} of {corpses}, chance {chance:P0}); "
                              + $"party now {party.MemberRoster.TotalManCount}/{party.Party.PartySizeLimit}.");
            }
        }
    }
}
