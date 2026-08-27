using System.Collections.Generic;
using System.IO;
using System.Xml;
using SOTOR.Extensions;
using SOTOR.Extensions.ExtendedInfoSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ModuleManager;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorRivalOverrides
    {
        private const string FileName = "sotor_overrides.xml";
        private const string LookupFileName = "sotor_overrides_lookup.txt";

        public sealed class LordPin
        {
            public string IdOrName;
            public bool ById;
            public bool? Caster;
            public string LoreId;
            public int Level;
            public bool Matched;
        }

        private static readonly HashSet<string> _mundaneCultures = new HashSet<string>();
        private static readonly HashSet<string> _mundaneClans = new HashSet<string>();

        private static readonly Dictionary<string, Trad> _culturePinsById = new Dictionary<string, Trad>();
        private static readonly Dictionary<string, Trad> _culturePinsByName = new Dictionary<string, Trad>();
        private static readonly Dictionary<string, Trad> _clanPinsById = new Dictionary<string, Trad>();
        private static readonly Dictionary<string, Trad> _clanPinsByName = new Dictionary<string, Trad>();
        private static readonly Dictionary<string, LordPin> _lordPinsById = new Dictionary<string, LordPin>();
        private static readonly Dictionary<string, LordPin> _lordPinsByName = new Dictionary<string, LordPin>();
        private static readonly List<LordPin> _lordPins = new List<LordPin>();
        private static readonly List<string> _unmatchedClanPins = new List<string>();
        private static bool _loaded;

        private static bool _any;

        public static string LastLoadError { get; private set; }

        public static int LordPinCount => _lordPins.Count;
        public static int ClanPinCount => _clanPinsById.Count + _clanPinsByName.Count;
        public static int CulturePinCount => _culturePinsById.Count + _culturePinsByName.Count;

        public static string CulturePinFingerprint
        {
            get
            {
                if (_culturePinsById.Count + _culturePinsByName.Count == 0) return string.Empty;
                var parts = new List<string>();
                foreach (var kv in _culturePinsById) parts.Add(kv.Key + "=" + kv.Value);
                foreach (var kv in _culturePinsByName) parts.Add(kv.Key + "=" + kv.Value);
                parts.Sort(string.CompareOrdinal);
                return string.Join(",", parts.ToArray());
            }
        }

        public static Trad ClanTraditionPin(Clan clan)
        {
            if (!_any || clan == null || clan == Clan.PlayerClan) return Trad.None;
            if (clan.StringId != null && _clanPinsById.TryGetValue(clan.StringId, out var t)) return t;
            if (_clanPinsByName.Count > 0)
            {
                var name = clan.Name?.ToString();
                if (!string.IsNullOrEmpty(name) && _clanPinsByName.TryGetValue(Norm(name), out t)) return t;
            }
            return Trad.None;
        }

        public static Trad CultureTraditionPin(CultureObject culture)
        {
            if (!_any || culture == null) return Trad.None;
            var byId = CultureTraditionPinById(culture.StringId);
            if (byId != Trad.None) return byId;
            if (_culturePinsByName.Count > 0)
            {
                var name = culture.Name?.ToString();
                if (!string.IsNullOrEmpty(name) && _culturePinsByName.TryGetValue(Norm(name), out var t)) return t;
            }
            return Trad.None;
        }

        public static Trad CultureTraditionPinById(string cultureId)
        {
            if (!_any || string.IsNullOrEmpty(cultureId)) return Trad.None;
            return _culturePinsById.TryGetValue(cultureId, out var t) ? t : Trad.None;
        }

        public static bool IsMundaneCulture(CultureObject culture)
        {
            if (!_any || culture == null || _mundaneCultures.Count == 0) return false;
            if (!string.IsNullOrEmpty(culture.StringId) && _mundaneCultures.Contains(culture.StringId)) return true;
            var name = culture.Name?.ToString();
            return !string.IsNullOrEmpty(name) && _mundaneCultures.Contains(Norm(name));
        }

        public static bool IsMundaneCultureById(string cultureId)
        {
            return _any && !string.IsNullOrEmpty(cultureId) && _mundaneCultures.Contains(cultureId);
        }

        public static bool IsMundaneClan(Clan clan)
        {
            if (!_any || clan == null || _mundaneClans.Count == 0) return false;
            if (!string.IsNullOrEmpty(clan.StringId) && _mundaneClans.Contains(clan.StringId)) return true;
            var name = clan.Name?.ToString();
            return !string.IsNullOrEmpty(name) && _mundaneClans.Contains(Norm(name));
        }

        public static bool HasClanPin(Clan clan)
        {
            if (!_any || clan == null) return false;
            if (ClanTraditionPin(clan) != Trad.None) return true;
            return IsMundaneClan(clan);
        }

        public static LordPin FindLordPin(Hero hero)
        {
            if (!_any || hero == null) return null;
            if (hero == Hero.MainHero || (hero.Clan != null && hero.Clan == Clan.PlayerClan)) return null;
            if (hero.StringId != null && _lordPinsById.TryGetValue(hero.StringId, out var p)) return p;
            if (_lordPinsByName.Count > 0)
            {
                var name = hero.Name?.ToString();
                if (!string.IsNullOrEmpty(name) && _lordPinsByName.TryGetValue(Norm(name), out p)) return p;
            }
            return null;
        }

        public static bool? LordCasterPin(Hero hero) => FindLordPin(hero)?.Caster;
        public static string LordLorePin(Hero hero) => FindLordPin(hero)?.LoreId;
        public static int LordLevelPin(Hero hero) => FindLordPin(hero)?.Level ?? 0;

        public static void Load()
        {
            if (_loaded) return;
            Reload();
        }

        public static void Reload()
        {
            _loaded = true;
            _mundaneCultures.Clear();
            _mundaneClans.Clear();
            _culturePinsById.Clear();
            _culturePinsByName.Clear();
            _clanPinsById.Clear();
            _clanPinsByName.Clear();
            _lordPinsById.Clear();
            _lordPinsByName.Clear();
            _lordPins.Clear();
            _unmatchedClanPins.Clear();
            _any = false;

            var path = FilePath(FileName);
            if (path == null)
            {
                SotorLog.Warn("SotorRivalOverrides: could not resolve module path; no overrides.");
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    WriteDefaultFile(path);
                    SotorLog.Info($"SotorRivalOverrides: no override file; wrote a commented template to {path}.");
                }

                var doc = new XmlDocument();
                doc.Load(path);

                foreach (XmlNode node in doc.SelectNodes("//Clans/Clan"))
                {
                    var id = Attr(node, "id");
                    var name = Attr(node, "name");
                    var loreRaw = Attr(node, "lore");
                    string loreId = ResolveLoreId(loreRaw);
                    var trad = SotorTraditions.TradForLore(loreId);

                    if (IsMundaneWord(loreRaw))
                    {
                        if (!string.IsNullOrEmpty(id)) _mundaneClans.Add(id);
                        else if (!string.IsNullOrEmpty(name)) _mundaneClans.Add(Norm(name));
                        else SotorLog.Warn("SotorRivalOverrides: a mundane <Clan> entry has neither id nor name; skipped.");
                        continue;
                    }
                    if (trad == Trad.None)
                    {
                        SotorLog.Warn($"SotorRivalOverrides: <Clan {id ?? name}> has no usable lore "
                                      + $"('{loreRaw}'); entry skipped. See the comments in {FileName} for valid values.");
                        continue;
                    }

                    if (SotorTraditions.IsMemberOnly(trad))
                    {
                        SotorLog.Warn($"SotorRivalOverrides: <Clan {id ?? name}> pins '{loreRaw}', but Dark/High "
                                      + "are member-only lores no clan may teach. Pin a <Lord> instead; entry skipped.");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(id)) _clanPinsById[id] = trad;
                    else if (!string.IsNullOrEmpty(name)) _clanPinsByName[Norm(name)] = trad;
                    else SotorLog.Warn("SotorRivalOverrides: a <Clan> entry has neither id nor name; skipped.");
                }

                foreach (XmlNode node in doc.SelectNodes("//Cultures/Culture"))
                {
                    var id = Attr(node, "id");
                    var name = Attr(node, "name");
                    var loreRaw = Attr(node, "lore");
                    string loreId = ResolveLoreId(loreRaw);
                    var trad = SotorTraditions.TradForLore(loreId);

                    if (IsMundaneWord(loreRaw))
                    {
                        if (!string.IsNullOrEmpty(id)) _mundaneCultures.Add(id);
                        else if (!string.IsNullOrEmpty(name)) _mundaneCultures.Add(Norm(name));
                        else SotorLog.Warn("SotorRivalOverrides: a mundane <Culture> entry has neither id nor name; skipped.");
                        continue;
                    }
                    if (trad == Trad.None)
                    {
                        SotorLog.Warn($"SotorRivalOverrides: <Culture {id ?? name}> has no usable lore "
                                      + $"('{loreRaw}'); entry skipped.");
                        continue;
                    }

                    if (SotorTraditions.IsMemberOnly(trad))
                    {
                        SotorLog.Warn($"SotorRivalOverrides: <Culture {id ?? name}> pins '{loreRaw}', but Dark/High "
                                      + "are member-only lores no culture may teach. Pin a <Lord> instead; entry skipped.");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(id)) _culturePinsById[id] = trad;
                    else if (!string.IsNullOrEmpty(name)) _culturePinsByName[Norm(name)] = trad;
                    else SotorLog.Warn("SotorRivalOverrides: a <Culture> entry has neither id nor name; skipped.");
                }

                foreach (XmlNode node in doc.SelectNodes("//Lords/Lord"))
                {
                    var id = Attr(node, "id");
                    var name = Attr(node, "name");
                    if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name))
                    {
                        SotorLog.Warn("SotorRivalOverrides: a <Lord> entry has neither id nor name; skipped.");
                        continue;
                    }

                    var pin = new LordPin { IdOrName = id ?? name, ById = !string.IsNullOrEmpty(id) };

                    var casterRaw = Attr(node, "caster");
                    if (!string.IsNullOrEmpty(casterRaw))
                    {
                        if (bool.TryParse(casterRaw, out bool c)) pin.Caster = c;
                        else SotorLog.Warn($"SotorRivalOverrides: <Lord {pin.IdOrName}> caster='{casterRaw}' "
                                           + "is not true/false; attribute ignored.");
                    }

                    var loreRaw = Attr(node, "lore");
                    if (!string.IsNullOrEmpty(loreRaw))
                    {
                        pin.LoreId = ResolveLoreId(loreRaw);
                        if (pin.LoreId == null)
                        {
                            SotorLog.Warn($"SotorRivalOverrides: <Lord {pin.IdOrName}> lore='{loreRaw}' is not a "
                                          + $"known lore; attribute ignored. See the comments in {FileName}.");
                        }
                    }

                    var levelRaw = Attr(node, "level");
                    if (!string.IsNullOrEmpty(levelRaw))
                    {
                        if (int.TryParse(levelRaw, out int lv))
                        {
                            if (lv < 1) lv = 1;
                            if (lv > SotorTraditions.MaxCasterLevel) lv = SotorTraditions.MaxCasterLevel;
                            pin.Level = lv;
                        }
                        else
                        {
                            SotorLog.Warn($"SotorRivalOverrides: <Lord {pin.IdOrName}> level='{levelRaw}' is not "
                                          + "a number; attribute ignored.");
                        }
                    }

                    if (pin.Caster == null && pin.LoreId == null && pin.Level == 0)
                    {
                        SotorLog.Warn($"SotorRivalOverrides: <Lord {pin.IdOrName}> sets nothing; skipped.");
                        continue;
                    }

                    _lordPins.Add(pin);
                    if (pin.ById) _lordPinsById[pin.IdOrName] = pin;
                    else _lordPinsByName[Norm(pin.IdOrName)] = pin;
                }

                LastLoadError = null;
                _any = ClanPinCount + CulturePinCount + _lordPins.Count
                       + _mundaneCultures.Count + _mundaneClans.Count > 0;

                SotorLog.Info($"SotorRivalOverrides loaded: {ClanPinCount} clan pin(s), {CulturePinCount} culture pin(s), {_lordPins.Count} lord pin(s), {_mundaneCultures.Count + _mundaneClans.Count} mundane pin(s).");
            }
            catch (System.Exception ex)
            {
                _mundaneCultures.Clear();
            _mundaneClans.Clear();
            _culturePinsById.Clear();
                _culturePinsByName.Clear();
                _clanPinsById.Clear();
                _clanPinsByName.Clear();
                _lordPinsById.Clear();
                _lordPinsByName.Clear();
                _lordPins.Clear();
                _any = false;

                LastLoadError = ex.Message;
                SotorLog.Warn($"SotorRivalOverrides: {FileName} could not be read, so ALL overrides in it were "
                              + $"IGNORED - not just the bad line. {ex.GetType().Name}: {ex.Message} "
                              + "Most often a missing '/>' or an unclosed quote. Fix the XML and reload.");
            }
        }

        public static List<LordPin> UnmatchedLordPins()
        {
            var list = new List<LordPin>();
            foreach (var p in _lordPins)
            {
                if (!p.Matched) list.Add(p);
            }
            return list;
        }

        public static List<string> UnmatchedClanPins()
        {
            _unmatchedClanPins.Clear();
            var seenIds = new HashSet<string>(_clanPinsById.Keys);
            var seenNames = new HashSet<string>(_clanPinsByName.Keys);
            foreach (var clan in Clan.All)
            {
                if (clan == null) continue;
                if (clan.StringId != null) seenIds.Remove(clan.StringId);
                var name = clan.Name?.ToString();
                if (!string.IsNullOrEmpty(name)) seenNames.Remove(Norm(name));
            }
            _unmatchedClanPins.AddRange(seenIds);
            _unmatchedClanPins.AddRange(seenNames);
            return _unmatchedClanPins;
        }

        private static string Attr(XmlNode node, string attr) => node?.Attributes?[attr]?.Value;

        private static string Norm(string s) => s.Trim().ToLowerInvariant();

        public static bool IsMundaneWord(string raw)
        {
            return !string.IsNullOrEmpty(raw) && Norm(raw) == "none";
        }

        public static string ResolveLoreId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var norm = Norm(raw);
            foreach (var t in SotorTraditions.AllTraditions)
            {
                string loreId = SotorTraditions.LoreIdFor(t);
                if (loreId != null && Norm(loreId) == norm) return loreId;
                if (Norm(t.ToString()) == norm) return loreId;
            }
            return null;
        }

        private static string FilePath(string fileName)
        {
            try
            {
                var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
                return Path.Combine(modulePath, "ModuleData", fileName);
            }
            catch
            {
                return null;
            }
        }

        public static void WriteLookupFile()
        {
            var path = FilePath(LookupFileName);
            if (path == null || Campaign.Current == null) return;
            try
            {
                var sb = new System.Text.StringBuilder(64 * 1024);
                sb.AppendLine("SOTOR override lookup, regenerated on every campaign load. The mod never reads it;");
                sb.AppendLine("it is where you find the ids and current state for ModuleData/" + FileName + ".");
                sb.AppendLine();
                sb.AppendLine("=== Cultures:  id | name | lore taught ===");
                sb.AppendLine("Use these ids for <Culture .../>. A total conversion may reuse a native id for a");
                sb.AppendLine("completely different faction, so trust this list over the name you expect.");
                sb.AppendLine();
                var seenCultures = new List<string>();
                foreach (var clan in Clan.All)
                {
                    if (clan == null || clan.IsEliminated || clan.IsBanditFaction) continue;
                    var cu = clan.Culture;
                    if (cu == null || string.IsNullOrEmpty(cu.StringId) || seenCultures.Contains(cu.StringId)) continue;
                    seenCultures.Add(cu.StringId);
                    var ct = SotorCultureTraditions.TraditionFor(cu);
                    sb.Append(cu.StringId).Append(" | ").Append(cu.Name)
                      .Append(" | ").Append(ct == Trad.None ? "(unused - Lore Assignment is By Clan)" : ct.ToString())
                      .AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("=== Clans:  id | name | tier | hosts casters? | tradition ===");
                sb.AppendLine("Tradition is the lore the clan teaches right now, pins already applied.");
                sb.AppendLine();

                foreach (var clan in Clan.All)
                {
                    if (clan == null || clan.IsEliminated || clan.IsBanditFaction) continue;
                    bool eligible = SotorRivalSeeder.IsCasterEligibleClan(clan);
                    var trad = SotorRivalSeeder.DeriveClanTradition(clan);
                    sb.Append(clan.StringId).Append(" | ").Append(clan.Name)
                      .Append(" | tier ").Append(clan.Tier)
                      .Append(" | ").Append(clan == Clan.PlayerClan ? "player clan, never seeded" : (eligible ? "yes" : "no"))
                      .Append(" | ").Append(eligible ? trad.ToString() : (trad + " if made eligible"))
                      .AppendLine();
                }

                var nameCount = new Dictionary<string, int>();
                var lords = new List<Hero>();
                foreach (var hero in Hero.AllAliveHeroes)
                {
                    if (hero == null || !hero.IsLord || hero.IsChild) continue;
                    lords.Add(hero);
                    var n = Norm(hero.Name?.ToString() ?? "");
                    nameCount[n] = (nameCount.TryGetValue(n, out int c) ? c : 0) + 1;
                }

                sb.AppendLine();
                sb.AppendLine("=== Lords:  id | name | clan | current magic ===");
                sb.AppendLine("Pins are already applied, so a pinned lord reads back his pinned state here.");
                sb.AppendLine();
                foreach (var hero in lords)
                {
                    sb.Append(hero.StringId).Append(" | ").Append(hero.Name)
                      .Append(" | ").Append(hero.Clan?.Name?.ToString() ?? "clanless");

                    if (hero.IsAbilityUser())
                    {
                        var info = hero.GetExtendedInfo();
                        var loreList = new List<string>();
                        if (info?.AcquiredLores != null)
                        {
                            foreach (var l in info.AcquiredLores)
                            {
                                if (l != SotorLores.MinorMagic) loreList.Add(l);
                            }
                        }
                        int level = SotorRivalSeeder.HeroCasterLevel(hero, hero.Clan?.Tier ?? 3);
                        sb.Append(" | caster L").Append(level)
                          .Append(" [").Append(string.Join(", ", loreList)).Append("]");
                    }
                    else
                    {
                        sb.Append(" | not a caster");
                    }

                    var n = Norm(hero.Name?.ToString() ?? "");
                    if (nameCount.TryGetValue(n, out int cnt) && cnt > 1)
                    {
                        sb.Append("   << name shared by ").Append(cnt).Append(" lords, pin by id");
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(path, sb.ToString());
                SotorLog.Info($"SotorRivalOverrides: lookup written to {path} ({lords.Count} lords).");
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorRivalOverrides: could not write lookup file: {ex.Message}");
            }
        }

        private static void WriteDefaultFile(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<!--");
                sb.AppendLine("  SOTOR player overrides. Decide who the wizards are and which lore each clan teaches.");
                sb.AppendLine("  Three layers, narrowest wins: a LORD beats his CLAN, and a clan beats its CULTURE.");
                sb.AppendLine("  Anything you don't pin keeps its normal value, so only add the entries you care about.");
                sb.AppendLine("  Delete this file to get a fresh template.");
                sb.AppendLine();
                sb.AppendLine("  To apply changes, save this file and load your campaign. No game restart needed.");
                sb.AppendLine("  The log at Documents/Mount and Blade II Bannerlord/Logs/SOTOR/latest.txt shows how");
                sb.AppendLine("  many pins were read and names any entry that matched nobody.");
                sb.AppendLine();
                sb.AppendLine("  To find ids, load a campaign once and open sotor_overrides_lookup.txt next to this");
                sb.AppendLine("  file. It lists every clan and lord with id, name and current magic. Matching by");
                sb.AppendLine("  name=\"...\" also works and ignores letter case, but ids are safer - several lords");
                sb.AppendLine("  can share a name, and a name pin hits all of them.");
                sb.AppendLine();
                sb.AppendLine("  Lores you can use in lore=\"...\":");
                sb.AppendLine("    LoreOfFire, LoreOfHeavens, LoreOfLight, LoreOfLife, LoreOfBeasts, LoreOfMetal,");
                sb.AppendLine("    LoreOfDeath, LoreOfNecromancy - the eight clan lores, for clans and lords.");
                sb.AppendLine("    DarkMagic, HighMagic - lords only, no clan may teach these. Such a lord becomes");
                sb.AppendLine("    a hidden Doomweaver or Loremaster, an archmage concealed until discovered.");
                sb.AppendLine("    Short names work too: lore=\"Fire\" means LoreOfFire, lore=\"Dark\" means DarkMagic.");
                sb.AppendLine("    lore=\"None\" means NO MAGIC AT ALL. Use it on a culture or clan you want left");
                sb.AppendLine("    mundane. Nothing is mundane unless you say so. For a single lord use caster=\"false\".");
                sb.AppendLine();
                sb.AppendLine("  Pins never touch you or your clan. Companions learn magic through the spellbook.");
                sb.AppendLine("-->");
                sb.AppendLine("<SotorOverrides>");
                sb.AppendLine();
                sb.AppendLine("  <!-- Which lore a whole CULTURE teaches. Every clan of that culture follows it,");
                sb.AppendLine("       which saves pinning them one at a time. Culture pins apply whichever way Lore");
                sb.AppendLine("       Assignment is set in the settings menu.");
                sb.AppendLine("       Find the ids in sotor_overrides_lookup.txt. They are NOT always what you expect:");
                sb.AppendLine("       total conversions reuse the native ids for their own factions. -->");
                sb.AppendLine("  <Cultures>");
                sb.AppendLine("    <!-- <Culture id=\"vlandia\" lore=\"LoreOfFire\" /> -->");
                sb.AppendLine("    <!-- <Culture id=\"battania\" lore=\"LoreOfLife\" /> -->");
                sb.AppendLine("    <!-- <Culture id=\"khuzait\" lore=\"None\" />   no magic among the Khuzait -->");
                sb.AppendLine("  </Cultures>");
                sb.AppendLine();
                sb.AppendLine("  <!-- Which lore a clan teaches. Every caster in the clan follows the pinned lore, as");
                sb.AppendLine("       if the clan had always taught it. Pinning LoreOfNecromancy gives the clan");
                sb.AppendLine("       skeleton troops, like any other Necromancy clan. Uncomment an example and edit");
                sb.AppendLine("       it, or add your own lines. -->");
                sb.AppendLine("  <Clans>");
                sb.AppendLine("    <!-- <Clan id=\"clan_empire_south_1\" lore=\"LoreOfFire\" /> -->");
                sb.AppendLine("    <!-- <Clan name=\"dey Meroc\" lore=\"LoreOfNecromancy\" /> -->");
                sb.AppendLine("    <!-- <Clan id=\"clan_vlandia_3\" lore=\"None\" />   this clan stays mundane -->");
                sb.AppendLine("  </Clans>");
                sb.AppendLine();
                sb.AppendLine("  <!-- Individual lords. Every attribute is optional; set only what you want to force.");
                sb.AppendLine("         caster=\"true\"   He is a wizard, whatever his roll says. Add lore= to pick his");
                sb.AppendLine("                         lore, otherwise he follows his clan. Works even in clans the");
                sb.AppendLine("                         seeding would skip, like low-tier or minor-faction ones.");
                sb.AppendLine("         caster=\"false\"  Never a wizard. Seeded magic is taken back on load.");
                sb.AppendLine("         lore=\"...\"      His personal lore. Only applies if he is a caster, so set");
                sb.AppendLine("                         caster=\"true\" too to be sure. DarkMagic or HighMagic make");
                sb.AppendLine("                         him a hidden master on top of his clan lore.");
                sb.AppendLine("         level=\"1..6\"    1-2 novice, 3-4 adept, 5 master, 6 archmage. Sets his");
                sb.AppendLine("                         Spellcraft and how much of his lore he knows. Never lowers");
                sb.AppendLine("                         skill a wizard already has. Dark and High holders are always");
                sb.AppendLine("                         archmages, so level is ignored for them.");
                sb.AppendLine("       Tavern wanderers are a poor target. The game respawns them with new ids, so a");
                sb.AppendLine("       wanderer pin only lasts while that individual exists. -->");
                sb.AppendLine("  <Lords>");
                sb.AppendLine("    <!-- <Lord name=\"Raganvad\" caster=\"true\" lore=\"LoreOfNecromancy\" level=\"4\" /> -->");
                sb.AppendLine("    <!-- <Lord id=\"lord_2_7\" caster=\"false\" /> -->");
                sb.AppendLine("    <!-- <Lord id=\"lord_4_1\" lore=\"HighMagic\" caster=\"true\" /> -->");
                sb.AppendLine("  </Lords>");
                sb.AppendLine("</SotorOverrides>");

                File.WriteAllText(path, sb.ToString());
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorRivalOverrides: could not write default file: {ex.Message}");
            }
        }
    }
}
