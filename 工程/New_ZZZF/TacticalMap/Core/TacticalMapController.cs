using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;
using New_ZZZF.TacticalMap.UI;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 小地图总控制器：烘焙地形、驱动追踪器、派发编队指令、管理 UI 层与镜头联动。
    /// 绘制由 MinimapWidget 直接在 OnRender 里完成（读取本控制器暴露的数据），不再依赖位图纹理。
    /// 所有对外依赖都收敛在这里，方便整个 TacticalMap 文件夹整体抽离成独立 mod。
    /// </summary>
    public sealed class TacticalMapController
    {
        private readonly Mission _mission;
        private readonly TerrainCache _cache;
        private readonly FormationTracker _formationTracker;
        private readonly AgentTracker _agentTracker;
        private readonly OrderSystem _orderSystem;
        private TacticalMapLayer _layer;
        private bool _visible;
        private float _accum;
        private bool _cameraLink;
        private Vec2? _playerPos;
        private Vec2? _camTarget;
        private int _agentVersion;

        public TerrainCache Cache => _cache;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public Vec2? PlayerPos => _playerPos;
        public Vec2? CameraTarget => _camTarget;
        // 动态单位层（每个 agent 一个点），供 MinimapWidget 烘焙成纹理
        public byte[] AgentRGBA => _cache.AgentRGBA;
        public int AgentDataVersion => _agentVersion;

        public TacticalMapController(Mission mission)
        {
            _mission = mission;
            var settings = TacticalSettings.Instance;
            _cache = new TerrainCache(settings);
            _formationTracker = new FormationTracker();
            _agentTracker = new AgentTracker(_cache);
            _orderSystem = new OrderSystem(_cache);
            CameraController.Instance = new CameraController();
        }

        /// <summary>战斗开局烘焙地形；失败返回 false（UI 不会显示）。</summary>
        public bool Initialize(Mission mission)
        {
            if (mission == null || mission.Scene == null) return false;
            return _cache.TryBake(mission.Scene);
        }

        public void SetVisible(MissionScreen ms, bool visible)
        {
            if (visible && _layer == null)
            {
                _layer = new TacticalMapLayer(this);
                _layer.Create(ms);
                _accum = TacticalSettings.Instance.UpdateInterval; // 立刻出第一帧
            }
            else if (!visible && _layer != null)
            {
                _layer.Destroy(ms);
                _layer = null;
                if (CameraController.Instance != null) CameraController.Instance.Disable();
            }
            _visible = visible;
        }

        /// <summary>每帧调用（仅在可见时）。标记/密度按 UpdateInterval 节流刷新；绘制由控件每帧完成。</summary>
        public void Tick(Mission mission, MissionScreen ms, float dt)
        {
            if (!_visible || _layer == null) return;

            _playerPos = (_mission.MainAgent != null) ? _mission.MainAgent.Position.AsVec2 : (Vec2?)null;
            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos : (Vec2?)null;

            _accum += dt;
            if (_accum >= TacticalSettings.Instance.UpdateInterval)
            {
                _accum = 0f;
                _formationTracker.Update(mission);
                _agentTracker.Update(mission);
                _agentVersion++;   // 单位层已刷新，通知纹理缓存重建
            }
        }

        /// <summary>小地图点击：根据按键决定移动 / 攻击移动 / 朝向，并可联动镜头。</summary>
        public void HandleClick(Vec2 mousePixel, bool shift, bool rightButton)
        {
            if (_layer == null) return;
            if (!_layer.HitTestMinimap(mousePixel, out Vec2 uv)) return;
            Vec2 world = _cache.UVToWorld(uv);

            TacticalClickMode mode = rightButton ? TacticalClickMode.Face
                : shift ? TacticalClickMode.AttackMove
                : TacticalClickMode.Move;
            _orderSystem.IssueOrder(_mission, world, mode);

            if (FeatureGate.IsEnabled(TacticalFeature.CameraLink) && _cameraLink && CameraController.Instance != null)
            {
                CameraController.Instance.Enable(world);
            }
        }

        /// <summary>C 键：切换“小地图点击联动镜头”模式。</summary>
        public void ToggleCameraFollow()
        {
            _cameraLink = !_cameraLink;
            if (CameraController.Instance != null && !_cameraLink)
                CameraController.Instance.Disable();
            string msg = _cameraLink ? "战术地图：已开启 点击联动镜头" : "战术地图：已关闭 点击联动镜头";
            InformationManager.DisplayMessage(new InformationMessage(msg, new Color(0.2f, 0.9f, 1f, 1f)));
        }
    }
}
