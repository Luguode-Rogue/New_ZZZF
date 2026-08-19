using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.InputSystem;
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

        private static readonly Lazy<TacticalMapHtmlUi> InstanceHolder =
            new Lazy<TacticalMapHtmlUi>(() => new TacticalMapHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private TacticalMapController _controller;
        private bool _registered;
        private bool _pageOpened;
        private float _publishAccum;
        private float _keyHoldAccum;
        private bool _toggleKeyDown;
        private bool _longPressTriggered;
        private string _lastRuntimeSignature;
        private int _lastTerrainSignature;
        private TacticalMapUiMode _mode = TacticalMapUiMode.CompactPassive;
        private string _terrainBase64;
        private string _riskBase64;

        public static TacticalMapHtmlUi Instance { get { return InstanceHolder.Value; } }
        public bool IsVisible { get { return _pageOpened && _mode != TacticalMapUiMode.Hidden; } }
        public TacticalMapUiMode Mode { get { return _mode; } }
        public bool IsInteractive
        {
            get { return _mode == TacticalMapUiMode.CompactInteractive || _mode == TacticalMapUiMode.FullInteractive; }
        }

        private TacticalMapHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            TacticalMapLog.Info("InitializeOnFrameworkReady. FrameworkReady=" + HtmlUiService.IsReady);
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            TacticalMapLog.Section("HTMLUI REGISTER");
            if (_registered || !HtmlUiService.IsReady)
            {
                TacticalMapLog.Info("Register skipped. Registered=" + _registered + ", Ready=" + HtmlUiService.IsReady);
                return;
            }

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                string uiRoot = Path.Combine(assemblyDir, "UI");
                TacticalMapLog.Info("AssemblyDir=" + assemblyDir);
                TacticalMapLog.Info("ContentRoot=" + uiRoot + ", Exists=" + Directory.Exists(uiRoot));
                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException(uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _scope.RegisterContentRoot(ContentRootName, uiRoot);
                _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "TacticalMap/index.html")
                {
                    ContentRootId = ContentRootName,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Passive
                });
                RegisterCommands();
                _registered = true;
                TacticalMapLog.Info("Registration SUCCESS. PageId=" + _pageId);
                if (_controller != null) OpenForMission();
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("Registration FAILED.", ex);
                throw;
            }
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("clientLog", payload =>
            {
                string message = payload?["message"]?.Value<string>() ?? "<empty>";
                TacticalMapLog.Info("JS: " + message);
            });
            _scope.RegisterCommand("toggleInteractive", _ => ToggleInteractive());
            _scope.RegisterCommand("setInteractive", payload => SetInteractive(payload?["value"]?.Value<bool>() ?? false));
            _scope.RegisterCommand("longPressNext", _ => AdvanceLongPress());
            _scope.RegisterCommand("escape", _ => SetInteractive(false));
            _scope.RegisterCommand("selectFormation", payload =>
            {
                string name = payload?["name"]?.Value<string>();
                TacticalMapLog.Info("HTML command selectFormation=" + (name ?? "<clear>"));
                if (string.IsNullOrWhiteSpace(name)) _controller?.HandleHtmlClearFormationSelection();
                else _controller?.HandleHtmlSelectFormation(name);
                PublishState(true);
            });
            _scope.RegisterCommand("move", payload => ExecuteUv("move", payload, _controller?.HandleHtmlMoveClick));
            _scope.RegisterCommand("face", payload => ExecuteUv("face", payload, _controller?.HandleHtmlFaceClick));
            _scope.RegisterCommand("camera", payload => ExecuteUv("camera", payload, _controller?.HandleHtmlCameraClick));
            _scope.RegisterCommand("refresh", _ => PublishState(true));
            _scope.RegisterRequest("getState", _ => Task.FromResult<object>(BuildRuntimeState()));
        }

        private static void ExecuteUv(string command, JToken payload, Action<float, float> handler)
        {
            TacticalMapLog.Info("HTML command " + command + " received. Handler=" + (handler != null));
            if (handler == null || payload == null) return;
            float u = payload["u"]?.Value<float>() ?? -1f;
            float v = payload["v"]?.Value<float>() ?? -1f;
            TacticalMapLog.Info(command + " UV=" + u + "," + v);
            handler(u, v);
        }

        public void AttachController(TacticalMapController controller)
        {
            TacticalMapLog.Section("HTMLUI ATTACH CONTROLLER");
            _controller = controller;
            _mode = TacticalMapUiMode.CompactPassive;
            ResetInputState();
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _riskBase64 = null;
            TacticalMapLog.Info("AttachController. Registered=" + _registered + ", Ready=" + HtmlUiService.IsReady);
            if (_registered && HtmlUiService.IsReady) OpenForMission();
        }

        public void DetachController()
        {
            TacticalMapLog.Section("HTMLUI DETACH CONTROLLER");
            try
            {
                if (_pageOpened && _registered && HtmlUiService.IsReady)
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex) { TacticalMapLog.Error("Page close failed.", ex); }

            _pageOpened = false;
            _controller = null;
            _mode = TacticalMapUiMode.CompactPassive;
            ResetInputState();
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _riskBase64 = null;
            try { HtmlUiService.SetInputMode(HtmlUiInputMode.Passive); } catch { }
            TacticalMapLog.Info("DetachController completed.");
        }

        private void OpenForMission()
        {
            if (_controller == null || !_registered || !HtmlUiService.IsReady || _pageOpened) return;
            try
            {
                if (!HtmlUiService.Pages.Open(_pageId))
                {
                    TacticalMapLog.Warn("Pages.Open returned false.");
                    return;
                }
                _pageOpened = true;
                ApplyInputMode();
                TacticalMapLog.Info("PAGE OPENED. PageId=" + _pageId);
                PublishState(true);
            }
            catch (Exception ex)
            {
                _pageOpened = false;
                TacticalMapLog.Error("Page open failed.", ex);
            }
        }

        public void Tick(float dt)
        {
            if (_controller == null) return;
            if (!_pageOpened && _registered && HtmlUiService.IsReady) OpenForMission();

            // Before the page captures input, Bannerlord receives N directly.
            // Once Captured, JS owns N/ESC so WebView2 focus does not break the state machine.
            if (!IsInteractive) UpdatePassiveToggleKey(dt);

            if (!_pageOpened) return;
            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum >= 0.10f)
            {
                _publishAccum = 0f;
                PublishState(false);
            }
        }

        private void UpdatePassiveToggleKey(float dt)
        {
            bool isDown;
            try { isDown = Input.IsKeyDown(TacticalSettings.Instance.ToggleKey); }
            catch (Exception ex) { TacticalMapLog.Error("Toggle key read failed.", ex); return; }

            if (isDown && !_toggleKeyDown)
            {
                _toggleKeyDown = true;
                _keyHoldAccum = 0f;
                _longPressTriggered = false;
                TacticalMapLog.Info("Passive N DOWN. Mode=" + _mode);
                return;
            }

            if (isDown)
            {
                _keyHoldAccum += Math.Max(0f, dt);
                if (!_longPressTriggered && _keyHoldAccum >= TacticalSettings.Instance.ToggleLongPressThreshold)
                {
                    _longPressTriggered = true;
                    TacticalMapLog.Info("Passive N LONG. Duration=" + _keyHoldAccum);
                    AdvanceLongPress();
                }
                return;
            }

            if (_toggleKeyDown)
            {
                if (!_longPressTriggered)
                {
                    TacticalMapLog.Info("Passive N SHORT. Duration=" + _keyHoldAccum);
                    ToggleInteractive();
                }
                ResetInputState();
            }
        }

        private void ToggleInteractive()
        {
            TacticalMapUiMode before = _mode;
            switch (_mode)
            {
                case TacticalMapUiMode.CompactPassive: _mode = TacticalMapUiMode.CompactInteractive; break;
                case TacticalMapUiMode.CompactInteractive: _mode = TacticalMapUiMode.CompactPassive; break;
                case TacticalMapUiMode.FullPassive: _mode = TacticalMapUiMode.FullInteractive; break;
                case TacticalMapUiMode.FullInteractive: _mode = TacticalMapUiMode.FullPassive; break;
                default: _mode = TacticalMapUiMode.CompactPassive; break;
            }
            TacticalMapLog.Info("ToggleInteractive: " + before + " -> " + _mode);
            ApplyInputMode();
            PublishState(true);
        }

        private void AdvanceLongPress()
        {
            TacticalMapUiMode before = _mode;
            switch (_mode)
            {
                case TacticalMapUiMode.CompactPassive: _mode = TacticalMapUiMode.FullPassive; break;
                case TacticalMapUiMode.CompactInteractive: _mode = TacticalMapUiMode.FullInteractive; break;
                case TacticalMapUiMode.FullPassive:
                case TacticalMapUiMode.FullInteractive: _mode = TacticalMapUiMode.Hidden; break;
                default: _mode = TacticalMapUiMode.CompactPassive; break;
            }
            TacticalMapLog.Info("AdvanceLongPress: " + before + " -> " + _mode);
            ApplyInputMode();
            PublishState(true);
        }

        private void SetInteractive(bool interactive)
        {
            TacticalMapUiMode before = _mode;
            switch (_mode)
            {
                case TacticalMapUiMode.FullPassive:
                case TacticalMapUiMode.FullInteractive:
                    _mode = interactive ? TacticalMapUiMode.FullInteractive : TacticalMapUiMode.FullPassive;
                    break;
                case TacticalMapUiMode.Hidden:
                    _mode = interactive ? TacticalMapUiMode.CompactInteractive : TacticalMapUiMode.CompactPassive;
                    break;
                default:
                    _mode = interactive ? TacticalMapUiMode.CompactInteractive : TacticalMapUiMode.CompactPassive;
                    break;
            }
            TacticalMapLog.Info("SetInteractive(" + interactive + "): " + before + " -> " + _mode);
            ApplyInputMode();
            PublishState(true);
        }

        private void ApplyInputMode()
        {
            try
            {
                HtmlUiInputMode inputMode = IsInteractive ? HtmlUiInputMode.Captured : HtmlUiInputMode.Passive;
                HtmlUiService.SetInputMode(inputMode);
                TacticalMapLog.Info("InputMode=" + inputMode + ", Mode=" + _mode);
            }
            catch (Exception ex) { TacticalMapLog.Error("SetInputMode failed.", ex); }
        }

        private void ResetInputState()
        {
            _keyHoldAccum = 0f;
            _toggleKeyDown = false;
            _longPressTriggered = false;
        }

        private void PublishState(bool force)
        {
            if (!_pageOpened || !_registered || _controller == null) return;
            try
            {
                PublishStaticStateIfChanged();
                object runtime = BuildRuntimeState();
                string signature = JsonConvert.SerializeObject(runtime, Formatting.None);
                if (!force && signature == _lastRuntimeSignature) return;
                _lastRuntimeSignature = signature;
                _scope.SetState(RuntimeStateKey, runtime);
            }
            catch (Exception ex) { TacticalMapLog.Error("State publish failed.", ex); }
        }

        private void PublishStaticStateIfChanged()
        {
            var cache = _controller.Cache;
            int signature = ComputeTerrainSignature(cache);
            if (signature == _lastTerrainSignature) return;

            _terrainBase64 = cache.TerrainBaseRGBA == null ? null : Convert.ToBase64String(cache.TerrainBaseRGBA);
            _riskBase64 = TacticalSettings.Instance.EnableRiskOverlay && cache.RiskRGBA != null ? Convert.ToBase64String(cache.RiskRGBA) : null;
            _lastTerrainSignature = signature;
            _scope.SetState(StaticStateKey, new
            {
                width = cache.Width,
                height = cache.Height,
                baked = cache.IsBaked,
                error = cache.LastError ?? string.Empty,
                worldWidth = cache.WorldW,
                worldHeight = cache.WorldH,
                terrainVersion = signature,
                terrainBaseRgba = _terrainBase64,
                riskRgba = _riskBase64,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            });
            TacticalMapLog.Info("Static State published. Baked=" + cache.IsBaked + ", Size=" + cache.Width + "x" + cache.Height);
        }

        private object BuildRuntimeState()
        {
            var cache = _controller.Cache;
            var settings = TacticalSettings.Instance;
            var formations = new List<object>();
            foreach (var f in _controller.FormationSnapshots)
            {
                Vec2 uv = cache.WorldToUV(f.AveragePosition);
                formations.Add(new
                {
                    name = f.Name ?? string.Empty,
                    count = f.Count,
                    player = f.IsPlayer,
                    enemy = f.IsEnemy,
                    u = Clamp01(uv.X),
                    v = Clamp01(uv.Y),
                    facingU = f.Facing.X,
                    facingV = f.Facing.Y
                });
            }

            var agents = new List<object>();
            Vec2? player = _controller.PlayerPos;
            if (player.HasValue && settings.EnableAgentMarkers)
            {
                float maxDist2 = settings.AgentDetailDistance * settings.AgentDetailDistance;
                foreach (var agent in _controller.AgentSnapshots)
                {
                    Vec2 world = cache.UVToWorld(new Vec2(agent.U, agent.V));
                    if ((world - player.Value).LengthSquared > maxDist2) continue;
                    agents.Add(new { u = Clamp01(agent.U), v = Clamp01(agent.V), player = agent.PlayerTeam, neutral = agent.Neutral });
                }
            }

            Vec2 playerUv = player.HasValue ? cache.WorldToUV(player.Value) : Vec2.Zero;
            Vec2? target = _controller.CameraTarget;
            Vec2 targetUv = target.HasValue ? cache.WorldToUV(target.Value) : Vec2.Zero;

            return new
            {
                mode = _mode.ToString(),
                visible = _mode != TacticalMapUiMode.Hidden && _controller.IsVisible,
                interactive = IsInteractive,
                selectedFormation = _controller.SelectedFormationName,
                player = player.HasValue ? (object)new { u = Clamp01(playerUv.X), v = Clamp01(playerUv.Y), facingU = _controller.PlayerFacing.X, facingV = _controller.PlayerFacing.Y } : null,
                cameraTarget = target.HasValue ? (object)new { u = Clamp01(targetUv.X), v = Clamp01(targetUv.Y) } : null,
                formations = formations,
                agents = agents,
                agentVersion = _controller.AgentDataVersion
            };
        }

        private static int ComputeTerrainSignature(TacticalMap.Terrain.TerrainCache cache)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + cache.Width;
                hash = hash * 31 + cache.Height;
                hash = hash * 31 + (cache.IsBaked ? 1 : 0);
                hash = hash * 31 + (cache.TerrainBaseRGBA == null ? 0 : cache.TerrainBaseRGBA.Length);
                hash = hash * 31 + (cache.RiskRGBA == null ? 0 : cache.RiskRGBA.Length);
                hash = hash * 31 + (cache.LastError ?? string.Empty).GetHashCode();
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
            try { if (_scope != null) _scope.Dispose(); } catch { }
            _scope = null;
            _registered = false;
            _pageId = null;
        }
    }

    public enum TacticalMapUiMode
    {
        CompactPassive,
        CompactInteractive,
        FullPassive,
        FullInteractive,
        Hidden
    }
}
