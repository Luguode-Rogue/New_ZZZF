using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.UI
{
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
        private const string PageName = "tacticalmap.html";
        private const string ContentRootName = "tacticalmap";

        private HtmlUiConsumerScope _scope;
        private string _rootId;
        private string _pageId;
        private TacticalMapController _controller;
        private bool _registered;
        private bool _visible;
        private bool _fullscreen;
        private bool _interactive;
        private bool _terrainPublished;
        private int _lastAgentVersion = -1;
        private float _stateAccum;
        private UiState _uiState = UiState.CompactPassive;

        public bool IsRegistered => _registered;
        public bool IsVisible => _visible;
        public bool IsFullscreen => _fullscreen;
        public bool IsInteractive => _interactive;
        public UiState State => _uiState;

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                string uiRoot = ResolveUiRoot(assemblyDir);
                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException($"TacticalMap HtmlUI content root not found. Runtime='{Path.Combine(assemblyDir, "TacticalMapUI")}'");

                _scope = HtmlUiService.CreateScope(OwnerId);
                _rootId = _scope.RegisterContentRoot(ContentRootName, uiRoot);
                _pageId = _scope.RegisterPage(
                    new HtmlUiPage(PageName, "index.html")
                    {
                        ContentRootId = _rootId,
                        HotReload = true,
                        DefaultInputMode = HtmlUiInputMode.Passive
                    });

                _scope.RegisterCommand("mapClick", payload =>
                {
                    if (!_interactive || _controller == null) return;
                    float u = payload?["u"]?.ToObject<float?>() ?? -1f;
                    float v = payload?["v"]?.ToObject<float?>() ?? -1f;
                    string mode = payload?["mode"]?.ToObject<string>() ?? "move";
                    if (string.Equals(mode, "face", StringComparison.OrdinalIgnoreCase))
                        _controller.HandleHtmlFaceClick(u, v);
                    else
                        _controller.HandleHtmlMoveClick(u, v);
                });

                _scope.RegisterCommand("cameraClick", payload =>
                {
                    if (!_interactive || _controller == null) return;
                    float u = payload?["u"]?.ToObject<float?>() ?? -1f;
                    float v = payload?["v"]?.ToObject<float?>() ?? -1f;
                    _controller.HandleHtmlCameraClick(u, v);
                });

                _scope.RegisterCommand("exitInteraction", _ =>
                {
                    if (_visible) SetUiState(_fullscreen ? UiState.FullPassive : UiState.CompactPassive);
                });

                _scope.RegisterCommand("refresh", _ => PublishState(forceTerrain: true));
                _scope.RegisterRequest("getMapData", _ => Task.FromResult<object>(BuildMapData()));
                _registered = true;
                ApplyInputMode();

                if (_controller != null && _visible)
                    SetVisible(true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] 注册失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static string ResolveUiRoot(string assemblyDir)
        {
            string runtimeRoot = Path.Combine(assemblyDir, "TacticalMapUI");
            if (Directory.Exists(runtimeRoot)) return runtimeRoot;

            string sourceRoot = Path.GetFullPath(Path.Combine(
                assemblyDir,
                "..", "..", "..", "工程", "New_ZZZF", "TacticalMap", "HtmlUI"));
            if (Directory.Exists(sourceRoot))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TMap][HtmlUI] 运行时 UI 未部署，使用工程 HtmlUI 源目录回退路径。"));
                return sourceRoot;
            }

            return runtimeRoot;
        }

        public void AttachController(TacticalMapController controller)
        {
            _controller = controller;
            _terrainPublished = false;
            _lastAgentVersion = -1;
            _stateAccum = 0f;
            if (controller == null)
            {
                SetUiState(UiState.Hidden);
                return;
            }
            if (_visible)
                PublishState(forceTerrain: true);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
            {
                _uiState = UiState.Hidden;
                _interactive = false;
                _fullscreen = false;
            }
            else if (_uiState == UiState.Hidden)
            {
                _uiState = UiState.CompactPassive;
            }

            ApplyInputMode();
            if (!_registered || !HtmlUiService.IsReady || _pageId == null) return;

            try
            {
                if (visible)
                {
                    HtmlUiService.Pages.Open(_pageId);
                    PublishState(forceTerrain: !_terrainPublished);
                }
                else
                {
                    HtmlUiService.Pages.Close(_pageId);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] 显示切换失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void SetUiState(UiState state)
        {
            _uiState = state;
            _visible = state != UiState.Hidden;
            _fullscreen = state == UiState.FullPassive || state == UiState.FullInteractive;
            _interactive = state == UiState.CompactInteractive || state == UiState.FullInteractive;
            ApplyInputMode();
            PublishState(forceTerrain: false);
        }

        public void ToggleInteraction()
        {
            if (!_visible) return;
            SetUiState(_fullscreen
                ? (_interactive ? UiState.FullPassive : UiState.FullInteractive)
                : (_interactive ? UiState.CompactPassive : UiState.CompactInteractive));
        }

        public void ToggleFullscreenPreservingInteraction()
        {
            if (!_visible) return;
            SetUiState(_fullscreen
                ? (_interactive ? UiState.CompactInteractive : UiState.CompactPassive)
                : (_interactive ? UiState.FullInteractive : UiState.FullPassive));
        }

        public void ResetForMission() => SetUiState(UiState.CompactPassive);

        private void ApplyInputMode()
        {
            if (!HtmlUiService.IsReady) return;
            try
            {
                HtmlUiService.SetInputMode(_interactive ? HtmlUiInputMode.Captured : HtmlUiInputMode.Passive);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[TMap][HtmlUI] 输入模式切换失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void Tick(float dt = 0.016f)
        {
            if (!_visible || !_registered || _controller == null) return;
            _stateAccum += dt;
            if (_stateAccum < 0.10f && _terrainPublished && _controller.AgentDataVersion == _lastAgentVersion) return;
            _stateAccum = 0f;
            PublishState(forceTerrain: !_terrainPublished);
        }

        private void PublishState(bool forceTerrain)
        {
            if (_scope == null || _controller == null) return;

            var cache = _controller.Cache;
            var formations = (_controller.FormationSnapshots ?? new List<FormationSnapshot>())
                .Select((x, index) => new
                {
                    index,
                    player = x.IsPlayer,
                    enemy = x.IsEnemy,
                    x.Name,
                    count = x.Count,
                    x.Color,
                    x.AveragePosition.X,
                    y = x.AveragePosition.Y,
                    fx = x.Facing.X,
                    fy = x.Facing.Y,
                    uvx = cache.WorldToUV(x.AveragePosition).X,
                    uvy = cache.WorldToUV(x.AveragePosition).Y
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
            int agentVersion = _controller.AgentDataVersion;

            _scope.SetState("map", new
            {
                visible = _visible,
                fullscreen = _fullscreen,
                interactive = _interactive,
                uiState = _uiState.ToString(),
                player = player.HasValue ? new
                {
                    uvx = cache.WorldToUV(player.Value).X,
                    uvy = cache.WorldToUV(player.Value).Y,
                    fx = facing.X,
                    fy = facing.Y
                } : null,
                camera = camera.HasValue ? new
                {
                    uvx = cache.WorldToUV(camera.Value).X,
                    uvy = cache.WorldToUV(camera.Value).Y
                } : null,
                formations,
                agents,
                agentDetailDistance = TacticalSettings.Instance.AgentDetailDistance,
                agentVersion
            });

            if (forceTerrain && cache.IsBaked)
            {
                _scope.SetState("mapStatic", BuildMapData());
                _terrainPublished = true;
            }

            _lastAgentVersion = agentVersion;
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
            if (source == null || sw <= 0 || sh <= 0) return new byte[0];
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
                if (_registered && _scope != null)
                    _scope.Dispose();
            }
            catch { }
            finally
            {
                _scope = null;
                _controller = null;
                _registered = false;
                _visible = false;
                _fullscreen = false;
                _interactive = false;
                _terrainPublished = false;
                _lastAgentVersion = -1;
                _stateAccum = 0f;
                _uiState = UiState.Hidden;
            }
        }
    }
}
