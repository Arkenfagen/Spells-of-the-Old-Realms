using TaleWorlds.Library;

namespace SOTOR.AbilitySystem
{

    public class ProjectileCrosshair_VM : ViewModel
    {

        private string _spriteName = "test_spell_crosshair";
        private bool _isVisible;

        [DataSourceProperty]
        public string SpriteName
        {
            get => _spriteName;
            set
            {
                if (value != _spriteName)
                {
                    _spriteName = value;
                    OnPropertyChangedWithValue(value, nameof(SpriteName));
                }
            }
        }

        [DataSourceProperty]
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (value != _isVisible)
                {
                    _isVisible = value;
                    OnPropertyChangedWithValue(value, nameof(IsVisible));
                }
            }
        }
    }
}
