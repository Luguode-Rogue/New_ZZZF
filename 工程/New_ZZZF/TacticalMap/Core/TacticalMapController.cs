using System;
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
    /// TacticalMap 总控制器：提供地图数据、编队/单位追踪、订单与镜头操作。
    /// 当前 HTMLUI 重制版不再创建旧 Gauntlet TacticalMapLayer；UI 生命周期由 HtmlUI consumer 管理。
    /// </summary>
    public sealed class TacticalMapController
    {
        private readonly Mission _mission;
        private readonly TerrainCache _cache;
        private readonly FormationTracker _formationTracker;
        private readonly AgentTracker _agentTracker;
        private readonly OrderSystem _orderSystem;
        private bool _visible;
        private float _accum;
        private Vec2? _playerPos;
        private Vec2? _camTarget;
        private Vec2 _playerFacing = Vec2.Zero;
        private int _agentVersion;
        private readonly List<AgentMapSnapshot> _agentSnapshots = new List<AgentMapSnapshot>();

        public TerrainCache Cache => _cache;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public IReadOnlyList<AgentMapSnapshot> AgentSnapshots => _agentSnapshots;
        public Vec2? PlayerPos => _playerPos;
        public Vec2? CameraTarget => _camTarget;
        public Vec2 PlayerFacing => _playerFacing;
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

        public bool Initialize(Mission mission)
        {
            if (mission == null || mission.Scene == null) return false;
            return _cache.TryBake(mission.Scene);
        }

        /// <summary>
        /// 控制地图是否存在于 HTMLUI 中。旧 Gauntlet Layer 已退出运行时路径。
        /// </summary>
        public void SetVisible(MissionScreen ms, bool visible)
        {
            _visible = visible;
            if (!visible && CameraController.Instance != null)
                CameraController.Instance.Disable();
        }

        public void Tick(Mission mission, MissionScreen ms, float dt)
        {
            if (!_visible) return;

            _playerPos = (_mission.MainAgent != null) ? _mission.MainAgent.Position.AsVec2 : (Vec2?)null;
            if (_mission.MainAgent != null)
            {
                float af = _mission.MainAgent.LookDirectionAsAngle;
                _playerFacing = new Vec2((float)Math.Cos(af), (float)Math.Sin(af));
            }

            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos
                : (Vec2?)null;

            if (CameraController.Instance != null)
            {
                if (ms != null && mission != null && mission.Scene != null)
                    CameraController.Instance.Initialize(ms, mission.Scene);
                CameraController.Instance.CaptureBaseHeight(mission);
                CameraController.Instance.Tick(dt);
            }

            _accum += dt;
            if (_accum < TacticalSettings.Instance.UpdateInterval) return;

            _accum = 0f;
            _formationTracker.Update(mission);
            _agentTracker.Update(mission);
            RebuildAgentSnapshots(mission);
            _agentVersion++;

            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos
                : (Vec2?)null;
        }

        private void RebuildAgentSnapshots(Mission mission)
        {
            _agentSnapshots.Clear();
            if (mission == null || !_cache.IsBaked) return;
            if (!TacticalSettings.Instance.EnableAgentMarkers) return;

            foreach (var agent in mission.Agents)
            {
                if (agent == null || agent.Health <= 0f || !agent.IsHuman) continue;

                Vec2 position = agent.Position.AsVec2;
                Vec2 uv = _cache.WorldToUV(position);
                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f) continue;

                bool playerTeam = agent.Team != null && agent.Team.IsPlayerTeam;
                _agentSnapshots.Add(new AgentMapSnapshot
                {
                    U = uv.X,
                    V = uv.Y,
                    PlayerTeam = playerTeam,
                    Neutral = agent.Team == null
                });
            }
        }

        /// <summary>
        /// HTML 左键：移动命令。
        /// </summary>
        public void HandleHtmlMoveClick(float u, float v)
        {
            if (!ValidateHtmlUv(u, v)) return;
            IssueOrderAtWorld(_cache.UVToWorld(new Vec2(u, v)), TacticalClickMode.Move);
        }

        /// <summary>
        /// HTML 右键：朝向命令。
        /// </summary>
        public void HandleHtmlFaceClick(float u, float v)
        {
            if (!ValidateHtmlUv(u, v)) return;
            IssueOrderAtWorld(_cache.UVToWorld(new Vec2(u, v)), TacticalClickMode.Face);
        }

        /// <summary>
        /// HTML 中键：将战场镜头切换到地图目标位置。
        /// </summary>
        public void HandleHtmlCameraClick(float u, float v)
        {
            if (!ValidateHtmlUv(u, v)) return;
            if (!FeatureGate.IsEnabled(TacticalFeature.CameraLink)) return;
            CameraController.Instance?.Enable(_cache.UVToWorld(new Vec2(u, v)));
        }

        private bool ValidateHtmlUv(float u, float v)
        {
            return _visible && _cache.IsBaked && u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private void IssueOrderAtWorld(Vec2 world, TacticalClickMode mode)
        {
            _orderSystem.IssueOrder(_mission, world, mode);
        }

        /// <summary>
        /// 保留旧接口以便兼容尚未清理的调用方；HTMLUI 不再使用旧 Layer 命中测试。
        /// </summary>
        public void HandleClick(Vec2 mousePixel, bool shift, bool rightButton)
        {
        }

        public void ToggleCameraFollow()
        {
            if (!FeatureGate.IsEnabled(TacticalFeature.CameraLink)) return;
            if (CameraController.Instance == null) return;

            CameraController.Instance.PreviewModeEnabled = !CameraController.Instance.PreviewModeEnabled;
            if (!CameraController.Instance.PreviewModeEnabled)
                CameraController.Instance.Disable();
        }
    }

    public sealed class AgentMapSnapshot
    {
        public float U { get; set; }
        public float V { get; set; }
        public bool PlayerTeam { get; set; }
        public bool Neutral { get; set; }
    }
}
