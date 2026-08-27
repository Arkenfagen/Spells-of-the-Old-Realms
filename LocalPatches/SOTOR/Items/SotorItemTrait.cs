using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using SOTOR.AbilitySystem;
using TaleWorlds.Core;

namespace SOTOR.Items
{

    [Serializable]
    [XmlType("ItemTrait")]
    public class SotorItemTrait
    {
        [XmlAttribute]
        public string ItemTraitStringId { get; set; }

        [XmlAttribute]
        public string ItemTraitName { get; set; } = "Invalid ItemTrait";

        [XmlElement]
        public string ItemTraitDescription { get; set; } = "";

        [XmlElement]
        public ResistanceTuple ResistanceTuple { get; set; }

        [XmlElement]
        public AmplifierTuple AmplifierTuple { get; set; }

        [XmlElement]
        public DamageProportionTuple AdditionalDamageTuple { get; set; }

        [XmlElement("OnWeaponHitScript")]
        public WeaponScriptTuple OnWeaponHitScript { get; set; }

        [XmlAttribute]
        public string ImbuedStatusEffectId { get; set; } = "none";

        [XmlAttribute]
        public float ImbuedEffectChance { get; set; } = 0.25f;

        [XmlAttribute]
        public string IconName { get; set; } = "none";

        [XmlElement("WeaponParticlePreset")]
        public WeaponParticlePreset WeaponParticlePreset { get; set; }

        [XmlAttribute]
        public bool IsCraftable { get; set; } = false;

        [XmlElement]
        public SotorItemTraitItemType ValidItemType { get; set; } = SotorItemTraitItemType.Invalid;

        [XmlElement]
        public SotorIngredientType IngredientItem { get; set; } = SotorIngredientType.Invalid;

        [XmlElement]
        public int IngredientAmount { get; set; } = 1;

        [XmlElement]
        public StatsTuple StatsTuple { get; set; }

        [XmlAttribute]
        public string RequiredLore { get; set; } = "";

        [XmlAttribute]
        public int LearnThreshold { get; set; } = 25;

        [XmlAttribute]
        public string Tradition { get; set; } = "";

        [XmlAttribute]
        public string RequiredSkill { get; set; } = "";

        [XmlAttribute]
        public int SkillThreshold { get; set; } = 0;

        public bool HasSkillRequirement => !string.IsNullOrEmpty(RequiredSkill);

        public string[] RequiredSkillIds =>
            string.IsNullOrEmpty(RequiredSkill)
                ? new string[0]
                : RequiredSkill.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        public bool HasLoreRequirement => !string.IsNullOrEmpty(RequiredLore);

        public int TypeOrder
        {
            get
            {
                if (RequiredLore == "DarkMagic") return 1;
                if (RequiredLore == "HighMagic") return 2;
                if (HasLoreRequirement) return 0;
                switch (Tradition)
                {
                    case "Elf": return 3;
                    case "Dwarf": return 4;
                    case "Sigmar": return 5;
                    case "Ulric": return 6;
                    case "Shallya": return 7;
                    case "Lady": return 8;
                    default: return 9;
                }
            }
        }

        public SpellCastingLevel RequiredCastingLevel
        {
            get
            {
                if (LearnThreshold <= 50) return SpellCastingLevel.Minor;
                if (LearnThreshold <= 100) return SpellCastingLevel.Entry;
                if (LearnThreshold <= 175) return SpellCastingLevel.Adept;
                return SpellCastingLevel.Master;
            }
        }

