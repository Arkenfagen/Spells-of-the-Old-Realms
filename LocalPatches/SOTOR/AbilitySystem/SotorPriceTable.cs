using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.ModuleManager;

namespace SOTOR.AbilitySystem
{

    public static class SotorPriceTable
    {
        private const string FileName = "sotor_spell_prices.xml";

        private static readonly Dictionary<int, int> DefaultTierCosts = new Dictionary<int, int>
        {
            [1] = 5000,
            [2] = 10000,
            [3] = 25000,
            [4] = 50000,
        };

        private static readonly Dictionary<string, int> DefaultLoreCosts = SotorLores.Prices;

        private static readonly Dictionary<int, int> _tierCosts = new Dictionary<int, int>();
        private static readonly Dictionary<string, int> _loreCosts = new Dictionary<string, int>();

        private static readonly Dictionary<string, int> _spellCosts = new Dictionary<string, int>();
        private static bool _loaded;

        public static int GetSpellCost(string stringId, int spellTier)
        {
            if (!string.IsNullOrEmpty(stringId) && _spellCosts.TryGetValue(stringId, out var perSpell))
            {
                return perSpell;
            }
            return GetSpellBaseCostForTier(spellTier);
        }

        public static int GetSpellBaseCostForTier(int spellTier)
        {
            if (_tierCosts.TryGetValue(spellTier, out var cost))
            {
                return cost;
            }
            return DefaultTierCosts.TryGetValue(spellTier, out var def) ? def : 0;
        }

        public static int GetLoreUnlockCost(string loreId)
        {
            if (loreId != null && _loreCosts.TryGetValue(loreId, out var cost))
            {
                return cost;
            }
            return DefaultLoreCosts.TryGetValue(loreId ?? string.Empty, out var def) ? def : 0;
        }

        private static string FilePath()
        {
            try
            {
                var modulePath = ModuleHelper.GetModuleFullPath("SOTOR");
                return Path.Combine(modulePath, "ModuleData", FileName);
            }
            catch
            {
                return null;
            }
        }

        public static void Load()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            _tierCosts.Clear();
            _loreCosts.Clear();
            _spellCosts.Clear();

            var path = FilePath();
            if (path == null)
            {
                SotorLog.Warn("SotorPriceTable: could not resolve module path; using default prices.");
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    WriteDefaultFile(path);
                    SotorLog.Info($"SotorPriceTable: no price file; wrote defaults to {path}.");
                }

                var doc = new XmlDocument();
                doc.Load(path);

                foreach (XmlNode tierNode in doc.SelectNodes("//SpellTiers/Tier"))
                {
                    if (TryReadInt(tierNode, "level", out var level) && TryReadInt(tierNode, "cost", out var cost) && cost >= 0)
                    {
                        _tierCosts[level] = cost;
                    }
                }

                foreach (XmlNode loreNode in doc.SelectNodes("//LorePrices/Lore"))
                {
                    var id = loreNode?.Attributes?["id"]?.Value;
                    if (!string.IsNullOrEmpty(id) && TryReadInt(loreNode, "cost", out var cost) && cost >= 0)
                    {
                        _loreCosts[id] = cost;
                    }
                }

                foreach (XmlNode spellNode in doc.SelectNodes("//Spells/Spell"))
                {
                    var id = spellNode?.Attributes?["id"]?.Value;
                    if (!string.IsNullOrEmpty(id) && TryReadInt(spellNode, "cost", out var cost) && cost >= 0)
                    {
                        _spellCosts[id] = cost;
                    }
                }

                SotorLog.Info($"SotorPriceTable loaded: {_tierCosts.Count} tier override(s), {_loreCosts.Count} lore override(s), {_spellCosts.Count} per-spell override(s).");
            }
            catch (System.Exception ex)
            {

                _tierCosts.Clear();
                _loreCosts.Clear();
                _spellCosts.Clear();
                SotorLog.Warn($"SotorPriceTable.Load failed ({ex.GetType().Name}): {ex.Message}; using default prices.");
            }
        }

        private static bool TryReadInt(XmlNode node, string attr, out int value)
        {
            value = 0;
            var raw = node?.Attributes?[attr]?.Value;
            return !string.IsNullOrEmpty(raw) && int.TryParse(raw, out value);
        }

        private static void WriteDefaultFile(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sb.AppendLine("<!-- SOTOR spell/lore GOLD prices. Edit the cost numbers to taste; delete the file to");
                sb.AppendLine("     restore these defaults. A malformed value simply falls back to its default. -->");
                sb.AppendLine("<SpellPrices>");
                sb.AppendLine("  <!-- Per-spell price to LEARN a spell, by its SpellTier. -->");
                sb.AppendLine("  <SpellTiers>");
                for (int tier = 1; tier <= 4; tier++)
                {
                    sb.AppendLine($"    <Tier level=\"{tier}\" cost=\"{DefaultTierCosts[tier]}\" />");
                }
                sb.AppendLine("  </SpellTiers>");
                sb.AppendLine("  <!-- Price to UNLOCK an entire lore (magic school). -->");
                sb.AppendLine("  <LorePrices>");
                foreach (var kv in DefaultLoreCosts)
                {
                    sb.AppendLine($"    <Lore id=\"{kv.Key}\" cost=\"{kv.Value}\" />");
                }
                sb.AppendLine("  </LorePrices>");
                sb.AppendLine("  <!-- OPTIONAL: override the price of INDIVIDUAL spells. Any spell you list here");
                sb.AppendLine("       ignores its tier price above; every spell you DON'T list keeps its tier price,");
                sb.AppendLine("       so you only add the few you want to change. The id is the spell's StringID from");
                sb.AppendLine("       ModuleData/tor_custom_xmls/tor_abilitytemplates.xml (open it: each spell's");
                sb.AppendLine("       StringID=\"...\" is the id, and its Name=\"...\" is what you see in the spellbook).");
                sb.AppendLine("       Uncomment the example below and edit it, or add your own <Spell .../> lines. -->");
                sb.AppendLine("  <Spells>");
                sb.AppendLine("    <!-- <Spell id=\"Fireball\" cost=\"8000\" /> -->");
                sb.AppendLine("  </Spells>");
                sb.AppendLine("</SpellPrices>");

                File.WriteAllText(path, sb.ToString());
            }
            catch (System.Exception ex)
            {
                SotorLog.Warn($"SotorPriceTable: could not write default file: {ex.Message}");
            }
        }
    }
}
