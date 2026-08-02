using System;
using System.IO;
using TaleWorlds.ModuleManager;

namespace SOTOR
{

    public static class SotorSettings
    {

        public static bool UseThrownAmberSpear = true;

        public static bool EnableSkeletonArmies = true;

        public static bool EnableMindControlledArmies = true;

        public static bool EnableCompanionSpellcasters = false;

        public static string SpellcraftAttributeId = "intelligence";

        public static int HudMode = 0;

        public static bool EnableArcaneConduit = true;

        public static bool EnableSpellDamageLog = true;

        public static bool EnableWindsOnMagicKill = false;
        public static float WindsOnMagicKillAmount = 0f;

        public static bool EnableArmorWomRechargeTweak = false;
        public static float ArmorWomRechargeEffectPercent = 0f;

        public static float ArmorWomRechargeEffectMultiplier =>
            EnableArmorWomRechargeTweak ? 1f + ArmorWomRechargeEffectPercent / 100f : 1f;

        public static bool EnableSpellEffectivenessTweak = false;
        public static float SpellEffectivenessBonusPercent = 0f;

        public static float SpellEffectivenessBonusFraction =>
            EnableSpellEffectivenessTweak ? SpellEffectivenessBonusPercent / 100f : 0f;

        public static bool DisableMagicInSieges = false;

        public static bool EnableSpellShipDamage = true;

        public static float SpellShipDamagePercent = 100f;

        public static float SpellShipDamageMultiplier =>
            EnableSpellShipDamage ? SpellShipDamagePercent / 100f : 0f;

        public static bool EnableBurningDeckDamage = true;

        public static float BurningDeckDamagePerSecond = 4f;

        public static bool EnableAbandonShipAI = true;

        public static bool EnableCastSlowMotion = true;

        public static int JavelinAttackStateTest = -1;

        private const string FileName = "sotor_settings.txt";
        private static bool _loaded;

        private static string SettingsPath()
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

            try
            {
                var path = SettingsPath();
                if (path == null || !File.Exists(path))
                {
                    SotorLog.Info($"SotorSettings: no settings file; using defaults (UseThrownAmberSpear={UseThrownAmberSpear}).");
                    return;
                }

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw?.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    {
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                    {
                        continue;
                    }
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    ApplyKey(key, val);
                }

                SotorLog.Info($"SotorSettings loaded: UseThrownAmberSpear={UseThrownAmberSpear}.");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorSettings.Load failed ({ex.GetType().Name}): {ex.Message}; using defaults.");
            }
        }

        private static void ApplyKey(string key, string val)
        {
            switch (key)
            {
                case "UseThrownAmberSpear":
                    if (bool.TryParse(val, out var b)) UseThrownAmberSpear = b;
                    break;
                case "JavelinAttackStateTest":
                    if (int.TryParse(val, out var n)) JavelinAttackStateTest = n;
                    break;
            }
        }

        public static void Save()
        {
            try
            {
                var path = SettingsPath();
                if (path == null)
                {
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,
                    "# SOTOR settings. Edit values (true/false); one key=value per line.\n" +
                    "# UseThrownAmberSpear: false = spell bolt (v1), true = real thrown amber javelin (v2).\n" +
                    $"UseThrownAmberSpear={UseThrownAmberSpear}\n");
                SotorLog.Info($"SotorSettings saved: UseThrownAmberSpear={UseThrownAmberSpear}.");
            }
            catch (Exception ex)
            {
                SotorLog.Warn($"SotorSettings.Save failed: {ex.Message}");
            }
        }
    }
}
