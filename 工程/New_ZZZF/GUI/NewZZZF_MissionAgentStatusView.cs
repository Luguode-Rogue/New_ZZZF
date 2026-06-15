using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 战场法力/耐力 HUD 视图
    /// 继承 MissionView，在 OnMissionScreenTick 中懒初始化 UI
    /// </summary>
    public class NewZZZF_MissionAgentStatusView : MissionView
    {
        private GauntletLayer _gauntletLayer;
        private NewZZZF_MissionAgentStatusVM _dataSource;
        private GauntletMovieIdentifier _movie;
        private bool _initialized;
        private bool _triedInit;

        // MissionView 的每帧回调（MissionScreen 激活时每帧调用）
        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            // 懒初始化：Agent.Main 存在且 MissionScreen 存在
            if (!_triedInit && Agent.Main != null && this.MissionScreen != null)
            {
                _triedInit = true;
                TryInitializeUI(this.MissionScreen);
            }

            // 每帧更新 VM 数据
            if (_dataSource != null && Agent.Main != null)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(Agent.Main.Index, out var comp);
                _dataSource.UpdateFromComponent(comp);
            }
        }

        private void TryInitializeUI(MissionScreen missionScreen)
        {
            if (_initialized)
                return;
            _initialized = true;

            try
            {
                _dataSource = new NewZZZF_MissionAgentStatusVM();

                _gauntletLayer = new GauntletLayer("NewZZZF_MissionHUD", 30, false)
                {
                    IsFocusLayer = false
                };

                _movie = _gauntletLayer.LoadMovie("NewZZZF_MissionAgentStatus", _dataSource);

                if (_movie == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[New_ZZZF] LoadMovie 返回 null，请检查 GUI/Prefabs/NewZZZF_MissionAgentStatus.xml"));
                    _gauntletLayer = null;
                    _dataSource = null;
                    _initialized = false;
                    return;
                }

                missionScreen.AddLayer(_gauntletLayer);
                InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] HUD 已加载"));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[New_ZZZF] 初始化异常: {ex.Message}"));
                _initialized = false;
                _triedInit = false;
            }
        }

        public override void OnRemoveBehavior()
        {
            CleanupLayer();
            _dataSource = null;
            base.OnRemoveBehavior();
        }

        private void CleanupLayer()
        {
            if (_gauntletLayer != null && _movie != null)
            {
                _gauntletLayer.ReleaseMovie(_movie);
                _movie = null;
            }

            if (_gauntletLayer != null)
            {
                if (this.MissionScreen != null)
                    this.MissionScreen.RemoveLayer(_gauntletLayer);
                _gauntletLayer = null;
            }
        }
    }
}
