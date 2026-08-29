using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace SOTOR
{

    public static class SotorText
    {

        public static TextObject GetObject(string id, string englishFallback = null)
        {
            if (!string.IsNullOrEmpty(id))
            {
                try
                {

                    if (GameTexts.TryGetText(id, out var found) && found != null && !string.IsNullOrEmpty(found.Value))
                    {

                        return new TextObject(found.Value);
                    }
                }
                catch
                {

                }
            }

            return new TextObject(englishFallback ?? MissingText(id));
        }

        private static string MissingText(string id)
        {
            return "[missing string: " + (string.IsNullOrEmpty(id) ? "(no id)" : id) + "]";
        }

        public static string Get(string id, string englishFallback = null)
        {
            return GetObject(id, englishFallback).Value ?? string.Empty;
        }

        public static string Rendered(string id, string englishFallback = null)
        {
            return GetObject(id, englishFallback).ToString() ?? string.Empty;
        }

        public static string Marker(string raw, ref string cache, ref int cacheLang)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (!raw.StartsWith("{=")) return raw;

            int lang = -1;
            try { lang = MBTextManager.GetActiveTextLanguageIndex(); }
            catch { }
            if (cache != null && cacheLang == lang) return cache;

            try { cache = new TextObject(raw).ToString(); }
            catch { cache = StripMarker(raw); }
            cacheLang = lang;
            return cache;
        }

        public static string StripMarker(string raw)
        {
            if (string.IsNullOrEmpty(raw) || !raw.StartsWith("{=")) return raw ?? string.Empty;
            int close = raw.IndexOf('}');
            return close < 0 ? raw : raw.Substring(close + 1);
        }

        public static string EnumName(System.Enum value)
        {
            if (value == null) return string.Empty;
            string name = value.ToString();
            return Rendered("sotor_enum_" + value.GetType().Name + "_" + name, name);
        }

        public static void SetPlayerVariables()
        {
            try
            {
                var player = Hero.MainHero;
                if (player == null) return;

                MBTextManager.SetTextVariable("STUDENT", player.FirstName ?? player.Name);
            }
            catch
            {

            }
        }
    }
}
