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
        private Terrain.TerrainPhotographer _photographer;
        private int _fpsFrames;
        private float _fpsAccum;
        private float _worstFrame;
        private int _spikeFrames;
        private static int _battleSeq;
        private int _thisBattleSeq;
        private System.Diagnostics.Stopwatch _battleWatch;

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
            TickPhotoCapture();

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
                string perf = Diagnostics.TacticalMapPerf.DrainReport(5.0);
                TacticalMapLog.Info("Mission heartbeat. UIVisible=" + TacticalMapHtmlUi.Instance.IsVisible +
                                    " Mode=" + TacticalMapHtmlUi.Instance.Mode +
                                    " Baked=" + _controller.Cache.IsBaked +
                                    " FPS=" + (avgFrame > 0f ? (1f / avgFrame).ToString("0") : "?") +
                                    " avgFrame=" + (avgFrame * 1000f).ToString("0.0") + "ms" +
                                    " worstFrame=" + (_worstFrame * 1000f).ToString("0") +
                                    "ms spikes250ms=" + _spikeFrames +
                                    " | perf " + perf +
                                    " | " + SampleProcessCpu(5.0));
                _fpsFrames = 0;
                _fpsAccum = 0f;
                _worstFrame = 0f;
                _spikeFrames = 0;
            }
        }

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

        /// <summary>驱动原生 SceneView 拍照器。渲染器自身负责 Scene/Camera/RT 与 PNG 回读。</summary>
        private void TickPhotoCapture()
        {
            if (_controller == null || !_ready) return;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            bool applied = false;
            try
            {
                if (_photographer == null) _photographer = new Terrain.TerrainPhotographer();
                if (!_photographer.IsActive && !_photographer.IsCompleted && !_photographer.Failed)
                    _photographer.Start(Mission, _controller.Cache);
                applied = _photographer.Tick();
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] tick failed.", ex);
            }
            Diagnostics.TacticalMapPerf.Add(Diagnostics.TacticalMapPerf.PhotoTick,
                System.Diagnostics.Stopwatch.GetTimestamp() - t0);

            if (applied)
                TacticalMapHtmlUi.Instance.PublishState(true);
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
            string photoState = _photographer != null
                ? "Native.IsCompleted=" + _photographer.IsCompleted + " Failed=" + _photographer.Failed
                : "Native=null";
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
            try { _photographer?.OnMissionEnd(); }
            catch (Exception ex) { TacticalMapLog.Error("[PhotoNative] mission end failed.", ex); }
            _photographer = null;
            base.OnEndMission();
        }
    }
}
