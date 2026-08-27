using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SOTOR.AbilitySystem.Rivals
{

    [EncyclopediaViewModel(typeof(SotorTraditionObject))]
    public class SotorTraditionPageVM : EncyclopediaContentPageVM
    {
        private readonly SotorTraditionObject _tradition;
        private string _titleText;
        private string _descriptionText;
        private string _countText;
        private string _followersText;
        private string _standingText;
        private MBBindingList<SotorPractitionerVM> _practitioners = new MBBindingList<SotorPractitionerVM>();

        public SotorTraditionPageVM(EncyclopediaPageArgs args)
            : base(args)
        {
            _tradition = args.Obj as SotorTraditionObject;
            RefreshValues();
        }

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set
            {
                if (value != _titleText)
                {
                    _titleText = value;
                    OnPropertyChangedWithValue(value, "TitleText");
                }
            }
        }

        [DataSourceProperty]
        public string DescriptionText
        {
            get => _descriptionText;
            set
            {
                if (value != _descriptionText)
                {
                    _descriptionText = value;
                    OnPropertyChangedWithValue(value, "DescriptionText");
                }
            }
        }

        [DataSourceProperty]
        public string CountText
        {
            get => _countText;
            set
            {
                if (value != _countText)
                {
                    _countText = value;
                    OnPropertyChangedWithValue(value, "CountText");
                }
            }
        }

        [DataSourceProperty]
        public string StandingText
        {
            get => _standingText;
            set
            {
                if (value != _standingText)
                {
                    _standingText = value;
                    OnPropertyChangedWithValue(value, "StandingText");
                }
            }
        }

        [DataSourceProperty]
        public string FollowersText
        {
            get => _followersText;
            set
            {
                if (value != _followersText)
                {
                    _followersText = value;
                    OnPropertyChangedWithValue(value, "FollowersText");
                }
            }
        }

        [DataSourceProperty]
        public MBBindingList<SotorPractitionerVM> Practitioners
        {
            get => _practitioners;
            set
            {
                if (value != _practitioners)
                {
                    _practitioners = value;
                    OnPropertyChangedWithValue(value, "Practitioners");
                }
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();
            if (_tradition == null) return;

            TitleText = _tradition.Name.ToString();
            DescriptionText = _tradition.Description.ToString();
            FollowersText = SotorText.GetObject("sotor_enc_traditions_followers").ToString();
            StandingText = BuildStandingText();

            Practitioners.Clear();

            var heroes = _tradition.CurrentPractitioners();

            var ordered = new List<Hero>(heroes.Count);
            var unknown = new List<Hero>(heroes.Count);
            foreach (var hero in heroes)
            {
                if (SotorPractitionerVM.PlayerKnows(hero))
                {
                    ordered.Add(hero);
                }
                else
                {
                    unknown.Add(hero);
                }
            }
            ordered.AddRange(unknown);

            foreach (var hero in ordered)
            {
                Practitioners.Add(new SotorPractitionerVM(hero, SotorPractitionerVM.PlayerKnows(hero)));
            }

            CountText = ordered.Count == 0
                ? (_tradition.IsMemberOnly
                    ? SotorText.GetObject("sotor_enc_traditions_none_known_hidden")
                    : SotorText.GetObject("sotor_enc_traditions_none_known")).ToString()
                : string.Empty;
        }

        private string BuildStandingText()
        {
            if (!SotorSettings.EnableRivalCasters || !SotorRivalStanding.IsReady) return string.Empty;
            if (_tradition == null || _tradition.Tradition == Trad.None) return string.Empty;

            int standing = SotorRivalStanding.GetTradition(_tradition.Tradition);

            string bandId = standing >= SotorTeachingLogic.RespectedStanding
                ? "sotor_enc_standing_band_respected"
                : (standing <= SotorTeachingLogic.DespisedStanding
                    ? "sotor_enc_standing_band_despised"
                    : "sotor_enc_standing_band_neutral");

            var line = SotorText.GetObject("sotor_enc_standing");
            line.SetTextVariable("VALUE", standing);
            line.SetTextVariable("BAND", SotorText.GetObject(bandId));
            return line.ToString();
        }

        public override string GetName() => _titleText ?? "";

        public override string GetNavigationBarURL()
        {
            string home = HyperlinkTexts.GetGenericHyperlinkText(
                "Home", GameTexts.FindText("str_encyclopedia_home").ToString());
            string traditionsLeg = HyperlinkTexts.GetGenericHyperlinkText(
                "ListPage-" + SotorTraditionEncyclopediaPage.PageIdentifier,
                SotorText.GetObject("sotor_enc_traditions_title").ToString());
            return home + " \\ " + traditionsLeg + " \\ " + (_titleText ?? "");
        }
    }
}
