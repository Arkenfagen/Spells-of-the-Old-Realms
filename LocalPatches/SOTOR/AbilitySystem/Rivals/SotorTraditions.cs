using System.Collections.Generic;
using SOTOR.AbilitySystem;

namespace SOTOR.AbilitySystem.Rivals
{

    public enum Trad
    {
        None = 0,
        Fire,
        Heavens,
        Light,
        Life,
        Beasts,
        Metal,
        Death,
        Necromancy,
        Dark,
        High,
    }

    public static class SotorTraditions
    {

        public static readonly Trad[] ClanTraditions =
        {
            Trad.Fire, Trad.Heavens, Trad.Light, Trad.Life,
            Trad.Beasts, Trad.Metal, Trad.Death, Trad.Necromancy,
        };

        public static readonly Trad[] MemberOnlyTraditions = { Trad.Dark, Trad.High };

        public static bool IsClanTradition(Trad t)
        {
            for (int i = 0; i < ClanTraditions.Length; i++)
            {
                if (ClanTraditions[i] == t) return true;
            }
            return false;
        }

        public static bool IsMemberOnly(Trad t) => t == Trad.Dark || t == Trad.High;

        public static string LoreIdFor(Trad t)
        {
            switch (t)
            {
                case Trad.Fire: return SotorLores.LoreOfFire;
                case Trad.Heavens: return SotorLores.LoreOfHeavens;
                case Trad.Light: return SotorLores.LoreOfLight;
                case Trad.Life: return SotorLores.LoreOfLife;
                case Trad.Beasts: return SotorLores.LoreOfBeasts;
                case Trad.Metal: return SotorLores.LoreOfMetal;
                case Trad.Death: return SotorLores.LoreOfDeath;
                case Trad.Necromancy: return SotorLores.LoreOfNecromancy;
                case Trad.Dark: return SotorLores.DarkMagic;
                case Trad.High: return SotorLores.HighMagic;
                default: return null;
            }
        }

        public static Trad TradForLore(string loreId)
        {
            if (loreId == SotorLores.LoreOfFire) return Trad.Fire;
            if (loreId == SotorLores.LoreOfHeavens) return Trad.Heavens;
            if (loreId == SotorLores.LoreOfLight) return Trad.Light;
            if (loreId == SotorLores.LoreOfLife) return Trad.Life;
            if (loreId == SotorLores.LoreOfBeasts) return Trad.Beasts;
            if (loreId == SotorLores.LoreOfMetal) return Trad.Metal;
            if (loreId == SotorLores.LoreOfDeath) return Trad.Death;
            if (loreId == SotorLores.LoreOfNecromancy) return Trad.Necromancy;
            if (loreId == SotorLores.DarkMagic) return Trad.Dark;
            if (loreId == SotorLores.HighMagic) return Trad.High;
            return Trad.None;
        }

        public static int Rarity(Trad t)
        {
            switch (t)
            {
                case Trad.None: return 0;
                case Trad.Dark:
                case Trad.High: return 3;
                default: return 1;
            }
        }

        public static string KeySuffix(Trad t)
        {
            switch (t)
            {
                case Trad.Fire: return "fire";
                case Trad.Heavens: return "heavens";
                case Trad.Light: return "light";
                case Trad.Life: return "life";
                case Trad.Beasts: return "beasts";
                case Trad.Metal: return "metal";
                case Trad.Death: return "death";
                case Trad.Necromancy: return "necromancy";
                case Trad.Dark: return "dark";
                case Trad.High: return "high";
                default: return null;
            }
        }

        public static string NameKey(Trad t)
        {
            var s = KeySuffix(t);
            return s == null ? null : "sotor_trad_name_" + s;
        }

        public static string DescriptionKey(Trad t)
        {
            var s = KeySuffix(t);
            return s == null ? null : "sotor_trad_desc_" + s;
        }

        public static string ObjectStringId(Trad t)
        {
            var s = KeySuffix(t);
            return s == null ? null : "sotor_trad_" + s;
        }

        public static Trad FromObjectStringId(string stringId)
        {
            if (string.IsNullOrEmpty(stringId)) return Trad.None;
            for (int i = 0; i < AllTraditions.Length; i++)
            {
                if (ObjectStringId(AllTraditions[i]) == stringId) return AllTraditions[i];
            }
            return Trad.None;
        }

