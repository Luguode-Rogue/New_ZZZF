using System;
using System.IO;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// 独立战场状态 HUD HtmlUI Consumer。
    /// 页面默认 Passive，不创建输入补丁、不管理 HWND，仅负责 Page 生命周期与业务状态发布。
    /// </summary>
    public sealed class MissionAgentStatusHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.MissionAgentStatus";
        private const string PageName = "missionhud";
        private const string ContentRootName = "missionhud";
        private const string StateKey = "missionHud";

        private static readonly Lazy<MissionAgentStatusHtmlUi> _instance =
            new Lazy<MissionAgentStatusHtmlUi>(() => new MissionAgentStatusHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private bool _registered;
        private bool _pageOpened;
        private bool _missionActive;
        private bool _missionOpenAttempted;
        private float _publishAccum;
        private string _lastSignature;

        public static MissionAgentStatusHtmlUi Instance => _instance.Value;
        public bool IsRegistered => _registered;
        public bool IsOpen => _pageOpened;

        private MissionAgentStatusHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady)
                return;

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(MissionAgentStatusHtmlUi).Assembly.Location) ?? ".";
                DirectoryInfo binDir = Directory.GetParent(assemblyDir);
                DirectoryInfo moduleDir = binDir == null ? null : Directory.GetParent(binDir.FullName);
                string uiRoot = moduleDir == null
                    ? Path.Combine(assemblyDir, "UI", "MissionHud")
                    : Path.Combine(moduleDir.FullName, "UI", "MissionHud");

                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException("MissionHud HtmlUI content root not found: " + uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _scope.RegisterContentRoot(ContentRootName, uiRoot);
                _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "index.html")
                {
                    ContentRootId = ContentRootName,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Passive,
                    CloseOnEscape = false,
                    Opened = () => _pageOpened = true,
                    Closed = () => _pageOpened = false
                });

                _registered = true;
                HtmlUiLogger.Info("MissionAgentStatus HtmlUI registered. Root=" + uiRoot);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI registration failed.", ex);
                _scope = null;
                _pageId = null;
                _registered = false;
            }
        }

        public void Tick(float dt)
        {
            if (!_registered || !HtmlUiService.IsReady)
                return;

            bool missionNowActive = Mission.Current != null;
            if (!missionNowActive)
            {
                StopForMission();
                return;
            }

            if (!_missionActive)
            {
                _missionActive = true;
                _missionOpenAttempted = false;
                _publishAccum = 0f;
                _lastSignature = null;
            }

            // 每场战斗只自动打开一次。之后其它 Consumer 可以正常切换当前 Page，HUD 不会反复抢占。
            if (!_pageOpened && !_missionOpenAttempted)
            {
                _missionOpenAttempted = true;
                EnsureOpen();
            }

            if (!_pageOpened)
                return;

            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f)
                return;

            _publishAccum = 0f;
            PublishState(false);
        }

        private void EnsureOpen()
        {
            if (_pageOpened || !_registered || !HtmlUiService.IsReady)
                return;

            try
            {
                if (!HtmlUiService.Pages.Open(_pageId))
                    return;

                _pageOpened = true;
                _lastSignature = null;
                PublishState(true);
            }
            catch (Exception ex)
            {
                _pageOpened = false;
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI open failed.", ex);
            }
        }

        private void PublishState(bool force)
        {
            if (!_pageOpened || _scope == null || !HtmlUiService.IsReady)
                return;

            try
            {
                object state = MissionAgentStatusHtmlState.Build(Agent.Main);
                string signature = JsonConvert.SerializeObject(state, Formatting.None);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal))
                    return;

                _lastSignature = signature;
                _scope.SetState(StateKey, state);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI state publish failed.", ex);
            }
        }

        public void StopForMission()
        {
            if (!_missionActive && !_pageOpened && !_missionOpenAttempted)
                return;

            _missionActive = false;
            _missionOpenAttempted = false;
            _pageOpened = false;
            _publishAccum = 0f;
            _lastSignature = null;

            if (_registered && HtmlUiService.IsReady && string.Equals(HtmlUiService.Pages.CurrentId, _pageId, StringComparison.OrdinalIgnoreCase))
            {
                try { HtmlUiService.Pages.Close(_pageId); }
                catch (Exception ex) { HtmlUiLogger.Error("MissionAgentStatus HtmlUI mission close failed.", ex); }
            }
        }

        public void Dispose()
        {
            StopForMission();
            try
            {
                if (_registered && HtmlUiService.IsReady && !string.IsNullOrEmpty(_pageId))
                    HtmlUiService.Pages.Unregister(_pageId);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI page unregister failed.", ex);
            }

            try { _scope?.Dispose(); }
            catch (Exception ex) { HtmlUiLogger.Error("MissionAgentStatus HtmlUI scope dispose failed.", ex); }

            _scope = null;
            _pageId = null;
            _registered = false;
            _pageOpened = false;
            _missionActive = false;
            _missionOpenAttempted = false;
            _publishAccum = 0f;
            _lastSignature = null;
        }
    }
}
