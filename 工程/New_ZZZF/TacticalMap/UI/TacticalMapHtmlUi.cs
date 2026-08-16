using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using BannerlordHtmlUI;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// TacticalMap 的第二套 HTML UI。旧 Gauntlet UI 保留并行运行；本类只负责 HtmlUI Bridge/UI 生命周期。
    /// </summary>
    public sealed class TacticalMapHtmlUi : IDisposable
    {
        private const string OwnerId = "New_ZZZF.TacticalMap";
        private const string PageName = "tacticalmap.html";
        private const string ContentRootName = "tacticalmap";

        private HtmlUiConsumerScope _scope;
        private string _rootId;
        private string _pageId;
        private TacticalMapController _controller;
        private bool _registered;
        private bool _visible;
        private bool _cameraLink;
        private bool _terrainPublished;
        private int _lastAgentVersion = -1;

        public bool IsRegistered => _registered;
        public bool IsVisible => _visible;

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                string uiRoot = Path.Combine(assemblyDir, "TacticalMapUI");

                _scope = HtmlUiService.CreateScope(OwnerId);
                _rootId = _scope.RegisterContentRoot(ContentRootName, uiRoot);
                _pageId = _scope.RegisterPage(
                    new HtmlUiPage(PageName, "index.html")
                    {
                        ContentRootId = _rootId,
                        HotReload = true,
                        DefaultInputMode = HtmlUiInputMode.Captured
                    });

                _scope.RegisterCommand("setCameraLink", payload =>
                {
                    bool enabled = payload?["enabled"]?.Value<bool>() ?? !_cameraLink;
                    if (_controller == null) return;
                    if (enabled != _cameraLink) _controller.ToggleCameraFollow();
                    _cameraLink = enabled;
                    PublishState(forceTerrain: false);
                });

                _scope.RegisterCommand("mapClick", payload =>
                {
                    if (_controller == null) return;
                    float u = payload?["u"]?.Value<float>() ?? -1f;
                    float v = payload?["v"]?.Value<float>() ?? -1f;
                    string mode = payload?["mode"]?.Value<string>() ?? "move";
                    _controller.HandleHtmlMapClick(u, v, mode);
                });

                _scope.RegisterCommand("close", _ => SetVisible(false));
                _scope.RegisterCommand("refresh", _ => PublishState(forceTerrain: true));

                _scope.RegisterRequest("getMapData", _ => Task.FromResult<object>(BuildMapData()));
                _registered = true;

                if (_controller != null && _visible)
                    SetVisible(true);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap][HtmlUI] 注册失败: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public void AttachController(TacticalMapController controller)
        {
            _controller = controller;
            if (_registered && _visible)
                PublishState(forceTerrain: true);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!_registered || !HtmlUiService.IsReady || _pageId == null)
                return;

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

        public void Tick()
        {
            if (!_visible || !_registered || _controller == null) return;
            PublishState(forceTerrain: !_terrainPublished);
        }

        private void PublishState(bool forceTerrain)
        {
            if (_scope == null || _controller == null) return;

            var cache = _controller.Cache;
            var formations = (_controller.FormationSnapshots ?? new List<FormationSnapshot>())
                .Select(x => new
                {
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

            Vec2? player = _controller.PlayerPos;
            Vec2? camera = _controller.CameraTarget;
            Vec2 facing = _controller.PlayerFacing;

            _scope.SetState("map", new
            {
                visible = _visible,
                cameraLink = _cameraLink,
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
                agentVersion = _controller.AgentDataVersion
            });

            if (forceTerrain && cache.IsBaked)
            {
                _scope.SetState("mapStatic", BuildMapData());
                _terrainPublished = true;
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
            if (source == null || sw <= 0 || sh <= 0) return Array.Empty<byte>();
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
                _terrainPublished = false;
                _lastAgentVersion = -1;
            }
        }
    }
}
