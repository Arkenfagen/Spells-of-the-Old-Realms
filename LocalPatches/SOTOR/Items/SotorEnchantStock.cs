using System.Collections.Generic;

namespace SOTOR.Items
{

    public static class SotorEnchantStock
    {

        public const int Common = 0;
        public const int Uncommon = 1;
        public const int Rare = 2;
        public const int VeryRare = 3;

        public struct IngredientEntry
        {
            public SotorIngredientType Type;
            public int Rarity;

            public int LoreUse;

            public bool IsLoreStaple;
        }

        public static int RarityOf(SotorIngredientType type)
        {
            switch (type)
            {
                case SotorIngredientType.DragonBlood: return VeryRare;
                case SotorIngredientType.WarpstoneDust: return Rare;
                case SotorIngredientType.AmberCrystal: return Uncommon;
                default: return Common;
            }
        }

        private static int CapFor(int rarity, float prosperity, IngredientEntry e)
        {
            int cap;
            switch (rarity)
            {
                case VeryRare: cap = 2; break;
                case Rare: cap = 3; break;
                case Uncommon: cap = 5; break;
                default: cap = 8; break;
            }
            if (prosperity >= SotorBookShelf.RichProsperity) cap += 2;
            else if (prosperity >= SotorBookShelf.MidProsperity) cap += 1;

            if (e.IsLoreStaple) cap += 2;
            else if (e.LoreUse > 0) cap += 1;
            return cap;
        }

        private static bool Carries(int rarity, IngredientEntry e, float presenceRoll)
        {
            switch (rarity)
            {
                case VeryRare: return e.LoreUse > 0 && presenceRoll < 0.5f;
                case Rare: return e.LoreUse > 0 ? presenceRoll < 0.8f : presenceRoll < 0.25f;
                case Uncommon: return presenceRoll >= 0.2f || e.LoreUse > 0;
                default: return true;
            }
        }

        public static Dictionary<SotorIngredientType, int> IngredientStockFor(
            string seedText, string townId, int weekIndex, float prosperity,
            List<IngredientEntry> entries)
        {
            var stock = new Dictionary<SotorIngredientType, int>();
            if (entries == null) return stock;

            foreach (var e in entries)
            {
                int rarity = e.Rarity;
                string key = seedText + "|reagent|" + townId + "|" + weekIndex + "|" + (int)e.Type;
                if (!Carries(rarity, e, SotorBookShelf.Roll(key + "|present"))) continue;

                int cap = CapFor(rarity, prosperity, e);
                int count = 1 + (int)(SotorBookShelf.Roll(key + "|count") * cap);
                if (count > cap) count = cap;
                if (count > 0) stock[e.Type] = count;
            }
            return stock;
        }
    }
}
