using System;
using System.IO;
using BannerlordHtmlUI;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 纯 Framework 显示冒烟测试。
    /// 不访问 TacticalMapController、Mission、State、Command、Request、输入或鼠标。
    /// 页面仅用于验证 BannerlordHtmlUI 的 ContentRoot -> Page -> Navigate -> Overlay 显示链。
    /// </summary>
    public sealed class TacticalMapHtmlUiSmokeTest : IDisposable
    {
        private const string OwnerId = "New_ZZZF.TacticalMap.SmokeTest";
        private const string ContentRootName = "tacticalmap-smoke";
        private const string PageName = "tacticalmap-smoke.html";

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private bool _registered;
        private bool _openRequested;
        private bool _open;

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        public void RequestOpen()
        {
            _openRequested = true;
            if (_registered && HtmlUiService.IsReady)
                Open();
        }

        public void Close()
        {
            _openRequested = false;
            if (!_registered || !HtmlUiService.IsReady || string.IsNullOrEmpty(_pageId))
                return;

            try
            {
                HtmlUiService.Pages.Close(_pageId);
                _open = false;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMapSmoke] Close 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady)
                return;

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUiSmokeTest).Assembly.Location) ?? ".";
                string uiRoot = Path.Combine(assemblyDir, "TacticalMapUI");

                if (!Directory.Exists(uiRoot))
                {
                    throw new DirectoryNotFoundException(
                        "TacticalMap SmokeTest HtmlUI root not found: " + uiRoot);
                }

                _scope = HtmlUiService.CreateScope(OwnerId);
                string rootId = _scope.RegisterContentRoot(ContentRootName, uiRoot);

                _pageId = _scope.RegisterPage(
                    new HtmlUiPage(PageName, "SmokeTest/index.html")
                    {
                        ContentRootId = rootId,
                        HotReload = false,
                        DefaultInputMode = HtmlUiInputMode.Passive
                    });

                _registered = true;

                if (_openRequested)
                    Open();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMapSmoke] Register 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private void Open()
        {
            if (!_registered || !HtmlUiService.IsReady || string.IsNullOrEmpty(_pageId))
                return;

            try
            {
                bool result = HtmlUiService.Pages.Open(_pageId);
                _open = result;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMapSmoke] Pages.Open={result}, Current={HtmlUiService.Pages.CurrentId ?? "<null>"}, Visible={HtmlUiService.Host.IsVisible}, Input={HtmlUiService.Host.InputMode}"));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMapSmoke] Open 失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void Dispose()
        {
            try
            {
                Close();
                _scope?.Dispose();
            }
            catch { }
            finally
            {
                _scope = null;
                _pageId = null;
                _registered = false;
                _openRequested = false;
                _open = false;
            }
        }
    }
}