        public static bool IsValidFor(SotorItemTrait trait, ItemObject.ItemTypeEnum itemType)
        {
            if (trait == null) return false;
            switch (trait.ValidItemType)
            {
                case SotorItemTraitItemType.Melee:
                    return itemType == ItemObject.ItemTypeEnum.OneHandedWeapon
                        || itemType == ItemObject.ItemTypeEnum.TwoHandedWeapon
                        || itemType == ItemObject.ItemTypeEnum.Polearm;
                case SotorItemTraitItemType.Thrown:
                    return itemType == ItemObject.ItemTypeEnum.Thrown;
                case SotorItemTraitItemType.Ammo:
                    return itemType == ItemObject.ItemTypeEnum.Arrows
                        || itemType == ItemObject.ItemTypeEnum.Bolts
                        || itemType == ItemObject.ItemTypeEnum.Bullets
                        || itemType == ItemObject.ItemTypeEnum.SlingStones
                        || itemType == ItemObject.ItemTypeEnum.Thrown;
                case SotorItemTraitItemType.Ranged:
                    return itemType == ItemObject.ItemTypeEnum.Thrown
                        || itemType == ItemObject.ItemTypeEnum.Bow
                        || itemType == ItemObject.ItemTypeEnum.Crossbow
                        || itemType == ItemObject.ItemTypeEnum.Sling
                        || itemType == ItemObject.ItemTypeEnum.Musket
                        || itemType == ItemObject.ItemTypeEnum.Pistol;
                case SotorItemTraitItemType.Weapon:
                    return itemType == ItemObject.ItemTypeEnum.OneHandedWeapon
                        || itemType == ItemObject.ItemTypeEnum.TwoHandedWeapon
                        || itemType == ItemObject.ItemTypeEnum.Polearm
                        || itemType == ItemObject.ItemTypeEnum.Thrown
                        || itemType == ItemObject.ItemTypeEnum.Bow
                        || itemType == ItemObject.ItemTypeEnum.Crossbow
                        || itemType == ItemObject.ItemTypeEnum.Sling
                        || itemType == ItemObject.ItemTypeEnum.Musket
                        || itemType == ItemObject.ItemTypeEnum.Pistol;
                case SotorItemTraitItemType.Shield:
                    return itemType == ItemObject.ItemTypeEnum.Shield;
                case SotorItemTraitItemType.Armor:
                    return itemType == ItemObject.ItemTypeEnum.HeadArmor
                        || itemType == ItemObject.ItemTypeEnum.BodyArmor
                        || itemType == ItemObject.ItemTypeEnum.LegArmor
                        || itemType == ItemObject.ItemTypeEnum.HandArmor
                        || itemType == ItemObject.ItemTypeEnum.ChestArmor
                        || itemType == ItemObject.ItemTypeEnum.Cape;
                default:
                    return false;
            }
        }

        public bool IsValidForItem(ItemObject item)
        {
            return item != null && IsValidFor(this, item.ItemType);
        }
    }

    [Serializable]
    public enum SotorItemTraitItemType
    {
        Invalid,
        Weapon,
        Armor,
        Ammo,
        Shield,
        Ranged,
        Thrown,
        Melee
    }

    [Serializable]
    public enum SotorIngredientType
    {
        Invalid,
        ArcaneScroll,
        BlessedWater,
        DragonBlood,
        AmberCrystal,
        WarpstoneDust,
        GemStone
    }

    [Serializable]
    public enum SotorItemTraitStatType
    {
        Invalid,
        HealthMax,
        HealthRegen,
        WindsOfMagicMax,
        WindsOfMagicRegen,
        PartySpeed,
        Skill,
        ShieldHealth,
        ArmorPenetration,
        SpellRadius,
        MovementSpeed,
        CustomResourceGain,
        MissileSpeed,
        SwingSpeed,
        ReloadSpeed,
        ShieldDamage,
        Cleave,
        MultiPenetration,
        ScatterShot,
        ShieldPenetration
    }

    [Serializable]
    public class ResistanceTuple
    {
        [XmlAttribute]
        public DamageType ResistedDamageType = DamageType.Invalid;

        [XmlAttribute]
        public float ReductionPercent = 0f;
    }

    [Serializable]
    public class AmplifierTuple
    {
        [XmlAttribute]
        public DamageType AmplifiedDamageType = DamageType.Invalid;

        [XmlAttribute]
        public float DamageAmplifier = 0f;
    }

    [Serializable]
    public class DamageProportionTuple
    {
        [XmlAttribute]
        public DamageType DamageType = DamageType.Invalid;

        [XmlAttribute]
        public float Percent = 0f;
    }

    [Serializable]
    public class StatsTuple
    {
        [XmlAttribute]
        public SotorItemTraitStatType StatType { get; set; } = SotorItemTraitStatType.Invalid;

        [XmlAttribute]
        public string SkillId { get; set; } = "none";

        [XmlAttribute]
        public float Value { get; set; } = 0f;
    }

    [Serializable]
    public class WeaponScriptTuple
    {

        [XmlAttribute]
        public string WeaponScriptName { get; set; } = "invalid";

        [XmlArray("WeaponScriptArguments")]
        [XmlArrayItem("WeaponScriptArgument")]
        public List<string> WeaponScriptArguments { get; set; } = new List<string>();

        public string ShortName
        {
            get
            {
                if (string.IsNullOrEmpty(WeaponScriptName)) return "";
                int i = WeaponScriptName.LastIndexOf('.');
                return i >= 0 ? WeaponScriptName.Substring(i + 1) : WeaponScriptName;
            }
        }
    }

    [Serializable]
    public class WeaponParticlePreset
    {
        [XmlAttribute]
        public string ParticlePrefab { get; set; } = "invalid";

        [XmlAttribute]
        public bool IsUniqueSingleCopy { get; set; } = false;
    }
}
