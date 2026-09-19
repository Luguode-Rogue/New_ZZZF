using System;
using System.IO;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using Newtonsoft.Json.Linq;
using New_ZZZF.BattleHud;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Systems.BattleEquipment
{
    public sealed class BattleEquipmentHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.BattleEquipment";
        private const string ContentRoot = "ui";
        private const string PageName = "battleequipment";
        private const string HintName = "battleequipmenthint";
        private const string SessionState = "equipmentSession";
        private const string HintState = "equipmentHint";

        private static readonly Lazy<BattleEquipmentHtmlUi> LazyInstance =
            new Lazy<BattleEquipmentHtmlUi>(() => new BattleEquipmentHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private string _hintId;
        private bool _registered;
        private bool _hintShown;
        private bool _pageOpened;
        private bool _pausedByUs;
        private bool _closing;
        private BattleEquipmentSession _session;

        public static BattleEquipmentHtmlUi Instance => LazyInstance.Value;
        public bool IsOpen => _pageOpened;

        private BattleEquipmentHtmlUi() { }

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;
            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(BattleEquipmentHtmlUi).Assembly.Location) ?? ".";
                DirectoryInfo binDir = Directory.GetParent(assemblyDir);
                DirectoryInfo moduleDir = binDir == null ? null : Directory.GetParent(binDir.FullName);
                string uiRoot = moduleDir == null ? Path.Combine(assemblyDir, "UI") : Path.Combine(moduleDir.FullName, "UI");
                if (!Directory.Exists(uiRoot)) throw new DirectoryNotFoundException("BattleEquipment UI root not found: " + uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _scope.RegisterContentRoot(ContentRoot, uiRoot);
                _hintId = _scope.RegisterSurface(new HtmlUiSurface(HintName, "BattleEquipmentHint/index.html")
                {
                    ContentRootId = ContentRoot,
                    ZIndex = 160,
                    InputDemand = HtmlUiInputMode.Passive,
                    CoexistWithPage = true
                });
                _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "BattleEquipment/index.html")
                {
                    ContentRootId = ContentRoot,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Captured,
                    CloseOnEscape = true,
                    Closed = OnPageClosed
                });

                _scope.RegisterRequest("getSession", _ => Task.FromResult<object>(_session?.BuildState()));
                _scope.RegisterRequest("commit", payload => Task.FromResult(Commit(payload)));
                _scope.RegisterCommand("cancel", _ => Close());
                _scope.RegisterCommand("clientLog", payload => TacticalMapLog.Info("[BattleEquipment JS] " + (payload?["message"]?.Value<string>() ?? string.Empty)));
                _registered = true;
                TacticalMapLog.Info("[BattleEquipment] HtmlUI registered. Root=" + uiRoot);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleEquipment] HtmlUI registration failed.", ex);
            }
        }

        public void OnMissionStarted()
        {
            if (!_registered || !HtmlUiService.IsReady || _hintShown) return;
            try { _hintShown = HtmlUiService.Surfaces.Show(_hintId); }
            catch (Exception ex) { TacticalMapLog.Error("[BattleEquipment] Hint show failed.", ex); }
            UpdateHint(null, 0f, false);
        }

        public void OnMissionEnded()
        {
            Close();
            if (_hintShown && _registered && HtmlUiService.IsReady)
            {
                try { HtmlUiService.Surfaces.Hide(_hintId); } catch { }
            }
            _hintShown = false;
        }

        public void UpdateHint(Agent target, float progress, bool holding)
        {
            if (!_registered || !_hintShown || _pageOpened) return;
            _scope.SetState(HintState, new
            {
                visible = target != null,
                target = target?.Name ?? string.Empty,
                holding,
                progress = Math.Max(0f, Math.Min(1f, progress)),
                text = holding ? "继续按住交互键" : "长按交互键交换装备"
            });
        }

        public bool Open(Agent player, Agent target)
        {
            if (_pageOpened || !_registered || !HtmlUiService.IsReady || player == null || target == null) return false;
            try
            {
                _session = new BattleEquipmentSession(player, target);
                TacticalMapHtmlUi.Instance.SetCaptureSuspended(true);
                BattleHudHtmlUi.Instance.SetCaptureSuspended(true);
                if (!HtmlUiService.Pages.Open(_pageId))
                {
                    ResumeOtherUi();
                    _session = null;
                    return false;
                }
                _pageOpened = true;
                _scope.SetState(SessionState, _session.BuildState());
                if (!MBCommon.IsPaused)
                {
                    MBCommon.PauseGameEngine();
                    _pausedByUs = true;
                }
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleEquipment] Open failed.", ex);
                CleanupAfterClose();
                return false;
            }
        }

        public void Close()
        {
            if (_closing) return;
            _closing = true;
            try
            {
                if (_pageOpened && _registered && HtmlUiService.IsReady)
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex) { TacticalMapLog.Error("[BattleEquipment] Close failed.", ex); }
            finally
            {
                CleanupAfterClose();
                _closing = false;
            }
        }

        private object Commit(JToken payload)
        {
            if (_session == null) return new { ok = false, message = "当前没有有效的换装会话。" };
            object result = _session.Commit(payload);
            bool ok = JToken.FromObject(result)["ok"]?.Value<bool>() ?? false;
            if (ok) Close();
            return result;
        }

        private void OnPageClosed()
        {
            if (_closing) return;
            CleanupAfterClose();
        }

        private void CleanupAfterClose()
        {
            _pageOpened = false;
            _session = null;
            if (_pausedByUs)
            {
                _pausedByUs = false;
                if (MBCommon.IsPaused) MBCommon.UnPauseGameEngine();
            }
            ResumeOtherUi();
        }

        private static void ResumeOtherUi()
        {
            try { TacticalMapHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
            try { BattleHudHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
        }

        public void Dispose()
        {
            OnMissionEnded();
            try { _scope?.Dispose(); } catch { }
            _scope = null;
            _registered = false;
            _pageId = null;
            _hintId = null;
        }
    }
}
