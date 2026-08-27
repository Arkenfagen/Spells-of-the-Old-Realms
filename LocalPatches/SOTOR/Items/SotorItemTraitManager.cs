using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SOTOR.AbilitySystem;
using SOTOR.AbilitySystem.StatusEffects;
using TaleWorlds.ModuleManager;

namespace SOTOR.Items
{

    public static class SotorItemTraitManager
    {
        private const string TemplateFileName = "sotor_itemtraits.xml";

        private static List<SotorItemTrait> _traits = new List<SotorItemTrait>();
        private static Dictionary<string, SotorItemTrait> _byId =
            new Dictionary<string, SotorItemTrait>(StringComparer.Ordinal);

        public static IReadOnlyList<SotorItemTrait> AllTraits => _traits;

        public static SotorItemTrait GetTrait(string traitId)
        {
            if (string.IsNullOrWhiteSpace(traitId)) return null;
            return _byId.TryGetValue(traitId, out var t) ? t : null;
        }

        public static List<SotorItemTrait> GetTraits(IEnumerable<string> traitIds)
        {
            var list = new List<SotorItemTrait>();
            if (traitIds == null) return list;
            foreach (var id in traitIds)
            {
                var t = GetTrait(id);
                if (t != null) list.Add(t);
            }
            return list;
        }

        public static List<SotorItemTrait> CraftableTraits => _traits.Where(t => t.IsCraftable).ToList();

        public static void LoadTemplates()
        {
            var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
            var path = Path.Combine(modulePath, "ModuleData", "tor_custom_xmls", TemplateFileName);
            if (!File.Exists(path))
            {
                SotorLog.Warn($"Item-trait definitions not found at {path}");
                return;
            }

            var loaded = LoadFrom(path, out var problems);
            foreach (var p in problems) SotorLog.Warn("Item trait: " + p);

            foreach (var t in loaded)
            {
                if (!string.IsNullOrEmpty(t.ImbuedStatusEffectId) && t.ImbuedStatusEffectId != "none"
                    && StatusEffectManager.GetTemplate(t.ImbuedStatusEffectId) == null)
                {
                    SotorLog.Warn($"Item trait {t.ItemTraitStringId}: imbued status effect '{t.ImbuedStatusEffectId}' is not defined");
                }
            }

            _traits = loaded;
            RebuildCache();
            if (_traits.Count == 0)
            {

                SotorLog.Warn($"Item-trait file parsed to ZERO traits — the enchanting roster is empty. Check {path}");
            }
            SotorLog.Info($"Loaded {_traits.Count} item trait(s) ({CraftableTraits.Count} craftable) from {path}");
        }

