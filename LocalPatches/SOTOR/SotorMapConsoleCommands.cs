using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SOTOR.AbilitySystem.Rivals;
using SOTOR.Items;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR
{

    public static class SotorMapConsoleCommands
    {

        private static readonly HashSet<TerrainType> WaterTerrain = new HashSet<TerrainType>
        {
            TerrainType.Water, TerrainType.CoastalSea, TerrainType.OpenSea, TerrainType.Lake,
            TerrainType.River, TerrainType.NonNavigableRiver, TerrainType.Fording,
            TerrainType.Bridge, TerrainType.UnderBridge, TerrainType.Beach,
        };

        private const int MaxExamples = 5;
        private const float ExampleSpacingSq = 80f * 80f;

        public static string TerrainHere(List<string> args)
        {
            if (Campaign.Current == null) return "sotor.terrain_here: load a campaign first.";
            try
            {
                var party = MobileParty.MainParty;
                if (party == null) return "sotor.terrain_here: no main party.";
                var pos = party.Position;
                var flat = pos.ToVec2();
                var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(pos.Face);

                float snow = Items.SotorIngredientDropModel.SnowAt(flat);
                bool snowy = snow > Items.SotorIngredientDropModel.SnowLyingThreshold;

                var lane = terrain == TerrainType.Plain
                    ? Items.SotorIngredientDropModel.PlainLane(flat)
                    : Items.SotorIngredientDropModel.TerrainLane(terrain);
                string yield = terrain == TerrainType.Plain ? " at HALF value (open ground)" : " at full value";

                string answer = snowy
                    ? $"Blessed Water - snow is lying here (snow={snow:0.00}), which overrides the ground"
                    : $"{lane}{yield}";

                float cell = Items.SotorIngredientDropModel.PlainRegionSize;
                string region = terrain == TerrainType.Plain
                    ? $"  plains region ({Math.Floor(flat.X / cell)},{Math.Floor(flat.Y / cell)})"
                    : "";

                return $"Standing on {terrain} at ({flat.X:0},{flat.Y:0}){region}\n"
                       + $"A battle here feeds: {answer}\n"
                       + $"snow={snow:0.00} (lies above {Items.SotorIngredientDropModel.SnowLyingThreshold})";
            }
            catch (Exception ex)
            {
                return "sotor.terrain_here failed: " + ex.Message;
            }
        }

        public static string MapTerrain(List<string> args)
        {
            if (Campaign.Current == null) return "sotor.map_terrain: load a campaign first.";
            var scene = Campaign.Current.MapSceneWrapper;
            if (scene == null) return "sotor.map_terrain: no map scene.";

            float step = 2f;
            if (args != null && args.Count > 0 && float.TryParse(args[0], out float given))
                step = MBMath.ClampFloat(given, 0.5f, 20f);

            try
            {
                scene.GetMapBorders(out Vec2 min, out Vec2 max, out float _);
                if (max.X - min.X <= 0f || max.Y - min.Y <= 0f)
                    return "sotor.map_terrain: the map reported no extent.";

                var counts = new Dictionary<TerrainType, int>();
                var examples = new Dictionary<TerrainType, List<Vec2>>();
                var islands = new HashSet<int>();
                var plainCells = new Dictionary<PlainCell, int>();
                int total = 0, offMap = 0;

                for (float y = min.Y; y <= max.Y; y += step)
                {
                    for (float x = min.X; x <= max.X; x += step)
                    {
                        var here = new Vec2(x, y);
                        var rec = scene.GetFaceIndex(new CampaignVec2(here, isOnLand: true));
                        if (!rec.IsValid())
                            rec = scene.GetFaceIndex(new CampaignVec2(here, isOnLand: false));
                        if (!rec.IsValid()) { offMap++; continue; }

                        TerrainType t = scene.GetFaceTerrainType(rec);
                        total++;
                        counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
                        if (!WaterTerrain.Contains(t)) islands.Add(rec.FaceIslandIndex);

                        if (t == TerrainType.Plain)
                        {
                            var cell = new PlainCell(
                                (int)Math.Floor(x / Items.SotorIngredientDropModel.PlainRegionSize),
                                (int)Math.Floor(y / Items.SotorIngredientDropModel.PlainRegionSize));
                            plainCells[cell] = plainCells.TryGetValue(cell, out int pc) ? pc + 1 : 1;
                        }

                        if (!examples.TryGetValue(t, out var spots))
                            examples[t] = spots = new List<Vec2>();
                        if (spots.Count < MaxExamples
                            && spots.All(p => p.DistanceSquared(here) > ExampleSpacingSq))
                            spots.Add(here);
                    }
                }

                var faceCounts = new Dictionary<TerrainType, int>();
                var faceSpots = new Dictionary<TerrainType, List<Vec2>>();
                int faceTotal = 0;
                try
                {
                    faceTotal = scene.GetNumberOfNavigationMeshFaces();
                    for (int i = 0; i < faceTotal; i++)
                    {
                        var rec = scene.GetFaceAtIndex(i);
                        if (!rec.IsValid()) continue;
                        TerrainType t = scene.GetFaceTerrainType(rec);
                        faceCounts[t] = faceCounts.TryGetValue(t, out int c) ? c + 1 : 1;

                        if (!faceSpots.TryGetValue(t, out var spots))
                            faceSpots[t] = spots = new List<Vec2>();
                        if (spots.Count < MaxExamples)
                        {
                            var centre = scene.GetNavigationMeshCenterPosition(i);
                            if (spots.All(p => p.DistanceSquared(centre) > ExampleSpacingSq))
                                spots.Add(centre);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SotorLog.Warn($"map_terrain: face walk failed ({ex.Message}); grid results still valid.");
                }

                foreach (var kv in faceSpots)
                    if (!examples.ContainsKey(kv.Key)) examples[kv.Key] = kv.Value;

                int landTotal = counts.Where(kv => !WaterTerrain.Contains(kv.Key)).Sum(kv => kv.Value);
                int waterTotal = total - landTotal;

                var sb = new StringBuilder();
                sb.AppendLine($"MAP TERRAIN SURVEY  ({total:N0} on-map samples at {step} units, "
                              + $"{offMap:N0} off-map skipped, map {max.X - min.X:0} x {max.Y - min.Y:0})");
                sb.AppendLine($"  land {landTotal:N0} samples, water {waterTotal:N0} samples, "
                              + $"{islands.Count} land island(s), {faceTotal:N0} navmesh faces");
                sb.AppendLine();
                sb.AppendLine("  terrain              samples   % of land    faces   % faces   reagent lane");

                var allTypes = counts.Keys.Concat(faceCounts.Keys).Distinct()
                                     .OrderByDescending(t => counts.TryGetValue(t, out int c) ? c : 0)
                                     .ThenByDescending(t => faceCounts.TryGetValue(t, out int f) ? f : 0);
                foreach (var t in allTypes)
                {
                    bool water = WaterTerrain.Contains(t);
                    int n = counts.TryGetValue(t, out int cc) ? cc : 0;
                    int fc = faceCounts.TryGetValue(t, out int ff) ? ff : 0;
                    int denom = water ? waterTotal : landTotal;
                    string share = denom > 0 ? (100f * n / denom).ToString("0.00") + "%" : "-";
                    string fshare = faceTotal > 0 ? (100f * fc / faceTotal).ToString("0.00") + "%" : "-";

                    string lane = t == TerrainType.Plain
                        ? "(rolled per region - see below)"
                        : Items.SotorIngredientDropModel.TerrainLane(t).ToString();
                    sb.AppendLine($"  {t,-18} {n,9:N0}   {(water ? "(sea) " + share : share),10}"
                                  + $" {fc,8:N0}  {fshare,8}   {lane}");
                }

                sb.AppendLine();
                sb.AppendLine("  WHERE TO FIND EACH TERRAIN (nearest settlement to a sample point):");
                foreach (var kv in counts.OrderBy(k => k.Value))
                {
                    if (!examples.TryGetValue(kv.Key, out var spots) || spots.Count == 0) continue;
                    var named = spots.Select(p => $"({p.X:0},{p.Y:0}) {NearestSettlement(p)}");
                    sb.AppendLine($"  {kv.Key,-18} {string.Join("  |  ", named)}");
                }

                AppendPlainRegionMap(sb, plainCells);
                AppendFarmingGuide(sb, plainCells, scene);
                AppendSettlementTable(sb, scene);

                foreach (var line in sb.ToString().Split('\n'))
                    SotorLog.Info(line.TrimEnd('\r'));

                var rare = counts.Where(kv => !WaterTerrain.Contains(kv.Key)
                                              && landTotal > 0 && 100f * kv.Value / landTotal < 1f)
                                 .OrderBy(kv => kv.Value)
                                 .Select(kv => $"{kv.Key} {(100f * kv.Value / landTotal):0.00}%");
                string rareText = string.Join(", ", rare);

                return $"Surveyed {total:N0} points. Full table written to Logs/SOTOR/latest.txt.\n"
                       + $"Land terrain under 1%: {(rareText.Length > 0 ? rareText : "none")}";
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"map_terrain failed: {ex.Message}");
                return "sotor.map_terrain failed: " + ex.Message;
            }
        }

        private struct PlainCell : IEquatable<PlainCell>
        {
            public readonly int X, Y;
            public PlainCell(int x, int y) { X = x; Y = y; }
            public bool Equals(PlainCell o) => X == o.X && Y == o.Y;
            public override bool Equals(object o) => o is PlainCell c && Equals(c);
            public override int GetHashCode() => X * 397 ^ Y;
        }

        private static char LaneLetter(SotorIngredientType t)
        {
            switch (t)
            {
                case SotorIngredientType.AmberCrystal: return 'A';
                case SotorIngredientType.GemStone: return 'G';
                case SotorIngredientType.WarpstoneDust: return 'W';
                case SotorIngredientType.BlessedWater: return 'B';
                case SotorIngredientType.ArcaneScroll: return 'S';
                case SotorIngredientType.DragonBlood: return 'D';
                default: return '?';
            }
        }

        private static void AppendPlainRegionMap(StringBuilder sb, Dictionary<PlainCell, int> plainCells)
        {
            sb.AppendLine();
            sb.AppendLine($"  PLAINS BY REGION  (cell = {Items.SotorIngredientDropModel.PlainRegionSize:0} units, "
                          + "A=Amber G=Gemstone W=Warpstone, '.' = little or no open ground)");
            if (plainCells.Count == 0) { sb.AppendLine("    no plains found."); return; }

            int minX = plainCells.Keys.Min(c => c.X), maxX = plainCells.Keys.Max(c => c.X);
            int minY = plainCells.Keys.Min(c => c.Y), maxY = plainCells.Keys.Max(c => c.Y);

            int busiest = plainCells.Values.Max();
            int floor = Math.Max(3, busiest / 20);

            float cell = Items.SotorIngredientDropModel.PlainRegionSize;
            var header = new StringBuilder("        ");
            for (int x = minX; x <= maxX; x++) header.Append($"{x,-4}");
            sb.AppendLine(header + "  <- cell X");
            sb.AppendLine("    north");
            for (int y = maxY; y >= minY; y--)
            {
                var row = new StringBuilder($"  {y,3} ");
                for (int x = minX; x <= maxX; x++)
                {
                    plainCells.TryGetValue(new PlainCell(x, y), out int n);
                    if (n < floor) { row.Append(".   "); continue; }
                    var centre = new Vec2((x + 0.5f) * cell, (y + 0.5f) * cell);
                    row.Append(LaneLetter(Items.SotorIngredientDropModel.PlainLane(centre))).Append("   ");
                }
                sb.AppendLine(row.ToString());
            }
            sb.AppendLine($"    (cell N spans map units N x {cell:0} .. (N+1) x {cell:0})");

            sb.AppendLine();
            sb.AppendLine("    every plains region, with somewhere to aim for:");
            foreach (var kv in plainCells.Where(k => k.Value >= floor)
                                         .OrderByDescending(k => k.Value))
            {
                var centre = new Vec2((kv.Key.X + 0.5f) * cell, (kv.Key.Y + 0.5f) * cell);
                var lane = Items.SotorIngredientDropModel.PlainLane(centre);
                sb.AppendLine($"      ({kv.Key.X},{kv.Key.Y}) x {kv.Key.X * cell:0}-{(kv.Key.X + 1) * cell:0}"
                              + $" y {kv.Key.Y * cell:0}-{(kv.Key.Y + 1) * cell:0}"
                              + $"  {lane,-14} nr {NearestFortification(centre)}");
            }

            var tally = new Dictionary<char, int>();
            foreach (var kv in plainCells)
            {
                if (kv.Value < floor) continue;
                var centre = new Vec2((kv.Key.X + 0.5f) * Items.SotorIngredientDropModel.PlainRegionSize,
                                      (kv.Key.Y + 0.5f) * Items.SotorIngredientDropModel.PlainRegionSize);
                char c = LaneLetter(Items.SotorIngredientDropModel.PlainLane(centre));
                tally[c] = tally.TryGetValue(c, out int n) ? n + 1 : 1;
            }
            sb.AppendLine("    regions: " + string.Join(", ", tally.OrderByDescending(k => k.Value)
                                                             .Select(k => $"{k.Key}={k.Value}")));
        }

        private static void AppendFarmingGuide(StringBuilder sb, Dictionary<PlainCell, int> plainCells,
                                               TaleWorlds.CampaignSystem.Map.IMapScene scene)
        {
            sb.AppendLine();
            sb.AppendLine("  WHERE TO FARM EACH REAGENT");

            var byLane = new Dictionary<SotorIngredientType, List<string>>();
            void Add(SotorIngredientType lane, string what)
            {
                if (!byLane.TryGetValue(lane, out var list)) byLane[lane] = list = new List<string>();
                if (list.Count < 6) list.Add(what);
            }

            foreach (var s in Settlement.All.Where(x => x.IsHideout))
            {
                try
                {
                    var terrain = scene.GetFaceTerrainType(s.Position.Face);
                    var lane = terrain == TerrainType.Plain
                        ? Items.SotorIngredientDropModel.PlainLane(s.Position.ToVec2())
                        : Items.SotorIngredientDropModel.TerrainLane(terrain);
                    Add(lane, $"hideout nr {NearestFortification(s.Position.ToVec2())} [{terrain}]");
                }
                catch (Exception) { }
            }

            foreach (var kv in plainCells.OrderByDescending(k => k.Value).Take(40))
            {
                var centre = new Vec2((kv.Key.X + 0.5f) * Items.SotorIngredientDropModel.PlainRegionSize,
                                      (kv.Key.Y + 0.5f) * Items.SotorIngredientDropModel.PlainRegionSize);
                Add(Items.SotorIngredientDropModel.PlainLane(centre), $"plains nr {NearestFortification(centre)}");
            }

            foreach (var t in SotorEnchantingIngredients.AllTypes)
            {
                if (t == SotorIngredientType.ArcaneScroll)
                {
                    sb.AppendLine("  Arcane Scroll     sack or raid settlements (town > castle > village), and wizard lords");
                    continue;
                }
                if (t == SotorIngredientType.DragonBlood)
                {
                    sb.AppendLine("  Dragon Blood      lords only, scaling with their clan tier - no terrain feeds it");
                    continue;
                }
                byLane.TryGetValue(t, out var list);
                string where = list != null && list.Count > 0
                    ? string.Join("; ", list)
                    : (t == SotorIngredientType.BlessedWater
                        ? "no hideout sits on water - this one comes from sea and river battles instead"
                        : "(no hideout or plains region on this map feeds it)");
                sb.AppendLine($"  {t,-17} {where}");
            }
            sb.AppendLine("  Blessed Water     also any sea or river battle, and any fight on snowy ground in winter");
        }

        private static void AppendSettlementTable(StringBuilder sb, TaleWorlds.CampaignSystem.Map.IMapScene scene)
        {
            sb.AppendLine();
            sb.AppendLine("  WHAT EACH SETTLEMENT PAYS  (scrolls are the average at 100% loot share;");
            sb.AppendLine("   the two lanes are fed by whoever defends it, so they scale with the garrison)");
            sb.AppendLine($"  {"settlement",-22}{"type",-8}{"ground",-12}{"terrain pays",-15}"
                          + $"{"rulers pay",-15}{"scrolls",7}");

            var rows = Settlement.All.Where(s => s.IsTown || s.IsCastle).ToList();
            foreach (var s in rows.OrderByDescending(x => Items.SotorIngredientDropModel.SettlementScrollScore(x)))
            {
                try
                {
                    var terrain = scene.GetFaceTerrainType(s.Position.Face);
                    var terrainLane = terrain == TerrainType.Plain
                        ? Items.SotorIngredientDropModel.PlainLane(s.Position.ToVec2())
                        : Items.SotorIngredientDropModel.TerrainLane(terrain);

                    var trad = s.OwnerClan != null && !s.OwnerClan.IsBanditFaction
                        ? SotorRivalSeeder.DeriveClanTradition(s.OwnerClan)
                        : Trad.None;
                    var loreLane = Items.SotorIngredientDropModel.LoreLane(trad);

                    float score = Items.SotorIngredientDropModel.SettlementScrollScore(s);
                    float scrolls = score * Items.SotorIngredientDropModel.DropAmplitude(SotorIngredientType.ArcaneScroll)
                                    * Items.SotorIngredientDropModel.MeanRandom(SotorIngredientType.ArcaneScroll);

                    string ruled = loreLane == SotorIngredientType.Invalid
                        ? "-"
                        : $"{loreLane} ({trad})";

                    if (loreLane == terrainLane && loreLane != SotorIngredientType.Invalid)
                        ruled += " *same*";

                    sb.AppendLine($"  {s.Name,-22}{(s.IsTown ? "town" : "castle"),-8}{terrain,-12}"
                                  + $"{terrainLane,-15}{ruled,-15}{scrolls,7:0.0}");
                }
                catch (Exception) { }
            }
            sb.AppendLine("  * same = terrain and rulers feed the SAME reagent, so they stack into one bigger haul.");
        }

        private static string NearestFortification(Vec2 p)
        {
            Settlement best = null;
            float bestSq = float.MaxValue;
            foreach (var s in Settlement.All)
            {
                if (!s.IsTown && !s.IsCastle) continue;
                float d = s.Position.DistanceSquared(p);
                if (d < bestSq) { bestSq = d; best = s; }
            }
            return best == null ? NearestSettlement(p) : $"{best.Name} {Math.Sqrt(bestSq):0}u";
        }

        private static string NearestSettlement(Vec2 p)
        {
            Settlement best = null;
            float bestSq = float.MaxValue;
            foreach (var s in Settlement.All)
            {
                float d = s.Position.DistanceSquared(p);
                if (d < bestSq) { bestSq = d; best = s; }
            }
            if (best == null) return "?";
            return $"{best.Name} {Math.Sqrt(bestSq):0}u";
        }
    }
}
