using System.Collections.Generic;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using Helpers;
using SOTOR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Localization;

namespace SOTOR.AbilitySystem.Rivals
{

    [ViewModelMixin("RefreshValues")]
    public class HeroEncyclopediaTraditionMixin : BaseViewModelMixin<EncyclopediaHeroPageVM>
    {

        private static readonly string RowLabel = SotorText.GetObject("sotor_enc_hero_tradition_label").ToString() + ": ";

        public HeroEncyclopediaTraditionMixin(EncyclopediaHeroPageVM vm)
            : base(vm)
        {
            AddTraditionRow();
        }

        public override void OnRefresh()
        {
            AddTraditionRow();
        }

        private void AddTraditionRow()
        {
            try
            {
                var vm = ViewModel;
                if (vm == null || vm.Stats == null) return;

                foreach (var existing in vm.Stats.ToList())
                {
                    if (existing != null && existing.Definition == RowLabel)
                    {
                        vm.Stats.Remove(existing);
                    }
                }

                if (!SotorSettings.EnableRivalCasters) return;

                var hero = vm.Obj as Hero;
                if (hero == null || !hero.IsAbilityUser()) return;

                if (!SotorPractitionerVM.PlayerKnows(hero)) return;

                var trad = SotorRivalSeeder.SocialTradition(hero);
                if (trad == Trad.None) return;

                if (SotorTraditions.IsMemberOnly(trad) && !SotorRivalReveal.IsRevealed(hero)) return;

                var tradition = SotorTraditionObject.For(trad);
                if (tradition == null) return;

                var link = HyperlinkTexts.GetSettlementHyperlinkText(tradition.EncyclopediaLink, tradition.Name);
                vm.Stats.Add(new StringPairItemVM(RowLabel, link.ToString(), null));
            }
            catch (System.Exception ex)
            {

                SotorLog.Error($"Encyclopedia: hero tradition row failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
