using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace SOTOR.CampaignBehaviors
{

    public class SotorGraveyardNightWatchPartyComponent : PartyComponent
    {
        [CachedData]
        private TextObject _cachedName;

        [SaveableProperty(1)]
        public Settlement Settlement { get; private set; }

        public override Hero PartyOwner => Settlement.Owner ?? Settlement.MapFaction.Leader;

        public override Settlement HomeSettlement => Settlement;

        public override TextObject Name
        {
            get
            {
                if (_cachedName == null)
                {
                    _cachedName = new TextObject("{=*}{SETTLEMENT_NAME}'s Nightwatch");
                    _cachedName.SetTextVariable("SETTLEMENT_NAME", HomeSettlement.Name);
                }
                return _cachedName;
            }
        }

        public SotorGraveyardNightWatchPartyComponent() { }

        private SotorGraveyardNightWatchPartyComponent(Settlement settlement)
        {
            Settlement = settlement;
        }

        public static MobileParty CreateParty(Settlement settlement)
        {
            return MobileParty.CreateParty(
                settlement?.StringId + "_sotor_nightwatchparty_1",
                new SotorGraveyardNightWatchPartyComponent(settlement));
        }

        protected override void OnMobilePartySetOnCreation()
        {
            MobileParty.ActualClan = Settlement.OwnerClan;

            MobileParty.InitializeMobilePartyAtPosition(Settlement.Culture.MilitiaPartyTemplate, Settlement.GatePosition);
            MobileParty.MemberRoster.Clear();
            BuildNightwatchRoster();
            MobileParty.Party.SetVisualAsDirty();
            MobileParty.Ai.DisableAi();
            MobileParty.Aggressiveness = 0f;
        }

        private const int NightwatchFloor = 8;
        private const int NightwatchCap = 30;

        private const int VillageNightwatchFloor = 4;
        private const int VillageNightwatchCap = 15;

        private void BuildNightwatchRoster()
        {
            var built = BuildNightwatchRoster(Settlement);
            MobileParty.MemberRoster.Add(built);
        }

        public static TroopRoster BuildNightwatchRoster(Settlement settlement)
        {
            var roster = TroopRoster.CreateDummyTroopRoster();
            var culture = settlement.Culture;
            var meleeBasic = culture.MeleeMilitiaTroop;
            var rangedBasic = culture.RangedMilitiaTroop;
            var meleeElite = culture.MeleeEliteMilitiaTroop ?? meleeBasic;
            var rangedElite = culture.RangedEliteMilitiaTroop ?? rangedBasic;
            if (meleeBasic == null && rangedBasic == null) return roster;

            int count;
            float eliteRatio;
            if (settlement.IsVillage)
            {

                float hearth = settlement.Village != null ? settlement.Village.Hearth : 0f;
                count = MBRandom.RoundRandomized(hearth / 70f) + MBRandom.RandomInt(-2, 3);
                count = MathF.Max(VillageNightwatchFloor, MathF.Min(VillageNightwatchCap, count));
                eliteRatio = 0f;
            }
            else
            {

                float militia = settlement.IsTown ? settlement.Town.Militia : settlement.Militia;
                count = MBRandom.RoundRandomized(militia * 0.15f) + MBRandom.RandomInt(-2, 3);
                count = MathF.Max(NightwatchFloor, MathF.Min(NightwatchCap, count));

                float prosperity = settlement.IsTown ? settlement.Town.Prosperity : 0f;
                eliteRatio = MathF.Clamp((prosperity - 3000f) / 4000f, 0f, 0.6f);
            }

            int meleeCount = MathF.Round(count * 0.6f);
            int rangedCount = count - meleeCount;
            AddNightwatchTroops(roster, meleeBasic, meleeElite, meleeCount, eliteRatio);
            AddNightwatchTroops(roster, rangedBasic, rangedElite, rangedCount, eliteRatio);
            return roster;
        }

        private static void AddNightwatchTroops(TroopRoster roster, CharacterObject basic, CharacterObject elite, int number, float eliteRatio)
        {
            if (number <= 0) return;
            for (int i = 0; i < number; i++)
            {
                bool useElite = elite != null && MBRandom.RandomFloat < eliteRatio;
                var troop = useElite ? elite : basic;
                if (troop != null) roster.AddToCounts(troop, 1);
            }
        }

        public override Banner GetDefaultComponentBanner() => PartyOwner?.ClanBanner;
    }
}
