using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR
{

    public static class SotorRibbon
    {

        public const int DefaultMs = 4000;

        public static void Show(TextObject message, int extraTimeInMs = DefaultMs, Hero speaker = null)
        {
            if (message == null) return;
            MBInformationManager.AddQuickInformation(message, extraTimeInMs, Portrait(speaker), null, "");
        }

        public static void Show(string message, int extraTimeInMs = DefaultMs, Hero speaker = null)
        {
            if (!string.IsNullOrEmpty(message)) Show(new TextObject(message), extraTimeInMs, speaker);
        }

        private static BasicCharacterObject Portrait(Hero speaker)
        {
            if (speaker?.CharacterObject != null) return speaker.CharacterObject;

            return Campaign.Current != null ? Hero.MainHero?.CharacterObject : null;
        }
    }
}
