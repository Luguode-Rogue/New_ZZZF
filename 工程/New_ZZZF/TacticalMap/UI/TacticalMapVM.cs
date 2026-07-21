using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 小地图界面的轻量 ViewModel（后续可由 MCM 接管显示选项）。
    /// </summary>
    public sealed class TacticalMapVM : ViewModel
    {
        private string _title = "战术地图 (RTS Minimap)";
        private bool _showRisk = true;

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set { if (value != _title) { _title = value; OnPropertyChanged("Title"); } }
        }

        [DataSourceProperty]
        public bool ShowRisk
        {
            get => _showRisk;
            set { if (value != _showRisk) { _showRisk = value; OnPropertyChanged("ShowRisk"); } }
        }
    }
}
