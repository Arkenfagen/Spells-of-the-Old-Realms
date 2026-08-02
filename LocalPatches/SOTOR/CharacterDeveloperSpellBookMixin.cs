using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SOTOR
{
    [ViewModelMixin("RefreshValues")]
    public class CharacterDeveloperSpellBookMixin : BaseViewModelMixin<CharacterDeveloperVM>
    {
        private bool _isSpellBookButtonVisible;

        public CharacterDeveloperSpellBookMixin(CharacterDeveloperVM vm)
            : base(vm)
        {
            RefreshSpellBookButtonVisibility();
        }

        [DataSourceProperty]
        public bool IsSpellBookButtonVisible
        {
            get => _isSpellBookButtonVisible;
            set
            {
                if (value == _isSpellBookButtonVisible)
                {
                    return;
                }

                _isSpellBookButtonVisible = value;
                OnPropertyChangedWithValue(value, nameof(IsSpellBookButtonVisible));
            }
        }

        [DataSourceMethod]
        public void ExecuteOpenSpellBook()
        {
            if (Campaign.Current == null || Game.Current == null)
            {
                return;
            }

            var state = Game.Current.GameStateManager.CreateState<SotorSpellBookState>();
            Game.Current.GameStateManager.PushState(state, 0);
        }

        public override void OnRefresh()
        {
            RefreshSpellBookButtonVisibility();
        }

        private void RefreshSpellBookButtonVisibility()
        {
            IsSpellBookButtonVisible = Hero.MainHero != null;
        }
    }
}
