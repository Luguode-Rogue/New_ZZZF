using TaleWorlds.Library;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// 单个 Action 卡片 VM。对应 ItemTemplate 里的一个 Button。
    /// </summary>
    public class ActionItemVM : ViewModel
    {
        private readonly string _name;
        private readonly int _globalIndex;
        private readonly System.Action<int> _onSelect;
        private bool _isEnabled;
        private bool _isSelected;

        public ActionItemVM(string name, int globalIndex, bool isEnabled, System.Action<int> onSelect)
        {
            _name = name;
            _globalIndex = globalIndex;
            _isEnabled = isEnabled;
            _onSelect = onSelect;
        }

        [DataSourceProperty]
        public string Name => _name;

        [DataSourceProperty]
        public int GlobalIndex => _globalIndex;

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); } }
        }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } }
        }

        /// <summary> ItemTemplate 的 ButtonWidget Command.Click 绑定到这个方法。 </summary>
        public void ExecuteSelect()
        {
            if (!_isEnabled)
                return;
            M0_Probe.M0Log.Info("ITEM_CLICK index=" + _globalIndex);
            _onSelect?.Invoke(_globalIndex);
        }
    }
}
