using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// TacticalMap game-side controller. It exposes terrain, navigation, formations, agents,
    /// orders and camera state. HTMLUI owns presentation and input routing.
    /// </summary>
    public sealed class TacticalMapController
    {
        private readonly Mission _mission;
        private readonly TerrainCache _cache;
        private readonly NavMeshMap _navigationMap;
        private readonly FormationTracker _formationTracker;
        private readonly OrderSystem _orderSystem;
        private bool _visible;
        private float _accum;
        private Vec2? _playerPos;
        private Vec2? _camTarget;
        private Vec2 _playerFacing = Vec2.Zero;
        private int _agentVersion;
        private readonly List<AgentMapSnapshot> _agentSnapshots = new List<AgentMapSnapshot>();
        private string _selectedFormationName;

        public TerrainCache Cache => _cache;
        public NavMeshMap NavigationMap => _navigationMap;
        public bool IsVisible => _visible;
        public List<FormationSnapshot> FormationSnapshots => _formationTracker.Snapshots;
        public IReadOnlyList<AgentMapSnapshot> AgentSnapshots => _agentSnapshots;
        public Vec2? PlayerPos => _playerPos;
        public Vec2? CameraTarget => _camTarget;
        public Vec2 PlayerFacing => _playerFacing;
        public byte[] AgentRGBA => _cache.AgentRGBA;
        public int AgentDataVersion => _agentVersion;
        public string SelectedFormationName => _selectedFormationName;

        public TacticalMapController(Mission mission)
        {
            _mission = mission;
            var settings = TacticalSettings.Instance;
            _cache = new TerrainCache(settings);
            _navigationMap = new NavMeshMap(_cache);
            _formationTracker = new FormationTracker();
            _orderSystem = new OrderSystem(_cache);
            CameraController.Instance = new CameraController();
        }

        public bool Initialize(Mission mission)
        {
            if (mission == null || mission.Scene == null) return false;
            if (!_cache.TryBake(mission.Scene, mission)) return false;

            // Use the game's actual AI navigation surface as the authoritative walkability layer.
            _navigationMap.Build(mission.Scene);
            return true;
        }

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
                _playerFacing = _mission.MainAgent.LookDirection.AsVec2;
                if (_playerFacing.LengthSquared > 1E-4f)
                    _playerFacing = _playerFacing.Normalized();
            }

            _camTarget = (CameraController.Instance != null && CameraController.Instance.Active)
                ? CameraController.Instance.TargetWorldPos
                : (Vec2?)null;

            if (CameraController.Instance != null)
            {
                long camStart = System.Diagnostics.Stopwatch.GetTimestamp();
                if (ms != null && mission != null && mission.Scene != null)
                    CameraController.Instance.Initialize(ms, mission.Scene);
                CameraController.Instance.CaptureBaseHeight(mission);
                CameraController.Instance.Tick(dt);
                Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.Camera,
                    System.Diagnostics.Stopwatch.GetTimestamp() - camStart);
            }

            _accum += dt;
            if (_accum < TacticalSettings.Instance.UpdateInterval) return;

            _accum = 0f;
            long snapStart = System.Diagnostics.Stopwatch.GetTimestamp();
            _formationTracker.Update(mission);
            Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.Snapshots,
                System.Diagnostics.Stopwatch.GetTimestamp() - snapStart);

            long agentStart = System.Diagnostics.Stopwatch.GetTimestamp();
            RebuildAgentSnapshots(mission);
            Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.Agents,
                System.Diagnostics.Stopwatch.GetTimestamp() - agentStart);
            _agentVersion++;

            if (!string.IsNullOrWhiteSpace(_selectedFormationName))
            {
                bool stillExists = false;
                foreach (var formation in _formationTracker.Snapshots)
                {
                    if (formation.IsPlayer && string.Equals(formation.Name, _selectedFormationName, StringComparison.OrdinalIgnoreCase))
                    {
                        stillExists = true;
                        break;
                    }
                }
                if (!stillExists)
                    _selectedFormationName = null;
            }

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

        public void HandleHtmlSelectFormation(string formationName)
        {
            if (!_visible || string.IsNullOrWhiteSpace(formationName)) return;

            foreach (var formation in _formationTracker.Snapshots)
            {
                if (!formation.IsPlayer) continue;
                if (string.Equals(formation.Name, formationName, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedFormationName = formation.Name;
                    return;
                }
            }
        }

        public void HandleHtmlClearFormationSelection()
        {
            _selectedFormationName = null;
        }

        public void HandleHtmlMoveClick(float u, float v)
        {
            if (!ValidateHtmlUv(u, v)) return;
            Vec2 world = _cache.UVToWorld(new Vec2(u, v));
            int issued = _orderSystem.IssueOrderCounted(_mission, world, TacticalClickMode.Move, _selectedFormationName);
            TacticalMapLog.Info("Move order result: issued=" + issued + " uv=(" + u.ToString("0.000") + "," + v.ToString("0.000") +
                                ") world=(" + world.X.ToString("0.0") + "," + world.Y.ToString("0.0") + ")");
        }

        public void HandleHtmlFaceClick(float u, float v)
        {
            if (!ValidateHtmlUv(u, v)) return;
            Vec2 world = _cache.UVToWorld(new Vec2(u, v));
            int issued = _orderSystem.IssueOrderCounted(_mission, world, TacticalClickMode.Face, _selectedFormationName);
            TacticalMapLog.Info("Face order result: issued=" + issued + " uv=(" + u.ToString("0.000") + "," + v.ToString("0.000") +
                                ") world=(" + world.X.ToString("0.0") + "," + world.Y.ToString("0.0") + ")");
        }

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
            _orderSystem.IssueOrder(_mission, world, mode, _selectedFormationName);
        }

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