        public static readonly Trad[] AllTraditions =
        {
            Trad.Fire, Trad.Heavens, Trad.Light, Trad.Life,
            Trad.Beasts, Trad.Metal, Trad.Death, Trad.Necromancy,
            Trad.Dark, Trad.High,
        };

        public const int LearnLoreStandingBase = 25;
        public const int LearnSpellStandingBase = 5;

        public static int LearnLoreStanding => SotorSettings.StandingLearnLore;
        public static int LearnSpellStanding => SotorSettings.StandingLearnSpell;
        public static int ExecuteCasterStanding => SotorSettings.StandingExecuteCaster;
        public static int FreeCasterStanding => SotorSettings.StandingFreeCaster;
        public static int AssistCasterStanding => SotorSettings.StandingAssistCaster;

        public static int LearnStandingDelta(Trad reader, Trad learned, int baseAmount)
        {
            if (reader == Trad.None || learned == Trad.None) return 0;
            int aff = Affinity(reader, learned);
            if (aff == 0) return 0;

            return (int)System.Math.Round(baseAmount * (aff / 2.0));
        }

        public static int LearnRelationForLord(int traditionDelta, int lordCasterLevel)
        {
            if (traditionDelta == 0) return 0;
            float factor = lordCasterLevel >= 5 ? 1.5f : (lordCasterLevel >= 3 ? 1.0f : 0.6f);
            int v = (int)System.Math.Round(traditionDelta * factor);

            if (v == 0) v = traditionDelta > 0 ? 1 : -1;
            return v;
        }

        public static int Notoriety(Trad t)
        {
            if (t == Trad.None) return 0;
            int n = 0;
            for (int i = 0; i < AllTraditions.Length; i++)
            {
                if (Affinity(AllTraditions[i], t) <= -2) n++;
            }
            return n;
        }

        private static readonly int[,] Grid =
        {

             {  2,  -2,   1,  -1,  -1,   0,  -1,  -2,  -2,   0 },
             { -2,   2,   1,   1,  -1,   0,   0,  -2,  -2,   1 },
             {  1,   1,   2,   1,  -1,   1,  -1,  -2,  -2,   2 },
             { -1,   1,   1,   2,   1,  -1,  -2,  -2,  -2,   1 },
             { -1,  -1,  -1,   1,   2,  -2,  -1,  -1,   0,  -1 },
             {  0,   0,   1,  -1,  -2,   2,   1,  -2,  -2,   1 },
             { -1,   0,  -1,  -2,  -1,   1,   2,  -2,  -2,  -1 },
             { -1,  -1,  -2,  -1,  -1,  -1,   1,   2,   1,  -1 },
             {  0,   0,   0,   0,   0,   0,   1,   1,   2,   0 },
             {  0,   0,   0,   0,   0,   0,   0,   0,  -1,   2 },
        };

        private static int GridIndex(Trad t)
        {
            switch (t)
            {
                case Trad.Fire: return 0;
                case Trad.Heavens: return 1;
                case Trad.Light: return 2;
                case Trad.Life: return 3;
                case Trad.Beasts: return 4;
                case Trad.Metal: return 5;
                case Trad.Death: return 6;
                case Trad.Necromancy: return 7;
                case Trad.Dark: return 8;
                case Trad.High: return 9;
                default: return -1;
            }
        }

        public static int Affinity(Trad reader, Trad subject)
        {
            if (reader == Trad.None || subject == Trad.None) return 0;

            int r = GridIndex(reader);
            int s = GridIndex(subject);
            if (r < 0 || s < 0) return 0;
            return Grid[r, s];
        }

        public static bool IsAsymmetricPair(Trad a, Trad b)
        {
            return Affinity(a, b) != Affinity(b, a);
        }