        public static List<SotorItemTrait> LoadFrom(string path, out List<string> problems)
        {
            problems = new List<string>();
            List<SotorItemTrait> list;
            var serializer = new XmlSerializer(typeof(List<SotorItemTrait>), new XmlRootAttribute("ItemTraits"));
            using (var stream = File.OpenRead(path))
            {
                list = serializer.Deserialize(stream) as List<SotorItemTrait> ?? new List<SotorItemTrait>();
            }

            var kept = new List<SotorItemTrait>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var t in list)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.ItemTraitStringId)) { problems.Add("entry without ItemTraitStringId"); continue; }
                if (!seen.Add(t.ItemTraitStringId)) { problems.Add($"duplicate id {t.ItemTraitStringId}"); continue; }
                if (!Validate(t, out var reason)) { problems.Add(reason); continue; }
                kept.Add(t);
            }
            return kept;
        }

        private static readonly HashSet<string> KnownLores = new HashSet<string>(StringComparer.Ordinal)
        {
            SotorLores.LoreOfFire, SotorLores.LoreOfHeavens, SotorLores.LoreOfLight, SotorLores.LoreOfDeath,
            SotorLores.LoreOfNecromancy, SotorLores.LoreOfBeasts, SotorLores.LoreOfLife, SotorLores.LoreOfMetal,
            SotorLores.HighMagic, SotorLores.DarkMagic,
        };

        private static readonly HashSet<string> KnownSkills = new HashSet<string>(StringComparer.Ordinal)
        {
            "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing",
            "Riding", "Athletics", "Crafting", "Scouting", "Medicine", "Charm", "Spellcraft",
        };

        private static bool Validate(SotorItemTrait t, out string reason)
        {
            reason = "";
            if (t.IsCraftable)
            {
                if (t.HasLoreRequirement && !KnownLores.Contains(t.RequiredLore))
                {
                    reason = $"{t.ItemTraitStringId}: unknown RequiredLore '{t.RequiredLore}'";
                    return false;
                }
                if (t.HasLoreRequirement && t.HasSkillRequirement)
                {
                    reason = $"{t.ItemTraitStringId}: carries BOTH a lore and a skill gate";
                    return false;
                }
                if (t.HasSkillRequirement)
                {
                    foreach (var id in t.RequiredSkillIds)
                    {
                        if (!KnownSkills.Contains(id))
                        {
                            reason = $"{t.ItemTraitStringId}: unknown RequiredSkill '{id}'";
                            return false;
                        }
                    }
                    if (t.SkillThreshold <= 0)
                    {
                        reason = $"{t.ItemTraitStringId}: skill gate without a positive SkillThreshold";
                        return false;
                    }
                }
                if (t.ValidItemType == SotorItemTraitItemType.Invalid)
                {
                    reason = $"{t.ItemTraitStringId}: craftable trait without ValidItemType";
                    return false;
                }
            }

            if (t.StatsTuple != null && t.StatsTuple.StatType != SotorItemTraitStatType.Invalid)
            {
                if (!StatTypeFitsSlot(t, out reason)) return false;
            }
            return true;
        }

        private static bool StatTypeFitsSlot(SotorItemTrait t, out string reason)
        {
            reason = "";
            bool ok = true;
            var stat = t.StatsTuple.StatType;
            switch (t.ValidItemType)
            {
                case SotorItemTraitItemType.Weapon:
                case SotorItemTraitItemType.Ammo:
                case SotorItemTraitItemType.Shield:
                case SotorItemTraitItemType.Ranged:
                case SotorItemTraitItemType.Thrown:
                case SotorItemTraitItemType.Melee:
                    switch (stat)
                    {
                        case SotorItemTraitStatType.HealthMax:
                        case SotorItemTraitStatType.HealthRegen:
                        case SotorItemTraitStatType.WindsOfMagicMax:
                        case SotorItemTraitStatType.WindsOfMagicRegen:
                        case SotorItemTraitStatType.Skill:
                        case SotorItemTraitStatType.SpellRadius:
                        case SotorItemTraitStatType.MovementSpeed:
                            ok = false;
                            break;
                        case SotorItemTraitStatType.ShieldHealth:
                            ok = t.ValidItemType == SotorItemTraitItemType.Shield;
                            break;
                        case SotorItemTraitStatType.ShieldDamage:
                            ok = t.ValidItemType != SotorItemTraitItemType.Shield;
                            break;
                        case SotorItemTraitStatType.SwingSpeed:
                        case SotorItemTraitStatType.Cleave:
                            ok = t.ValidItemType == SotorItemTraitItemType.Melee;
                            break;
                        case SotorItemTraitStatType.MissileSpeed:
                        case SotorItemTraitStatType.MultiPenetration:
                        case SotorItemTraitStatType.ShieldPenetration:
                            ok = t.ValidItemType == SotorItemTraitItemType.Ranged
                              || t.ValidItemType == SotorItemTraitItemType.Thrown
                              || t.ValidItemType == SotorItemTraitItemType.Ammo;
                            break;
                        case SotorItemTraitStatType.ReloadSpeed:
                            ok = t.ValidItemType == SotorItemTraitItemType.Ranged;
                            break;
                    }
                    break;
                case SotorItemTraitItemType.Armor:

                    switch (stat)
                    {
                        case SotorItemTraitStatType.ShieldHealth:
                        case SotorItemTraitStatType.ArmorPenetration:
                        case SotorItemTraitStatType.MissileSpeed:
                        case SotorItemTraitStatType.SwingSpeed:
                        case SotorItemTraitStatType.ReloadSpeed:
                        case SotorItemTraitStatType.ShieldDamage:
                        case SotorItemTraitStatType.Cleave:
                        case SotorItemTraitStatType.MultiPenetration:
                            ok = false;
                            break;
                    }
                    break;
            }
            if (!ok)
            {
                reason = $"{t.ItemTraitStringId}: stat {t.StatsTuple.StatType} does not fit {t.ValidItemType}";
                return false;
            }
            return true;
        }

        private static void RebuildCache()
        {
            var d = new Dictionary<string, SotorItemTrait>(StringComparer.Ordinal);
            foreach (var t in _traits)
            {
                if (!d.ContainsKey(t.ItemTraitStringId)) d.Add(t.ItemTraitStringId, t);
            }
            _byId = d;
        }
    }
}
