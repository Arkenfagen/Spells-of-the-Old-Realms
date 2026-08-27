using System.Collections.Generic;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.CampaignBehaviors
{

    public class SotorRumourBehavior : CampaignBehaviorBase
    {
        private const int RumourCost = SotorRumourLogic.RumourCost;

        private Hero _offered;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {

        }

        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _offered = null;
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {

            starter.AddPlayerLine("sotor_rumour_ask", "tavernkeeper_list_companion_types", "sotor_rumour_answer",
                SotorText.Get("sotor_rumour_ask"),
                CanAskForRumour, OnBuyRumour, 100, CanAffordRumour);

            starter.AddDialogLine("sotor_rumour_answer", "sotor_rumour_answer", "sotor_rumour_after",
                SotorText.Get("sotor_rumour_answer"),
                SetAnswerText, null, 100);

            starter.AddDialogLine("sotor_rumour_none", "sotor_rumour_answer", "tavernkeeper_pretalk",
                SotorText.Get("sotor_rumour_none"),
                null, null, 90);

            starter.AddPlayerLine("sotor_rumour_again", "sotor_rumour_after", "sotor_rumour_answer",
                SotorText.Get("sotor_rumour_again"),
                HasAnotherRumour, OnBuyRumour, 100, CanAffordRumour);
            starter.AddPlayerLine("sotor_rumour_done", "sotor_rumour_after", "tavernkeeper_pretalk",
                SotorText.Get("sotor_rumour_done"),
                null, null, 90);
        }

        private bool CanAskForRumour()
        {
            if (!SotorSettings.EnableRivalCasters) return false;
            var ch = CharacterObject.OneToOneConversationCharacter;
            if (ch == null || ch.Occupation != Occupation.Tavernkeeper) return false;
            return Settlement.CurrentSettlement != null;
        }

        private static void SetPriceVariables()
        {
            MBTextManager.SetTextVariable("SOTOR_RUMOUR_PRICE", RumourCost);
            MBTextManager.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
        }

        private bool HasAnotherRumour()
        {
            return NearestUnknownCaster(Settlement.CurrentSettlement) != null;
        }

        private bool CanAffordRumour(out TextObject explanation)
        {
            SetPriceVariables();
            if (Hero.MainHero.Gold < RumourCost)
            {
                explanation = SotorText.GetObject("sotor_rumour_too_poor");
                return false;
            }
            explanation = SotorText.GetObject("sotor_rumour_price_hint");
            return true;
        }

        private void OnBuyRumour()
        {
            _offered = NearestUnknownCaster(Settlement.CurrentSettlement);
            if (_offered == null) return;

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, RumourCost, false);
            SotorRivalReveal.MarkKnownToPlayer(_offered);

            var trad = SotorRivalSeeder.SocialTradition(_offered);
            SotorLog.Info($"RivalRumour: {Settlement.CurrentSettlement?.Name}'s keeper named {_offered.Name} "
                          + $"({trad}, home {_offered.HomeSettlement?.Name}) for {RumourCost} gold.");
        }

        private bool SetAnswerText()
        {
            if (_offered == null) return false;

            MBTextManager.SetTextVariable("NAME", _offered.EncyclopediaLinkWithName);

            var tradition = SotorTraditionObject.For(SotorRivalSeeder.SocialTradition(_offered));
            MBTextManager.SetTextVariable("TRADITION",
                tradition != null ? tradition.Name : SotorText.GetObject("sotor_rumour_unknown_tradition"));

            var home = _offered.HomeSettlement;
            var here = Settlement.CurrentSettlement;
            if (home != null)
            {
                MBTextManager.SetTextVariable("WHERE", home.EncyclopediaLinkWithName);
                MBTextManager.SetTextVariable("DIRECTION", DirectionWord(here, home));
            }
            else
            {

                MBTextManager.SetTextVariable("WHERE", SotorText.GetObject("sotor_rumour_nowhere"));
                MBTextManager.SetTextVariable("DIRECTION", SotorText.GetObject("sotor_rumour_direction_unknown"));
            }
            return true;
        }

        private static TextObject DirectionWord(Settlement here, Settlement there)
        {
            if (here == null || there == null)
            {
                return SotorText.GetObject("sotor_rumour_direction_" + SotorRumourLogic.DirectionUnknown);
            }

            var a = here.GetPosition2D;
            var b = there.GetPosition2D;
            return SotorText.GetObject(
                "sotor_rumour_direction_" + SotorRumourLogic.DirectionKeySuffix(a.x, a.y, b.x, b.y));
        }

        private static float RumourRange()
        {
            return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(
                MobileParty.NavigationType.All) * 2f;
        }

        private static Hero NearestUnknownCaster(Settlement here)
        {
            if (here == null) return null;
            float range = RumourRange();

            Hero best = null;
            float bestDistance = float.MaxValue;

            foreach (var hero in Hero.AllAliveHeroes)
            {
                if (!IsRumourCandidate(hero)) continue;

                var home = hero.HomeSettlement;
                if (home == null) continue;

                float distance = TownDistance(home, here);
                if (distance < range && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = hero;
                }
            }
            return best;
        }

        private static float TownDistance(Settlement from, Settlement to)
        {
            var navigation = (from.HasPort && to.HasPort)
                ? MobileParty.NavigationType.All
                : MobileParty.NavigationType.Default;
            return Campaign.Current.Models.MapDistanceModel.GetDistance(from, to, false, false, navigation);
        }

        private static bool IsRumourCandidate(Hero hero)
        {
            if (hero == null || hero == Hero.MainHero) return false;
            if (!hero.IsAlive || hero.IsChild || hero.IsNotable) return false;
            if (!hero.IsLord) return false;
            if (!hero.IsAbilityUser()) return false;

            if (hero.Clan == Clan.PlayerClan) return false;

            if (SotorPractitionerVM.PlayerKnows(hero)) return false;

            return SotorRumourLogic.TraditionIsGossipable(SotorRivalSeeder.SocialTradition(hero));
        }
    }
}
