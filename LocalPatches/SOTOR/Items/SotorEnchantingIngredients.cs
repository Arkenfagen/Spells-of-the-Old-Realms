using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace SOTOR.Items
{

    public static class SotorEnchantingIngredients
    {
        private static readonly Dictionary<SotorIngredientType, string> Ids =
            new Dictionary<SotorIngredientType, string>
            {
                [SotorIngredientType.ArcaneScroll] = "sotor_tradegood_arcane_scroll",
                [SotorIngredientType.BlessedWater] = "sotor_tradegood_blessed_water",
                [SotorIngredientType.DragonBlood] = "sotor_tradegood_dragonblood",
                [SotorIngredientType.AmberCrystal] = "sotor_tradegood_amber_crystal",
                [SotorIngredientType.WarpstoneDust] = "sotor_tradegood_warpstone_dust",
                [SotorIngredientType.GemStone] = "sotor_tradegood_gemstone",
            };

        public static readonly SotorIngredientType[] AllTypes =
        {
            SotorIngredientType.ArcaneScroll, SotorIngredientType.BlessedWater,
            SotorIngredientType.DragonBlood, SotorIngredientType.AmberCrystal,
            SotorIngredientType.WarpstoneDust, SotorIngredientType.GemStone,
        };

        private static readonly Dictionary<SotorIngredientType, string> Sprites =
            new Dictionary<SotorIngredientType, string>
            {
                [SotorIngredientType.ArcaneScroll] = "sotor_reagent_scroll",
                [SotorIngredientType.BlessedWater] = "sotor_reagent_blessedwater",
                [SotorIngredientType.DragonBlood] = "sotor_reagent_dragonblood",
                [SotorIngredientType.AmberCrystal] = "sotor_reagent_amber",
                [SotorIngredientType.WarpstoneDust] = "sotor_reagent_warpstone",
                [SotorIngredientType.GemStone] = "sotor_reagent_gemstone",
            };

        public static string IconAsText(SotorIngredientType type)
        {
            return Sprites.TryGetValue(type, out var sprite)
                ? "<img src=\"" + sprite + "\" extend=\"7\">"
                : "";
        }

        public static ItemObject GetItem(SotorIngredientType type)
        {
            return Ids.TryGetValue(type, out var id)
                ? MBObjectManager.Instance?.GetObject<ItemObject>(id)
                : null;
        }

        public static bool IsIngredientItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            foreach (var pair in Ids) if (pair.Value == itemId) return true;
            return false;
        }

        public static int PurchasePrice(SotorIngredientType type)
        {
            var item = GetItem(type);
            return item != null ? item.Value * 2 : 0;
        }
    }
}
