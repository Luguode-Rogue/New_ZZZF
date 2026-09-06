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
        /// <summary>地图 PNG 图片 content root（指向 Logs\PhotoMapDump，运行时数据）。</summary>
        private const string ImageContentRootName = "tmapcache";
        /// <summary>与 HtmlUiHost.MapContentRoot 的 host 命名规则一致（scoped id 小写化+非法字符转'-'）。</summary>
        private const string ImageServiceBaseUrl = "https://bannerlord-htmlui-new-zzzf-tacticalmap-tmapcache.local";

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

            try
            {
                string assemblyDir = Path.GetDirectoryName(typeof(TacticalMapHtmlUi).Assembly.Location) ?? ".";
                DirectoryInfo binDir = Directory.GetParent(assemblyDir);
                DirectoryInfo moduleDir = binDir == null ? null : Directory.GetParent(binDir.FullName);
                string uiRoot = moduleDir == null
                    ? Path.Combine(assemblyDir, "UI")
                    : Path.Combine(moduleDir.FullName, "UI");
                if (!Directory.Exists(uiRoot))
                    throw new DirectoryNotFoundException("TacticalMap HtmlUI content root not found: " + uiRoot);

                _scope = HtmlUiService.CreateScope(OwnerId);
                _scope.RegisterContentRoot(ContentRootName, uiRoot);
                // 地图图片（terrain/navmesh/risk PNG）经虚拟主机由浏览器原生管线加载，
                // 绕开 4MB Base64 JSON + ExecuteScriptAsync 通道（进战斗卡顿残留大头）。
                string imageRoot = Terrain.PhotoMapDump.GetDumpDirectoryForContentRoot();
                if (imageRoot != null)
                    _scope.RegisterContentRoot(ImageContentRootName, imageRoot);
                _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "TacticalMap/index.html")
                {
                    ContentRootId = ContentRootName,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Passive,
                    CloseOnEscape = false
                });

                RegisterCommands();
                _registered = true;
                HtmlUiLogger.Info("TacticalMap HtmlUI registered. Root=" + uiRoot);
                if (_controller != null) OpenForMission();
            }
            catch (Exception ex)
            {
                // 必须隔离：本回调经 HtmlUiService.Ready 在 WebView2 UI 线程触发，
                // 异常冲入 WinForms 消息循环会导致不可控行为。降级为不注册（功能静默关闭）。
                TacticalMapLog.Error("TacticalMap HtmlUI registration failed; feature disabled.", ex);
                _registered = false;
                _pageOpened = false;
            }
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("toggleInteractive", _ => ToggleInteractive());
            _scope.RegisterCommand("setInteractive", payload =>
            {
                bool value = payload?["value"]?.Value<bool>() ?? false;
                SetInteractive(value);
            });
            _scope.RegisterCommand("escape", _ => SetInteractive(false));
            _scope.RegisterCommand("clientLog", payload =>
            {
                string message = payload?["message"]?.Value<string>() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(message))
                    TacticalMapLog.Info("JS: " + message);
            });
            _scope.RegisterCommand("canvasRect", payload =>
            {
                TacticalMapNativeMouseInterceptor.UpdateCanvasRect(
                    payload?["x"]?.Value<float>() ?? 0f,
                    payload?["y"]?.Value<float>() ?? 0f,
                    payload?["w"]?.Value<float>() ?? 0f,
                    payload?["h"]?.Value<float>() ?? 0f,
                    payload?["dpr"]?.Value<float>() ?? 1f);
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
            _scope.RegisterRequest("getState", payload => { var r = BuildRuntimeState(out string sig); return Task.FromResult<object>(r); });
            // 静态大图（地形照片 / 风险层 / NavMesh 的 Base64）走 Request 按需拉取：
            // 避免每次进战斗把数 MB Base64 塞进 State 广播（双重 JSON 序列化 + ~20MB LOH 字符串尖峰）。
            _scope.RegisterRequest("getMapData", _ => Task.FromResult<object>(BuildMapDataState()));
        }

        private static void ExecuteUv(string command, JToken payload, Action<float, float> handler)
        {
            if (handler == null || payload == null) return;
            float u = payload["u"]?.Value<float>() ?? -1f;
            float v = payload["v"]?.Value<float>() ?? -1f;
            TacticalMapLog.Info("HTML " + command + " click u=" + u.ToString("0.000") + " v=" + v.ToString("0.000"));
            handler(u, v);
        }

        public void AttachController(TacticalMapController controller)
        {
            _controller = controller;
            _mode = TacticalMapUiMode.CompactPassive;
            _publishAccum = 0f;
            _lastRuntimeSignature = null;
            _lastTerrainSignature = 0;
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

            // Native mouse path: Chromium input is unreliable while the game owns the foreground,
            // so interactive clicks are polled here instead (see TacticalMapNativeMouseInterceptor).
            if (_mode == TacticalMapUiMode.FullInteractive && _registered && HtmlUiService.IsReady)
            {
                var windowState = HtmlUiService.Host.GetWindowState();
                TacticalMapNativeMouseInterceptor.Tick(_controller, new System.Drawing.Rectangle(
                    windowState.Left, windowState.Top, windowState.Width, windowState.Height));
            }
            else
            {
                TacticalMapNativeMouseInterceptor.Tick(null, System.Drawing.Rectangle.Empty);
            }

            _publishAccum += Math.Max(0f, dt);
            // 0.33s（3fps）：小地图态势 3fps 足够，浏览器事件/eval 频率较 0.2s 降 40%
            // （开战后 payload 变大，每 0.2s 一次 40-80KB ExecuteScriptAsync 会反复打断 Chromium JS 线程）
            if (_publishAccum < 0.33f) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        public void ToggleInteractive()
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

        /// <summary>runtime state 的完整键（绕过 StateStore 直发 state 事件时使用）。</summary>
        private string RuntimeStateFullKey => OwnerId + "." + RuntimeStateKey;

        /// <summary>供拍照底图完成等外部事件强制重发布地图静态状态。</summary>
        public void PublishState(bool force)
        {
            if (!_pageOpened || !_registered || _controller == null) return;
            try
            {
                long pubStart = System.Diagnostics.Stopwatch.GetTimestamp();
                long buildStart = pubStart;
                PublishStaticStateIfChanged();
                Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.StaticPub,
                    System.Diagnostics.Stopwatch.GetTimestamp() - buildStart);

                buildStart = System.Diagnostics.Stopwatch.GetTimestamp();
                object runtime = BuildRuntimeState(out string signature);
                Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.RuntimeBuild,
                    System.Diagnostics.Stopwatch.GetTimestamp() - buildStart);

                if (!force && string.Equals(signature, _lastRuntimeSignature, StringComparison.Ordinal)) return;
                _lastRuntimeSignature = signature;
                // 绕过 StateStore 直发 state 事件：StateStore.AreEqual 对非标量做两次 JToken 全量
                // 转换（1000 agents 时每次 ~50ms），加上签名与 SendEvent 的序列化，每 0.2s 一轮
                // 把主线程均摊开销推到 20-40ms/帧（17:19 会话实测 FPS 63→22）。
                // 代价：framework.getStateSnapshot 不再含 runtime，页面初载后最长一个发布周期无数据。
                long sendStart = System.Diagnostics.Stopwatch.GetTimestamp();
                HtmlUiService.SendEvent("state:" + RuntimeStateFullKey, runtime);
                Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.RuntimeSend,
                    System.Diagnostics.Stopwatch.GetTimestamp() - sendStart);
            }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMap HtmlUI state publish failed.", ex); }
        }

        /// <summary>
        /// 静态 State 只发布元数据与版本号；Base64 大图由前端在版本变化时经 getMapData Request 拉取。
        /// </summary>
        private void PublishStaticStateIfChanged()
        {
            Terrain.TerrainCache cache = _controller.Cache;
            Terrain.NavMeshMap navMesh = _controller.NavigationMap;
            int terrainSignature = ComputeTerrainSignature(cache, navMesh);
            if (terrainSignature == _lastTerrainSignature) return;
            _lastTerrainSignature = terrainSignature;

            bool usePhoto = cache.PhotoBaseRGBA != null;
            byte[] risk = TacticalSettings.Instance.EnableRiskOverlay ? cache.TacticalRGBA : null;
            // 把照片/NavMesh/风险层写成固定名 PNG（同签名跳过），前端 <img> 直载
            Terrain.PhotoMapDump.WriteServiceImages(
                cache.PhotoBaseRGBA, cache.PhotoWidth, cache.PhotoHeight, cache.BakeSignature,
                navMesh == null ? null : navMesh.RGBA, cache.Width, cache.Height,
                navMesh == null ? 0 : navMesh.Version,
                risk, cache.Width, cache.Height, terrainSignature);
            TacticalMapLog.Info("[PhotoMap] static publishing (meta only): photo=" + (usePhoto ? "yes" : "no") +
                " photoVer=" + cache.PhotoVersion +
                (usePhoto ? " dims=" + cache.PhotoWidth + "x" + cache.PhotoHeight : "") +
                " terrainVersion=" + terrainSignature +
                " navMeshVer=" + (navMesh == null ? 0 : navMesh.Version));
            _scope.SetState(StaticStateKey, new
            {
                width = cache.Width,
                height = cache.Height,
                terrainWidth = usePhoto ? cache.PhotoWidth : cache.Width,
                terrainHeight = usePhoto ? cache.PhotoHeight : cache.Height,
                baked = cache.IsBaked,
                error = cache.LastError ?? string.Empty,
                worldWidth = cache.WorldW,
                worldHeight = cache.WorldH,
                terrainVersion = terrainSignature,
                navMeshVersion = navMesh == null ? 0 : navMesh.Version,
                photoReady = usePhoto,
                photoUrl = usePhoto ? ImageServiceBaseUrl + "/terrain.png?v=" + terrainSignature : null,
                navMeshUrl = navMesh != null && navMesh.RGBA != null
                    ? ImageServiceBaseUrl + "/navmesh.png?v=" + terrainSignature : null,
                riskUrl = risk != null ? ImageServiceBaseUrl + "/risk.png?v=" + terrainSignature : null,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            });
        }

        /// <summary>getMapData Request：返回含 Base64 大图的完整静态数据（前端按 terrainVersion 判断是否重新解码）。</summary>
        private object BuildMapDataState()
        {
            Terrain.TerrainCache cache = _controller.Cache;
            Terrain.NavMeshMap navMesh = _controller.NavigationMap;
            bool usePhoto = cache.PhotoBaseRGBA != null;
            byte[] tactical = TacticalSettings.Instance.EnableRiskOverlay ? cache.TacticalRGBA : null;
            return new
            {
                width = cache.Width,
                height = cache.Height,
                terrainWidth = usePhoto ? cache.PhotoWidth : cache.Width,
                terrainHeight = usePhoto ? cache.PhotoHeight : cache.Height,
                worldWidth = cache.WorldW,
                worldHeight = cache.WorldH,
                terrainVersion = _lastTerrainSignature,
                navMeshVersion = navMesh == null ? 0 : navMesh.Version,
                terrainBaseRgba = cache.PhotoBaseRGBA != null ? Convert.ToBase64String(cache.PhotoBaseRGBA) : null,
                tacticalRgba = tactical != null ? Convert.ToBase64String(tactical) : null,
                riskRgba = tactical != null ? Convert.ToBase64String(tactical) : null,
                navMeshRgba = navMesh != null && navMesh.RGBA != null ? Convert.ToBase64String(navMesh.RGBA) : null,
                enableRisk = TacticalSettings.Instance.EnableRiskOverlay
            };
        }

        /// <summary>
        /// 构建运行时状态并输出廉价签名（构建时顺手拼接，避免全量 JSON 序列化做去重）。
        /// agents/pathPoints 使用扁平数值数组（JSON 体积降 ~70%，序列化与传输同比例变便宜）。
        /// </summary>
        private object BuildRuntimeState(out string signature)
        {
            var sig = new System.Text.StringBuilder(4096);
            Terrain.TerrainCache cache = _controller.Cache;
            New_ZZZF.TacticalMap.Config.TacticalSettings settings = New_ZZZF.TacticalMap.Config.TacticalSettings.Instance;
            var formations = new List<object>();
            foreach (var f in _controller.FormationSnapshots)
            {
                Vec2 uv = cache.WorldToUV(f.AveragePosition);
                Vec2 orderUv = cache.WorldToUV(f.OrderPosition);

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
                    orderV = Clamp01(orderUv.Y)
                });

                sig.Append(f.Name).Append('|').Append(f.Count).Append('|')
                   .Append(Clamp01(uv.X)).Append(',').Append(Clamp01(uv.Y)).Append('|')
                   .Append(f.HasOrder ? '1' : '0')
                   .Append(Clamp01(orderUv.X)).Append(',').Append(Clamp01(orderUv.Y))
                   .Append(';');
            }

            // agents 扁平化：每 3 个 float = [u, v, flag]（flag bit0=玩家队，bit1=中立）。
            // 数量超限（战斗高峰数百人进视野）按步长抽稀——payload 有硬上限，
            // 避免 ExecuteScriptAsync 的 eval 负载随战况膨胀。
            var agentsFlat = new List<float>();
            Vec2? player = _controller.PlayerPos;
            if (player.HasValue && settings.EnableAgentMarkers)
            {
                float limitSquared = settings.AgentDetailDistance * settings.AgentDetailDistance;
                var inRange = new List<AgentMapSnapshot>();
                foreach (var agent in _controller.AgentSnapshots)
                {
                    Vec2 world = cache.UVToWorld(new Vec2(agent.U, agent.V));
                    if ((world - player.Value).LengthSquared > limitSquared) continue;
                    inRange.Add(agent);
                }

                const int MaxAgents = 350;
                int step = inRange.Count > MaxAgents ? (inRange.Count + MaxAgents - 1) / MaxAgents : 1;
                for (int i = 0; i < inRange.Count; i += step)
                {
                    var agent = inRange[i];
                    float u = Clamp01(agent.U);
                    float v = Clamp01(agent.V);
                    float flag = (agent.PlayerTeam ? 1f : 0f) + (agent.Neutral ? 2f : 0f);
                    agentsFlat.Add(u);
                    agentsFlat.Add(v);
                    agentsFlat.Add(flag);
                    sig.Append(u).Append(',').Append(v).Append(',').Append(flag).Append(';');
                }
            }

            Vec2? target = _controller.CameraTarget;
            Vec2 playerUv = player.HasValue ? cache.WorldToUV(player.Value) : Vec2.Zero;
            Vec2 targetUv = target.HasValue ? cache.WorldToUV(target.Value) : Vec2.Zero;
            sig.Append('|').Append(_mode).Append('|').Append(_controller.IsVisible)
               .Append('|').Append(IsInteractive).Append('|').Append(_controller.SelectedFormationName)
               .Append('|').Append(Clamp01(playerUv.X)).Append(',').Append(Clamp01(playerUv.Y))
               .Append('|').Append(Clamp01(targetUv.X)).Append(',').Append(Clamp01(targetUv.Y))
               .Append('|').Append(_controller.AgentDataVersion);
            signature = sig.ToString();

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
                agentsFlat,
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
                hash = hash * 31 + cache.PhotoWidth;
                hash = hash * 31 + cache.PhotoHeight;
                hash = hash * 31 + (cache.LastError ?? string.Empty).GetHashCode();
                hash = hash * 31 + (navMesh == null ? 0 : navMesh.Version);
                hash = hash * 31 + cache.PhotoVersion; // 拍照底图替换后必须重发布
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