        public const uint SaltClanTradition = 0x5A170001u;
        public const uint SaltIsCaster = 0x5A170002u;
        public const uint SaltCasterLevel = 0x5A170003u;
        public const uint SaltMemberOnlyClan = 0x5A170004u;
        public const uint SaltMemberOnlyWhich = 0x5A170005u;
        public const uint SaltWandererIsCaster = 0x5A170006u;
        public const uint SaltWandererTradition = 0x5A170007u;
        public const uint SaltSpellcraft = 0x5A170008u;
        public const uint SaltArgumentPool = 0x5A170009u;

        public const uint SaltBloodline = 0x5A17000Au;

        public const uint SaltAlleleA = 0x5A17000Bu;
        public const uint SaltAlleleB = 0x5A17000Cu;
        public const uint SaltInheritance = 0x5A17000Du;

        private static readonly int[] SpellcraftByLevel = { 0, 30, 60, 110, 160, 230, 300 };
        public const int SpellcraftJitter = 40;
        public const int SpellcraftMin = 10;
        public const int SpellcraftMax = 300;

        public static int SpellcraftForLevel(int level, float roll01)
        {
            if (level < 1) level = 1;
            if (level >= SpellcraftByLevel.Length) level = SpellcraftByLevel.Length - 1;
            int baseValue = SpellcraftByLevel[level];

            int shift = (int)((roll01 * 2f - 1f) * SpellcraftJitter);
            int value = baseValue + shift;
            if (value < SpellcraftMin) value = SpellcraftMin;
            if (value > SpellcraftMax) value = SpellcraftMax;
            return value;
        }

        public const int MaxSpellTier = 4;

        public static int MaxSpellTierForLevel(int casterLevel)
        {
            if (casterLevel <= 1) return 2;
            if (casterLevel <= 3) return 3;
            return MaxSpellTier;
        }

        public static bool KnowsSpellTier(int casterLevel, int spellTier)
        {
            if (spellTier <= 0) return true;
            return spellTier <= MaxSpellTierForLevel(casterLevel);
        }

        public static Trad ClanTraditionFromRoll(float roll01)
        {
            int n = ClanTraditions.Length;
            int idx = (int)(roll01 * n);
            if (idx < 0) idx = 0;
            if (idx >= n) idx = n - 1;
            return ClanTraditions[idx];
        }

        public static bool IsCasterFromRoll(float roll01, float sharePercent)
        {
            return roll01 < sharePercent / 100f;
        }

        public const int TierOffset = -1;

        public static int CasterLevelFromScore(int ceiling, bool oldEnough, bool cleverEnough, int powerShift = 0)
        {

            int baseLevel = ceiling + TierOffset;

            int nudge = (oldEnough ? 1 : 0) + (cleverEnough ? 1 : 0) - 1;
            int level = baseLevel + nudge + powerShift;

            if (level < 1) level = 1;
            if (level > MaxCasterLevel) level = MaxCasterLevel;
            return level;
        }

        public const int MaxCasterLevel = 6;

        public static Trad MemberOnlyFromRoll(float roll01)
        {
            return roll01 < 0.6f ? Trad.Dark : Trad.High;
        }

        public const int RivalRelation = 25;
        public const int NemesisRelation = 55;
        public const int LordPairCap = NemesisRelation;

        public static int RelationForAffinity(int affinity)
        {
            switch (affinity)
            {
                case 2: return NemesisRelation;
                case 1: return RivalRelation;
                case -1: return -RivalRelation;
                case -2: return -NemesisRelation;
                default: return 0;
            }
        }

        public static int LordPairRelationDelta(Trad a, Trad b)
        {
            return LordPairRelationDelta(a, b, strongRelations: true);
        }

        public static int LordPairRelationDelta(Trad a, Trad b, bool strongRelations)
        {
            if (a == Trad.None || b == Trad.None) return 0;
            int ab = Affinity(a, b);
            int ba = Affinity(b, a);

            int avg = RoundHalfAwayFromZero((ab + ba) / 2.0);

            int delta = strongRelations ? RelationForAffinity(avg) : avg * 3;
            int cap = strongRelations ? LordPairCap : 20;
            if (delta > cap) delta = cap;
            if (delta < -cap) delta = -cap;
            return delta;
        }

        private static int RoundHalfAwayFromZero(double v)
        {
            return (int)(v < 0 ? System.Math.Ceiling(v - 0.5) : System.Math.Floor(v + 0.5));
        }
    }
}
