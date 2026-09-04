using System;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using New_ZZZF.GUI;

namespace New_ZZZF
{
    /// <summary>
    /// 战场状态 HTML HUD 的 MissionView 适配层。
    /// 不创建 Gauntlet/Win32/WebView；只负责在 GameThread 上生成并发布业务状态。
    /// HTML 页面和输入由 BannerlordHtmlUI Framework / TacticalMap 页面统一承载。
    /// </summary>
    public sealed class NewZZZF_MissionAgentStatusView : MissionView
    {
        private const string OwnerId = "New_ZZZF.MissionAgentStatus";
        private const string StateKey = "missionHud";
        private HtmlUiConsumerScope _scope;
        private bool _registerRequested;
        private float _publishAccum;
        private string _lastSignature;

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (!_registerRequested)
            {
                _registerRequested = true;
                try
                {
                    HtmlUiService.OnReady(RegisterState);
                }
                catch (Exception ex)
                {
                    HtmlUiLogger.Error("MissionAgentStatus HtmlUI OnReady registration failed.", ex);
                    _registerRequested = false;
                }
            }

            if (_scope == null || !HtmlUiService.IsReady)
                return;

            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f)
                return;

            _publishAccum = 0f;
            PublishState(false);
        }

        private void RegisterState()
        {
            if (_scope != null || !HtmlUiService.IsReady)
                return;

            try
            {
                _scope = HtmlUiService.CreateScope(OwnerId);
                _lastSignature = null;
                PublishState(true);
                HtmlUiLogger.Info("MissionAgentStatus HtmlUI state scope registered.");
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI state scope registration failed.", ex);
                _scope = null;
                _registerRequested = false;
            }
        }

        private void PublishState(bool force)
        {
            if (_scope == null || !HtmlUiService.IsReady)
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

        public override void OnRemoveBehavior()
        {
            try
            {
                if (_scope != null)
                    _scope.Dispose();
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("MissionAgentStatus HtmlUI scope dispose failed.", ex);
            }

            _scope = null;
            _lastSignature = null;
            _publishAccum = 0f;
            base.OnRemoveBehavior();
        }
    }
}
