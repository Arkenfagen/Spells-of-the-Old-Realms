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
