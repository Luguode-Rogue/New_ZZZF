using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace New_ZZZF.ActionExplorer
{
    /// <summary>
    /// Action Explorer M1/M2 UI ViewModel。
    ///
    /// 当前架构：
    ///     20 个固定 UI 格子
    ///     ↓
    ///     VM 的 Slot01 ~ Slot20
    ///     ↓
    ///     当前页 Action
    ///
    /// 不再使用 GridWidget / ItemTemplate / MBBindingList。
    ///
    /// 当前阶段：
    ///     100 个测试 Action
    ///
    /// 后续阶段：
    ///     将 CreateTestActions() 替换为真实 Action 数据扫描。
    /// </summary>
    public class ActionExplorerVM : ViewModel
    {
        public const int PageSize = 20;

        private const int TotalTestActions = 100;

        private readonly List<ActionInfo> _allActions;

        private int _currentPage;
        private int _totalPages;
        private int _selectedGlobalIndex = -1;

        private string _statusText;
        private string _pageText;
        private string _selectedActionText;

        // =========================================================
        // M6：Preview 绑定属性
        //     PreviewActionId  -> 完整 act_xxx，驱动 Tableau 播放
        //     PreviewShouldLoop -> 循环播放（M6 第一阶段开启）
        // =========================================================

        private string _selectedActionId = "";
        private string _previewActionId = "";
        private bool _previewShouldLoop = true;
        private string _previewBodyProperties = "";

        // M6：预览适配层（VM 不直接操作播放 / Agent / Camera）
        private readonly ActionPreview _preview;

        private bool _canPreviousPage;
        private bool _canNextPage;

        // =========================================================
        // Slot 01
        // =========================================================

        private string _slot01Text = "";
        private bool _slot01Selected;

        [DataSourceProperty]
        public string Slot01Text
        {
            get { return _slot01Text; }
            set
            {
                if (_slot01Text == value)
                    return;

                _slot01Text = value;
                OnPropertyChanged(nameof(Slot01Text));
            }
        }

        [DataSourceProperty]
        public bool Slot01Selected
        {
            get { return _slot01Selected; }
            set
            {
                if (_slot01Selected == value)
                    return;

                _slot01Selected = value;
                OnPropertyChanged(nameof(Slot01Selected));
            }
        }

        // =========================================================
        // Slot 02
        // =========================================================

        private string _slot02Text = "";
        private bool _slot02Selected;

        [DataSourceProperty]
        public string Slot02Text
        {
            get { return _slot02Text; }
            set
            {
                if (_slot02Text == value)
                    return;

                _slot02Text = value;
                OnPropertyChanged(nameof(Slot02Text));
            }
        }

        [DataSourceProperty]
        public bool Slot02Selected
        {
            get { return _slot02Selected; }
            set
            {
                if (_slot02Selected == value)
                    return;

                _slot02Selected = value;
                OnPropertyChanged(nameof(Slot02Selected));
            }
        }

        // =========================================================
        // Slot 03
        // =========================================================

        private string _slot03Text = "";
        private bool _slot03Selected;

        [DataSourceProperty]
        public string Slot03Text
        {
            get { return _slot03Text; }
            set
            {
                if (_slot03Text == value)
                    return;

                _slot03Text = value;
                OnPropertyChanged(nameof(Slot03Text));
            }
        }

        [DataSourceProperty]
        public bool Slot03Selected
        {
            get { return _slot03Selected; }
            set
            {
                if (_slot03Selected == value)
                    return;

                _slot03Selected = value;
                OnPropertyChanged(nameof(Slot03Selected));
            }
        }

        // =========================================================
        // Slot 04
        // =========================================================

        private string _slot04Text = "";
        private bool _slot04Selected;

        [DataSourceProperty]
        public string Slot04Text
        {
            get { return _slot04Text; }
            set
            {
                if (_slot04Text == value)
                    return;

                _slot04Text = value;
                OnPropertyChanged(nameof(Slot04Text));
            }
        }

        [DataSourceProperty]
        public bool Slot04Selected
        {
            get { return _slot04Selected; }
            set
            {
                if (_slot04Selected == value)
                    return;

                _slot04Selected = value;
                OnPropertyChanged(nameof(Slot04Selected));
            }
        }

        // =========================================================
        // Slot 05
        // =========================================================

        private string _slot05Text = "";
        private bool _slot05Selected;

        [DataSourceProperty]
        public string Slot05Text
        {
            get { return _slot05Text; }
            set
            {
                if (_slot05Text == value)
                    return;

                _slot05Text = value;
                OnPropertyChanged(nameof(Slot05Text));
            }
        }

        [DataSourceProperty]
        public bool Slot05Selected
        {
            get { return _slot05Selected; }
            set
            {
                if (_slot05Selected == value)
                    return;

                _slot05Selected = value;
                OnPropertyChanged(nameof(Slot05Selected));
            }
        }

        // =========================================================
        // Slot 06
        // =========================================================

        private string _slot06Text = "";
        private bool _slot06Selected;

        [DataSourceProperty]
        public string Slot06Text
        {
            get { return _slot06Text; }
            set
            {
                if (_slot06Text == value)
                    return;

                _slot06Text = value;
                OnPropertyChanged(nameof(Slot06Text));
            }
        }

        [DataSourceProperty]
        public bool Slot06Selected
        {
            get { return _slot06Selected; }
            set
            {
                if (_slot06Selected == value)
                    return;

                _slot06Selected = value;
                OnPropertyChanged(nameof(Slot06Selected));
            }
        }

        // =========================================================
        // Slot 07
        // =========================================================

        private string _slot07Text = "";
        private bool _slot07Selected;

        [DataSourceProperty]
        public string Slot07Text
        {
            get { return _slot07Text; }
            set
            {
                if (_slot07Text == value)
                    return;

                _slot07Text = value;
                OnPropertyChanged(nameof(Slot07Text));
            }
        }

        [DataSourceProperty]
        public bool Slot07Selected
        {
            get { return _slot07Selected; }
            set
            {
                if (_slot07Selected == value)
                    return;

                _slot07Selected = value;
                OnPropertyChanged(nameof(Slot07Selected));
            }
        }

        // =========================================================
        // Slot 08
        // =========================================================

        private string _slot08Text = "";
        private bool _slot08Selected;

        [DataSourceProperty]
        public string Slot08Text
        {
            get { return _slot08Text; }
            set
            {
                if (_slot08Text == value)
                    return;

                _slot08Text = value;
                OnPropertyChanged(nameof(Slot08Text));
            }
        }

        [DataSourceProperty]
        public bool Slot08Selected
        {
            get { return _slot08Selected; }
            set
            {
                if (_slot08Selected == value)
                    return;

                _slot08Selected = value;
                OnPropertyChanged(nameof(Slot08Selected));
            }
        }

        // =========================================================
        // Slot 09
        // =========================================================

        private string _slot09Text = "";
        private bool _slot09Selected;

        [DataSourceProperty]
        public string Slot09Text
        {
            get { return _slot09Text; }
            set
            {
                if (_slot09Text == value)
                    return;

                _slot09Text = value;
                OnPropertyChanged(nameof(Slot09Text));
            }
        }

        [DataSourceProperty]
        public bool Slot09Selected
        {
            get { return _slot09Selected; }
            set
            {
                if (_slot09Selected == value)
                    return;

                _slot09Selected = value;
                OnPropertyChanged(nameof(Slot09Selected));
            }
        }

        // =========================================================
        // Slot 10
        // =========================================================

        private string _slot10Text = "";
        private bool _slot10Selected;

        [DataSourceProperty]
        public string Slot10Text
        {
            get { return _slot10Text; }
            set
            {
                if (_slot10Text == value)
                    return;

                _slot10Text = value;
                OnPropertyChanged(nameof(Slot10Text));
            }
        }

        [DataSourceProperty]
        public bool Slot10Selected
        {
            get { return _slot10Selected; }
            set
            {
                if (_slot10Selected == value)
                    return;

                _slot10Selected = value;
                OnPropertyChanged(nameof(Slot10Selected));
            }
        }

        // =========================================================
        // Slot 11
        // =========================================================

        private string _slot11Text = "";
        private bool _slot11Selected;

        [DataSourceProperty]
        public string Slot11Text
        {
            get { return _slot11Text; }
            set
            {
                if (_slot11Text == value)
                    return;

                _slot11Text = value;
                OnPropertyChanged(nameof(Slot11Text));
            }
        }

        [DataSourceProperty]
        public bool Slot11Selected
        {
            get { return _slot11Selected; }
            set
            {
                if (_slot11Selected == value)
                    return;

                _slot11Selected = value;
                OnPropertyChanged(nameof(Slot11Selected));
            }
        }

        // =========================================================
        // Slot 12
        // =========================================================

        private string _slot12Text = "";
        private bool _slot12Selected;

        [DataSourceProperty]
        public string Slot12Text
        {
            get { return _slot12Text; }
            set
            {
                if (_slot12Text == value)
                    return;

                _slot12Text = value;
                OnPropertyChanged(nameof(Slot12Text));
            }
        }

        [DataSourceProperty]
        public bool Slot12Selected
        {
            get { return _slot12Selected; }
            set
            {
                if (_slot12Selected == value)
                    return;

                _slot12Selected = value;
                OnPropertyChanged(nameof(Slot12Selected));
            }
        }

        // =========================================================
        // Slot 13
        // =========================================================

        private string _slot13Text = "";
        private bool _slot13Selected;

        [DataSourceProperty]
        public string Slot13Text
        {
            get { return _slot13Text; }
            set
            {
                if (_slot13Text == value)
                    return;

                _slot13Text = value;
                OnPropertyChanged(nameof(Slot13Text));
            }
        }

        [DataSourceProperty]
        public bool Slot13Selected
        {
            get { return _slot13Selected; }
            set
            {
                if (_slot13Selected == value)
                    return;

                _slot13Selected = value;
                OnPropertyChanged(nameof(Slot13Selected));
            }
        }

        // =========================================================
        // Slot 14
        // =========================================================

        private string _slot14Text = "";
        private bool _slot14Selected;

        [DataSourceProperty]
        public string Slot14Text
        {
            get { return _slot14Text; }
            set
            {
                if (_slot14Text == value)
                    return;

                _slot14Text = value;
                OnPropertyChanged(nameof(Slot14Text));
            }
        }

        [DataSourceProperty]
        public bool Slot14Selected
        {
            get { return _slot14Selected; }
            set
            {
                if (_slot14Selected == value)
                    return;

                _slot14Selected = value;
                OnPropertyChanged(nameof(Slot14Selected));
            }
        }

        // =========================================================
        // Slot 15
        // =========================================================

        private string _slot15Text = "";
        private bool _slot15Selected;

        [DataSourceProperty]
        public string Slot15Text
        {
            get { return _slot15Text; }
            set
            {
                if (_slot15Text == value)
                    return;

                _slot15Text = value;
                OnPropertyChanged(nameof(Slot15Text));
            }
        }

        [DataSourceProperty]
        public bool Slot15Selected
        {
            get { return _slot15Selected; }
            set
            {
                if (_slot15Selected == value)
                    return;

                _slot15Selected = value;
                OnPropertyChanged(nameof(Slot15Selected));
            }
        }

        // =========================================================
        // Slot 16
        // =========================================================

        private string _slot16Text = "";
        private bool _slot16Selected;

        [DataSourceProperty]
        public string Slot16Text
        {
            get { return _slot16Text; }
            set
            {
                if (_slot16Text == value)
                    return;

                _slot16Text = value;
                OnPropertyChanged(nameof(Slot16Text));
            }
        }

        [DataSourceProperty]
        public bool Slot16Selected
        {
            get { return _slot16Selected; }
            set
            {
                if (_slot16Selected == value)
                    return;

                _slot16Selected = value;
                OnPropertyChanged(nameof(Slot16Selected));
            }
        }

        // =========================================================
        // Slot 17
        // =========================================================

        private string _slot17Text = "";
        private bool _slot17Selected;

        [DataSourceProperty]
        public string Slot17Text
        {
            get { return _slot17Text; }
            set
            {
                if (_slot17Text == value)
                    return;

                _slot17Text = value;
                OnPropertyChanged(nameof(Slot17Text));
            }
        }

        [DataSourceProperty]
        public bool Slot17Selected
        {
            get { return _slot17Selected; }
            set
            {
                if (_slot17Selected == value)
                    return;

                _slot17Selected = value;
                OnPropertyChanged(nameof(Slot17Selected));
            }
        }

        // =========================================================
        // Slot 18
        // =========================================================

        private string _slot18Text = "";
        private bool _slot18Selected;

        [DataSourceProperty]
        public string Slot18Text
        {
            get { return _slot18Text; }
            set
            {
                if (_slot18Text == value)
                    return;

                _slot18Text = value;
                OnPropertyChanged(nameof(Slot18Text));
            }
        }

        [DataSourceProperty]
        public bool Slot18Selected
        {
            get { return _slot18Selected; }
            set
            {
                if (_slot18Selected == value)
                    return;

                _slot18Selected = value;
                OnPropertyChanged(nameof(Slot18Selected));
            }
        }

        // =========================================================
        // Slot 19
        // =========================================================

        private string _slot19Text = "";
        private bool _slot19Selected;

        [DataSourceProperty]
        public string Slot19Text
        {
            get { return _slot19Text; }
            set
            {
                if (_slot19Text == value)
                    return;

                _slot19Text = value;
                OnPropertyChanged(nameof(Slot19Text));
            }
        }

        [DataSourceProperty]
        public bool Slot19Selected
        {
            get { return _slot19Selected; }
            set
            {
                if (_slot19Selected == value)
                    return;

                _slot19Selected = value;
                OnPropertyChanged(nameof(Slot19Selected));
            }
        }

        // =========================================================
        // Slot 20
        // =========================================================

        private string _slot20Text = "";
        private bool _slot20Selected;

        [DataSourceProperty]
        public string Slot20Text
        {
            get { return _slot20Text; }
            set
            {
                if (_slot20Text == value)
                    return;

                _slot20Text = value;
                OnPropertyChanged(nameof(Slot20Text));
            }
        }

        [DataSourceProperty]
        public bool Slot20Selected
        {
            get { return _slot20Selected; }
            set
            {
                if (_slot20Selected == value)
                    return;

                _slot20Selected = value;
                OnPropertyChanged(nameof(Slot20Selected));
            }
        }

        // =========================================================
        // 通用 UI
        // =========================================================

        public ActionExplorerVM()
        {
            M0_Probe.M0Log.Lifecycle("M4", "VM_CREATE");

            // =========================================================
            // M4：使用 ActionDatabase 的真实 Action 数据（替换测试数据）
            // 只改数据源，固定 20 格 / Slot / 选择 / XML 全部不动。
            // =========================================================

            ActionDatabase.EnsureInitialized();

            M0_Probe.M0Log.Info(
                "M4_VM_DATABASE count=" + ActionDatabase.Count);

            // 复制一份，VM 拥有自己的 _allActions，
            // 以后加搜索/过滤/排序时不会破坏数据库原始数据。
            _allActions = new List<ActionInfo>(ActionDatabase.Actions);

            _totalPages =
                (_allActions.Count + PageSize - 1) / PageSize;

            if (_totalPages < 1)
                _totalPages = 1;

            _currentPage = 0;

            _statusText = "ACTION EXPLORER READY";
            _pageText = "1 / " + _totalPages;
            _selectedActionText = "请选择 Action";

            // M6：初始化预览适配层（渲染由 XML 的 Tableau 完成）
            _preview = new ActionPreview();

            // M6：提供预览角色身体属性。
            // CharacterTableau 内部 _agentVisuals.SetVisible(_bodyProperties != BodyProperties.Default)
            // 必须给有效 BodyProperties 否则角色被隐藏 -> 黑屏。
            // 取 Hero.MainHero（战役中必存在）；若战役外则回退到一个固定有效 code。
            try
            {
                if (Hero.MainHero != null && Hero.MainHero.BodyProperties != null)
                {
                    PreviewBodyProperties = Hero.MainHero.BodyProperties.ToString();
                }
                else
                {
                    // 回退：典型男性英雄 body properties code
                    PreviewBodyProperties = "0.000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000";
                }
                M0_Probe.M0Log.Lifecycle(
                    "M6",
                    "PREVIEW_BODY_PROPS set len=" + PreviewBodyProperties.Length);
            }
            catch (System.Exception ex)
            {
                M0_Probe.M0Log.Warn("PREVIEW_BODY_PROPS failed: " + ex);
            }

            RefreshPage();

            M0_Probe.M0Log.Lifecycle(
                "M4",
                "VM_READY actions=" + _allActions.Count +
                " pages=" + _totalPages);

            M0_Probe.M0Log.Lifecycle(
                "M6",
                "VM_READY preview=tableau");
        }

        [DataSourceProperty]
        public string StatusText
        {
            get { return _statusText; }
            set
            {
                if (_statusText == value)
                    return;

                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }

        [DataSourceProperty]
        public string PageText
        {
            get { return _pageText; }
            set
            {
                if (_pageText == value)
                    return;

                _pageText = value;
                OnPropertyChanged(nameof(PageText));
            }
        }

        [DataSourceProperty]
        public string SelectedActionText
        {
            get { return _selectedActionText; }
            set
            {
                if (_selectedActionText == value)
                    return;

                _selectedActionText = value;
                OnPropertyChanged(nameof(SelectedActionText));
            }
        }

        // =========================================================
        // M6：完整 Action ID（act_xxx），调试用
        // =========================================================

        [DataSourceProperty]
        public string SelectedActionId
        {
            get { return _selectedActionId; }
            set
            {
                if (_selectedActionId == value)
                    return;

                _selectedActionId = value;
                OnPropertyChanged(nameof(SelectedActionId));
            }
        }

        // =========================================================
        // M6：Preview 播放驱动属性
        //
        // 绑定到 XML 中 CharacterTableauWidget 的
        // CustomAnimation / ShouldLoopCustomAnimation。
        // 只传完整 act_xxx，由内置 Tableau 渲染播放。
        // =========================================================

        [DataSourceProperty]
        public string PreviewActionId
        {
            get { return _previewActionId; }
            set
            {
                if (_previewActionId == value)
                    return;

                _previewActionId = value;
                OnPropertyChanged(nameof(PreviewActionId));
            }
        }

        [DataSourceProperty]
        public bool PreviewShouldLoop
        {
            get { return _previewShouldLoop; }
            set
            {
                if (_previewShouldLoop == value)
                    return;

                _previewShouldLoop = value;
                OnPropertyChanged(nameof(PreviewShouldLoop));
            }
        }

        // =========================================================
        // M6：预览角色身体属性（BodyProperties code）
        //
        // 关键修复：CharacterTableau 内部角色可见性判断是
        //     _agentVisuals.SetVisible(_bodyProperties != BodyProperties.Default)
        // 若不提供 BodyProperties，角色被 SetVisible(false) -> 黑屏。
        // 这里默认取 Hero.MainHero 的身体属性 code，
        // 保证 Tableau 内有有效角色可渲染。
        // =========================================================

        [DataSourceProperty]
        public string PreviewBodyProperties
        {
            get { return _previewBodyProperties; }
            set
            {
                if (_previewBodyProperties == value)
                    return;

                _previewBodyProperties = value;
                OnPropertyChanged(nameof(PreviewBodyProperties));
            }
        }

        [DataSourceProperty]
        public bool CanPreviousPage
        {
            get { return _canPreviousPage; }
            set
            {
                if (_canPreviousPage == value)
                    return;

                _canPreviousPage = value;
                OnPropertyChanged(nameof(CanPreviousPage));
            }
        }

        [DataSourceProperty]
        public bool CanNextPage
        {
            get { return _canNextPage; }
            set
            {
                if (_canNextPage == value)
                    return;

                _canNextPage = value;
                OnPropertyChanged(nameof(CanNextPage));
            }
        }

        // =========================================================
        // 页面刷新
        // =========================================================

        private void RefreshPage()
        {
            int startIndex = _currentPage * PageSize;

            ClearSlots();

            for (int i = 0; i < PageSize; i++)
            {
                int globalIndex = startIndex + i;

                if (globalIndex >= _allActions.Count)
                    break;

                ActionInfo action = _allActions[globalIndex];

                SetSlot(
                    i + 1,
                    action.Name,
                    globalIndex == _selectedGlobalIndex);
            }

            PageText =
                (_currentPage + 1) +
                " / " +
                _totalPages;

            CanPreviousPage =
                _currentPage > 0;

            CanNextPage =
                _currentPage < _totalPages - 1;

            M0_Probe.M0Log.Info(
                "M4_PAGE_REFRESH page=" +
                (_currentPage + 1) +
                " start=" +
                startIndex);
        }

        // =========================================================
        // 清空 20 个格子
        // =========================================================

        private void ClearSlots()
        {
            Slot01Text = "";
            Slot02Text = "";
            Slot03Text = "";
            Slot04Text = "";
            Slot05Text = "";
            Slot06Text = "";
            Slot07Text = "";
            Slot08Text = "";
            Slot09Text = "";
            Slot10Text = "";
            Slot11Text = "";
            Slot12Text = "";
            Slot13Text = "";
            Slot14Text = "";
            Slot15Text = "";
            Slot16Text = "";
            Slot17Text = "";
            Slot18Text = "";
            Slot19Text = "";
            Slot20Text = "";

            Slot01Selected = false;
            Slot02Selected = false;
            Slot03Selected = false;
            Slot04Selected = false;
            Slot05Selected = false;
            Slot06Selected = false;
            Slot07Selected = false;
            Slot08Selected = false;
            Slot09Selected = false;
            Slot10Selected = false;
            Slot11Selected = false;
            Slot12Selected = false;
            Slot13Selected = false;
            Slot14Selected = false;
            Slot15Selected = false;
            Slot16Selected = false;
            Slot17Selected = false;
            Slot18Selected = false;
            Slot19Selected = false;
            Slot20Selected = false;
        }

        // =========================================================
        // 设置 Slot
        // =========================================================

        private void SetSlot(
            int slot,
            string text,
            bool selected)
        {
            switch (slot)
            {
                case 1:
                    Slot01Text = text;
                    Slot01Selected = selected;
                    break;

                case 2:
                    Slot02Text = text;
                    Slot02Selected = selected;
                    break;

                case 3:
                    Slot03Text = text;
                    Slot03Selected = selected;
                    break;

                case 4:
                    Slot04Text = text;
                    Slot04Selected = selected;
                    break;

                case 5:
                    Slot05Text = text;
                    Slot05Selected = selected;
                    break;

                case 6:
                    Slot06Text = text;
                    Slot06Selected = selected;
                    break;

                case 7:
                    Slot07Text = text;
                    Slot07Selected = selected;
                    break;

                case 8:
                    Slot08Text = text;
                    Slot08Selected = selected;
                    break;

                case 9:
                    Slot09Text = text;
                    Slot09Selected = selected;
                    break;

                case 10:
                    Slot10Text = text;
                    Slot10Selected = selected;
                    break;

                case 11:
                    Slot11Text = text;
                    Slot11Selected = selected;
                    break;

                case 12:
                    Slot12Text = text;
                    Slot12Selected = selected;
                    break;

                case 13:
                    Slot13Text = text;
                    Slot13Selected = selected;
                    break;

                case 14:
                    Slot14Text = text;
                    Slot14Selected = selected;
                    break;

                case 15:
                    Slot15Text = text;
                    Slot15Selected = selected;
                    break;

                case 16:
                    Slot16Text = text;
                    Slot16Selected = selected;
                    break;

                case 17:
                    Slot17Text = text;
                    Slot17Selected = selected;
                    break;

                case 18:
                    Slot18Text = text;
                    Slot18Selected = selected;
                    break;

                case 19:
                    Slot19Text = text;
                    Slot19Selected = selected;
                    break;

                case 20:
                    Slot20Text = text;
                    Slot20Selected = selected;
                    break;
            }
        }

        // =========================================================
        // 点击 Slot
        //
        // XML：
        // Command.Click="@ExecuteSelect01"
        // ...
        // Command.Click="@ExecuteSelect20"
        // =========================================================

        public void ExecuteSelect01()
        {
            SelectSlot(1);
        }

        public void ExecuteSelect02()
        {
            SelectSlot(2);
        }

        public void ExecuteSelect03()
        {
            SelectSlot(3);
        }

        public void ExecuteSelect04()
        {
            SelectSlot(4);
        }

        public void ExecuteSelect05()
        {
            SelectSlot(5);
        }

        public void ExecuteSelect06()
        {
            SelectSlot(6);
        }

        public void ExecuteSelect07()
        {
            SelectSlot(7);
        }

        public void ExecuteSelect08()
        {
            SelectSlot(8);
        }

        public void ExecuteSelect09()
        {
            SelectSlot(9);
        }

        public void ExecuteSelect10()
        {
            SelectSlot(10);
        }

        public void ExecuteSelect11()
        {
            SelectSlot(11);
        }

        public void ExecuteSelect12()
        {
            SelectSlot(12);
        }

        public void ExecuteSelect13()
        {
            SelectSlot(13);
        }

        public void ExecuteSelect14()
        {
            SelectSlot(14);
        }

        public void ExecuteSelect15()
        {
            SelectSlot(15);
        }

        public void ExecuteSelect16()
        {
            SelectSlot(16);
        }

        public void ExecuteSelect17()
        {
            SelectSlot(17);
        }

        public void ExecuteSelect18()
        {
            SelectSlot(18);
        }

        public void ExecuteSelect19()
        {
            SelectSlot(19);
        }

        public void ExecuteSelect20()
        {
            SelectSlot(20);
        }

        // =========================================================
        // Slot → Action
        // =========================================================

        private void SelectSlot(int slot)
        {
            int globalIndex =
                _currentPage * PageSize +
                (slot - 1);

            if (globalIndex < 0 ||
                globalIndex >= _allActions.Count)
            {
                return;
            }

            SelectAction(globalIndex);
        }

        // =========================================================
        // Action 选择
        // =========================================================

        private void SelectAction(int globalIndex)
        {
            if (globalIndex < 0 ||
                globalIndex >= _allActions.Count)
            {
                return;
            }

            _selectedGlobalIndex = globalIndex;

            ActionInfo action =
                _allActions[globalIndex];

            // =====================================================
            // M5：UI 信息（短名逻辑保持不动）
            // =====================================================

            SelectedActionText =
                action.Name;

            SelectedActionId =
                action.Id;

            StatusText =
                "SELECTED: " +
                action.Id;

            UpdateSelectionVisuals();

            // =====================================================
            // M6：Preview 驱动
            //
            // 永远使用完整 Action ID（act_xxx）。
            // 绑定到 XML 的 CharacterTableauWidget，
            // 由内置 CharacterTableauTextureProvider 播放。
            // =====================================================

            PreviewActionId =
                action.Id;

            // M6 日志验收点（不改播放行为，真实渲染由 Tableau 完成）
            _preview.PlayAction(action.Id);

            StatusText =
                "PLAYING: " +
                action.Id;

            M0_Probe.M0Log.Info(
                "M6_ACTION_SELECTED index=" +
                globalIndex +
                " id=" +
                action.Id +
                " display=" +
                action.Name +
                " preview=" +
                action.Id);
        }

        // =========================================================
        // 更新选中框
        // =========================================================

        private void UpdateSelectionVisuals()
        {
            int pageStart =
                _currentPage * PageSize;

            Slot01Selected =
                _selectedGlobalIndex == pageStart + 0;

            Slot02Selected =
                _selectedGlobalIndex == pageStart + 1;

            Slot03Selected =
                _selectedGlobalIndex == pageStart + 2;

            Slot04Selected =
                _selectedGlobalIndex == pageStart + 3;

            Slot05Selected =
                _selectedGlobalIndex == pageStart + 4;

            Slot06Selected =
                _selectedGlobalIndex == pageStart + 5;

            Slot07Selected =
                _selectedGlobalIndex == pageStart + 6;

            Slot08Selected =
                _selectedGlobalIndex == pageStart + 7;

            Slot09Selected =
                _selectedGlobalIndex == pageStart + 8;

            Slot10Selected =
                _selectedGlobalIndex == pageStart + 9;

            Slot11Selected =
                _selectedGlobalIndex == pageStart + 10;

            Slot12Selected =
                _selectedGlobalIndex == pageStart + 11;

            Slot13Selected =
                _selectedGlobalIndex == pageStart + 12;

            Slot14Selected =
                _selectedGlobalIndex == pageStart + 13;

            Slot15Selected =
                _selectedGlobalIndex == pageStart + 14;

            Slot16Selected =
                _selectedGlobalIndex == pageStart + 15;

            Slot17Selected =
                _selectedGlobalIndex == pageStart + 16;

            Slot18Selected =
                _selectedGlobalIndex == pageStart + 17;

            Slot19Selected =
                _selectedGlobalIndex == pageStart + 18;

            Slot20Selected =
                _selectedGlobalIndex == pageStart + 19;
        }

        // =========================================================
        // 上一页
        // =========================================================

        public void ExecutePreviousPage()
        {
            if (!CanPreviousPage)
                return;

            _currentPage--;

            RefreshPage();

            M0_Probe.M0Log.Info(
                "M4_PREV_PAGE -> " +
                (_currentPage + 1));
        }

        // =========================================================
        // 下一页
        // =========================================================

        public void ExecuteNextPage()
        {
            if (!CanNextPage)
                return;

            _currentPage++;

            RefreshPage();

            M0_Probe.M0Log.Info(
                "M4_NEXT_PAGE -> " +
                (_currentPage + 1));
        }

        // =========================================================
        // 关闭
        // =========================================================

        public void ExecuteClose()
        {
            M0_Probe.M0Log.Lifecycle(
                "M4",
                "CLOSE_COMMAND");

            // M6：关闭前停止预览
            _preview.Stop();

            CloseRequested?.Invoke();
        }

        public event Action CloseRequested;

        // =========================================================
        // 测试 Action
        // =========================================================

        private List<ActionInfo> CreateTestActions()
        {
            var result =
                new List<ActionInfo>();

            for (int i = 1; i <= TotalTestActions; i++)
            {
                result.Add(
                    new ActionInfo(
                        "action_test_" +
                        i.ToString("000"),

                        "Action " +
                        i.ToString("000")));
            }

            return result;
        }

        // =========================================================
        // Finalize
        // =========================================================

        public override void OnFinalize()
        {
            M0_Probe.M0Log.Lifecycle(
                "M4",
                "VM_FINALIZE");

            // M6：清理预览适配层
            _preview.Dispose();

            CloseRequested = null;

            _allActions.Clear();

            base.OnFinalize();
        }
    }
}
