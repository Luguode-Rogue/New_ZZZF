using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.UI
{
    public sealed class TacticalMapHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.TacticalMap";
        private const string PageName = "tacticalmap";
        private const string ContentRootName = "tacticalmap";
        private const string RuntimeStateKey = "tacticalMap.runtime";
        private const string StaticStateKey = "tacticalMap.static";

        private static readonly Lazy<TacticalMapHtmlUi> _instance =
            new Lazy<TacticalMapHtmlUi>(() => new TacticalMapHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private TacticalMapController _controller;
        private bool _registered;
        private bool _pageOpened;
        private float _publishAccum;
        private string _lastRuntimeSignature;
        private int _lastTerrainSignature;
        private TacticalMapUiMode _mode = TacticalMapUiMode.CompactPassive;
        private string _terrainBase64;
        private string _tacticalBase64;
        private string _navMeshBase64;

        public static TacticalMapHtmlUi Instance => _instance.Value;
        public bool IsVisible => _pageOpened;
        public TacticalMapUiMode Mode => _mode;
        public bool IsInteractive => _mode == TacticalMapUiMode.FullInteractive;

        private TacticalMapHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
            string uiRoot = Path.Combine(assemblyDir, "UI");
            if (!Directory.Exists(uiRoot))
                throw new DirectoryNotFoundException("TacticalMap HtmlUI content root not found: " + uiRoot);

            _scope = HtmlUiService.CreateScope(OwnerId);
            _scope.RegisterContentRoot(ContentRootName, uiRoot);
            _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "TacticalMap/index.html")
            {
                ContentRootId = ContentRootName,
                HotReload = true,
                DefaultInputMode = HtmlUiInputMode.Passive,
                CloseOnEscape = false
            });

            RegisterCommands();
            _registered = true;
            HtmlUiLogger.Info("TacticalMap HtmlUI registered.");
            if (_controller != null) OpenForMission();
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("toggleInteractive", _ => ToggleInteractive());
            _scope.RegisterCommand("setInteractive", payload =>
            {
                bool value = payload?["value"]?.Value<bool>() ?? false;
                SetInteractive(value);
            });
            _scope.RegisterCommand("longPressNext", _ => AdvanceLongPress());
            _scope.RegisterCommand("escape", _ => SetInteractive(false));
            _scope.RegisterCommand("clientLog", payload =>
            {
                string message = payload?["message"]?.Value<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(message))
                    TacticalMapLog.Info("JS: " + message);
            });
            _scope.RegisterCommand("selectFormation", payload =>
            {
                string name = payload?["name"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(name))
                    _controller?.HandleHtmlClearFormationSelection();
                else
                    _controller?.HandleHtmlSelectFormation(name);
                PublishState(true);
            });
            _scope.RegisterCommand("move", payload =>
            {
                TacticalMapController controller = _controller;
                if (controller != null) ExecuteUv("move", payload, controller.HandleHtmlMoveClick);
            });
            _scope.RegisterCommand("face", payload =>
            {
                TacticalMapController controller = _controller;
                if (controller != null) ExecuteUv("face", payload, controller.HandleHtmlFaceClick);
            });
            _scope.RegisterCommand("camera", payload =>
            {
                TacticalMapController controller = _controller;
                if (controller != null) ExecuteUv("camera", payload, controller.HandleHtmlCameraClick);
            });
            _scope.RegisterCommand("refresh", _ => PublishState(true));
            _scope.RegisterRequest("getState", _ => Task.FromResult<object>(BuildRuntimeState()));
        }

        private static void ExecuteUv(string command, JToken payload, Action<float, float> handler)
        {
            if (handler == null || payload == null) return;
            float u = payload["u"]?.Value<float>() ?? -1f;
            float v = payload["v"]?.Value<float>() ?? -1f;
            handler(u, v);
        }

        public void AttachController(TacticalMapController controller)
        {
            _controller = controller;
            _mode = TacticalMapUiMode.CompactPassive;
            _publishAccum = 0f;
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _tacticalBase64 = null;
            _navMeshBase64 = null;
            if (_registered && HtmlUiService.IsReady) OpenForMission();
        }

        public void DetachController()
        {
            try
            {
                if (_pageOpened && _registered && HtmlUiService.IsReady)
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMap HtmlUI close failed.", ex); }

            _pageOpened = false;
            _controller = null;
            _mode = TacticalMapUiMode.CompactPassive;
            _publishAccum = 0f;
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _tacticalBase64 = null;
            _navMeshBase64 = null;
            try { HtmlUiService.SetInputMode(HtmlUiInputMode.Hidden); } catch { }
        }

        private void OpenForMission()
        {
            if (_controller == null || !_registered || !HtmlUiService.IsReady || _pageOpened) return;
            try
            {
                if (!HtmlUiService.Pages.Open(_pageId)) return;
                _pageOpened = true;
                ApplyInputMode();
                PublishState(true);
            }
            catch (Exception ex)
            {
                _pageOpened = false;
                TacticalMapLog.Error("TacticalMap HtmlUI open failed.", ex);
                HtmlUiLogger.Error("TacticalMap HtmlUI open failed.", ex);
            }
        }

        public void Tick(float dt)
        {
            if (_controller == null) return;
            if (!_pageOpened && _registered && HtmlUiService.IsReady) OpenForMission();
            if (!_pageOpened) return;

            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        public void ToggleInteractive()
        {
            SetInteractive(!IsInteractive);
        }

        public void AdvanceLongPress()
        {
            SetInteractive(!IsInteractive);
        }

        public void SetInteractive(bool interactive)
        {
            TacticalMapUiMode next = interactive ? TacticalMapUiMode.FullInteractive : TacticalMapUiMode.CompactPassive;
            if (_mode == next) return;
            _mode = next;
            ApplyInputMode();
            PublishState(true);
        }

        private void ApplyInputMode()
        {
            try
            {
                HtmlUiService.SetInputMode(IsInteractive
                    ? HtmlUiInputMode.MouseCaptured
                    : HtmlUiInputMode.Passive);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap HtmlUI input mode change failed.", ex);
            }
        }

        private void PublishState(bool force)
        {
            if (!_pageOpened || !_registered || _controller == null) return;
            try
            {
                PublishStaticStateIfChanged();
                object runtime = BuildRuntimeState();
                string signature = JsonConvert.SerializeObject(runtime, Formatting.None);
                if (!force && string.Equals(signature, _lastRuntimeSignature, StringComparison.Ordinal)) return;
                _lastRuntimeSignature = signature;
                _scope.SetState(RuntimeStateKey, runtime);
            }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMap HtmlUI state publish failed.", ex); }
        }

        private void PublishStaticStateIfChanged()
        {
            Terrain.TerrainCache cache = _controller.Cache;
            Terrain.NavMeshMap navMesh = _controller.NavigationMap;
            int terrainSignature = ComputeTerrainSignature(cache, navMesh);
            if (terrainSignature == _lastTerrainSignature) return;

            _terrainBase64 = cache.TerrainBaseRGBA == null ? null : Convert.ToBase64String(cache.TerrainBaseRGBA);
            _tacticalBase64 = TacticalSettings.Instance.EnableRiskOverlay && cache.TacticalRGBA != null
                ? Convert.ToBase64String(cache.TacticalRGBA) : null;
            _navMeshBase64 = navMesh != null && navMesh.RGBA != null
                ? Convert.ToBase64String(navMesh.RGBA) : null;
            _lastTerrainSignature = terrainSignature;

            _scope.SetState(StaticStateKey, new
            {
                width = cache.Width,
                height = cache.Height,
                baked = cache.IsBaked,
                error = cache.LastError ?? string.Empty,
                worldWidth = cache.WorldW,
                worldHeight = cache.WorldH,
                terrainVersion = terrainSignature,
                navMeshVersion = navMesh == null ? 0 : navMesh.Version,
                terrainBaseRgba = _terrainBase64,
                tacticalRgba = _tacticalBase64,
                riskRgba = _tacticalBase64,
                navMeshRgba = _navMeshBase64,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            });
        }

        private object BuildRuntimeState()
        {
            Terrain.TerrainCache cache = _controller.Cache;
            New_ZZZF.TacticalMap.Config.TacticalSettings settings = New_ZZZF.TacticalMap.Config.TacticalSettings.Instance;
            var formations = new List<object>();
            foreach (var f in _controller.FormationSnapshots)
            {
                Vec2 uv = cache.WorldToUV(f.AveragePosition);
                Vec2 orderUv = cache.WorldToUV(f.OrderPosition);
                var route = new List<object>();
                if (f.PathPoints != null)
                {
                    foreach (Vec2 worldPoint in f.PathPoints)
                    {
                        Vec2 pathUv = cache.WorldToUV(worldPoint);
                        route.Add(new
                        {
                            u = Clamp01(pathUv.X),
                            v = Clamp01(pathUv.Y)
                        });
                    }
                }

                formations.Add(new
                {
                    name = f.Name ?? string.Empty,
                    count = f.Count,
                    player = f.IsPlayer,
                    enemy = f.IsEnemy,
                    neutral = f.IsNeutral,
                    u = Clamp01(uv.X),
                    v = Clamp01(uv.Y),
                    facingU = f.Facing.X,
                    facingV = f.Facing.Y,
                    hasOrder = f.HasOrder,
                    orderU = Clamp01(orderUv.X),
                    orderV = Clamp01(orderUv.Y),
                    pathPoints = route
                });
            }

            var agents = new List<object>();
            Vec2? player = _controller.PlayerPos;
            if (player.HasValue && settings.EnableAgentMarkers)
            {
                float limitSquared = settings.AgentDetailDistance * settings.AgentDetailDistance;
                foreach (var agent in _controller.AgentSnapshots)
                {
                    Vec2 world = cache.UVToWorld(new Vec2(agent.U, agent.V));
                    if ((world - player.Value).LengthSquared > limitSquared) continue;
                    agents.Add(new
                    {
                        u = Clamp01(agent.U),
                        v = Clamp01(agent.V),
                        player = agent.PlayerTeam,
                        neutral = agent.Neutral
                    });
                }
            }

            Vec2? target = _controller.CameraTarget;
            Vec2 playerUv = player.HasValue ? cache.WorldToUV(player.Value) : Vec2.Zero;
            Vec2 targetUv = target.HasValue ? cache.WorldToUV(target.Value) : Vec2.Zero;
            return new
            {
                mode = _mode.ToString(),
                visible = _controller.IsVisible,
                interactive = IsInteractive,
                selectedFormation = _controller.SelectedFormationName,
                player = player.HasValue ? (object)new
                {
                    u = Clamp01(playerUv.X),
                    v = Clamp01(playerUv.Y),
                    facingU = _controller.PlayerFacing.X,
                    facingV = _controller.PlayerFacing.Y
                } : null,
                cameraTarget = target.HasValue ? (object)new { u = Clamp01(targetUv.X), v = Clamp01(targetUv.Y) } : null,
                formations,
                agents,
                agentVersion = _controller.AgentDataVersion,
                agentDetailDistance = settings.AgentDetailDistance
            };
        }

        private static int ComputeTerrainSignature(Terrain.TerrainCache cache, Terrain.NavMeshMap navMesh)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + cache.Width;
                hash = hash * 31 + cache.Height;
                hash = hash * 31 + (cache.IsBaked ? 1 : 0);
                hash = hash * 31 + (cache.TerrainBaseRGBA == null ? 0 : cache.TerrainBaseRGBA.Length);
                hash = hash * 31 + (cache.TacticalRGBA == null ? 0 : cache.TacticalRGBA.Length);
                hash = hash * 31 + (cache.LastError ?? string.Empty).GetHashCode();
                hash = hash * 31 + (navMesh == null ? 0 : navMesh.Version);
                return hash;
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public void Dispose()
        {
            DetachController();
            try { _scope?.Dispose(); } catch { }
            _scope = null;
            _registered = false;
            _pageId = null;
        }
    }

    public enum TacticalMapUiMode
    {
        CompactPassive,
        FullInteractive
    }
}