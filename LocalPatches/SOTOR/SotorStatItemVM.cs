using TaleWorlds.Library;

namespace SOTOR
{

    public class SotorStatItemVM : ViewModel
    {
        private string _label;
        private string _value;
        private string _iconSprite = "";

        public SotorStatItemVM(string label, string value)
        {
            _label = label;
            _value = value;
        }

        public SotorStatItemVM(string label, string value, string iconSprite)
        {
            _label = label;
            _value = value;
            _iconSprite = iconSprite ?? "";
        }

        [DataSourceProperty]
        public string Label
        {
            get => _label;
            set
            {
                if (value == _label)
                {
                    return;
                }

                _label = value;
                OnPropertyChangedWithValue(value, nameof(Label));
            }
        }

        [DataSourceProperty]
        public string Value
        {
            get => _value;
            set
            {
                if (value == _value)
                {
                    return;
                }

                _value = value;
                OnPropertyChangedWithValue(value, nameof(Value));
            }
        }

        [DataSourceProperty]
        public string IconSprite
        {
            get => _iconSprite;
            set
            {
                if (value == _iconSprite)
                {
                    return;
                }

                _iconSprite = value;
                OnPropertyChangedWithValue(value, nameof(IconSprite));
                OnPropertyChanged(nameof(HasIcon));
            }
        }

        [DataSourceProperty]
        public bool HasIcon => !string.IsNullOrEmpty(_iconSprite);
    }
}
