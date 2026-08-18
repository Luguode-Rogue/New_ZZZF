using HarmonyLib;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Config
{
    /// <summary>
    /// TacticalMap HTMLUI 显示链隔离测试入口。
    /// 当前阶段故意不启动 TacticalMap Controller、输入补丁或业务 UI；只展示一个静态 HTML 页面。
    /// </summary>
    public static class TacticalMapBootstrap
    {
        private static Harmony _harmony;
        private static TacticalMapHtmlUiSmokeTest _smokeTest;

        public static TacticalMapHtmlUi HtmlUi => null;

        public static void OnSubModuleLoad()
        {
            try
            {
                _harmony = new Harmony("TacticalMap");
                _smokeTest = new TacticalMapHtmlUiSmokeTest();
                _smokeTest.InitializeOnFrameworkReady();

                InformationManager.DisplayMessage(new InformationMessage(
                    "[TMapSmoke] 仅启动静态 BannerlordHtmlUI 展示测试。"));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMapSmoke] Bootstrap 异常: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public static void OnMissionStart(Mission mission)
        {
            if (mission == null)
                return;

            _smokeTest?.RequestOpen();
        }

        public static void OnMissionEnd()
        {
            _smokeTest?.Close();
        }

        public static void Dispose()
        {
            try { _smokeTest?.Dispose(); }
            catch { }
            finally
            {
                _smokeTest = null;
                _harmony = null;
            }
        }
    }
}
