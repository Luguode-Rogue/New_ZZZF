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
        private readonly List<AgentSnapshot> _agentSnapshots = new List<AgentSnapshot>();

        public TerrainCache Cache => _cache;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public IReadOnlyList<AgentSnapshot> AgentSnapshots => _agentSnapshots;
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

        public void SetVisible(MissionScreen ms, bool visible)
        {
            if (visible && _layer == null)
            {
                _layer = new TacticalMapLayer(this);
                _layer.Create(ms);
                _accum = TacticalSettings.Instance.UpdateInterval;
            }
            else if (!visible && _layer != null)
            {
                _layer.Destroy(ms);
                _layer = null;
                if (CameraController.Instance != null) CameraController.Instance.Disable();
            }
            _visible = visible;
        }

        public void Tick(Mission mission, MissionScreen ms, float dt)
        {
            if (!_visible || _layer == null) return;

            _playerPos = _mission.MainAgent != null ? _mission.MainAgent.Position.AsVec2 : (Vec2?)null;
            if (_mission.MainAgent != null)
            {
                float af = _mission.MainAgent.LookDirectionAsAngle;
                _playerFacing = new Vec2((float)Math.Cos(af), (float)Math.Sin(af));
            }
            _camTarget = CameraController.Instance != null && CameraController.Instance.Active
                ? CameraController.Instance.TargetWorldPos : (Vec2?)null;

            if (CameraController.Instance != null)
            {
                CameraController.Instance.Initialize(ms, mission.Scene);
                CameraController.Instance.CaptureBaseHeight(mission);
                CameraController.Instance.Tick(dt);
            }

            _accum += dt;
            if (_accum >= TacticalSettings.Instance.UpdateInterval)
            {
                _accum = 0f;
                _formationTracker.Update(mission);
                _agentTracker.Update(mission);
                RefreshAgentSnapshots(mission);
                _agentVersion++;
            }
        }

        private void RefreshAgentSnapshots(Mission mission)
        {
            _agentSnapshots.Clear();
            if (mission == null) return;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || agent.Health <= 0f || !agent.IsHuman)
                    continue;

                Vec2 uv = _cache.WorldToUV(agent.Position.AsVec2);
                if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f)
                    continue;

                _agentSnapshots.Add(new AgentSnapshot
                {
                    U = uv.X,
                    V = uv.Y,
                    PlayerTeam = agent.Team != null && agent.Team.IsPlayerTeam,
                    Neutral = agent.Team == null
                });
            }
        }

        public void HandleClick(Vec2 mousePixel, bool shift, bool rightButton)
        {
            if (_layer == null) return;
            if (!_layer.HitTestMinimap(mousePixel, out Vec2 uv)) return;
            IssueOrderFromUv(uv, rightButton ? TacticalClickMode.Face : shift ? TacticalClickMode.AttackMove : TacticalClickMode.Move, true);
        }

        public void HandleHtmlMoveClick(float u, float v) => IssueOrderFromUv(new Vec2(u, v), TacticalClickMode.Move, false);
        public void HandleHtmlFaceClick(float u, float v) => IssueOrderFromUv(new Vec2(u, v), TacticalClickMode.Face, false);
        public void HandleHtmlCameraClick(float u, float v)
        {
            Vec2 world = _cache.UVToWorld(new Vec2(Clamp01(u), Clamp01(v)));
            if (FeatureGate.IsEnabled(TacticalFeature.CameraLink) && CameraController.Instance != null)
                CameraController.Instance.Enable(world);
        }

        private void IssueOrderFromUv(Vec2 uv, TacticalClickMode mode, bool cameraLink)
        {
            Vec2 world = _cache.UVToWorld(new Vec2(Clamp01(uv.X), Clamp01(uv.Y)));
            _orderSystem.IssueOrder(_mission, world, mode);

            if (cameraLink && FeatureGate.IsEnabled(TacticalFeature.CameraLink) && _cameraLink && CameraController.Instance != null)
                CameraController.Instance.Enable(world);
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        public void ToggleCameraFollow()
        {
            _cameraLink = !_cameraLink;
            if (CameraController.Instance != null)
            {
                CameraController.Instance.PreviewModeEnabled = _cameraLink;
                if (!_cameraLink) CameraController.Instance.Disable();
            }

            string msg = _cameraLink ? "战术地图：已开启 点击联动镜头" : "战术地图：已关闭 点击联动镜头";
            InformationManager.DisplayMessage(new InformationMessage(msg, new Color(0.2f, 0.9f, 1f, 1f)));

            if (CameraController.Instance != null)
            {
                CameraController.Instance.ReadRealCameraAngles(out float bearing, out float pitch, out Vec3 eye);
                float bearingDeg = bearing * 57.29578f;
                float pitchDeg = pitch * 57.29578f;
                if (bearingDeg < 0f) bearingDeg += 360f;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Cam] 真实相机 bearing={bearingDeg:F1}° pitch={pitchDeg:F1}° eye=({eye.x:F1},{eye.y:F1},{eye.z:F1})",
                    new Color(1f, 0.85f, 0.2f, 1f)));
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Cam] 固定参数(供替换): ViewBearing={bearing:F6}f; ViewHeight={(float)Math.Max(0, eye.z):F1}f;",
                    new Color(1f, 0.85f, 0.2f, 1f)));
            }
        }

        public sealed class AgentSnapshot
        {
            public float U { get; set; }
            public float V { get; set; }
            public bool PlayerTeam { get; set; }
            public bool Neutral { get; set; }
        }
    }
}