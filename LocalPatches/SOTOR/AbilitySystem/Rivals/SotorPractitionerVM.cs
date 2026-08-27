using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.AbilitySystem.Rivals
{

    public class SotorPractitionerVM : HeroVM
    {
        private readonly Hero _hero;
        private readonly bool _isKnown;

        public SotorPractitionerVM(Hero hero, bool isKnown)
            : base(hero, false)
        {
            _hero = hero;
            _isKnown = isKnown;

            if (!_isKnown)
            {
                Hint = new HintViewModel(
                    SotorText.GetObject("sotor_enc_traditions_unknown_hint"));
            }

            RefreshValues();
        }

        [DataSourceProperty]
        public HintViewModel Hint { get; private set; }

        [DataSourceProperty]
        public bool IsKnown => _isKnown;

        public override void RefreshValues()
        {
            base.RefreshValues();
            if (_hero == null) return;

            if (!_isKnown)
            {
                NameText = Descriptor(_hero).ToString();
            }
        }

        public void ExecuteOpenPractitioner()
        {
            if (!_isKnown) return;
            base.ExecuteLink();
        }

        public override void ExecuteBeginHint()
        {
            if (!_isKnown) return;
            base.ExecuteBeginHint();
        }

        public static TextObject Descriptor(Hero hero)
        {
            var t = SotorText.GetObject("sotor_enc_traditions_unknown");
            t.SetTextVariable("CULTURE", hero?.Culture?.Name ?? SotorText.GetObject("sotor_practitioner_culture_unknown"));
            t.SetTextVariable("ROLE", RoleWord(hero));
            return t;
        }

        private static TextObject RoleWord(Hero hero)
        {
            bool wanderer = hero != null && hero.Occupation == Occupation.Wanderer;
            return wanderer
                ? SotorText.GetObject("sotor_enc_traditions_role_wanderer")
                : SotorText.GetObject("sotor_enc_traditions_role_noble");
        }

        public static bool PlayerKnows(Hero hero)
        {
            if (hero == null) return false;
            if (hero == Hero.MainHero) return true;
            try
            {
                return Campaign.Current.Models.InformationRestrictionModel.DoesPlayerKnowDetailsOf(hero);
            }
            catch
            {
                return hero.IsKnownToPlayer;
            }
        }
    }
}
