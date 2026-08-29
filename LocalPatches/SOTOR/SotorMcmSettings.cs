using System;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace SOTOR
{

    public sealed class SotorMcmSettings : AttributeGlobalSettings<SotorMcmSettings>
    {
        public override string Id => "SOTOR_Settings_v1";

        public override string DisplayName => "Spells of the Old Realms";
        public override string FolderName => "SOTOR";

        public override string FormatType => "json2";

        private const string GroupFeatures = "{=sotor_mcm_grp_features}Features";
        private const string GroupTweaks = "{=sotor_mcm_grp_tweaks}Magic Tweaks";
        private const string GrpEff = "{=sotor_mcm_grp_tweaks}Magic Tweaks/{=sotor_mcm_grp_eff}Spell Effectiveness";
        private const string GrpWindsKill = "{=sotor_mcm_grp_tweaks}Magic Tweaks/{=sotor_mcm_grp_winds_kill}Winds on Magic Kill";
        private const string GrpArmor = "{=sotor_mcm_grp_tweaks}Magic Tweaks/{=sotor_mcm_grp_armor}Armor Effect on Winds Recharge";

        private const string GroupGore = "{=sotor_mcm_grp_gore}Spell Gore";

        private const string GroupPerf = "{=sotor_mcm_grp_features}Features/{=sotor_mcm_grp_perf}Performance";

        private const string GroupShipMagic = "{=sotor_mcm_grp_ship}Ship Magic";

        private const string GroupRivalWizards = "{=sotor_mcm_grp_rival}Rival Wizards";
        private const string GroupEnchanting = "{=sotor_mcm_grp_enchanting}Enchanting";

        private const string GrpRivalAdvanced =
            "{=sotor_mcm_grp_rival}Rival Wizards/{=sotor_mcm_grp_rival_adv}Advanced Generation Options";

        private const string GrpRivalPolitics =
            "{=sotor_mcm_grp_rival}Rival Wizards/{=sotor_mcm_grp_rival_politics}Tradition Politics";

        [SettingPropertyDropdown("{=sotor_mcm_hud_mode}Battle spell HUD", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_hud_mode_hint}When to show the bottom left spell and Winds of Magic panel in battle.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public Dropdown<string> HudMode { get; set; } = new Dropdown<string>(new[]
        {
            "{=sotor_mcm_hud_always}Always",
            "{=sotor_mcm_hud_casting}Only while casting",
            "{=sotor_mcm_hud_hidden}Hidden",
        }, 0);

        [SettingPropertyBool(
            "{=sotor_mcm_cast_slowmo}Cast Slow Motion",
            Order = 2, RequireRestart = false,
            HintText = "{=sotor_mcm_cast_slowmo_hint}Slows the battle to 30 percent speed while you aim or charge a spell. Turn it off to keep the game at full speed while you cast.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableCastSlowMotion { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_spell_dmg_log}Spell Damage Log",
            Order = 3, RequireRestart = false,
            HintText = "{=sotor_mcm_spell_dmg_log_hint}Shows a battle log line for your spell damage, healing, and friendly fire, colored by element.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableSpellDamageLog { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_amber_thrown}Improved Amber Spear",
            Order = 5, RequireRestart = false,
            HintText = "{=sotor_mcm_amber_thrown_hint}Swaps Amber Spear for a magic javelin that scales off Throwing and Spellcraft.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool UseThrownAmberSpear { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_skeleton_armies}Skeleton Armies",
            Order = 6, RequireRestart = false,
            HintText = "{=sotor_mcm_skeleton_armies_hint}Enables the resurrection of fallen enemies as skeletons after victorious battles.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableSkeletonArmies { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_mindcontrol_armies}Mind Controlled Armies",
            Order = 7, RequireRestart = false,
            HintText = "{=sotor_mcm_mindcontrol_armies_hint}Surviving units you mind controlled during a battle join your party afterward. Heroes never join. The in battle Mind Control spell works regardless of this setting.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableMindControlledArmies { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_companion_casters}Companion Spellcasters",
            Order = 8, RequireRestart = false,
            HintText = "{=sotor_mcm_companion_casters_hint}Lets your clan companions and family use magic and appear in the spellbook's hero cycle. Turning it off later keeps their learned lores, spells and Winds intact for when you turn it back on. The main hero is always a caster.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableCompanionSpellcasters { get; set; } = false;

        [SettingPropertyBool(
            "{=sotor_mcm_spare_civilians}Spells Spare Civilians",
            Order = 10, RequireRestart = false,
            HintText = "{=sotor_mcm_spare_civilians_hint}Spell damage skips bystanders such as arena spectators, townsfolk and merchants. Anyone actively fighting you is still a valid target.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool SpellsSpareCivilians { get; set; } = false;

        [SettingPropertyBool(
            "{=sotor_mcm_battle_regen}Winds regenerate in missions",
            Order = 9, RequireRestart = false,
            HintText = "{=sotor_mcm_battle_regen_hint}Recharges Winds of Magic for yourself, allies, and enemies while you are in a battle or town scene. Recharge rate depends on derived stats. Only works if you have a real time mod like Time Pass enabled.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableBattleWindsRegen { get; set; } = false;

        [SettingPropertyBool(
            "{=sotor_mcm_arcane_conduit}Arcane Conduit",
            Order = 4, RequireRestart = false,
            HintText = "{=sotor_mcm_arcane_conduit_hint}Lets casters channel to recharge Winds of Magic, at the cost of being sluggish and vulnerable while they do. Higher caster levels reduce the drawbacks. Affects enemy and companion wizards too.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public bool EnableArcaneConduit { get; set; } = true;

        [SettingPropertyDropdown("{=sotor_mcm_spellcraft_attr}Spellcraft Attribute", Order = 0, RequireRestart = true,
            HintText = "{=sotor_mcm_spellcraft_attr_hint}Which attribute governs the Spellcraft skill's learning rate. Takes effect after a restart. Default Intelligence.")]
        [SettingPropertyGroup(GroupFeatures, GroupOrder = 0)]
        public Dropdown<string> SpellcraftAttribute { get; set; } = new Dropdown<string>(new[]
        {
            "{=sotor_attr_vigor}Vigor",
            "{=sotor_attr_control}Control",
            "{=sotor_attr_endurance}Endurance",
            "{=sotor_attr_cunning}Cunning",
            "{=sotor_attr_social}Social",
            "{=sotor_attr_intelligence}Intelligence",
        }, 5);

        private static readonly string[] SpellcraftAttrIds =
            { "vigor", "control", "endurance", "cunning", "social", "intelligence" };

        [SettingPropertyBool(
            "{=sotor_mcm_disable_siege_magic}Disable Magic in Sieges",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_disable_siege_magic_hint}Disables spellcasting during siege battles.")]

        [SettingPropertyGroup(GroupTweaks, GroupOrder = 3)]
        public bool DisableMagicInSieges { get; set; } = false;

        [SettingPropertyBool(
            "{=sotor_mcm_spell_eff}Spell Effectiveness",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_spell_eff_hint}Adds a flat bonus to your Spell Effectiveness (scales spell damage and healing).")]
        [SettingPropertyGroup(GrpEff)]
        public bool EnableSpellEffectivenessTweak { get; set; } = false;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_spell_eff_pct}Spell effectiveness bonus",
            -100f, 200f, "0\\%", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_spell_eff_pct_hint}Percent added to Spell Effectiveness. -100% means spells deal no damage, 0% is unchanged, 200% adds triple.")]
        [SettingPropertyGroup(GrpEff)]
        public float SpellEffectivenessBonusPercent { get; set; } = 0f;

        [SettingPropertyBool(
            "{=sotor_mcm_winds_on_kill}Winds on Magic Kill",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_winds_on_kill_hint}Gives Winds of Magic on a spell kill. Player only.")]
        [SettingPropertyGroup(GrpWindsKill)]
        public bool EnableWindsOnMagicKill { get; set; } = false;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_winds_on_kill_amt}Winds per magic kill",
            -15f, 30f, "0.0", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_winds_on_kill_amt_hint}How much Winds of Magic each spell kill grants.")]
        [SettingPropertyGroup(GrpWindsKill)]
        public float WindsOnMagicKillAmount { get; set; } = 0f;

        [SettingPropertyBool(
            "{=sotor_mcm_armor_recharge}Armor Effect on Winds Recharge",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_armor_recharge_hint}Changes how much armor weight slows Winds of Magic recharge.")]
        [SettingPropertyGroup(GrpArmor)]
        public bool EnableArmorWomRechargeTweak { get; set; } = false;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_armor_recharge_pct}Armor effect on Winds recharge",
            -100f, 200f, "0\\%", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_armor_recharge_pct_hint}-100% removes the armor penalty. 0% keeps default. 200% triples the armor penalty.")]
        [SettingPropertyGroup(GrpArmor)]
        public float ArmorWomRechargeEffectPercent { get; set; } = 0f;

        [SettingPropertyInteger(
            "{=sotor_mcm_deaths_at_once}Spell deaths resolved at once",
            2, 24, "0", Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_deaths_at_once_hint}Lag and crash protection. Lower it if huge spells stutter or freeze. Higher resolves big spells faster, but is the riskiest setting here.")]
        [SettingPropertyGroup(GroupPerf, GroupOrder = 0)]
        public int SpellDeathsAtOnce { get; set; } = 12;

        [SettingPropertyBool(
            "{=sotor_mcm_gore}Spell Gore",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_gore_hint}Big spell kills can blow a body apart or scatter a skeleton into bones. Bombardments pack the most punch while hexes never dismember. Heroes are never affected.")]
        [SettingPropertyGroup(GroupGore, GroupOrder = 2)]
        public bool EnableSpellGore { get; set; } = true;

        [SettingPropertyInteger(
            "{=sotor_mcm_gore_at_once}Bodies that can burst at once",
            1, 24, "0", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_gore_at_once_hint}How many bodies come apart at once. Capped by Spell deaths resolved at once, under Performance.")]
        [SettingPropertyGroup(GroupGore, GroupOrder = 2)]
        public int SpellGoreAtOnce { get; set; } = 12;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_gore_gib}Body explosion chance",
            0f, 200f, "0\\%", Order = 2, RequireRestart = false,
            HintText = "{=sotor_mcm_gore_gib_hint}Scales how often a spell kill bursts a body apart. 100% is the default rate, 0% turns it off.")]
        [SettingPropertyGroup(GroupGore, GroupOrder = 2)]
        public float SpellGoreGibPercent { get; set; } = 100f;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_gore_shatter}Skeleton shatter chance",
            0f, 200f, "0\\%", Order = 3, RequireRestart = false,
            HintText = "{=sotor_mcm_gore_shatter_hint}Scales how often a spell kill scatters skeletons into bones. 130% is the default rate, 0% turns it off.")]
        [SettingPropertyGroup(GroupGore, GroupOrder = 2)]
        public float SpellGoreShatterPercent { get; set; } = 130f;

        [SettingPropertyBool(
            "{=sotor_mcm_ship_dmg}Spells Damage Ships",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_ship_dmg_hint}Requires the War Sails expansion. When on, your spells damage enemy ship hulls, set them ablaze, and shred their sails in naval battles. When off, magic only harms the crew.")]
        [SettingPropertyGroup(GroupShipMagic, GroupOrder = 1)]
        public bool EnableSpellShipDamage { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_ship_dmg_pct}Ship damage",
            0f, 300f, "0\\%", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_ship_dmg_pct_hint}Scales how much damage your spells deal to ships. 100% is the tuned default and 0% is the same as turning ship damage off.")]
        [SettingPropertyGroup(GroupShipMagic, GroupOrder = 1)]
        public float SpellShipDamagePercent { get; set; } = 100f;

        [SettingPropertyBool(
            "{=sotor_mcm_burning_deck}Burning Decks Hurt Crew",
            Order = 2, RequireRestart = false,
            HintText = "{=sotor_mcm_burning_deck_hint}Requires War Sails. When a ship is set ablaze, anyone standing on its deck takes fire damage over time until they get off. Vanilla leaves the crew unharmed on a burning ship.")]
        [SettingPropertyGroup(GroupShipMagic, GroupOrder = 1)]
        public bool EnableBurningDeckDamage { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "{=sotor_mcm_burning_deck_dps}Burning deck damage",
            0f, 20f, "0.0", Order = 3, RequireRestart = false,
            HintText = "{=sotor_mcm_burning_deck_dps_hint}Fire damage per second to anyone on an ablaze deck. Default 4.")]
        [SettingPropertyGroup(GroupShipMagic, GroupOrder = 1)]
        public float BurningDeckDamagePerSecond { get; set; } = 4f;

        [SettingPropertyBool(
            "{=sotor_mcm_abandon_ship}Crew Abandon Doomed Ships",
            Order = 4, RequireRestart = false,
            HintText = "{=sotor_mcm_abandon_ship_hint}Requires War Sails. Crew of a sinking or burning ship flee to the nearest safe ship by running across a bridge or swimming, preferring their own but boarding an enemy vessel if they must.")]
        [SettingPropertyGroup(GroupShipMagic, GroupOrder = 1)]
        public bool EnableAbandonShipAI { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_enchanting}Enable enchanting",
            Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_enchanting_hint}Enchant items with the magical lores you know. Town enchanter's quarters, blueprint books, reagent drops, and enchanted gear on rival wizards.")]
        [SettingPropertyGroup(GroupEnchanting, GroupOrder = 4)]
        public bool EnableEnchanting { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_magicloot}Enable magic item battle loot",
            Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_magicloot_hint}Victorious battles can reward a fallen enemy's weapon or armor bearing a random enchantment.")]
        [SettingPropertyGroup(GroupEnchanting, GroupOrder = 4)]
        public bool EnableMagicItemLoot { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_shopmaterials}Enchanter sells reagents",
            Order = 2, RequireRestart = false,
            HintText = "{=sotor_mcm_shopmaterials_hint}The enchanter's quarter stocks a small weekly supply of enchanting reagents. Turn off to make reagents drop only.")]
        [SettingPropertyGroup(GroupEnchanting, GroupOrder = 4)]
        public bool EnableEnchantShopMaterials { get; set; } = true;

        [SettingPropertyBool(
            "{=sotor_mcm_shopbooks}Enchanter sells blueprint books",
            Order = 3, RequireRestart = false,
            HintText = "{=sotor_mcm_shopbooks_hint}The enchanter's quarter stocks blueprint books, rotating weekly by the town's ruling lore and prosperity. Turn off to learn only from drops and masters.")]
        [SettingPropertyGroup(GroupEnchanting, GroupOrder = 4)]
        public bool EnableEnchantShopBooks { get; set; } = true;

        [SettingPropertyInteger(
            "{=sotor_mcm_reagent_rate}Reagent drop rate",
            25, 400, "0'%'", Order = 4, RequireRestart = false,
            HintText = "{=sotor_mcm_reagent_rate_hint}How many enchanting reagents defeated enemies drop, as a percent of the default rate.")]
        [SettingPropertyGroup(GroupEnchanting, GroupOrder = 4)]
        public int ReagentDropRatePercent { get; set; } = 100;

        [SettingPropertyBool(
            "{=sotor_mcm_rival_casters}Enable Rival Wizards",
            Order = 0, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_casters_hint}Fills the world with wizard lords who cast in battle. Each clan's tradition is fixed for the campaign. Off stops them casting.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public bool EnableRivalCasters
        {
            get => _enableRivalCasters;
            set { _enableRivalCasters = value; OnPropertyChanged(); CapturePending(); }
        }

        private bool _enableRivalCasters;

        [SettingPropertyBool(
            "{=sotor_mcm_spellbook_masters}Only Learn Magic From Masters",
            Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_spellbook_masters_hint}The spellbook screen no longer sells anything beyond Minor Magic. Seek out wizard lords and learn from them instead. Does nothing while Rival Wizards is off.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public bool SpellbookRequiresMasters
        {
            get => _spellbookRequiresMasters;
            set { _spellbookRequiresMasters = value; OnPropertyChanged(); CapturePending(); }
        }

        private bool _spellbookRequiresMasters = true;

        [SettingPropertyDropdown("{=sotor_mcm_rival_density}Magic Density", Order = 2, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_density_hint}How common wizard lords are. Picking a preset fills in the Advanced sliders. Editing a slider switches this to Custom.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public Dropdown<string> RivalMagicDensity
        {
            get => _rivalMagicDensity;
            set
            {

                Unhook(_rivalMagicDensity);
                _rivalMagicDensity = value;
                Hook(_rivalMagicDensity);
                OnPropertyChanged();
                CapturePending();
            }
        }

        private Dropdown<string> _rivalMagicDensity = NewDensityDropdown();

        private static Dropdown<string> NewDensityDropdown() => new Dropdown<string>(new[]
        {
            "{=sotor_mcm_rival_density_rumors}Rumors of Magic",
            "{=sotor_mcm_rival_density_low}Low Magic",
            "{=sotor_mcm_rival_density_age}Age of Sorcery",
            "{=sotor_mcm_rival_density_high}High Magic",
            "{=sotor_mcm_rival_density_custom}Custom",
        }, 2);

        public SotorMcmSettings()
        {
            Hook(_rivalMagicDensity);

        }

        public void ResetRivalToDefaults()
        {
            RivalWorldSeed = string.Empty;
            SpellbookRequiresMasters = true;
            RivalLoreSource = NewLoreSourceDropdown();
            RivalIncludeRulers = true;
            RivalIncludeMinorFactions = true;
            RivalRaiseDeadPartyCapPercent = 50;

            RivalMinClanTierForCaster = 0;
            StandingLordSharePercent = 100;
            StandingLearnLore = 25;
            StandingLearnSpell = 5;
            StandingExecuteCaster = -25;
            StandingFreeCaster = 10;
            StandingAssistCaster = 3;

            RivalMagicDensity.SelectedIndex = DefaultDensityIndex;

            CapturePending();
            SotorLog.Info("MCM: Rival Wizards settings restored to defaults.");
        }

        private const int DefaultDensityIndex = 2;

        private void Hook(Dropdown<string> d)
        {
            if (d != null) d.PropertyChanged += OnDensityDropdownChanged;
        }

        private void Unhook(Dropdown<string> d)
        {
            if (d != null) d.PropertyChanged -= OnDensityDropdownChanged;
        }

        private void OnDensityDropdownChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e != null && e.PropertyName != "SelectedIndex") return;
            int index = _rivalMagicDensity != null ? _rivalMagicDensity.SelectedIndex : 1;
            if (index == RivalDensityCustomIndex) return;

            var preset = PresetFor(index);

            _rivalCasterLordShare = preset.Lords;
            _rivalCasterWandererShare = preset.Wanderers;
            _rivalMemberOnlyLoreClanChance = preset.Forbidden;
            _rivalPowerShift = preset.Power;

            OnPropertyChanged(nameof(RivalCasterLordShare));
            OnPropertyChanged(nameof(RivalCasterWandererShare));
            OnPropertyChanged(nameof(RivalMemberOnlyLoreClanChance));
            OnPropertyChanged(nameof(RivalPowerShift));
            SotorLog.Info($"MCM: density preset {index} applied -> lords {preset.Lords}%, wanderers "
                          + $"{preset.Wanderers}%, forbidden {preset.Forbidden}%, power {preset.Power:+0;-0;0}.");
            CapturePending();
        }

        private bool IsOnSelectedPreset()
        {
            int index = _rivalMagicDensity != null ? _rivalMagicDensity.SelectedIndex : 1;
            if (index == RivalDensityCustomIndex) return true;

            var preset = PresetFor(index);
            return Near(_rivalCasterLordShare, preset.Lords)
                && Near(_rivalCasterWandererShare, preset.Wanderers)
                && Near(_rivalMemberOnlyLoreClanChance, preset.Forbidden)
                && _rivalPowerShift == preset.Power;
        }

        private void FlipToCustomIfOffPreset()
        {
            if (IsOnSelectedPreset()) return;
            _rivalMagicDensity.SelectedIndex = RivalDensityCustomIndex;
            SotorLog.Info("MCM: a density slider was edited, switching Magic Density to Custom.");
        }

        private const int RivalDensityCustomIndex = 4;

        [SettingPropertyDropdown("{=sotor_mcm_rival_lore_source}Lore Assignment", Order = 3, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_lore_source_hint}By Clan gives every wizard clan its own lore. By Culture gives a whole faction the same lore, so Vlandia or Battania read as a single order. Applies on the next load.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public Dropdown<string> RivalLoreSource
        {
            get => _rivalLoreSource;
            set { _rivalLoreSource = value; OnPropertyChanged(); CapturePending(); }
        }

        private Dropdown<string> _rivalLoreSource = NewLoreSourceDropdown();

        private static Dropdown<string> NewLoreSourceDropdown() => new Dropdown<string>(new[]
        {
            "{=sotor_mcm_rival_lore_by_clan}By Clan",
            "{=sotor_mcm_rival_lore_by_culture}By Culture",
        }, 0);

        [SettingPropertyBool(
            "{=sotor_mcm_rival_rulers}Wizard Kings",
            Order = 5, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_rulers_hint}Allows faction leaders to be wizards. Turn off for a world where only lesser nobles practise magic. Applies on the next load.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public bool RivalIncludeRulers
        {
            get => _rivalIncludeRulers;
            set { _rivalIncludeRulers = value; OnPropertyChanged(); CapturePending(); }
        }

        private bool _rivalIncludeRulers = true;

        [SettingPropertyInteger(
            "{=sotor_mcm_rival_raise_cap}Undead raised per battle",
            0, 100, "0\\%", Order = 7, RequireRestart = false,
            HintText = "{=sotor_mcm_rival_raise_cap_hint}Caps how many skeletons an AI necromancer can raise from one battle, as a percent of his party size limit.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public int RivalRaiseDeadPartyCapPercent { get; set; } = 50;

        [SettingPropertyBool(
            "{=sotor_mcm_rival_minor}Minor Faction Wizards",
            Order = 6, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_minor_hint}Lets mercenary companies and outlaw bands field wizards too. Off keeps magic to the great houses.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public bool RivalIncludeMinorFactions
        {
            get => _rivalIncludeMinorFactions;
            set { _rivalIncludeMinorFactions = value; OnPropertyChanged(); CapturePending(); }
        }

        private bool _rivalIncludeMinorFactions = true;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_lord_share}Effect on individual lords (%)",
            0, 100, "0'%'", Order = 0, RequireRestart = false,
            HintText = "{=sotor_mcm_std_lord_share_hint}How much of a standing change also influences each lord's relation in that order.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingLordSharePercent
        {
            get => _standingLordSharePercent;
            set { _standingLordSharePercent = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingLordSharePercent = 100;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_lore}Learn a lore",
            0, 50, "0", Order = 1, RequireRestart = false,
            HintText = "{=sotor_mcm_std_lore_hint}How much learning a whole lore moves your standing. Orders that favour that lore approve, rivals take offense.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingLearnLore
        {
            get => _standingLearnLore;
            set { _standingLearnLore = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingLearnLore = 25;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_spell}Learn a spell",
            0, 15, "0", Order = 2, RequireRestart = false,
            HintText = "{=sotor_mcm_std_spell_hint}How much learning a single spell moves your standing. Orders that favour that lore approve, rivals take offense.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingLearnSpell
        {
            get => _standingLearnSpell;
            set { _standingLearnSpell = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingLearnSpell = 5;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_execute}Execute a caster",
            -100, 0, "0", Order = 4, RequireRestart = false,
            HintText = "{=sotor_mcm_std_execute_hint}What executing one of an order's wizards costs you with it. Killing one in battle does not count.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingExecuteCaster
        {
            get => _standingExecuteCaster;
            set { _standingExecuteCaster = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingExecuteCaster = -25;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_free}Free a captured caster",
            0, 50, "0", Order = 5, RequireRestart = false,
            HintText = "{=sotor_mcm_std_free_hint}What releasing one of an order's captured wizards earns you. Ransoms and escapes do not count.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingFreeCaster
        {
            get => _standingFreeCaster;
            set { _standingFreeCaster = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingFreeCaster = 10;

        [SettingPropertyInteger(
            "{=sotor_mcm_std_assist}Fight beside an order",
            0, 15, "0", Order = 6, RequireRestart = false,
            HintText = "{=sotor_mcm_std_assist_hint}What fighting a battle alongside an order's wizard earns you, counted only once per battle.")]
        [SettingPropertyGroup(GrpRivalPolitics)]
        public int StandingAssistCaster
        {
            get => _standingAssistCaster;
            set { _standingAssistCaster = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _standingAssistCaster = 3;

        [SettingPropertyButton(
            "{=sotor_mcm_rival_info}Preview This World", Content = "{=sotor_mcm_rival_info_btn}Preview",
            Order = 8, RequireRestart = false,
            HintText = "{=sotor_mcm_rival_info_hint}Predicts what your current settings and seed would produce, by tradition. Reads the screen, so you can try numbers first. It does not describe the wizards your save already has.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public Action RivalWorldInfo { get; set; } = () => SotorMcmActions.ShowWorldReport();

        [SettingPropertyButton(
            "{=sotor_mcm_rival_reset}Restore Default Settings", Content = "{=sotor_mcm_rival_reset_btn}Defaults",
            Order = 9, RequireRestart = false,
            HintText = "{=sotor_mcm_rival_reset_hint}Puts the Rival Wizards settings back to their defaults. Press Done to apply them.")]
        [SettingPropertyGroup(GroupRivalWizards, GroupOrder = 5)]
        public Action RivalResetWorld { get; set; } = () => SotorMcmActions.ResetRivalOptions();

        [SettingPropertyInteger(

            "{=sotor_mcm_rival_power}Rival Wizard Power",
            -4, 4, "0", Order = 5, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_power_hint}Shifts how strong the world's wizards are. Zero is the default setting with no change. Your own clan is never affected, and hidden masters stay at full strength.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public int RivalPowerShift
        {
            get => _rivalPowerShift;
            set
            {

                bool wasOnPreset = IsOnSelectedPreset();
                _rivalPowerShift = value;
                OnPropertyChanged();
                if (wasOnPreset) FlipToCustomIfOffPreset();
                CapturePending();
            }
        }

        private int _rivalPowerShift;

        [SettingPropertyInteger(
            "{=sotor_mcm_rival_lord_share}Wizard lords",
            0, 100, "0\\%", Order = 1, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_lord_share_hint}Percent of eligible lords who are wizards. Editing this switches Magic Density to Custom.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public int RivalCasterLordShare
        {
            get => _rivalCasterLordShare;
            set
            {

                bool wasOnPreset = IsOnSelectedPreset();
                _rivalCasterLordShare = value;
                OnPropertyChanged();
                if (wasOnPreset) FlipToCustomIfOffPreset();
                CapturePending();
            }
        }

        [SettingPropertyInteger(
            "{=sotor_mcm_rival_wanderer_share}Tavern wizards",
            0, 100, "0\\%", Order = 2, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_wanderer_share_hint}Percent of wandering companions who start as wizards. Editing this switches Magic Density to Custom.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public int RivalCasterWandererShare
        {
            get => _rivalCasterWandererShare;
            set
            {

                bool wasOnPreset = IsOnSelectedPreset();
                _rivalCasterWandererShare = value;
                OnPropertyChanged();
                if (wasOnPreset) FlipToCustomIfOffPreset();
                CapturePending();
            }
        }

        [SettingPropertyInteger(
            "{=sotor_mcm_rival_forbidden}Hidden Dark or High masters",
            0, 100, "0\\%", Order = 3, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_forbidden_hint}Chance each lord of a great house is secretly a master of Dark or High Magic. At least one of each always exists. Editing this switches Magic Density to Custom.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public int RivalMemberOnlyLoreClanChance
        {
            get => _rivalMemberOnlyLoreClanChance;
            set
            {

                bool wasOnPreset = IsOnSelectedPreset();
                _rivalMemberOnlyLoreClanChance = value;
                OnPropertyChanged();
                if (wasOnPreset) FlipToCustomIfOffPreset();
                CapturePending();
            }
        }

        private int _rivalCasterLordShare = 20;
        private int _rivalCasterWandererShare = 20;
        private int _rivalMemberOnlyLoreClanChance = 3;

        [SettingPropertyInteger(
            "{=sotor_mcm_rival_min_tier}Minimum clan tier",
            0, 6, "0", Order = 4, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_min_tier_hint}Houses below this clan tier never field wizards. 0 means any house can. Applies to every density setting.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public int RivalMinClanTierForCaster
        {
            get => _rivalMinClanTierForCaster;
            set { _rivalMinClanTierForCaster = value; OnPropertyChanged(); CapturePending(); }
        }

        private int _rivalMinClanTierForCaster;

        [SettingPropertyText(
            "{=sotor_mcm_rival_seed}World seed",
            Order = 0, RequireRestart = true,
            HintText = "{=sotor_mcm_rival_seed_hint}The seed your world is built from. Determines who becomes a wizard. Clear it for your own seed.")]
        [SettingPropertyGroup(GrpRivalAdvanced)]
        public string RivalWorldSeed
        {
            get => string.IsNullOrWhiteSpace(_rivalWorldSeed)
                ? AbilitySystem.Rivals.SotorRivalSeeder.CampaignSeedText()
                : _rivalWorldSeed;
            set
            {

                var trimmed = value?.Trim();
                _rivalWorldSeed =
                    string.Equals(trimmed, AbilitySystem.Rivals.SotorRivalSeeder.CampaignSeedText(),
                        System.StringComparison.OrdinalIgnoreCase)
                        ? string.Empty
                        : value;
                OnPropertyChanged();
                CapturePending();
            }
        }

        private string _rivalWorldSeed = string.Empty;

        private void CapturePending()
        {

            SotorMcmPending.Instance = this;
        }

        public void SyncToStore()
        {
            SotorSettings.UseThrownAmberSpear = UseThrownAmberSpear;
            SotorSettings.EnableSkeletonArmies = EnableSkeletonArmies;
            SotorSettings.EnableMindControlledArmies = EnableMindControlledArmies;
            SotorSettings.EnableCompanionSpellcasters = EnableCompanionSpellcasters;
            SotorSettings.EnableRivalCasters = EnableRivalCasters;
            SotorSettings.SpellbookRequiresMasters = SpellbookRequiresMasters;
            SotorSettings.EnableEnchanting = EnableEnchanting;
            SotorSettings.SpellsSpareCivilians = SpellsSpareCivilians;
            SotorSettings.EnableMagicItemLoot = EnableMagicItemLoot;
            SotorSettings.EnableEnchantShopMaterials = EnableEnchantShopMaterials;
            SotorSettings.EnableEnchantShopBooks = EnableEnchantShopBooks;
            SotorSettings.ReagentDropRatePercent = ReagentDropRatePercent;
            SotorSettings.RivalLoreByCulture = RivalLoreSource?.SelectedIndex == 1;
            SotorSettings.RivalIncludeRulers = RivalIncludeRulers;
            SotorSettings.RivalIncludeMinorFactions = RivalIncludeMinorFactions;
            SotorSettings.RivalPowerShift = RivalPowerShift;
            SotorSettings.RivalRaiseDeadPartyCapPercent = RivalRaiseDeadPartyCapPercent;
            SotorSettings.StandingLordSharePercent = StandingLordSharePercent;
            SotorSettings.StandingLearnLore = StandingLearnLore;
            SotorSettings.StandingLearnSpell = StandingLearnSpell;
            SotorSettings.StandingExecuteCaster = StandingExecuteCaster;
            SotorSettings.StandingFreeCaster = StandingFreeCaster;
            SotorSettings.StandingAssistCaster = StandingAssistCaster;

            SotorSettings.RivalCasterLordShare = RivalCasterLordShare;
            SotorSettings.RivalCasterWandererShare = RivalCasterWandererShare;
            SotorSettings.RivalMemberOnlyLoreClanChance = RivalMemberOnlyLoreClanChance;

            SotorSettings.RivalMinClanTierForCaster = RivalMinClanTierForCaster;
            SotorSettings.RivalWorldSeed = RivalWorldSeed ?? string.Empty;
            SotorSettings.EnableArcaneConduit = EnableArcaneConduit;
            SotorSettings.EnableCastSlowMotion = EnableCastSlowMotion;
            int attrIdx = SpellcraftAttribute != null ? SpellcraftAttribute.SelectedIndex : 5;
            SotorSettings.SpellcraftAttributeId =
                (attrIdx >= 0 && attrIdx < SpellcraftAttrIds.Length) ? SpellcraftAttrIds[attrIdx] : "intelligence";
            SotorSettings.HudMode = HudMode != null ? HudMode.SelectedIndex : 0;
            SotorSettings.EnableSpellDamageLog = EnableSpellDamageLog;
            SotorSettings.EnableWindsOnMagicKill = EnableWindsOnMagicKill;
            SotorSettings.WindsOnMagicKillAmount = WindsOnMagicKillAmount;
            SotorSettings.EnableArmorWomRechargeTweak = EnableArmorWomRechargeTweak;
            SotorSettings.ArmorWomRechargeEffectPercent = ArmorWomRechargeEffectPercent;

            SotorSettings.EnableSpellGore = EnableSpellGore;
            SotorSettings.SpellGoreGibScale = SpellGoreGibPercent / 100f;
            SotorSettings.SpellGoreShatterScale = SpellGoreShatterPercent / 100f;
            SotorSettings.SpellGoreAtOnce = SpellGoreAtOnce;
            SotorSettings.SpellDeathsAtOnce = SpellDeathsAtOnce;
            SotorSettings.EnableBattleWindsRegen = EnableBattleWindsRegen;
            SotorSettings.EnableSpellEffectivenessTweak = EnableSpellEffectivenessTweak;
            SotorSettings.SpellEffectivenessBonusPercent = SpellEffectivenessBonusPercent;
            SotorSettings.DisableMagicInSieges = DisableMagicInSieges;
            SotorSettings.EnableSpellShipDamage = EnableSpellShipDamage;
            SotorSettings.SpellShipDamagePercent = SpellShipDamagePercent;
            SotorSettings.EnableBurningDeckDamage = EnableBurningDeckDamage;
            SotorSettings.BurningDeckDamagePerSecond = BurningDeckDamagePerSecond;
            SotorSettings.EnableAbandonShipAI = EnableAbandonShipAI;
        }

        private struct DensityPreset
        {
            public int Lords;
            public int Wanderers;
            public int Forbidden;
            public int Power;
        }

        private static DensityPreset PresetFor(int index)
        {
            switch (index)
            {
                case 0: return new DensityPreset { Lords = 3, Wanderers = 3, Forbidden = 2, Power = -1 };
                case 2: return new DensityPreset { Lords = 20, Wanderers = 20, Forbidden = 3, Power = 1 };
                case 3: return new DensityPreset { Lords = 40, Wanderers = 40, Forbidden = 5, Power = 2 };
                default: return new DensityPreset { Lords = 8, Wanderers = 8, Forbidden = 2, Power = 0 };
            }
        }

        private static bool Near(float a, float b)
        {
            return System.Math.Abs(a - b) < 0.01f;
        }

    }
}
