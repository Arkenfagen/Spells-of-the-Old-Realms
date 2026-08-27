using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.AbilitySystem.Rivals
{

    public static class SotorUnlockBlurb
    {

        private const string NoBlurb = "\u0000_sotor_no_blurb";

        public static void Publish(string variableName, string blurbId, Hero master, string loreId, string spellId)
        {
            SotorText.SetPlayerVariables();
            if (master != null) MBTextManager.SetTextVariable("MASTER", master.Name);

            string custom = SotorText.Get(blurbId, NoBlurb);

            TextObject message;
            if (custom != NoBlurb && !string.IsNullOrWhiteSpace(custom))
            {
                message = new TextObject(custom);
            }
            else
            {

                message = spellId != null
                    ? SotorText.GetObject("sotor_teach_learned_spell")
                    : SotorText.GetObject("sotor_teach_learned_lore");
            }

            if (master != null) message.SetTextVariable("MASTER", master.Name);
            if (loreId != null) message.SetTextVariable("LORE", LoreTitle(loreId));
            if (spellId != null) message.SetTextVariable("SPELL", SpellTitle(spellId));

            MBTextManager.SetTextVariable(variableName, message.ToString());
        }

        public static TextObject LoreTitle(string loreId)
        {
            return new TextObject(SotorLores.TitleFor(loreId));
        }

        public static TextObject SpellTitle(string abilityId)
        {
            var template = AbilityFactory.GetTemplate(abilityId);
            return new TextObject(string.IsNullOrEmpty(template?.Name) ? abilityId : template.Name);
        }
    }
}
