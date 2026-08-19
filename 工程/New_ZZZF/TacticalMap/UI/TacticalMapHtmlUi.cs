using System;
using System.Collections.Generic;
using System.IO;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Tracking;
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
        private float _keyHoldAccum;
        private bool _toggleKeyDown;
        private bool _longPressTriggered;
        private string _lastRuntimeSignature;
        private int _lastTerrainSignature;
        private TacticalMapUiMode _mode = TacticalMapUiMode.CompactPassive;
        private string _terrainBase64;
        private string _riskBase64;

        public static TacticalMapHtmlUi Instance => _instance.Value;
        public bool IsVisible => _pageOpened && _mode != TacticalMapUiMode.Hidden;
        public TacticalMapUiMode Mode => _mode;
        public bool IsInteractive => _mode == TacticalMapUiMode.CompactInteractive || _mode == TacticalMapUiMode.FullInteractive;

        private TacticalMapHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            TacticalMapLog.Info("TacticalMapHtmlUi.InitializeOnFrameworkReady called. FrameworkReady=" + HtmlUiService.IsReady);
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            TacticalMapLog.Section("HTMLUI REGISTER");
            TacticalMapLog.Info("Register entered. FrameworkReady=" + HtmlUiService.IsReady);
            if (_registered || !HtmlUiService.IsReady)
            {
                TacticalMapLog.Info("Register skipped. Registered=" + _registered + ", Ready=" + HtmlUiService.IsReady);
                return;
            }

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                string uiRoot = Path.Combine(assemblyDir, "UI");
                TacticalMapLog.Info("HtmlUI AssemblyDir=" + assemblyDir);
                TacticalMapLog.Info("HtmlUI ContentRoot=" + uiRoot + ", Exists=" + Directory.Exists(uiRoot));
                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException("TacticalMap HtmlUI content root not found: " + uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _scope.RegisterContentRoot(ContentRootName, uiRoot);
                TacticalMapLog.Info("ContentRoot registered: " + ContentRootName);
                _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "TacticalMap/index.html")
                {
                    ContentRootId = ContentRootName,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Passive
                });
                TacticalMapLog.Info("Page registered. PageId=" + _pageId + ", Html=TacticalMap/index.html");

                RegisterCommands();
                _registered = true;
                HtmlUiLogger.Info("TacticalMap HtmlUI registered.");
                TacticalMapLog.Info("TacticalMap HtmlUI registration SUCCESS.");

                if (_controller != null)
                    OpenForMission();
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap HtmlUI registration FAILED.", ex);
                throw;
            }
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("toggleInteractive", _ => { TacticalMapLog.Info("HTML command: toggleInteractive"); ToggleInteractive(); });
            _scope.RegisterCommand("setInteractive", payload => { bool value = payload?["value"]?.Value<bool>() ?? false; TacticalMapLog.Info("HTML command: setInteractive=" + value); SetInteractive(value); });
            _scope.RegisterCommand("longPressNext", _ => { TacticalMapLog.Info("HTML command: longPressNext"); AdvanceLongPress(); });
            _scope.RegisterCommand("escape", _ => { TacticalMapLog.Info("HTML command: escape"); SetInteractive(false); });
            _scope.RegisterCommand("selectFormation", payload =>
            {
                string name = payload?["name"]?.Value<string>();
                TacticalMapLog.Info("HTML command: selectFormation=" + (name ?? "<clear>"));
                if (string.IsNullOrWhiteSpace(name)) _controller?.HandleHtmlClearFormationSelection();
                else _controller?.HandleHtmlSelectFormation(name);
                PublishState(true);
            });
            _scope.RegisterCommand("move", payload => { TacticalMapLog.Info("HTML command: move"); if (_controller != null) ExecuteUv(payload, _controller.HandleHtmlMoveClick); });
            _scope.RegisterCommand("face", payload => { TacticalMapLog.Info("HTML command: face"); if (_controller != null) ExecuteUv(payload, _controller.HandleHtmlFaceClick); });
            _scope.RegisterCommand("camera", payload => { TacticalMapLog.Info("HTML command: camera"); if (_controller != null) ExecuteUv(payload, _controller.HandleHtmlCameraClick); });
            _scope.RegisterCommand("refresh", _ => { TacticalMapLog.Info("HTML command: refresh"); PublishState(true); });
            _scope.RegisterRequest("getState", _ => { TacticalMapLog.Info("HTML request: getState"); return System.Threading.Tasks.Task.FromResult<object>(BuildRuntimeState()); });
        }

        private static void ExecuteUv(JToken payload, Action<float, float> handler)
        {
            if (handler == null || payload == null) return;
            float u = payload["u"]?.Value<float>() ?? -1f;
            float v = payload["v"]?.Value<float>() ?? -1f;
            TacticalMapLog.Info("ExecuteUv u=" + u + ", v=" + v);
            handler(u, v);
        }

        public void AttachController(TacticalMapController controller)
        {
            TacticalMapLog.Section("HTMLUI ATTACH CONTROLLER");
            TacticalMapLog.Info("AttachController controllerNull=" + (controller == null));
            _controller = controller;
            _mode = TacticalMapUiMode.CompactPassive;
            _publishAccum = 0f;
            _keyHoldAccum = 0f;
            _toggleKeyDown = false;
            _longPressTriggered = false;
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _riskBase64 = null;
            if (_registered && HtmlUiService.IsReady) OpenForMission();
            else TacticalMapLog.Warn("AttachController deferred. Registered=" + _registered + ", Ready=" + HtmlUiService.IsReady);
        }

        public void DetachController()
        {
            TacticalMapLog.Section("HTMLUI DETACH CONTROLLER");
            TacticalMapLog.Info("DetachController PageOpened=" + _pageOpened + ", Registered=" + _registered);
            try { if (_pageOpened && _registered && HtmlUiService.IsReady) HtmlUiService.Pages.Close(_pageId); }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMap HtmlUI close failed.", ex); HtmlUiLogger.Error("TacticalMap HtmlUI close failed.", ex); }
            _pageOpened = false;
            _controller = null;
            _mode = TacticalMapUiMode.CompactPassive;
            _publishAccum = 0f;
            _keyHoldAccum = 0f;
            _toggleKeyDown = false;
            _longPressTriggered = false;
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
            _terrainBase64 = null;
            _riskBase64 = null;
            try { HtmlUiService.SetInputMode(HtmlUiInputMode.Passive); } catch { }
            TacticalMapLog.Info("DetachController completed.");
        }

        private void OpenForMission()
        {
            TacticalMapLog.Info("OpenForMission controller=" + (_controller != null) + ", registered=" + _registered + ", ready=" + HtmlUiService.IsReady + ", opened=" + _pageOpened);
            if (_controller == null || !_registered || !HtmlUiService.IsReady || _pageOpened) return;
            try
            {
                if (!HtmlUiService.Pages.Open(_pageId))
                {
                    TacticalMapLog.Warn("HtmlUiService.Pages.Open returned false. PageId=" + _pageId);
                    return;
                }
                _pageOpened = true;
                ApplyInputMode();
                TacticalMapLog.Info("HTMLUI PAGE OPENED. PageId=" + _pageId);
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
            UpdateToggleKey(dt);
            if (!_pageOpened) return;
            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        private void UpdateToggleKey(float dt)
        {
            if (IsInteractive && !_toggleKeyDown) return;
            bool isDown;
            try { isDown = Input.IsKeyDown(TacticalSettings.Instance.ToggleKey); }
            catch (Exception ex) { TacticalMapLog.Error("Toggle key read failed.", ex); return; }
            if (isDown && !_toggleKeyDown)
            {
                _toggleKeyDown = true;
                _keyHoldAccum = 0f;
                _longPressTriggered = false;
                TacticalMapLog.Info("Toggle key DOWN. Key=" + TacticalSettings.Instance.ToggleKey);
                return;
            }
            if (isDown)
            {
                _keyHoldAccum += Math.Max(0f, dt);
                if (!_longPressTriggered && _keyHoldAccum >= TacticalSettings.Instance.ToggleLongPressThreshold)
                {
                    _longPressTriggered = true;
                    TacticalMapLog.Info("Toggle key LONG PRESS. Duration=" + _keyHoldAccum);
                    AdvanceLongPress();
                }
                return;
            }
            if (_toggleKeyDown)
            {
                if (!_longPressTriggered)
                {
                    TacticalMapLog.Info("Toggle key SHORT PRESS. Duration=" + _keyHoldAccum);
                    ToggleInteractive();
                }
                _toggleKeyDown = false;
                _keyHoldAccum = 0f;
                _longPressTriggered = false;
            }
        }

        private void ToggleInteractive()
        {
            TacticalMapUiMode before = _mode;
            if (_mode == TacticalMapUiMode.CompactPassive) _mode = TacticalMapUiMode.CompactInteractive;
            else if (_mode == TacticalMapUiMode.CompactInteractive) _mode = TacticalMapUiMode.CompactPassive;
            else if (_mode == TacticalMapUiMode.FullPassive) _mode = TacticalMapUiMode.FullInteractive;
            else if (_mode == TacticalMapUiMode.FullInteractive) _mode = TacticalMapUiMode.FullPassive;
            else _mode = TacticalMapUiMode.CompactPassive;
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
                case TacticalMapUiMode.FullInteractive: _mode = interactive ? TacticalMapUiMode.FullInteractive : TacticalMapUiMode.FullPassive; break;
                case TacticalMapUiMode.Hidden: _mode = interactive ? TacticalMapUiMode.CompactInteractive : TacticalMapUiMode.CompactPassive; break;
                default: _mode = interactive ? TacticalMapUiMode.CompactInteractive : TacticalMapUiMode.CompactPassive; break;
            }
            TacticalMapLog.Info("SetInteractive(" + interactive + "): " + before + " -> " + _mode);
            ApplyInputMode();
            PublishState(true);
        }

        private void ApplyInputMode()
        {
            try
            {
                HtmlUiInputMode mode = IsInteractive ? HtmlUiInputMode.Captured : HtmlUiInputMode.Passive;
                HtmlUiService.SetInputMode(mode);
                TacticalMapLog.Info("InputMode applied: " + mode);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap HtmlUI input mode change failed.", ex);
                HtmlUiLogger.Error("TacticalMap HtmlUI input mode change failed.", ex);
            }
        }

        private void PublishState(bool force)
        {
            if (!_pageOpened || !_registered || _controller == null) return;
            try
            {
                PublishStaticStateIfChanged();
                var runtime = BuildRuntimeState();
                string signature = JsonConvert.SerializeObject(runtime, Formatting.None);
                if (!force && string.Equals(signature, _lastRuntimeSignature, StringComparison.Ordinal)) return;
                _lastRuntimeSignature = signature;
                _scope.SetState(RuntimeStateKey, runtime);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("TacticalMap HtmlUI state publish failed.", ex);
                HtmlUiLogger.Error("TacticalMap HtmlUI state publish failed.", ex);
            }
        }

        private void PublishStaticStateIfChanged()
        {
            var cache = _controller.Cache;
            int terrainSignature = ComputeTerrainSignature(cache);
            if (terrainSignature == _lastTerrainSignature) return;
            _terrainBase64 = cache.TerrainBaseRGBA == null ? null : Convert.ToBase64String(cache.TerrainBaseRGBA);
            _riskBase64 = TacticalSettings.Instance.EnableRiskOverlay && cache.RiskRGBA != null ? Convert.ToBase64String(cache.RiskRGBA) : null;
            _lastTerrainSignature = terrainSignature;
            TacticalMapLog.Info("Publishing static State. Baked=" + cache.IsBaked + ", Size=" + cache.Width + "x" + cache.Height + ", TerrainBytes=" + (cache.TerrainBaseRGBA == null ? 0 : cache.TerrainBaseRGBA.Length) + ", RiskBytes=" + (cache.RiskRGBA == null ? 0 : cache.RiskRGBA.Length) + ", Error=" + (cache.LastError ?? "<none>"));
            _scope.SetState(StaticStateKey, new
            {
                width = cache.Width,
                height = cache.Height,
                baked = cache.IsBaked,
                error = cache.LastError ?? string.Empty,
                worldWidth = cache.WorldW,
                worldHeight = cache.WorldH,
                terrainVersion = terrainSignature,
                terrainBaseRgba = _terrainBase64,
                riskRgba = _riskBase64,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            });
            TacticalMapLog.Info("Static State published.");
        }

        private object BuildRuntimeState()
        {
            var cache = _controller.Cache;
            var settings = TacticalSettings.Instance;
            var formations = new List<object>();
            foreach (var f in _controller.FormationSnapshots)
            {
                Vec2 uv = cache.WorldToUV(f.AveragePosition);
                formations.Add(new { name = f.Name ?? string.Empty, count = f.Count, player = f.IsPlayer, enemy = f.IsEnemy, u = Clamp01(uv.X), v = Clamp01(uv.Y), facingU = f.Facing.X, facingV = f.Facing.Y });
            }
            var agents = new List<object>();
            Vec2? player = _controller.PlayerPos;
            if (player.HasValue && settings.EnableAgentMarkers)
            {
                float detailDistanceSquared = settings.AgentDetailDistance * settings.AgentDetailDistance;
                foreach (var agent in _controller.AgentSnapshots)
                {
                    Vec2 world = cache.UVToWorld(new Vec2(agent.U, agent.V));
                    if ((world - player.Value).LengthSquared > detailDistanceSquared) continue;
                    agents.Add(new { u = Clamp01(agent.U), v = Clamp01(agent.V), player = agent.PlayerTeam, neutral = agent.Neutral });
                }
            }
            var playerUv = player.HasValue ? cache.WorldToUV(player.Value) : Vec2.Zero;
            var target = _controller.CameraTarget;
            var targetUv = target.HasValue ? cache.WorldToUV(target.Value) : Vec2.Zero;
            return new
            {
                mode = _mode.ToString(),
                visible = _mode != TacticalMapUiMode.Hidden && _controller.IsVisible,
                interactive = IsInteractive,
                selectedFormation = _controller.SelectedFormationName,
                player = player.HasValue ? (object)new { u = Clamp01(playerUv.X), v = Clamp01(playerUv.Y), facingU = _controller.PlayerFacing.X, facingV = _controller.PlayerFacing.Y } : null,
                cameraTarget = target.HasValue ? (object)new { u = Clamp01(targetUv.X), v = Clamp01(targetUv.Y) } : null,
                formations,
                agents,
                agentVersion = _controller.AgentDataVersion
            };
        }

        private static int ComputeTerrainSignature(Terrain.TerrainCache cache)
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
            TacticalMapLog.Section("HTMLUI DISPOSE");
            DetachController();
            try { _scope?.Dispose(); } catch (Exception ex) { TacticalMapLog.Error("HtmlUi scope dispose failed.", ex); }
            _scope = null;
            _registered = false;
            _pageId = null;
            TacticalMapLog.Info("TacticalMapHtmlUi disposed.");
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
