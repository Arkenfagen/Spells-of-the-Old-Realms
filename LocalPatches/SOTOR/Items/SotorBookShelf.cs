using System;
using System.Collections.Generic;
using System.Linq;
using SOTOR.AbilitySystem.Rivals;

namespace SOTOR.Items
{

    public static class SotorBookShelf
    {
        public const float MidProsperity = 3000f;
        public const float RichProsperity = 6000f;

        public struct Entry
        {
            public string TraitId;
            public string Lore;
            public int Threshold;
            public string Tradition;

            public string Skill;
        }

        public static int SlotCount(float prosperity)
        {
            if (prosperity >= RichProsperity) return 5;
            if (prosperity >= MidProsperity) return 4;
            return 3;
        }

        public static int TierCap(float prosperity)
        {
            if (prosperity >= RichProsperity) return 250;
            if (prosperity >= MidProsperity) return 175;
            return 100;
        }

        public static float Roll(string key)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in key)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return (hash & 0xFFFFFF) / (float)0x1000000;
            }
        }

        public static List<string> ShelfFor(string seedText, string townId, int weekIndex,
            float prosperity, string rulingLore, List<Entry> roster)
        {
            var shelf = new List<string>();
            if (roster == null || roster.Count == 0) return shelf;

            int slots = SlotCount(prosperity);
            int cap = TierCap(prosperity);
            bool rich = prosperity >= RichProsperity;
            string baseKey = seedText + "|shelf|" + townId + "|" + weekIndex + "|";

            var loreLine = roster.Where(e => e.Lore == rulingLore && e.Tradition != "Elf"
                                          && e.Threshold <= cap && !string.IsNullOrEmpty(rulingLore)).ToList();
            var loreElf = roster.Where(e => e.Lore == rulingLore && e.Tradition == "Elf"
                                         && e.Threshold <= cap && !string.IsNullOrEmpty(rulingLore)).ToList();
            var commons = roster.Where(e => string.IsNullOrEmpty(e.Lore) && string.IsNullOrEmpty(e.Skill)
                                         && e.Threshold <= cap).ToList();

            for (int slot = 0; slot < slots; slot++)
            {
                string slotKey = baseKey + slot;
                Entry? pick = null;

                if (rich && slot == slots - 1)
                {

                    if (Roll(slotKey + "|off") < 0.35f)
                    {
                        pick = PickUniform(roster, slotKey + "|offpick");
                    }
                }
                else
                {
                    float r = Roll(slotKey + "|kind");
                    if (string.IsNullOrEmpty(rulingLore))
                    {

                        if (r < 0.40f && commons.Count > 0) pick = PickUniform(commons, slotKey + "|c");
                    }
                    else if (r < 0.55f && loreLine.Count > 0)
                    {
                        pick = PickLowTierBiased(loreLine, slotKey + "|l");
                    }
                    else if (r < 0.80f && commons.Count > 0)
                    {
                        pick = PickUniform(commons, slotKey + "|c");
                    }
                    else if (r < 0.86f && loreElf.Count > 0)
                    {

                        pick = PickUniform(loreElf, slotKey + "|e");
                    }
                    else if (loreLine.Count > 0)
                    {
                        pick = PickLowTierBiased(loreLine, slotKey + "|f");
                    }
                }

                if (pick.HasValue && !shelf.Contains(pick.Value.TraitId))
                {
                    shelf.Add(pick.Value.TraitId);
                }
            }
            return shelf;
        }

        private static Entry? PickUniform(List<Entry> pool, string key)
        {
            if (pool.Count == 0) return null;
            return pool[(int)(Roll(key) * pool.Count) % pool.Count];
        }

        private static Entry? PickLowTierBiased(List<Entry> pool, string key)
        {
            if (pool.Count == 0) return null;
            float total = pool.Sum(e => 1f / Math.Max(1, e.Threshold));
            float r = Roll(key) * total;
            foreach (var e in pool)
            {
                r -= 1f / Math.Max(1, e.Threshold);
                if (r <= 0f) return e;
            }
            return pool[pool.Count - 1];
        }

        public static List<Entry> LiveRoster()
        {
            return SotorItemTraitManager.CraftableTraits
                .Select(t => new Entry
                {
                    TraitId = t.ItemTraitStringId,
                    Lore = t.RequiredLore ?? "",
                    Threshold = t.LearnThreshold,
                    Tradition = t.Tradition ?? "",
                    Skill = t.RequiredSkill ?? "",
                })
                .ToList();
        }
    }
}
