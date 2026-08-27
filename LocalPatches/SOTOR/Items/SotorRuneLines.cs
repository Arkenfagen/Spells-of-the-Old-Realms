using System;
using System.Collections.Generic;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace SOTOR.Items
{

    public static class SotorRuneLines
    {

        public struct LineInfo
        {
            public bool FromVillages;
            public bool FromClan;
            public float BackingHearth;

            public int ClanBest;
        }

        public static IEnumerable<string> LinesForVillageType(string villageTypeId)
        {
            switch (villageTypeId)
            {
                case "iron_mine":
                case "silver_mine":
                    yield return "Crafting";
                    break;
                case "lumberjack":
                    yield return "Crafting";
                    yield return "Bow";
                    break;
                case "trapper":
                    yield return "Bow";
                    break;
                case "vineyard":
                case "olive_trees":
                case "flax_plant":
                    yield return "Medicine";
                    break;
                default:
                    if (villageTypeId != null && villageTypeId.EndsWith("_horse_ranch", StringComparison.Ordinal))
                    {
                        yield return "Riding";
                    }
                    break;
            }
        }

        public static Dictionary<string, int> MinThresholds(IEnumerable<KeyValuePair<string, int>> runeSkillRows)
        {
            var min = new Dictionary<string, int>(StringComparer.Ordinal);
            if (runeSkillRows == null) return min;
            foreach (var row in runeSkillRows)
            {
                if (string.IsNullOrEmpty(row.Key) || row.Value <= 0) continue;
                if (!min.TryGetValue(row.Key, out int cur) || row.Value < cur) min[row.Key] = row.Value;
            }
            return min;
        }

        public static Dictionary<string, LineInfo> Resolve(
            IEnumerable<KeyValuePair<string, float>> boundVillages,
            IReadOnlyDictionary<string, int> clanBestBySkill,
            IReadOnlyDictionary<string, int> minThresholdByLine)
        {
            var result = new Dictionary<string, LineInfo>(StringComparer.Ordinal);
            if (minThresholdByLine == null || minThresholdByLine.Count == 0) return result;

            float totalHearth = 0f;
            var villageHearthByLine = new Dictionary<string, float>(StringComparer.Ordinal);
            if (boundVillages != null)
            {
                foreach (var v in boundVillages)
                {
                    totalHearth += v.Value;
                    foreach (var line in LinesForVillageType(v.Key))
                    {

                        if (!minThresholdByLine.ContainsKey(line)) continue;
                        villageHearthByLine.TryGetValue(line, out float h);
                        villageHearthByLine[line] = h + v.Value;
                    }
                }
            }

            foreach (var pair in villageHearthByLine)
            {
                result[pair.Key] = new LineInfo { FromVillages = true, BackingHearth = pair.Value };
            }

            if (clanBestBySkill != null)
            {
                foreach (var line in minThresholdByLine)
                {
                    if (!clanBestBySkill.TryGetValue(line.Key, out int best) || best < line.Value) continue;
                    result.TryGetValue(line.Key, out var info);
                    info.FromClan = true;
                    info.ClanBest = best;

                    if (!info.FromVillages) info.BackingHearth = totalHearth * 0.5f;
                    result[line.Key] = info;
                }
            }

            return result;
        }

        public static Dictionary<string, int> RosterMinThresholds()
        {
            var rows = new List<KeyValuePair<string, int>>();
            foreach (var t in SotorItemTraitManager.CraftableTraits)
            {
                if (!t.HasSkillRequirement) continue;
                foreach (var id in t.RequiredSkillIds)
                {
                    rows.Add(new KeyValuePair<string, int>(id, t.SkillThreshold));
                }
            }
            return MinThresholds(rows);
        }

        public static Dictionary<string, int> ClanBestSkills(Clan clan, IReadOnlyDictionary<string, int> lines)
        {
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            if (clan == null || lines == null) return best;
            foreach (var line in lines)
            {
                var skill = SotorSkillGate.Resolve(line.Key);
                if (skill == null) continue;
                int top = 0;
                foreach (var hero in clan.Heroes)
                {
                    if (hero == null || !hero.IsAlive) continue;
                    int v = hero.GetSkillValue(skill);
                    if (v > top) top = v;
                }
                if (top > 0) best[line.Key] = top;
            }
            return best;
        }

        public static Dictionary<string, LineInfo> For(Town town)
        {
            var thresholds = RosterMinThresholds();
            if (town == null) return new Dictionary<string, LineInfo>(StringComparer.Ordinal);

            var villages = new List<KeyValuePair<string, float>>();
            var bound = town.Villages;
            if (bound != null)
            {
                foreach (var v in bound)
                {
                    if (v == null) continue;
                    villages.Add(new KeyValuePair<string, float>(v.VillageType?.StringId ?? "", v.Hearth));
                }
            }

            return Resolve(villages, ClanBestSkills(town.OwnerClan, thresholds), thresholds);
        }

        public static void LogWorldCoverage()
        {
            try
            {
                var thresholds = RosterMinThresholds();
                if (thresholds.Count == 0)
                {
                    SotorLog.Info("Rune lines: no skill-gated runes in the roster; availability layer idle.");
                    return;
                }

                var villageTowns = new Dictionary<string, int>(StringComparer.Ordinal);
                int towns = 0, noSpecialist = 0;
                foreach (var town in Town.AllTowns)
                {
                    if (town == null || town.IsCastle) continue;
                    towns++;
                    bool any = false;
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    var bound = town.Villages;
                    if (bound != null)
                    {
                        foreach (var v in bound)
                        {
                            foreach (var line in LinesForVillageType(v?.VillageType?.StringId))
                            {
                                if (thresholds.ContainsKey(line) && seen.Add(line)) any = true;
                            }
                        }
                    }
                    foreach (var line in seen)
                    {
                        villageTowns.TryGetValue(line, out int n);
                        villageTowns[line] = n + 1;
                    }
                    if (!any) noSpecialist++;
                }

                var clanCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                int clans = 0;
                foreach (var clan in Clan.All)
                {
                    if (clan == null || clan.IsEliminated || clan.IsBanditFaction || clan.Heroes.Count == 0) continue;
                    clans++;
                    var best = ClanBestSkills(clan, thresholds);
                    foreach (var line in thresholds)
                    {
                        if (best.TryGetValue(line.Key, out int b) && b >= line.Value)
                        {
                            clanCounts.TryGetValue(line.Key, out int n);
                            clanCounts[line.Key] = n + 1;
                        }
                    }
                }

                var parts = new List<string>();
                foreach (var line in thresholds)
                {
                    villageTowns.TryGetValue(line.Key, out int vt);
                    clanCounts.TryGetValue(line.Key, out int cc);
                    parts.Add($"{line.Key}={vt}t/{cc}c");
                }
                parts.Sort(StringComparer.Ordinal);
                SotorLog.Info($"Rune lines ({towns} towns, {noSpecialist} without a specialist line; "
                              + $"{clans} clans; t=towns via villages, c=clans clearing the cheapest rune): "
                              + string.Join(" ", parts));

                var lordCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var casterCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                int lords = 0;
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || !hero.IsLord || hero.Clan == null || hero.Clan.IsBanditFaction) continue;
                    lords++;
                    bool caster = hero.IsAbilityUser();
                    foreach (var line in thresholds)
                    {
                        var skill = SotorSkillGate.Resolve(line.Key);
                        if (skill == null || hero.GetSkillValue(skill) < line.Value) continue;
                        lordCounts.TryGetValue(line.Key, out int n);
                        lordCounts[line.Key] = n + 1;
                        if (caster)
                        {
                            casterCounts.TryGetValue(line.Key, out int c);
                            casterCounts[line.Key] = c + 1;
                        }
                    }
                }
                var teacherParts = new List<string>();
                foreach (var line in thresholds)
                {
                    lordCounts.TryGetValue(line.Key, out int all);
                    casterCounts.TryGetValue(line.Key, out int cast);
                    teacherParts.Add($"{line.Key}={all}/{cast}");
                }
                teacherParts.Sort(StringComparer.Ordinal);
                SotorLog.Info($"Rune teachers ({lords} lords; per line: lords clearing the cheapest "
                              + "rune / the casters among them - only casters teach): "
                              + string.Join(" ", teacherParts));
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"Rune line coverage diagnostic failed: {ex.Message}");
            }
        }
    }
}
