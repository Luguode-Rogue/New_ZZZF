using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap 的全新 HTMLUI Consumer。
    /// 只负责数据发布、页面生命周期和用户动作转发；不直接访问 WebView2。
    /// </summary>
    public sealed class TacticalMapHtmlUi : IDisposable
    {
        public enum UiState
        {
            Hidden,
            CompactPassive,
            CompactInteractive,
            FullPassive,
            FullInteractive
        }

        private const string OwnerId = "New_ZZZF.TacticalMap";
        private const string ContentRootId = "tacticalmap";
        private const string PageId = "tacticalmap";

        private HtmlUiConsumerScope _scope;
        private TacticalMapController _controller;
        private string _contentRootId;
        private bool _registered;
        private bool _visible;
        private bool _fullscreen;
        private bool _interactive;
        private bool _staticPublished;
        private int _lastAgentVersion = -1;
        private float _stateAccumulator;
        private UiState _state = UiState.Hidden;

        public bool IsRegistered => _registered;
        public bool IsVisible => _visible;
        public bool IsFullscreen => _fullscreen;
        public bool IsInteractive => _interactive;
        public UiState State => _state;

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady)
                return;

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                string uiRoot = Path.Combine(assemblyDir, "TacticalMapUI");
                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException("TacticalMap HtmlUI 资源目录不存在: " + uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _contentRootId = _scope.RegisterContentRoot(ContentRootId, uiRoot);
                _scope.RegisterPage(new HtmlUiPage(PageId, "index.html")
                {
                    ContentRootId = _contentRootId,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Passive
                });

                _scope.RegisterCommand("mapClick", HandleMapClick);
                _scope.RegisterCommand("cameraClick", HandleCameraClick);
                _scope.RegisterCommand("exitInteraction", _ =>
                {
                    if (_visible)
                        SetUiState(_fullscreen ? UiState.FullPassive : UiState.CompactPassive);
                });
                _scope.RegisterCommand("toggleFullscreen", _ => ToggleFullscreenPreservingInteraction());
                _scope.RegisterCommand("refresh", _ => PublishState(true));
                _scope.RegisterRequest("getMapData", _ => Task.FromResult<object>(BuildMapData()));

                _registered = true;
                if (_visible)
                    ApplyOpenState();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] 注册失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void AttachController(TacticalMapController controller)
        {
            _controller = controller;
            _staticPublished = false;
            _lastAgentVersion = -1;
            _stateAccumulator = 0f;

            if (_controller == null)
            {
                SetUiState(UiState.Hidden);
                return;
            }

            if (_visible)
                PublishState(true);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                SetUiState(UiState.Hidden);
                return;
            }

            if (_state == UiState.Hidden)
                SetUiState(UiState.CompactPassive);
            else
                ApplyOpenState();
        }

        public void SetUiState(UiState state)
        {
            _state = state;
            _visible = state != UiState.Hidden;
            _fullscreen = state == UiState.FullPassive || state == UiState.FullInteractive;
            _interactive = state == UiState.CompactInteractive || state == UiState.FullInteractive;

            ApplyInputMode();

            if (!_registered || !HtmlUiService.IsReady)
                return;

            try
            {
                if (_visible)
                    ApplyOpenState();
                else
                    HtmlUiService.Pages.Close(PageId);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] 状态切换失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void ToggleInteraction()
        {
            if (!_visible)
                return;

            SetUiState(_fullscreen
                ? (_interactive ? UiState.FullPassive : UiState.FullInteractive)
                : (_interactive ? UiState.CompactPassive : UiState.CompactInteractive));
        }

        public void ToggleFullscreenPreservingInteraction()
        {
            if (!_visible)
                return;

            SetUiState(_fullscreen
                ? (_interactive ? UiState.CompactInteractive : UiState.CompactPassive)
                : (_interactive ? UiState.FullInteractive : UiState.FullPassive));
        }

        public void ResetForMission()
        {
            SetUiState(UiState.CompactPassive);
        }

        public void Tick(float dt)
        {
            if (!_visible || !_registered || _controller == null)
                return;

            _stateAccumulator += dt;
            if (_stateAccumulator < 0.10f && _staticPublished && _controller.AgentDataVersion == _lastAgentVersion)
                return;

            _stateAccumulator = 0f;
            PublishState(!_staticPublished);
        }

        private void ApplyOpenState()
        {
            if (!_registered || !HtmlUiService.IsReady)
                return;

            if (!HtmlUiService.Pages.Open(PageId))
            {
                _visible = false;
                _state = UiState.Hidden;
                _interactive = false;
                _fullscreen = false;
                ApplyInputMode();
                InformationManager.DisplayMessage(new InformationMessage("[TMap][HtmlUI] 页面打开失败: " + PageId));
                return;
            }

            ApplyInputMode();
            PublishState(!_staticPublished);
        }

        private void ApplyInputMode()
        {
            if (!HtmlUiService.IsReady)
                return;

            try
            {
                if (_interactive)
                    HtmlUiService.CaptureInput();
                else
                    HtmlUiService.ReleaseInput();
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] 输入模式切换失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private void HandleMapClick(JToken payload)
        {
            if (!_interactive || _controller == null)
                return;

            float u = payload?["u"]?.ToObject<float?>() ?? -1f;
            float v = payload?["v"]?.ToObject<float?>() ?? -1f;
            string mode = payload?["mode"]?.ToObject<string>() ?? "move";

            if (string.Equals(mode, "face", StringComparison.OrdinalIgnoreCase))
                _controller.HandleHtmlFaceClick(u, v);
            else
                _controller.HandleHtmlMoveClick(u, v);
        }

        private void HandleCameraClick(JToken payload)
        {
            if (!_interactive || _controller == null)
                return;

            float u = payload?["u"]?.ToObject<float?>() ?? -1f;
            float v = payload?["v"]?.ToObject<float?>() ?? -1f;
            _controller.HandleHtmlCameraClick(u, v);
        }

        private void PublishState(bool forceStatic)
        {
            if (_scope == null || _controller == null)
                return;

            var cache = _controller.Cache;
            var formations = _controller.FormationSnapshots
                .Select((x, index) =>
                {
                    Vec2 uv = cache.WorldToUV(x.AveragePosition);
                    return new
                    {
                        id = index,
                        player = x.IsPlayer,
                        enemy = x.IsEnemy,
                        name = x.Name ?? string.Empty,
                        count = x.Count,
                        color = x.Color,
                        u = uv.X,
                        v = uv.Y,
                        facingX = x.Facing.X,
                        facingY = x.Facing.Y
                    };
                })
                .ToArray();

            var agents = _controller.AgentSnapshots
                .Select(x => new
                {
                    u = x.U,
                    v = x.V,
                    player = x.PlayerTeam,
                    neutral = x.Neutral
                })
                .ToArray();

            Vec2? player = _controller.PlayerPos;
            Vec2? camera = _controller.CameraTarget;
            Vec2 facing = _controller.PlayerFacing;

            HtmlUiService.State.Set("tacticalmap", new
            {
                visible = _visible,
                fullscreen = _fullscreen,
                interactive = _interactive,
                uiState = _state.ToString(),
                player = player.HasValue ? new
                {
                    u = cache.WorldToUV(player.Value).X,
                    v = cache.WorldToUV(player.Value).Y,
                    facingX = facing.X,
                    facingY = facing.Y
                } : null,
                camera = camera.HasValue ? new
                {
                    u = cache.WorldToUV(camera.Value).X,
                    v = cache.WorldToUV(camera.Value).Y
                } : null,
                formations,
                agents,
                agentDetailDistance = TacticalSettings.Instance.AgentDetailDistance,
                agentVersion = _controller.AgentDataVersion
            });

            if (forceStatic && cache.IsBaked)
            {
                HtmlUiService.State.Set("tacticalmap.static", BuildMapData());
                _staticPublished = true;
            }

            _lastAgentVersion = _controller.AgentDataVersion;
        }

        private object BuildMapData()
        {
            if (_controller == null || !_controller.Cache.IsBaked)
                return new { ready = false };

            var cache = _controller.Cache;
            const int output = 160;
            byte[] terrain = DownsampleRgba(cache.TerrainBaseRGBA, cache.Width, cache.Height, output, output);
            byte[] risk = DownsampleRgba(cache.RiskRGBA, cache.Width, cache.Height, output, output);

            return new
            {
                ready = true,
                width = output,
                height = output,
                terrainRgba = Convert.ToBase64String(terrain),
                riskRgba = Convert.ToBase64String(risk),
                originX = cache.OriginX,
                originY = cache.OriginY,
                worldW = cache.WorldW,
                worldH = cache.WorldH
            };
        }

        private static byte[] DownsampleRgba(byte[] source, int sw, int sh, int dw, int dh)
        {
            if (source == null || sw <= 0 || sh <= 0)
                return new byte[0];

            byte[] output = new byte[dw * dh * 4];
            for (int y = 0; y < dh; y++)
            {
                int sy = Math.Min(sh - 1, (int)((long)y * sh / dh));
                for (int x = 0; x < dw; x++)
                {
                    int sx = Math.Min(sw - 1, (int)((long)x * sw / dw));
                    int si = (sy * sw + sx) * 4;
                    int di = (y * dw + x) * 4;
                    output[di] = source[si];
                    output[di + 1] = source[si + 1];
                    output[di + 2] = source[si + 2];
                    output[di + 3] = source[si + 3];
                }
            }
            return output;
        }

        public void Dispose()
        {
            try
            {
                if (_registered && HtmlUiService.IsReady)
                    HtmlUiService.Pages.Close(PageId);
            }
            catch
            {
            }

            try
            {
                _scope?.Dispose();
            }
            catch
            {
            }

            _scope = null;
            _contentRootId = null;
            _registered = false;
            _visible = false;
            _interactive = false;
            _fullscreen = false;
            _controller = null;
        }
    }
}
