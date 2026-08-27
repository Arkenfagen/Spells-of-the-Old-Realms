using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Settlements;

namespace SOTOR.Items
{

    public static class SotorRuneShelf
    {

        public struct RuneEntry
        {
            public string TraitId;
            public List<string> Skills;
            public int Threshold;
        }

        public struct LineState
        {
            public bool FromVillages;
            public float BackingHearth;
            public int ClanBest;
        }

        public const float MedianBackingHearth = 333f;

        public static float BaseChance(int threshold)
        {
            if (threshold <= 60) return 0.45f;
            if (threshold <= 110) return 0.28f;
            if (threshold <= 160) return 0.15f;
            return 0.06f;
        }

        public static float HearthFactor(float backingHearth)
        {
            float f = backingHearth / MedianBackingHearth;
            return f < 0.5f ? 0.5f : f > 2f ? 2f : f;
        }

        public static float ProsperityFactor(float prosperity)
        {
            if (prosperity >= SotorBookShelf.RichProsperity) return 1.30f;
            if (prosperity >= SotorBookShelf.MidProsperity) return 1.15f;
            return 1f;
        }

        public static float ClanFactor(int clanBest, int threshold)
        {
            float f = 1f + (clanBest - threshold) / 200f;
            return f < 0.85f ? 0.85f : f > 1.5f ? 1.5f : f;
        }

        public static float ChanceFor(RuneEntry rune, IReadOnlyDictionary<string, LineState> lines,
            float prosperity, out bool villageBacked)
        {
            villageBacked = false;
            if (rune.Skills == null || lines == null) return 0f;

            float best = 0f;
            foreach (var skill in rune.Skills)
            {
                if (skill == null || !lines.TryGetValue(skill, out var line)) continue;
                float chance = BaseChance(rune.Threshold)
                               * HearthFactor(line.BackingHearth)
                               * ProsperityFactor(prosperity)
                               * ClanFactor(line.ClanBest, rune.Threshold);
                if (chance > best)
                {
                    best = chance;
                    villageBacked = line.FromVillages;
                }
            }
            return best;
        }

        public static List<string> ShelfFor(string seedText, string townId, int weekIndex,
            float prosperity, IReadOnlyDictionary<string, LineState> lines, List<RuneEntry> roster)
        {
            var shelf = new List<string>();
            if (roster == null || roster.Count == 0 || lines == null || lines.Count == 0) return shelf;

            var passes = new List<(string traitId, bool specialist, float margin)>();
            foreach (var rune in roster)
            {
                if (string.IsNullOrEmpty(rune.TraitId)) continue;
                float chance = ChanceFor(rune, lines, prosperity, out bool villageBacked);
                if (chance <= 0f) continue;

                float roll = SotorBookShelf.Roll(seedText + "|runebook|" + townId + "|" + weekIndex + "|" + rune.TraitId);
                if (roll >= chance) continue;

                passes.Add((rune.TraitId, villageBacked, roll / chance));
            }

            passes.Sort((a, b) =>
            {
                if (a.specialist != b.specialist) return a.specialist ? -1 : 1;
                int byMargin = a.margin.CompareTo(b.margin);
                return byMargin != 0 ? byMargin : string.CompareOrdinal(a.traitId, b.traitId);
            });

            int slots = SotorBookShelf.SlotCount(prosperity);
            for (int i = 0; i < passes.Count && shelf.Count < slots; i++)
            {
                if (!shelf.Contains(passes[i].traitId)) shelf.Add(passes[i].traitId);
            }
            return shelf;
        }

        public static List<RuneEntry> RosterEntries()
        {
            var roster = new List<RuneEntry>();
            foreach (var t in SotorItemTraitManager.CraftableTraits)
            {
                if (!t.HasSkillRequirement) continue;
                roster.Add(new RuneEntry
                {
                    TraitId = t.ItemTraitStringId,
                    Skills = new List<string>(t.RequiredSkillIds),
                    Threshold = t.SkillThreshold,
                });
            }
            return roster;
        }

        public static Dictionary<string, LineState> LinesFor(Town town)
        {
            var lines = new Dictionary<string, LineState>(StringComparer.Ordinal);
            foreach (var pair in SotorRuneLines.For(town))
            {
                lines[pair.Key] = new LineState
                {
                    FromVillages = pair.Value.FromVillages,
                    BackingHearth = pair.Value.BackingHearth,
                    ClanBest = pair.Value.ClanBest,
                };
            }
            return lines;
        }
    }
}
