namespace SOTOR.AbilitySystem.Rivals
{

    public enum TeachOutcome
    {
        CanNegotiate,
        VetoWar,
        VetoDoctrine,
        NotTeachable,
        VetoStanding,
        VetoIgnorance,
        VetoRecentFailure,
        VetoCoerced,
    }

    public enum TeachDifficulty
    {
        VeryEasy = 0, Easy, EasyMedium, Medium, MediumHard, Hard, VeryHard, UltraHard, Impossible
    }

    public static class SotorTeachingLogic
    {

        public static bool HouseIgnoresLoremasters(Trad masterHouseTrad)
        {
            return masterHouseTrad == Trad.Necromancy || masterHouseTrad == Trad.Dark;
        }

        public static bool LoremasterExemptionApplies(bool playerMasteredHighMagic, Trad masterHouseTrad)
        {
            return playerMasteredHighMagic && !HouseIgnoresLoremasters(masterHouseTrad);
        }

        public static TeachOutcome Resolve(
            bool atWar,
            Trad masterHouseTrad,
            Trad playerMostHostileOwnedTrad,
            bool masterKnowsLore,
            int masterClanTier = 0,
            int playerClanTier = 0,
            int masterCasterLevel = 0,
            int playerCasterLevel = 0,
            bool playerMasteredHighMagic = false)
        {

            bool exempt = LoremasterExemptionApplies(playerMasteredHighMagic, masterHouseTrad);

            if (atWar) return TeachOutcome.VetoWar;

            if (!exempt
                && playerMostHostileOwnedTrad != Trad.None
                && SotorTraditions.Affinity(masterHouseTrad, playerMostHostileOwnedTrad) <= -2)
            {
                return TeachOutcome.VetoDoctrine;
            }

            if (!exempt && IsBeneathHisNotice(masterClanTier, playerClanTier))
            {
                return TeachOutcome.VetoStanding;
            }

            if (!exempt && IsTooIgnorantToTeach(masterCasterLevel, playerCasterLevel))
            {
                return TeachOutcome.VetoIgnorance;
            }

            if (!masterKnowsLore) return TeachOutcome.NotTeachable;

            return TeachOutcome.CanNegotiate;
        }

        public const int AffinityRelationModifier = 25;

        public static int AffinityRelationShift(Trad masterHouseTrad, Trad worstPlayerLore)
        {
            if (worstPlayerLore == Trad.None) return 0;
            int aff = SotorTraditions.Affinity(masterHouseTrad, worstPlayerLore);
            if (aff >= 1) return AffinityRelationModifier;
            if (aff <= -1) return -AffinityRelationModifier;
            return 0;
        }

        public static Trad OffendingTradition(Trad masterHouseTrad, System.Collections.Generic.IEnumerable<Trad> playerLores)
        {
            if (playerLores == null) return Trad.None;

            Trad worst = Trad.None;
            int worstAffinity = 0;
            int worstNotoriety = -1;
            foreach (var lore in playerLores)
            {
                if (lore == Trad.None) continue;
                int aff = SotorTraditions.Affinity(masterHouseTrad, lore);
                int noto = SotorTraditions.Notoriety(lore);

                if (worst == Trad.None || Beats(aff, noto, lore, worstAffinity, worstNotoriety, worst))
                {
                    worst = lore;
                    worstAffinity = aff;
                    worstNotoriety = noto;
                }
            }
            return worst;
        }

        private static bool Beats(int a, int n, Trad t, int ia, int inn, Trad it)
        {
            if (a != ia) return a < ia;
            if (n != inn) return n > inn;
            return string.CompareOrdinal(t.ToString(), it.ToString()) < 0;
        }

        public const int BeneathNoticeTierGap = 3;

        public static bool IsBeneathHisNotice(int masterClanTier, int playerClanTier)
        {
            return masterClanTier - playerClanTier >= BeneathNoticeTierGap;
        }

        public const int BeneathNoticeCasterGap = 2;

        public static bool IsTooIgnorantToTeach(int masterCasterLevel, int playerCasterLevel)
        {
            return masterCasterLevel - playerCasterLevel >= BeneathNoticeCasterGap;
        }

        public static bool IsDangerousSecret(Trad masterHouseTrad, Trad lore)
        {
            if (lore == Trad.None) return false;
            if (lore == masterHouseTrad) return false;
            return SotorTraditions.Affinity(masterHouseTrad, lore) < 0;
        }

        public static bool PassesDispositionGate(Trad masterHouseTrad, Trad lore, int relation)
        {
            if (!IsDangerousSecret(masterHouseTrad, lore)) return true;

            int needed = SotorTraditions.Affinity(masterHouseTrad, lore) <= -2 ? 60 : 30;
            return relation >= needed;
        }

        public static TeachDifficulty BaseDifficulty(int clanTier)
        {
            if (clanTier <= 1) return TeachDifficulty.Easy;
            if (clanTier <= 3) return TeachDifficulty.Medium;
            if (clanTier == 4) return TeachDifficulty.MediumHard;
            if (clanTier == 5) return TeachDifficulty.Hard;
            return TeachDifficulty.VeryHard;
        }

        public static TeachDifficulty AdjustDifficulty(TeachDifficulty baseDiff, Trad masterHouseTrad, Trad lore)
        {
            int aff = SotorTraditions.Affinity(masterHouseTrad, lore);
            int shifted = (int)baseDiff;
            if (aff >= 2) shifted -= 1;
            else if (aff <= -1) shifted += 1;
            if (shifted < (int)TeachDifficulty.VeryEasy) shifted = (int)TeachDifficulty.VeryEasy;
            if (shifted > (int)TeachDifficulty.Impossible) shifted = (int)TeachDifficulty.Impossible;
            return (TeachDifficulty)shifted;
        }

        public static int StageCount(Trad lore)
        {
            if (lore == Trad.Dark || lore == Trad.High) return 3;
            return 2;
        }

        public const int MinSuccesses = 1;

        public const int MaxSuccesses = 3;

        public const int FriendlyRelation = 30;

        public const int RespectedStanding = 30;
        public const int DespisedStanding = -30;

        public const int ImpressiveSpellcraftGap = 40;
        public const int OutclassedSpellcraftGap = -60;

        public const int BigTierGap = 3;

        public static int SuccessesRequired(
            Trad lore,
            int masterClanTier,
            int playerClanTier,
            int relation,
            int traditionStanding,
            int playerSpellcraft,
            int masterSpellcraft)
        {
            int n = 2;

            if (lore == Trad.Dark || lore == Trad.High) n += 1;
            if (masterClanTier >= 5) n += 1;
            if (masterClanTier - playerClanTier >= BigTierGap) n += 1;

            if (relation >= FriendlyRelation) n -= 1;

            if (traditionStanding >= RespectedStanding) n -= 1;
            else if (traditionStanding <= DespisedStanding) n += 1;

            int gap = playerSpellcraft - masterSpellcraft;
            if (gap >= ImpressiveSpellcraftGap) n -= 1;
            else if (gap <= OutclassedSpellcraftGap) n += 1;

            if (n < MinSuccesses) n = MinSuccesses;
            if (n > MaxSuccesses) n = MaxSuccesses;
            return n;
        }
    }
}
