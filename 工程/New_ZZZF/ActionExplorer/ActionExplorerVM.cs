using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// M1 ViewModel：100 个测试 Action → 分页 → 每页 20 个槽位 → 当前选择。
    /// 分页由 VM 控制（每次 RefreshPage 仅保留当前页 20 个 ActionItemVM）。
    /// </summary>
    public class ActionExplorerVM : ViewModel
    {
        private const int TotalActions = 100;
        private const int PageSize = 20;

        private readonly List<ActionInfo> _allActions = new List<ActionInfo>();
        private int _currentPage = 0; // 0-based
        private int _selectedGlobalIndex = -1;

        private string _statusText = "M1 UI READY";
        private string _pageText = "1 / 5";
        private string _selectedActionText = "请选择 Action";
        private bool _canPreviousPage;
        private bool _canNextPage;

        public MBBindingList<ActionItemVM> ActionItems { get; } = new MBBindingList<ActionItemVM>();

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        }

        [DataSourceProperty]
        public string PageText
        {
            get => _pageText;
            set { if (_pageText != value) { _pageText = value; OnPropertyChanged(nameof(PageText)); } }
        }

        [DataSourceProperty]
        public string SelectedActionText
        {
            get => _selectedActionText;
            set { if (_selectedActionText != value) { _selectedActionText = value; OnPropertyChanged(nameof(SelectedActionText)); } }
        }

        [DataSourceProperty]
        public bool CanPreviousPage
        {
            get => _canPreviousPage;
            set { if (_canPreviousPage != value) { _canPreviousPage = value; OnPropertyChanged(nameof(CanPreviousPage)); } }
        }

        [DataSourceProperty]
        public bool CanNextPage
        {
            get => _canNextPage;
            set { if (_canNextPage != value) { _canNextPage = value; OnPropertyChanged(nameof(CanNextPage)); } }
        }

        public ActionExplorerVM()
        {
            M0_Probe.M0Log.Lifecycle("M1", "VM_CREATE");

            for (int i = 0; i < TotalActions; i++)
            {
                _allActions.Add(new ActionInfo(i.ToString(), "Action " + (i + 1).ToString("D3")));
            }

            RefreshPage();
            M0_Probe.M0Log.Lifecycle("M1", "PAGE_CREATED");
        }

        /// <summary> 根据当前页填充 ActionItems（永远只有当前页 20 个 VM）。 </summary>
        private void RefreshPage()
        {
            int start = _currentPage * PageSize;
            ActionItems.Clear();

            for (int i = 0; i < PageSize; i++)
            {
                int globalIndex = start + i;
                if (globalIndex >= _allActions.Count)
                    break;

                ActionInfo info = _allActions[globalIndex];
                ActionItemVM item = new ActionItemVM(info.Name, globalIndex, true, OnItemSelect);
                item.IsSelected = (globalIndex == _selectedGlobalIndex);
                ActionItems.Add(item);
            }

            int totalPages = (TotalActions + PageSize - 1) / PageSize;
            PageText = (_currentPage + 1).ToString() + " / " + totalPages;
            CanPreviousPage = _currentPage > 0;
            CanNextPage = _currentPage < totalPages - 1;

            M0_Probe.M0Log.Info("PAGE_REFRESH page=" + (_currentPage + 1) + " items=" + ActionItems.Count);
        }

        /// <summary> ActionItemVM.ExecuteSelect 回调。 </summary>
        private void OnItemSelect(int globalIndex)
        {
            SelectAction(globalIndex);
        }

        public void SelectAction(int globalIndex)
        {
            _selectedGlobalIndex = globalIndex;
            ActionInfo info = _allActions[globalIndex];
            SelectedActionText = info.Name;
            StatusText = "SELECTED: " + info.Name;

            foreach (ActionItemVM vm in ActionItems)
                vm.IsSelected = (vm.GlobalIndex == globalIndex);

            M0_Probe.M0Log.Info("ACTION_SELECTED index=" + globalIndex + " name=" + info.Name);
        }

        public void ExecutePreviousPage()
        {
            if (_currentPage <= 0)
                return;
            _currentPage--;
            RefreshPage();
            M0_Probe.M0Log.Info("PREV_PAGE -> " + (_currentPage + 1));
        }

        public void ExecuteNextPage()
        {
            int totalPages = (TotalActions + PageSize - 1) / PageSize;
            if (_currentPage >= totalPages - 1)
                return;
            _currentPage++;
            RefreshPage();
            M0_Probe.M0Log.Info("NEXT_PAGE -> " + (_currentPage + 1));
        }

        public void ExecuteClose()
        {
            M0_Probe.M0Log.Lifecycle("M1", "CLOSE_CMD");
            CloseRequested?.Invoke();
        }

        public event Action CloseRequested;

        public override void OnFinalize()
        {
            base.OnFinalize();
            ActionItems.Clear();
            CloseRequested = null;
        }

        // ===== 固定 4×5 格子的 20 个点击入口（第一页全局索引 0~19）=====
        public void ExecuteSelect0()  => SelectAction(0);
        public void ExecuteSelect1()  => SelectAction(1);
        public void ExecuteSelect2()  => SelectAction(2);
        public void ExecuteSelect3()  => SelectAction(3);
        public void ExecuteSelect4()  => SelectAction(4);
        public void ExecuteSelect5()  => SelectAction(5);
        public void ExecuteSelect6()  => SelectAction(6);
        public void ExecuteSelect7()  => SelectAction(7);
        public void ExecuteSelect8()  => SelectAction(8);
        public void ExecuteSelect9()  => SelectAction(9);
        public void ExecuteSelect10() => SelectAction(10);
        public void ExecuteSelect11() => SelectAction(11);
        public void ExecuteSelect12() => SelectAction(12);
        public void ExecuteSelect13() => SelectAction(13);
        public void ExecuteSelect14() => SelectAction(14);
        public void ExecuteSelect15() => SelectAction(15);
        public void ExecuteSelect16() => SelectAction(16);
        public void ExecuteSelect17() => SelectAction(17);
        public void ExecuteSelect18() => SelectAction(18);
        public void ExecuteSelect19() => SelectAction(19);
    }
}
