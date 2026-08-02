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

        private const string GroupShipMagic = "{=sotor_mcm_grp_ship}Ship Magic";

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
            "{=sotor_mcm_arcane_conduit}Arcane Conduit",
            Order = 4, RequireRestart = false,
            HintText = "{=sotor_mcm_arcane_conduit_hint}Adds the Arcane Conduit ability to the spell wheel to channel Winds of Magic. Channeling recharges your Winds but makes you sluggish and more vulnerable to damage. Improving your caster level alleviates these effects.")]
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
        [SettingPropertyGroup(GroupTweaks, GroupOrder = 2)]
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

        public void SyncToStore()
        {
            SotorSettings.UseThrownAmberSpear = UseThrownAmberSpear;
            SotorSettings.EnableSkeletonArmies = EnableSkeletonArmies;
            SotorSettings.EnableMindControlledArmies = EnableMindControlledArmies;
            SotorSettings.EnableCompanionSpellcasters = EnableCompanionSpellcasters;
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
            SotorSettings.EnableSpellEffectivenessTweak = EnableSpellEffectivenessTweak;
            SotorSettings.SpellEffectivenessBonusPercent = SpellEffectivenessBonusPercent;
            SotorSettings.DisableMagicInSieges = DisableMagicInSieges;
            SotorSettings.EnableSpellShipDamage = EnableSpellShipDamage;
            SotorSettings.SpellShipDamagePercent = SpellShipDamagePercent;
            SotorSettings.EnableBurningDeckDamage = EnableBurningDeckDamage;
            SotorSettings.BurningDeckDamagePerSecond = BurningDeckDamagePerSecond;
            SotorSettings.EnableAbandonShipAI = EnableAbandonShipAI;
        }
    }
}
