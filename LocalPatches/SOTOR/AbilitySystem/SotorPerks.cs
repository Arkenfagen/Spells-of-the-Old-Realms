using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace SOTOR.AbilitySystem
{

    public class SotorPerks
    {
        private static SotorPerks _instance;
        public static SotorPerks Instance => _instance;

        private PerkObject _entrySpells;
        private PerkObject _adeptSpells;
        private PerkObject _masterSpells;
        private PerkObject _selfish;
        private PerkObject _wellControlled;
        private PerkObject _librarian;
        private PerkObject _storyTeller;
        private PerkObject _overCaster;
        private PerkObject _efficientSpellCaster;
        private PerkObject _improvision;
        private PerkObject _catalyst;
        private PerkObject _dampener;
        private PerkObject _arcaneLink;
        private PerkObject _trueTransmutation;

        public static PerkObject EntrySpells => _instance?._entrySpells;
        public static PerkObject AdeptSpells => _instance?._adeptSpells;
        public static PerkObject MasterSpells => _instance?._masterSpells;
        public static PerkObject Selfish => _instance?._selfish;
        public static PerkObject WellControlled => _instance?._wellControlled;
        public static PerkObject Librarian => _instance?._librarian;
        public static PerkObject StoryTeller => _instance?._storyTeller;
        public static PerkObject OverCaster => _instance?._overCaster;
        public static PerkObject EfficientSpellCaster => _instance?._efficientSpellCaster;
        public static PerkObject Improvision => _instance?._improvision;
        public static PerkObject Catalyst => _instance?._catalyst;
        public static PerkObject Dampener => _instance?._dampener;
        public static PerkObject ArcaneLink => _instance?._arcaneLink;

        public static PerkObject TrueTransmutation => _instance?._trueTransmutation;
        public static PerkObject Archmage => _instance?._trueTransmutation;

        public SotorPerks()
        {
            _instance = this;

            _entrySpells = Create("SotorEntrySpells");
            _adeptSpells = Create("SotorAdeptSpells");
            _masterSpells = Create("SotorMasterSpells");
            _selfish = Create("SotorSelfish");
            _wellControlled = Create("SotorWellControlled");
            _librarian = Create("SotorLibrarian");
            _storyTeller = Create("SotorStoryTeller");
            _overCaster = Create("SotorOverCaster");
            _efficientSpellCaster = Create("SotorEfficientSpellCaster");
            _improvision = Create("SotorImprovision");
            _catalyst = Create("SotorCatalyst");
            _dampener = Create("SotorDampener");
            _arcaneLink = Create("SotorArcaneLink");
            _trueTransmutation = Create("SotorTrueTransmutation");

            InitializeAll();
        }

        private static PerkObject Create(string id)
        {
            return Game.Current.ObjectManager.RegisterPresumedObject(new PerkObject(id));
        }

        private void InitializeAll()
        {
            var sc = SotorSkills.Spellcraft;
            const PartyRole personal = (PartyRole)12;
            const PartyRole partyLeader = (PartyRole)5;
            const PartyRole captain = (PartyRole)13;
            const EffectIncrementType add = (EffectIncrementType)0;
            const EffectIncrementType factor = (EffectIncrementType)1;
            const EffectIncrementType none = (EffectIncrementType)(-1);

            _entrySpells.Initialize(SotorText.Get("sotor_perk_novice_spellcaster_name", "Novice Spellcaster"), sc, 25, null,
                SotorText.Get("sotor_perk_novice_spellcaster_desc", "Gain access to entry level spells."), personal, 0f, none,
                "", (PartyRole)0, 0f, none, (TroopUsageFlags)65535, (TroopUsageFlags)65535);
            _adeptSpells.Initialize(SotorText.Get("sotor_perk_adept_spellcaster_name", "Adept Spellcaster"), sc, 100, null,
                SotorText.Get("sotor_perk_adept_spellcaster_desc", "Gain access to adept level spells. Unlocks the ability to learn the Lore of Necromancy."), personal, 0f, none,
                "", (PartyRole)0, 0f, none, (TroopUsageFlags)65535, (TroopUsageFlags)65535);
            _masterSpells.Initialize(SotorText.Get("sotor_perk_master_spellcaster_name", "Master Spellcaster"), sc, 200, null,
                SotorText.Get("sotor_perk_master_spellcaster_desc", "Gain access to master level spells."), personal, 0f, none,
                "", (PartyRole)0, 0f, none, (TroopUsageFlags)65535, (TroopUsageFlags)65535);

            _selfish.Initialize(SotorText.Get("sotor_perk_selfish_name", "Selfish"), sc, 50, _wellControlled,
                SotorText.Get("sotor_perk_selfish_desc", "Your damaging spells do 90% reduced damage to yourself."), personal, -0.9f, factor,
                SotorText.Get("sotor_perk_selfish_desc2", "Your self targeted buff spells have 50% more duration."), personal, 0.15f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _wellControlled.Initialize(SotorText.Get("sotor_perk_well_controlled_name", "Well Controlled"), sc, 50, _selfish,
                SotorText.Get("sotor_perk_well_controlled_desc", "Your damaging spells do 30% less damage to troops in your party."), personal, -0.3f, factor,
                SotorText.Get("sotor_perk_well_controlled_desc2", "Gain 5% advantage in simulation battles."), partyLeader, 0.05f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _librarian.Initialize(SotorText.Get("sotor_perk_librarian_name", "Librarian"), sc, 125, _storyTeller,
                SotorText.Get("sotor_perk_librarian_desc", "You gain 25% more Spellcraft experience from casting spells."), personal, 0.25f, factor,
                SotorText.Get("sotor_perk_librarian_desc2", "Learning new spells and lores costs 50% less gold."), personal, -0.5f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _storyTeller.Initialize(SotorText.Get("sotor_perk_storyteller_name", "Storyteller"), sc, 125, _librarian,
                SotorText.Get("sotor_perk_storyteller_desc", "Every companion in your party gains 1000 experience in a random skill per day."), partyLeader, 1000f, add,
                SotorText.Get("sotor_perk_storyteller_desc2", "Your party gains a permanent +5 increase to party morale."), partyLeader, 5f, add,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _overCaster.Initialize(SotorText.Get("sotor_perk_overcaster_name", "Overcaster"), sc, 150, _efficientSpellCaster,
                SotorText.Get("sotor_perk_overcaster_desc", "Your instant damaging and healing spells are 20% more effective but cost 30% more winds of magic."), personal, 0.2f, factor,
                "", (PartyRole)0, 0.15f, factor, (TroopUsageFlags)0, (TroopUsageFlags)0);

            _efficientSpellCaster.Initialize(SotorText.Get("sotor_perk_efficient_spellcaster_name", "Efficient Spellcaster"), sc, 150, _overCaster,
                SotorText.Get("sotor_perk_efficient_spellcaster_desc", "Your instant damaging and healing spells are 20% less effective, but cost 30% less winds of magic."), personal, -0.2f, factor,
                "", (PartyRole)0, -0.15f, factor, (TroopUsageFlags)0, (TroopUsageFlags)0);

            _improvision.Initialize(SotorText.Get("sotor_perk_improvision_name", "Improvision"), sc, 225, _catalyst,
                SotorText.Get("sotor_perk_improvision_desc", "Your Winds of Magic is set to 25 if you have less than that at the beginning of the battle."), personal, 25f, add,
                SotorText.Get("sotor_perk_improvision_desc2", "+10% Persuasion chance during speech checks."), personal, 0.1f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _catalyst.Initialize(SotorText.Get("sotor_perk_catalyst_name", "Catalyst"), sc, 225, _improvision,
                SotorText.Get("sotor_perk_catalyst_desc", "For every enchanted item in your equipment slots you gain +5 extra Winds of magic at the start of battle."), personal, 5f, add,
                SotorText.Get("sotor_perk_catalyst_desc2", "You gain +20% Winds of Magic regeneration while waiting in a town."), personal, 0.2f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _dampener.Initialize(SotorText.Get("sotor_perk_dampener_name", "Dampener"), sc, 250, _arcaneLink,
                SotorText.Get("sotor_perk_dampener_desc", "Damage dealt by your damaging spells is reduced by 15%, but troops in your formation take 30% less damage from spells."), personal, -0.15f, factor,
                SotorText.Get("sotor_perk_dampener_desc2", "You gain 5% ward save."), personal, -0.05f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _arcaneLink.Initialize(SotorText.Get("sotor_perk_arcane_link_name", "Arcane Link"), sc, 250, _dampener,
                SotorText.Get("sotor_perk_arcane_link_desc", "Any buffs you cast on a friendly unit will now also apply to you even if you are not in range."), personal, 1f, add,
                SotorText.Get("sotor_perk_arcane_link_desc2", "As formation Captain, all troops in your formation deal additional 10% magic damage."), captain, 0.1f, factor,
                (TroopUsageFlags)0, (TroopUsageFlags)0);

            _trueTransmutation.Initialize(SotorText.Get("sotor_perk_archmage_name", "Archmage"), sc, 300, null,
                SotorText.Get("sotor_perk_archmage_desc", "Become an Archmage. Unlocks the restricted High Magic and Dark Magic lores, and lets you set two enchantments on a single item."), personal, 0f, none,
                "", (PartyRole)0, 0f, none, (TroopUsageFlags)65535, (TroopUsageFlags)65535);

            SotorLog.Info("SotorPerks: registered 14 Spellcraft perks.");
        }
    }
}
