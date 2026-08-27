using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Items
{

    public static class SotorEnchantConsoleCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("enchant_item", "sotor")]
        public static string EnchantItem(List<string> args)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType)) return CampaignCheats.ErrorType;
            if (args.Count < 2) return "usage: sotor.enchant_item <traitId[,traitId2,...]> <itemId> [name...]  (adds an enchanted copy to the party roster)";

            var traitIds = new List<string>();
            foreach (var raw in args[0].Split(','))
            {
                string id = raw.Trim();
                if (id.Length == 0) continue;
                var t = SotorItemTraitManager.GetTrait(id);
                if (t == null) return $"unknown trait '{id}' (see sotor.list_traits)";
                if (!traitIds.Contains(t.ItemTraitStringId)) traitIds.Add(t.ItemTraitStringId);
            }
            if (traitIds.Count == 0) return "no traits given";

            var original = MBObjectManager.Instance.GetObject<ItemObject>(args[1]);
            if (original == null) return $"unknown item '{args[1]}'";
            foreach (var id in traitIds)
            {
                var t = SotorItemTraitManager.GetTrait(id);
                if (!t.IsValidForItem(original)) return $"trait {t.ItemTraitStringId} ({t.ValidItemType}) does not fit item type {original.ItemType}";
            }

            string name = args.Count > 2 ? string.Join(" ", args.Skip(2)) : null;
            var item = SotorEnchantmentHelper.CreateEnchantedItem(original, traitIds, name, playerCrafted: true);
            if (item == null) return "enchant failed (see log)";
            MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);
            return $"created {item.StringId} '{item.Name}' with {string.Join(", ", traitIds)}; added to party roster";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("list_traits", "sotor")]
        public static string ListTraits(List<string> args)
        {
            var traits = SotorItemTraitManager.CraftableTraits;
            string filter = args.FirstOrDefault();
            if (!string.IsNullOrEmpty(filter))
                traits = traits.Where(t => t.ItemTraitStringId.Contains(filter)
                                        || (t.RequiredLore ?? "").Contains(filter)
                                        || (t.RequiredSkill ?? "").Contains(filter)).ToList();
            var lines = traits.Select(t =>
                $"{t.ItemTraitStringId}  [{(t.HasSkillRequirement ? t.RequiredSkill + " " + t.SkillThreshold : t.HasLoreRequirement ? t.RequiredLore + " " + t.LearnThreshold : "common " + t.LearnThreshold)} {t.ValidItemType}]");
            return $"{traits.Count} trait(s):\n" + string.Join("\n", lines);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("learn_blueprint", "sotor")]
        public static string LearnBlueprint(List<string> args)
        {
            if (!CampaignCheats.CheckCheatUsage(ref CampaignCheats.ErrorType)) return CampaignCheats.ErrorType;
            if (args.Count < 1) return "usage: sotor.learn_blueprint <traitId|all>";
            var info = Extensions.HeroExtensions.GetExtendedInfo(Hero.MainHero);
            if (info == null) return "no extended info for the main hero";
            if (args[0] == "all")
            {
                foreach (var t in SotorItemTraitManager.CraftableTraits) info.AddBlueprint(t.ItemTraitStringId);
                return $"learned all {SotorItemTraitManager.CraftableTraits.Count} blueprints";
            }
            var trait = SotorItemTraitManager.GetTrait(args[0]);
            if (trait == null) return $"unknown trait '{args[0]}'";
            info.AddBlueprint(trait.ItemTraitStringId);
            return $"learned blueprint {trait.ItemTraitStringId}";
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("list_enchanted", "sotor")]
        public static string ListEnchanted(List<string> args)
        {
            var behavior = CampaignBehaviors.SotorEnchantingBehavior.Instance;
            if (behavior == null) return "no campaign";
            var lines = behavior.EnchantedItems.Select(kv =>
                $"{kv.Key.StringId}  '{kv.Value.NewItemName}'  from {kv.Value.OriginalItemStringId}  [{string.Join(",", kv.Value.ItemTraits ?? new List<string>())}]{(kv.Value.IsPlayerCrafted ? " (player)" : "")}");
            return $"{behavior.EnchantedItems.Count} enchanted item(s):\n" + string.Join("\n", lines);
        }
    }
}
