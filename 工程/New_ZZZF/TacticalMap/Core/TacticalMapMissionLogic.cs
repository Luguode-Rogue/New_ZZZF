using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using New_ZZZF.TacticalMap.UI;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Terrain;

namespace New_ZZZF.TacticalMap.Core
{
    public sealed class TacticalMapMissionLogic : MissionLogic
    {
        private TacticalMapController _controller;
        private MissionScreen _missionScreen;
        private bool _initialized;
        private bool _ready;
        private float _heartbeatAccum;
        private Terrain.TerrainPhotoCapture _photoCapture;
        private Terrain.TerrainPhotographer _photographer;
        private int _fpsFrames;       // heartbeat 周期内的帧计数（FPS 诊断）
        private float _fpsAccum;      // 帧时间累计（秒）
        private float _worstFrame;    // 周期内最差帧时间（秒）
        private int _spikeFrames;     // ≥250ms 的卡顿帧计数
        private static int _battleSeq;          // 进程内战场序号（跨场递增）
        private int _thisBattleSeq;
        private System.Diagnostics.Stopwatch _battleWatch;  // 本场时长

        public override void OnAfterMissionCreated()
        {
            if (_initialized) return;
            _thisBattleSeq = ++_battleSeq;
            _battleWatch = System.Diagnostics.Stopwatch.StartNew();
            TacticalMapLog.Section("BATTLE #" + _thisBattleSeq + " START");
            TacticalMapLog.Info("Mission=" + (Mission == null ? "null" : Mission.GetType().Name) +
                " Mode=" + (Mission == null ? "?" : Mission.Mode.ToString()) +
                " SceneReady=" + (Mission != null && Mission.Scene != null));
            try
            {
                base.OnAfterMissionCreated();
                InitializeController();
            }
            catch (Exception ex)
            {
                _initialized = true;
                _ready = false;
                TacticalMapLog.Error("Mission initialization threw.", ex);
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage(
                        $"[TMap] Mission 初始化异常: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (Mission == null)
            {
                TacticalMapHtmlUi.Instance.DetachController();
                _controller = null;
                _ready = false;
                return;
            }

            if (!_initialized && Mission.Scene != null)
                InitializeController();
            if (!_ready || _controller == null) return;

            if (_missionScreen == null)
            {
                _missionScreen = ScreenManager.TopScreen as MissionScreen;
                if (_missionScreen == null)
                {
                    // 诊断插桩（2026-09-06）：第二/三场战斗 heartbeat 完全缺失，怀疑 TopScreen 非
                    // MissionScreen（或主循环 tick 极慢被 dt 钳制）。无 MissionScreen 时也输出心跳，
                    // 直接观察真实 tick 频率与顶层 Screen 类型，区分"逻辑早退"与"主循环卡死"。
                    float diagFrame = Math.Max(0f, dt);
                    _fpsFrames++;
                    _fpsAccum += diagFrame;
                    if (diagFrame > _worstFrame) _worstFrame = diagFrame;
                    if (diagFrame >= 0.25f) _spikeFrames++;
                    _heartbeatAccum += diagFrame;
                    if (_heartbeatAccum >= 5f)
                    {
                        _heartbeatAccum = 0f;
                        float diagAvg = _fpsFrames > 0 ? _fpsAccum / _fpsFrames : 0f;
                        TacticalMapLog.Info("Mission heartbeat (NO MissionScreen). TopScreen=" +
                            (ScreenManager.TopScreen == null ? "<null>" : ScreenManager.TopScreen.GetType().FullName) +
                            " FPS=" + (diagAvg > 0f ? (1f / diagAvg).ToString("0") : "?") +
                            " avgFrame=" + (diagAvg * 1000f).ToString("0.0") + "ms" +
                            " worstFrame=" + (_worstFrame * 1000f).ToString("0") + "ms" +
                            " spikes250ms=" + _spikeFrames);
                        _fpsFrames = 0; _fpsAccum = 0f; _worstFrame = 0f; _spikeFrames = 0;
                    }
                    return;
                }
            }

            _controller.SetVisible(_missionScreen, true);
            _controller.Tick(Mission, _missionScreen, dt);
            TacticalMapHtmlUi.Instance.Tick(dt);
            TickPhotoCapture(dt);

            // 帧率诊断：heartbeat 每 5s 输出平均 FPS / 最差帧 / >250ms 卡顿帧计数
            float frame = Math.Max(0f, dt);
            _fpsFrames++;
            _fpsAccum += frame;
            if (frame > _worstFrame) _worstFrame = frame;
            if (frame >= 0.25f) _spikeFrames++;

            _heartbeatAccum += frame;
            if (_heartbeatAccum >= 5f)
            {
                _heartbeatAccum = 0f;
                float avgFrame = _fpsFrames > 0 ? _fpsAccum / _fpsFrames : 0f;
                // 分段耗时报告：各段为 5s 窗口内累计毫秒（括号内为调用次数），
                // 均摊到帧 = 值/5s/帧率；哪个段吃帧一目了然
                string perf = Diagnostics.TacticalMapPerf.DrainReport(5.0);
                TacticalMapLog.Info("Mission heartbeat. UIVisible=" + TacticalMapHtmlUi.Instance.IsVisible +
                                    " Mode=" + TacticalMapHtmlUi.Instance.Mode +
                                    " Baked=" + _controller.Cache.IsBaked +
                                    " FPS=" + (avgFrame > 0f ? (1f / avgFrame).ToString("0") : "?") +
                                    " avgFrame=" + (avgFrame * 1000f).ToString("0.0") + "ms" +
                                    " worstFrame=" + (_worstFrame * 1000f).ToString("0") + "ms" +
                                    " spikes250ms=" + _spikeFrames +
                                    " | perf " + perf +
                                    " | " + SampleProcessCpu(5.0));
                _fpsFrames = 0;
                _fpsAccum = 0f;
                _worstFrame = 0f;
                _spikeFrames = 0;
            }
        }

        /// <summary>进程级 CPU 采样（静态，跨场持续）：输出游戏与全部 WebView2 进程在窗口内的 CPU 占比。
        /// 单核满载 = 100%。用于把"开战掉帧"的责任在 游戏引擎 / 浏览器进程 之间定量切割。</summary>
        private static double _lastWebViewCpuMs;
        private static double _lastGameCpuMs;
        private static bool _cpuBaselineSet;

        private static string SampleProcessCpu(double windowSeconds)
        {
            try
            {
                double webMs = 0;
                int count = 0;
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("msedgewebview2"))
                {
                    try { webMs += p.TotalProcessorTime.TotalMilliseconds; count++; }
                    catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
                double gameMs = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

                if (!_cpuBaselineSet)
                {
                    _cpuBaselineSet = true;
                    _lastWebViewCpuMs = webMs;
                    _lastGameCpuMs = gameMs;
                    return "cpu(first-sample)";
                }

                double windowMs = System.Math.Max(1.0, windowSeconds * 1000.0);
                double webPct = (webMs - _lastWebViewCpuMs) / windowMs * 100.0;
                double gamePct = (gameMs - _lastGameCpuMs) / windowMs * 100.0;
                _lastWebViewCpuMs = webMs;
                _lastGameCpuMs = gameMs;
                return "cpu game=" + gamePct.ToString("0") + "%" +
                       " webview=" + webPct.ToString("0") + "%(" + count + "p)";
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("Process cpu sampling failed.", ex);
                return "cpu(n/a)";
            }
        }

        /// <summary>
        /// 拍照式底图：轻量烘焙完成后发起离屏俯视拍摄，分帧驱动直至应用。
        /// 每场战斗（同一轮次）只拍摄一次——不再于部署结束后重拍（2026-09-06 用户裁决）。
        /// 新路线（TerrainPhotoV2，默认）：渲染对象进程级单例、永不销毁的 TerrainPhotographer。
        /// 旧路线（PhotoMap，已停用）：TerrainPhotoCapture v6，代码保留供回退。
        /// </summary>
        private void TickPhotoCapture(float dt)
        {
            if (_controller == null || !_ready) return;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool applied = false;
            try
            {
                if (TacticalSettings.Instance.TerrainPhotoV2)
                {
                    if (_photographer == null) _photographer = new Terrain.TerrainPhotographer();
                    // 只在 Idle/未完成/未失败时 Start 一次——正在拍摄或已失败则只 Tick
                    if (!_photographer.IsActive && !_photographer.IsCompleted && !_photographer.Failed)
                        _photographer.Start(Mission, _controller.Cache);
                    applied = _photographer.Tick();
                }
                else if (TacticalSettings.Instance.PhotoMap)
                {
                    if (_photoCapture == null) _photoCapture = new Terrain.TerrainPhotoCapture();
                    if (!_photoCapture.IsActive && !_photoCapture.IsCompleted && !_photoCapture.Failed)
                        _photoCapture.Start(Mission, _controller.Cache);
                    applied = _photoCapture.Tick();
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoMap] tick failed.", ex);
            }
            Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.PhotoTick,
                System.Diagnostics.Stopwatch.GetTimestamp() - t0);

            if (applied)
                TacticalMapHtmlUi.Instance.PublishState(true); // 照片入缓存后强制重发布
        }

        private void InitializeController()
        {
            if (_initialized) return;
            _initialized = true;

            if (Mission == null) return;
            if (!FeatureGate.Enabled || !MissionSceneGuard.IsTacticalMapSupported(Mission))
            {
                _ready = false;
                return;
            }

            try
            {
                _controller = new TacticalMapController(Mission);
                _ready = _controller.Initialize(Mission);
                if (_ready)
                {
                    // Terrain bake samples the height/material layers. Scene geometry such as
                    // houses and fences is handled separately by SceneObstacleMap.
                    SceneObstacleMap.Rebuild(_controller.Cache, Mission.Scene);
                    TacticalMapHtmlUi.Instance.AttachController(_controller);
                }
            }
            catch (Exception ex)
            {
                _ready = false;
                TacticalMapLog.Error("Controller initialization failed.", ex);
            }
        }

        protected override void OnEndMission()
        {
            TacticalMapLog.Section("BATTLE #" + _thisBattleSeq + " END");
            long durMs = _battleWatch?.ElapsedMilliseconds ?? -1;
            string photoState =
                (_photographer != null ? "V2.IsCompleted=" + _photographer.IsCompleted : "V2=null") +
                (_photoCapture != null ? " | v6.IsCompleted=" + _photoCapture.IsCompleted + " IsActive=" + _photoCapture.IsActive + " Failed=" + _photoCapture.Failed : " | v6=null");
            TacticalMapLog.Info("duration=" + durMs + "ms photo[" + photoState + "]");
            try { TacticalMapHtmlUi.Instance.DetachController(); }
            catch (Exception ex) { TacticalMapLog.Error("TacticalMapHtmlUi.DetachController failed during mission end.", ex); }

            try
            {
                if (_controller != null && _missionScreen != null)
                    _controller.SetVisible(_missionScreen, false);
            }
            catch (Exception ex) { TacticalMapLog.Error("Controller visibility cleanup failed.", ex); }

            try { CameraController.Instance?.Destroy(); }
            catch (Exception ex) { TacticalMapLog.Error("CameraController cleanup failed.", ex); }

            CameraController.Instance = null;
            _missionScreen = null;
            _controller = null;
            _ready = false;
            _initialized = false;
            _heartbeatAccum = 0f;
            // 必须先显式释放渲染资源再丢弃引用：view 持有本场景指针，
            // 依赖 GC 终结器销毁会在下一场 Mission 开始时冻结引擎（实测）。
            try { _photoCapture?.Shutdown(); } catch (Exception ex) { TacticalMapLog.Error("[PhotoMap] shutdown failed.", ex); }
            _photoCapture = null;
            // v2：渲染对象进程级单例永不销毁，结束只停用视图
            try { _photographer?.OnMissionEnd(); } catch (Exception ex) { TacticalMapLog.Error("[PhotoV2] mission end failed.", ex); }
            _photographer = null;
            base.OnEndMission();
        }
    }
}
