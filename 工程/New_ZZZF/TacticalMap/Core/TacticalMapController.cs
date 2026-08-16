using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;
using New_ZZZF.TacticalMap.UI;
using System;

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
        private Vec2 _playerFacing = Vec2.Zero;
        private int _agentVersion;

        public TerrainCache Cache => _cache;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public Vec2? PlayerPos => _playerPos;
        public Vec2? CameraTarget => _camTarget;
        public Vec2 PlayerFacing => _playerFacing;
        public bool CameraLinkEnabled => _cameraLink;
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
            if (_mission.MainAgent != null)
            {
                float af = _mission.MainAgent.LookDirectionAsAngle; // 朝向角（弧度，绕 Z 轴）
                _playerFacing = new Vec2((float)Math.Cos(af), (float)Math.Sin(af));
            }
            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos : (Vec2?)null;

            // 驱动相机状态机（飞出 → 停留 → 飞回）。必须每帧调用，
            // 且要在 CameraController.Initialize 之后才有效。
            if (CameraController.Instance != null)
            {
                CameraController.Instance.Initialize(ms, mission.Scene);
                // 每帧幂等记录玩家基准高度（agent 就绪后即生效一次，之后值不变）
                CameraController.Instance.CaptureBaseHeight(mission);
                CameraController.Instance.Tick(dt);
            }

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
            IssueOrderAtWorld(_cache.UVToWorld(uv), rightButton ? TacticalClickMode.Face : shift ? TacticalClickMode.AttackMove : TacticalClickMode.Move);
        }

        /// <summary>HtmlUI 地图点击适配器：把 0..1 UV 转换为世界坐标后复用现有 OrderSystem。</summary>
        public void HandleHtmlMapClick(float u, float v, string mode)
        {
            if (!_visible || !_cache.IsBaked) return;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            TacticalClickMode clickMode;
            switch ((mode ?? "move").ToLowerInvariant())
            {
                case "face":
                    clickMode = TacticalClickMode.Face;
                    break;
                case "attack":
                case "attackmove":
                    clickMode = TacticalClickMode.AttackMove;
                    break;
                default:
                    clickMode = TacticalClickMode.Move;
                    break;
            }

            Vec2 world = _cache.UVToWorld(new Vec2(u, v));
            IssueOrderAtWorld(world, clickMode);
        }

        private void IssueOrderAtWorld(Vec2 world, TacticalClickMode mode)
        {
            _orderSystem.IssueOrder(_mission, world, mode);

            if (FeatureGate.IsEnabled(TacticalFeature.CameraLink) && _cameraLink && CameraController.Instance != null)
                CameraController.Instance.Enable(world);
        }

        /// <summary>C 键：切换“小地图点击联动镜头”模式。</summary>
        public void ToggleCameraFollow()
        {
            _cameraLink = !_cameraLink;
            if (CameraController.Instance != null)
            {
                // 同步开关：关闭时让镜头平滑飞回原位
                CameraController.Instance.PreviewModeEnabled = _cameraLink;
                if (!_cameraLink) { CameraController.Instance.Disable(); }
            }
            string msg = _cameraLink ? "战术地图：已开启 点击联动镜头" : "战术地图：已关闭 点击联动镜头";
            InformationManager.DisplayMessage(new InformationMessage(msg, new Color(0.2f, 0.9f, 1f, 1f)));

            // 调试用：输出【当前真实相机】的姿态（按 C 时打印），方便把调好的角度回贴给开发
            if (CameraController.Instance != null)
            {
                CameraController.Instance.ReadRealCameraAngles(out float bearing, out float pitch, out Vec3 eye);
                float bearingDeg = bearing * 57.29578f;
                float pitchDeg = pitch * 57.29578f;
                // 归一化到 [0,360)
                if (bearingDeg < 0f) { bearingDeg += 360f; }
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Cam] 真实相机 bearing={bearingDeg:F1}° pitch={pitchDeg:F1}° eye=({eye.x:F1},{eye.y:F1},{eye.z:F1})",
                    new Color(1f, 0.85f, 0.2f, 1f)));
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Cam] 固定参数(供替换): ViewBearing={bearing:F6}f; ViewHeight={(float)Math.Max(0, eye.z):F1}f;",
                    new Color(1f, 0.85f, 0.2f, 1f)));
            }
        }
    }
}
