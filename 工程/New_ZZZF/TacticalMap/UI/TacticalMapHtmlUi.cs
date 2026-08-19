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

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady)
                return;

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
                DefaultInputMode = HtmlUiInputMode.Passive
            });

            RegisterCommands();
            _registered = true;
            HtmlUiLogger.Info("TacticalMap HtmlUI registered.");

            if (_controller != null)
                OpenForMission();
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("toggleInteractive", _ => ToggleInteractive());
            _scope.RegisterCommand("setInteractive", payload => SetInteractive(payload?["value"]?.Value<bool>() ?? false));
            _scope.RegisterCommand("longPressNext", _ => AdvanceLongPress());
            _scope.RegisterCommand("escape", _ => SetInteractive(false));
            _scope.RegisterCommand("selectFormation", payload =>
            {
                string name = payload?["name"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(name))
                    _controller?.HandleHtmlClearFormationSelection();
                else
                    _controller?.HandleHtmlSelectFormation(name);
                PublishState(true);
            });
            _scope.RegisterCommand("move", payload => ExecuteUv(payload, _controller?.HandleHtmlMoveClick));
            _scope.RegisterCommand("face", payload => ExecuteUv(payload, _controller?.HandleHtmlFaceClick));
            _scope.RegisterCommand("camera", payload => ExecuteUv(payload, _controller?.HandleHtmlCameraClick));
            _scope.RegisterCommand("refresh", _ => PublishState(true));
            _scope.RegisterRequest("getState", _ => System.Threading.Tasks.Task.FromResult<object>(BuildRuntimeState()));
        }

        private static void ExecuteUv(JToken payload, Action<float, float> handler)
        {
            if (handler == null || payload == null)
                return;
            float u = payload["u"]?.Value<float>() ?? -1f;
            float v = payload["v"]?.Value<float>() ?? -1f;
            handler(u, v);
        }

        public void AttachController(TacticalMapController controller)
        {
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

            if (_registered && HtmlUiService.IsReady)
                OpenForMission();
        }

        public void DetachController()
        {
            try
            {
                if (_pageOpened && _registered && HtmlUiService.IsReady)
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("TacticalMap HtmlUI close failed.", ex);
            }

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
        }

        private void OpenForMission()
        {
            if (_controller == null || !_registered || !HtmlUiService.IsReady || _pageOpened)
                return;

            try
            {
                if (!HtmlUiService.Pages.Open(_pageId))
                    return;
                _pageOpened = true;
                ApplyInputMode();
                PublishState(true);
            }
            catch (Exception ex)
            {
                _pageOpened = false;
                HtmlUiLogger.Error("TacticalMap HtmlUI open failed.", ex);
            }
        }

        public void Tick(float dt)
        {
            if (_controller == null)
                return;

            if (!_pageOpened && _registered && HtmlUiService.IsReady)
                OpenForMission();

            UpdateToggleKey(dt);

            if (!_pageOpened)
                return;

            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f)
                return;
            _publishAccum = 0f;
            PublishState(false);
        }

        private void UpdateToggleKey(float dt)
        {
            if (IsInteractive && !_toggleKeyDown)
                return;

            bool isDown;
            try { isDown = Input.IsKeyDown(TacticalSettings.Instance.ToggleKey); }
            catch { return; }

            if (isDown && !_toggleKeyDown)
            {
                _toggleKeyDown = true;
                _keyHoldAccum = 0f;
                _longPressTriggered = false;
                return;
            }

            if (isDown)
            {
                _keyHoldAccum += Math.Max(0f, dt);
                if (!_longPressTriggered && _keyHoldAccum >= TacticalSettings.Instance.ToggleLongPressThreshold)
                {
                    _longPressTriggered = true;
                    AdvanceLongPress();
                }
                return;
            }

            if (_toggleKeyDown)
            {
                if (!_longPressTriggered)
                    ToggleInteractive();
                _toggleKeyDown = false;
                _keyHoldAccum = 0f;
                _longPressTriggered = false;
            }
        }

        private void ToggleInteractive()
        {
            if (_mode == TacticalMapUiMode.CompactPassive)
                _mode = TacticalMapUiMode.CompactInteractive;
            else if (_mode == TacticalMapUiMode.CompactInteractive)
                _mode = TacticalMapUiMode.CompactPassive;
            else if (_mode == TacticalMapUiMode.FullPassive)
                _mode = TacticalMapUiMode.FullInteractive;
            else if (_mode == TacticalMapUiMode.FullInteractive)
                _mode = TacticalMapUiMode.FullPassive;
            else
                _mode = TacticalMapUiMode.CompactPassive;
            ApplyInputMode();
            PublishState(true);
        }

        private void AdvanceLongPress()
        {
            switch (_mode)
            {
                case TacticalMapUiMode.CompactPassive:
                    _mode = TacticalMapUiMode.FullPassive;
                    break;
                case TacticalMapUiMode.CompactInteractive:
                    _mode = TacticalMapUiMode.FullInteractive;
                    break;
                case TacticalMapUiMode.FullPassive:
                case TacticalMapUiMode.FullInteractive:
                    _mode = TacticalMapUiMode.Hidden;
                    break;
                default:
                    _mode = TacticalMapUiMode.CompactPassive;
                    break;
            }
            ApplyInputMode();
            PublishState(true);
        }

        private void SetInteractive(bool interactive)
        {
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
            ApplyInputMode();
            PublishState(true);
        }

        private void ApplyInputMode()
        {
            try
            {
                HtmlUiService.SetInputMode(IsInteractive ? HtmlUiInputMode.Captured : HtmlUiInputMode.Passive);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("TacticalMap HtmlUI input mode change failed.", ex);
            }
        }

        private void PublishState(bool force)
        {
            if (!_pageOpened || !_registered || _controller == null)
                return;

            try
            {
                PublishStaticStateIfChanged();
                var runtime = BuildRuntimeState();
                string signature = JsonConvert.SerializeObject(runtime, Formatting.None);
                if (!force && string.Equals(signature, _lastRuntimeSignature, StringComparison.Ordinal))
                    return;
                _lastRuntimeSignature = signature;
                _scope.SetState(RuntimeStateKey, runtime);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("TacticalMap HtmlUI state publish failed.", ex);
            }
        }

        private void PublishStaticStateIfChanged()
        {
            var cache = _controller.Cache;
            int terrainSignature = ComputeTerrainSignature(cache);
            if (terrainSignature == _lastTerrainSignature)
                return;

            _terrainBase64 = cache.TerrainBaseRGBA == null ? null : Convert.ToBase64String(cache.TerrainBaseRGBA);
            _riskBase64 = TacticalSettings.Instance.EnableRiskOverlay && cache.RiskRGBA != null
                ? Convert.ToBase64String(cache.RiskRGBA)
                : null;

            _lastTerrainSignature = terrainSignature;
            _scope.SetState(StaticStateKey, new
            {
                width = cache.Width,
                height = cache.Height,
                terrainVersion = terrainSignature,
                terrainBaseRgba = _terrainBase64,
                riskRgba = _riskBase64,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            });
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
                float detailDistanceSquared = settings.AgentDetailDistance * settings.AgentDetailDistance;
                foreach (var agent in _controller.AgentSnapshots)
                {
                    Vec2 world = cache.UVToWorld(new Vec2(agent.U, agent.V));
                    if ((world - player.Value).LengthSquared > detailDistanceSquared)
                        continue;
                    agents.Add(new
                    {
                        u = Clamp01(agent.U),
                        v = Clamp01(agent.V),
                        player = agent.PlayerTeam,
                        neutral = agent.Neutral
                    });
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
                player = player.HasValue ? (object)new
                {
                    u = Clamp01(playerUv.X),
                    v = Clamp01(playerUv.Y),
                    facingU = _controller.PlayerFacing.X,
                    facingV = _controller.PlayerFacing.Y
                } : null,
                cameraTarget = target.HasValue ? (object)new
                {
                    u = Clamp01(targetUv.X),
                    v = Clamp01(targetUv.Y)
                } : null,
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
        CompactInteractive,
        FullPassive,
        FullInteractive,
        Hidden
    }
}
