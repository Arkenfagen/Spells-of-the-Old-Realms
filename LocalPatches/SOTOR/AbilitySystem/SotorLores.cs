using System.Collections.Generic;

namespace SOTOR.AbilitySystem
{

    public static class SotorLores
    {
        public const string MinorMagic = "MinorMagic";
        public const string LoreOfFire = "LoreOfFire";

        public const string LoreOfHeavens = "LoreOfHeavens";

        public const string LoreOfLight = "LoreOfLight";

        public const string LoreOfDeath = "LoreOfDeath";

        public const string LoreOfNecromancy = "LoreOfNecromancy";

        public const string LoreOfBeasts = "LoreOfBeasts";
        public const string LoreOfLife = "LoreOfLife";
        public const string LoreOfMetal = "LoreOfMetal";

        public const string HighMagic = "HighMagic";
        public const string DarkMagic = "DarkMagic";

        public static readonly string[] ArchmageUnlockableLores = { HighMagic, DarkMagic };

        public static readonly string[] DefaultOwnedLores = { MinorMagic, LoreOfFire };

        public static readonly Dictionary<string, int> Prices = new Dictionary<string, int>
        {
            [MinorMagic] = 10000,
            [LoreOfFire] = 100000,
            [LoreOfHeavens] = 100000,
            [LoreOfLight] = 100000,
            [LoreOfDeath] = 100000,
            [LoreOfNecromancy] = 150000,
            [LoreOfBeasts] = 100000,
            [LoreOfLife] = 100000,
            [LoreOfMetal] = 100000,
            [HighMagic] = 200000,
            [DarkMagic] = 200000,
        };

        public static readonly Dictionary<string, SpellCastingLevel> RequiredCasterLevel = new Dictionary<string, SpellCastingLevel>
        {
            [LoreOfNecromancy] = SpellCastingLevel.Adept,
            [HighMagic] = SpellCastingLevel.Archmage,
            [DarkMagic] = SpellCastingLevel.Archmage,
        };

        public static SpellCastingLevel GetRequiredCasterLevel(string loreId) =>
            RequiredCasterLevel.TryGetValue(loreId, out var lvl) ? lvl : SpellCastingLevel.None;

        public static readonly string[] AllShownLores = { MinorMagic, LoreOfLight, LoreOfLife, LoreOfHeavens, LoreOfBeasts, LoreOfFire, LoreOfMetal, LoreOfDeath, LoreOfNecromancy, DarkMagic, HighMagic };

        public static readonly System.Collections.Generic.HashSet<string> RightSideLores =
            new System.Collections.Generic.HashSet<string> { LoreOfNecromancy, HighMagic, DarkMagic };

        public static bool IsRightSideLore(string loreId) => RightSideLores.Contains(loreId);

        public static int GetPrice(string loreId) => SotorPriceTable.GetLoreUnlockCost(loreId);

        public struct LoreDisplay
        {
            public string LoreId;
            public string Title;
            public string SymbolSprite;
        }

        public static readonly Dictionary<string, LoreDisplay> Display = new Dictionary<string, LoreDisplay>
        {
            [MinorMagic] = new LoreDisplay { LoreId = MinorMagic, Title = "Minor Magic", SymbolSprite = "minormagic_symbol" },
            [LoreOfFire] = new LoreDisplay { LoreId = LoreOfFire, Title = "Lore of Fire", SymbolSprite = "firemagic_symbol" },
            [LoreOfHeavens] = new LoreDisplay { LoreId = LoreOfHeavens, Title = "Lore of Heavens", SymbolSprite = "celestial_symbol" },
            [LoreOfLight] = new LoreDisplay { LoreId = LoreOfLight, Title = "Lore of Light", SymbolSprite = "lightmagic_symbol" },
            [LoreOfDeath] = new LoreDisplay { LoreId = LoreOfDeath, Title = "Lore of Death", SymbolSprite = "deathmagic_symbol" },
            [LoreOfNecromancy] = new LoreDisplay { LoreId = LoreOfNecromancy, Title = "Lore of Necromancy", SymbolSprite = "necromancy_symbol" },
            [LoreOfBeasts] = new LoreDisplay { LoreId = LoreOfBeasts, Title = "Lore of Beasts", SymbolSprite = "beastmagic_symbol" },
            [LoreOfLife] = new LoreDisplay { LoreId = LoreOfLife, Title = "Lore of Life", SymbolSprite = "lifemagic_symbol" },
            [LoreOfMetal] = new LoreDisplay { LoreId = LoreOfMetal, Title = "Lore of Metal", SymbolSprite = "metalmagic_symbol" },

            [HighMagic] = new LoreDisplay { LoreId = HighMagic, Title = "High Magic", SymbolSprite = "highmagic_symbol" },
            [DarkMagic] = new LoreDisplay { LoreId = DarkMagic, Title = "Dark Magic", SymbolSprite = "darkmagic_symbol" },
        };
    }
}
