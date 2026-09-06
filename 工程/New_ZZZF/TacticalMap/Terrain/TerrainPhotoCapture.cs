using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using IoPath = System.IO.Path;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 拍照式地形底图 v6：基于反编译引擎源码的官方 API 路线。
    /// 参考：TaleWorlds.MountAndBlade.View\Tableaus\SceneTableau.cs / BrightnessDemoTableau.cs。
    ///
    /// 可用性实测结论（2026-09-06，源码：骑砍2源码\1.5.0\TaleWorlds.Engine）：
    /// - Mission.IsDeploymentFinished：✅ 纯托管属性，等待部署结束的唯一安全信号（零原生调用）
    /// - SceneView.ReadyToRender()：✅ 可用，但必须在视图启用状态下查询
    /// - SceneView.CheckSceneReadyToRender()：❌ 对主战斗场景恒 false（独立渲染场景专用）
    /// - Scene.IsLoadingFinished()：❌ 禁止在 Mission tick 中轮询（疑似与部署期加载线程互斥）
    /// - SetRenderWithPostfx/SetDoQuickExposure/EnsurePostfxSystem：❌ 严禁对共享战斗场景的
    ///   第二视图开启——双视图 postfx 渲染竞争损坏引擎状态，原版 RayCast 访问违例闪退（实测）
    /// - Texture.GetPixelData(byte[])：❌ 对 RenderTarget 直接 AccessViolationException
    ///   （原生实现不支持 RT 回读，损坏状态异常无法托管捕获回退），唯一回读路线 = SaveToFile 落盘
    /// - Mission 结束必须 Shutdown()：view 挂已销毁场景依赖 GC 跨线程销毁会冻结下一场（实测）
    ///
    /// 渲染窗口设计（用户明确）：进入战斗（含部署阶段）即启用视图拍摄，出图后立即禁用；
    /// 每场战斗只拍一次；Mission 结束必须 Shutdown()（Cleanup）后才能丢弃引用，
    /// 否则 view 挂着已销毁场景依赖 GC 终结器跨线程销毁，下一场开场引擎冻结（实测）。
    ///
    /// 流程：创建(即启用) → 等 ReadyToRender → settle 缓冲(mip 级联) → 渲染 15 帧 → 禁用
    /// → SaveToFile 落盘轮询 → battle bounds 对齐重采样 + 自动感光 → 应用进缓存。
    /// </summary>
    public sealed class TerrainPhotoCapture
    {
        private enum Stage { Idle, WaitingSceneReady, WaitingRender, WaitingFile, Completed, Failed }

        private const int PhotoSize = 2048;          // 渲染目标长边分辨率：越高，引擎选的纹理 mip 越细
        private const int PublishSize = 1024;        // 发布到 HTML 的底图长边
        private const int MaxSceneReadyFrames = 3600; // 等待场景加载完成的总超时（~60s）
        private const int SettleFramesAfterLoading = 60; // 启用后的稳定缓冲（~1s；原 180 帧 3s 拉长"进行中"窗口）
        private const int MinRenderFrames = 15;      // 渲染窗口：稳定画面所需最少帧数（~0.25s，最小化 agent 闪烁暴露）
        private const int MaxWaitFrames = 600;

        /// <summary>跨战斗的照片缓存：键 = 场景签名。同一野怪场景退出再进直接复用，零渲染零落盘。</summary>
        private sealed class CachedPhoto
        {
            public byte[] Rgba;
            public int Width;
            public int Height;
            public float WorldW;
            public float WorldH;
        }

        private static readonly System.Collections.Generic.Dictionary<int, CachedPhoto> PhotoCache =
            new System.Collections.Generic.Dictionary<int, CachedPhoto>();

        private SceneView _view;
        private Camera _camera;
        private Texture _target;
        private Stage _stage = Stage.Idle;
        private int _waitFrames;
        private int _settleFrames;
        private int _renderFrames;
        private long _lastDiskLength = -1;
        private string _pendingStablePath;
        private string _targetSavePath;
        private bool _triedSaveFile;
        private bool _viewEnabled;
        private int _deferredCleanupFrames = -1; // >=0 时倒计时，到 0 销毁渲染资源（给引擎异步管线排空时间）
        private System.Diagnostics.Stopwatch _stopwatch;
        private TerrainCache _cache;
        private TaleWorlds.MountAndBlade.Mission _mission;
        private float _centerX, _centerY, _halfW, _halfH;
        private int _texW, _texH;

        public bool IsCompleted { get; private set; }
        public bool Failed { get; private set; }
        public bool IsActive => _stage != Stage.Idle && _stage != Stage.Completed && _stage != Stage.Failed;

        /// <summary>已完成的拍照次数（支持部署结束后重拍）。</summary>
        public int CaptureCount { get; private set; }

        /// <summary>请求一次俯视拍照。缓存必须已就绪（IsBaked）。同场景签名命中照片缓存则零渲染直接发布。</summary>
        public void Start(TaleWorlds.MountAndBlade.Mission mission, TerrainCache cache)
        {
            Reset();
            if (mission?.Scene == null || cache == null || !cache.IsBaked) return;
            try
            {
                _mission = mission;
                _cache = cache;
                _centerX = cache.OriginX + cache.WorldW * 0.5f;
                _centerY = cache.OriginY + cache.WorldH * 0.5f;
                // 视口按 battle bounds 分轴外扩（非正方形）：不渲染边界外的无用区域
                _halfW = cache.WorldW * 0.5f + 4f;
                _halfH = cache.WorldH * 0.5f + 4f;
                _texW = PhotoSize;
                _texH = Math.Max(64, (int)Math.Round(PhotoSize * (double)_cache.WorldH / _cache.WorldW));
                // 计时器必须在缓存命中分支之前初始化（命中路径也会走 CompleteCapture 输出耗时）
                _stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 同场景签名照片缓存命中：零渲染、零落盘、零闪烁，直接发布
                if (PhotoCache.TryGetValue(cache.BakeSignature, out var cached)
                    && cached != null
                    && Math.Abs(cached.WorldW - cache.WorldW) < 1f
                    && Math.Abs(cached.WorldH - cache.WorldH) < 1f)
                {
                    _cache.ApplyPhotoPixels(cached.Rgba, cached.Width, cached.Height);
                    CompleteCapture();
                    TacticalMapLog.Info("[PhotoMap] photo cache hit (signature=" + cache.BakeSignature + "); skipped rendering.");
                    return;
                }

                _targetSavePath = IoPath.Combine(IoPath.GetTempPath(), "TMapPhotoRT.png");
                try { File.Delete(_targetSavePath); } catch { }
                _triedSaveFile = false;
                _viewEnabled = false;
                _settleFrames = 0;
                _renderFrames = 0;
                _pendingStablePath = null;
                _lastDiskLength = -1;

                CreateRenderObjects();
                _stage = Stage.WaitingSceneReady;
                _waitFrames = 0;
                TacticalMapLog.Info("[PhotoMap] v6 capture requested. halfW=" + _halfW.ToString("0.0") +
                                    " halfH=" + _halfH.ToString("0.0") + " captureNo=" + (CaptureCount + 1));
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoMap] start failed.", ex);
                FailCapture("exception in start: " + ex.GetType().Name);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public bool Tick()
        {
            // 延迟销毁：渲染+落盘完成后不立即销毁 view/RT——引擎异步渲染管线可能仍在引用，
            // 立即销毁会损坏引擎状态（2026-09-06 实测：photo applied 后原版 RayCast AV 闪退）。
            // 延迟约 2 秒待管线排空后再销毁。
            if (_deferredCleanupFrames > 0 && --_deferredCleanupFrames == 0) Cleanup();

            if (_stage == Stage.WaitingSceneReady) return TickWaitSceneReady();
            if (_stage == Stage.WaitingRender) return TickWaitRender();
            if (_stage == Stage.WaitingFile) return TickWaitFile();
            return false;
        }

        /// <summary>
        /// 等待视图就绪并完成纹理级联缓冲。
        /// 设计意图（用户明确）：进入战斗即启用（v4 顺序，创建时 SetEnable(true)），
        /// 部署阶段就开始拍摄——不等待任何部署/开战信号。
        /// </summary>
        private bool TickWaitSceneReady()
        {
            if (++_waitFrames > MaxSceneReadyFrames)
            {
                FailCapture("ReadyToRender never became true (" + MaxSceneReadyFrames + " frames after enable)");
                return false;
            }

            try
            {
                // ReadyToRender() 需在视图启用状态下查询（v4 实测顺序；禁用状态疑似恒 false）。
                // 已证伪的信号：
                // - CheckSceneReadyToRender：对主战斗场景恒 false（独立渲染场景专用）；
                // - Scene.IsLoadingFinished：tick 中轮询疑似与加载线程互斥；
                // - IsDeploymentFinished / AllowAiTicking：等待开战的设计已被用户否决——
                //   功能要求进入战斗即启用拍摄，部署阶段就开始。
                if (!_view.ReadyToRender())
                {
                    _settleFrames = 0;
                    return false;
                }

                // 启用后留级联缓冲：部署期纹理 mip 仍在流送，
                // 缓冲不足会拍到"半边有纹理半边平滑"的拼接缝（实测踩坑）。
                if (++_settleFrames < SettleFramesAfterLoading) return false;

                _stage = Stage.WaitingRender;
                _waitFrames = 0;
                _renderFrames = 0;
                TacticalMapLog.Info("[PhotoMap] render window opened (settle=" + SettleFramesAfterLoading +
                                    " frames); rendering for " + MinRenderFrames + " more frames.");
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoMap] scene-ready poll failed.", ex);
                FailCapture("exception in scene-ready poll: " + ex.GetType().Name);
                return false;
            }
        }

        /// <summary>短暂渲染窗口：ReadyToRender 后渲染 MinRenderFrames 帧即停。</summary>
        private bool TickWaitRender()
        {
            if (++_waitFrames > MaxWaitFrames)
            {
                FailCapture("render window never settled (" + MaxWaitFrames + " frames)");
                return false;
            }

            try
            {
                if (!_view.ReadyToRender()) return false;
                if (++_renderFrames < MinRenderFrames) return false;

                // 渲染内容已进入纹理：立即停用视图（闪烁窗口结束），之后回读
                try { _view.SetEnable(false); } catch { }
                _viewEnabled = false;
                _stage = Stage.WaitingFile;
                _waitFrames = 0;
                TacticalMapLog.Info("[PhotoMap] offscreen render settled (" + _renderFrames + " frames); view disabled.");
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoMap] render poll failed.", ex);
                FailCapture("exception in render poll: " + ex.GetType().Name);
                return false;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private bool TickWaitFile()
        {
            if (++_waitFrames > MaxWaitFrames)
            {
                FailCapture("PNG never stabilized (" + MaxWaitFrames + " frames, saveFileRequested=" + _triedSaveFile + ")");
                return false;
            }

            try
            {
                // 唯一回读路线：Texture.SaveToFile 异步落盘 → 轮询 PNG 稳定 → 解码。
                // GetPixelData 已证伪：对 RenderTarget 调用直接 AccessViolationException
                //（引擎原生实现不支持 RT 回读，损坏状态异常无法在托管侧捕获回退），2026-09-06 实测。

                // 第 15 帧：对渲染目标调 Texture.SaveToFile（异步落盘；原 60 帧无必要地拉长窗口）
                if (_waitFrames == 15 && !_triedSaveFile && _target != null)
                {
                    _triedSaveFile = true;
                    try
                    {
                        _target.SaveToFile(_targetSavePath, false);
                        TacticalMapLog.Info("[PhotoMap] SaveToFile requested: " + _targetSavePath);
                    }
                    catch (Exception ex)
                    {
                        TacticalMapLog.Warn("[PhotoMap] SaveToFile threw: " + ex.GetType().Name);
                    }
                }

                if (!_triedSaveFile) return false;

                string path = _targetSavePath;
                if (!File.Exists(path)) return false;
                long len = new FileInfo(path).Length;
                if (len <= 0) return false;
                if (_pendingStablePath != path || _lastDiskLength != len)
                {
                    _pendingStablePath = path;
                    _lastDiskLength = len;
                    return false; // 大小连续两帧一致才认为写完
                }

                ApplyPhotoFromPng(path);
                CompleteCapture();
                try { File.Delete(path); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoMap] disk route failed.", ex);
                FailCapture("exception in disk route: " + ex.GetType().Name);
                return false;
            }
        }

        private void CompleteCapture()
        {
            IsCompleted = true;
            CaptureCount++;
            _stage = Stage.Completed;
            // 渲染资源延迟销毁（立即销毁会损坏引擎状态 → 原版 RayCast AV 闪退，实测）
            DeferCleanup();
            TacticalMapLog.Info("[PhotoMap] photo applied. captureNo=" + CaptureCount +
                                " PhotoVersion=" + _cache.PhotoVersion + " publishSize=" + PublishSize +
                                " elapsed=" + (_stopwatch?.ElapsedMilliseconds ?? -1) + "ms");
        }

        /// <summary>失败统一出口：输出完整诊断快照（阶段/计数器/资源状态/落盘文件状态），一次日志定位断点。</summary>
        private void FailCapture(string reason)
        {
            string pngState = "<no-attempt>";
            try
            {
                if (!string.IsNullOrEmpty(_targetSavePath))
                {
                    var fi = new FileInfo(_targetSavePath);
                    pngState = fi.Exists ? "exists len=" + fi.Length : "missing";
                }
            }
            catch { pngState = "<stat-failed>"; }

            TacticalMapLog.Warn("[PhotoMap] FAILED at " + _stage + ": " + reason +
                                " | waitFrames=" + _waitFrames + " settle=" + _settleFrames +
                                " render=" + _renderFrames + " viewEnabled=" + _viewEnabled +
                                " targetAlive=" + (_target != null) + " png=" + pngState +
                                " elapsed=" + _stopwatch.ElapsedMilliseconds + "ms");
            DeferCleanup();
            Failed = true;
            _stage = Stage.Failed;
        }

        private void CreateRenderObjects()
        {
            float camZ = _cache.MaxH + 300f;
            float far = camZ + 2000f;

            // 非方形 RT：长边 PhotoSize，短边按 WorldW:WorldH 比例——视口即 battle bounds 分轴外扩，
            // 不渲染/不落盘任何边界外区域（正方形视口时代约 27% 渲染面积浪费）
            _target = Texture.CreateRenderTarget("TMapPhoto", _texW, _texH, false, false, false, false);
            _view = SceneView.CreateSceneView();
            _view.SetScene(_mission.Scene);
            _view.SetRenderTarget(_target);
            _view.SetAutoDepthTargetCreation(true);
            _view.SetClearColor(4278190080u); // 不透明黑
            // 严禁对共享 Mission.Scene 的第二视图开启 postfx/快速曝光
            //（SetRenderWithPostfx + SetDoQuickExposure + EnsurePostfxSystem）：
            // 官方 BrightnessDemoTableau 仅对独立 mono_renderscene 使用；对共享战斗场景
            // 双视图 postfx 渲染竞争会损坏引擎状态，导致原版 RayCast 访问违例闪退（2026-09-06 实测）。
            // 亮度补偿由 ApplyPhoto 的软件自动感光（avg<55 时拉伸）承担。

            _camera = Camera.CreateCamera();
            // perspective=false → 正交；半宽/半高分轴定义视口覆盖（米），与 RT 纵横比一致
            _camera.SetViewVolume(false, -_halfW, _halfW, -_halfH, _halfH, 1f, far);
            _camera.LookAt(new Vec3(_centerX, _centerY, camZ), new Vec3(_centerX, _centerY, 0f), new Vec3(0f, 1f, 0f));
            _view.SetCamera(_camera);
            // 进入战斗即启用（用户明确的设计意图，v4 实测顺序）：ReadyToRender 需在启用状态下查询
            _view.SetEnable(true);
        }

        /// <summary>
        /// 把拍照像素盒式下采样到发布分辨率。
        /// 相机 up=+Y、right=+X：照片行 0 = 北（+Y）。发布缓冲行 0 = 最南（照片底行），
        /// 与 TerrainCache 栅格旧布局一致（行号大 = 世界 Y 大 = 北），HTML 侧无需改动。
        /// </summary>
        private void ApplyPhotoFromPng(string path)
        {
            using (var bmp = new System.Drawing.Bitmap(path))
            {
                int w = bmp.Width, h = bmp.Height;
                var rect = new System.Drawing.Rectangle(0, 0, w, h);
                var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] raw = new byte[w * h * 4];
                    for (int y = 0; y < h; y++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            System.IntPtr.Add(data.Scan0, y * stride), raw, y * w * 4, Math.Min(stride, w * 4));
                    }
                    ApplyPhoto(raw, w, h);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }

        private void ApplyPhoto(byte[] raw, int photoW, int photoH)
        {
            bool swapRB = TacticalSettings.Instance.PhotoMapSwapRedBlue;

            // 视口已按 battle bounds 分轴外扩（RT 非方形），照片即全量有效区域：
            // 输出宽高比 = WorldW:WorldH，与缓存栅格/UV 1:1 对齐（无裁剪、无变形、无错位）。
            int outW = PublishSize;
            int outH = Math.Max(1, (int)Math.Round(outW * (double)_cache.WorldH / _cache.WorldW));
            var output = new byte[outW * outH * 4];

            // 双线性采样（源矩形与输出尺寸非整数倍，盒平均会丢像素）
            for (int r = 0; r < outH; r++)
            {
                // 输出行 r（0=南）→ 源"南起行"浮点 → 照片行（从顶数，照片行 0 = 北）
                float fySouth = (r + 0.5f) * photoH / outH - 0.5f;
                float fy = photoH - 1f - fySouth;
                int y0 = (int)Math.Floor(fy);
                float ty = fy - y0;
                int y0c = ClampInt(y0, 0, photoH - 1);
                int y1c = ClampInt(y0 + 1, 0, photoH - 1);

                for (int c = 0; c < outW; c++)
                {
                    float fx = (c + 0.5f) * photoW / outW - 0.5f;
                    int x0 = (int)Math.Floor(fx);
                    float tx = fx - x0;
                    int x0c = ClampInt(x0, 0, photoW - 1);
                    int x1c = ClampInt(x0 + 1, 0, photoW - 1);

                    int i00 = (y0c * photoW + x0c) * 4;
                    int i01 = (y0c * photoW + x1c) * 4;
                    int i10 = (y1c * photoW + x0c) * 4;
                    int i11 = (y1c * photoW + x1c) * 4;
                    if (i11 + 3 >= raw.Length) continue;

                    float r0 = raw[i00] + (raw[i01] - raw[i00]) * tx;
                    float r1 = raw[i10] + (raw[i11] - raw[i10]) * tx;
                    float g0 = raw[i00 + 1] + (raw[i01 + 1] - raw[i00 + 1]) * tx;
                    float g1 = raw[i10 + 1] + (raw[i11 + 1] - raw[i10 + 1]) * tx;
                    float b0 = raw[i00 + 2] + (raw[i01 + 2] - raw[i00 + 2]) * tx;
                    float b1 = raw[i10 + 2] + (raw[i11 + 2] - raw[i10 + 2]) * tx;

                    float rr = r0 + (r1 - r0) * ty;
                    float gg = g0 + (g1 - g0) * ty;
                    float bb = b0 + (b1 - b0) * ty;

                    int dst = (r * outW + c) * 4;
                    output[dst + 3] = 255;
                    output[dst] = ClampByte(swapRB ? bb : rr);
                    output[dst + 1] = ClampByte(gg);
                    output[dst + 2] = ClampByte(swapRB ? rr : bb);
                }
            }

            // 自动感光拉伸（auto-levels）：仅在画面仍然过暗时执行（无 postfx 时代 avg≈22）。
            // v6 已启用官方曝光管线（BrightnessDemoTableau 方案），avg≈95 属正常曝光——
            // 此时拉伸会把正常照片按 6 倍增益拉成高对比偏色怪图（2026-09-06 实测教训）。
            int[] hist = new int[256];
            int counted = 0;
            int p5 = 0, p95 = 255;
            for (int i = 0; i < output.Length; i += 4)
            {
                int lum = (output[i] * 299 + output[i + 1] * 587 + output[i + 2] * 114) / 1000;
                if (lum < 6) continue;
                hist[lum]++;
                counted++;
            }

            double avgBefore = 0;
            bool stretch = false;
            if (counted > 0)
            {
                long acc0 = 0;
                for (int i = 0; i < output.Length; i += 4)
                {
                    avgBefore += (output[i] * 299 + output[i + 1] * 587 + output[i + 2] * 114) / 1000;
                }
                avgBefore /= counted;
                stretch = avgBefore < 55.0; // 只有曝光失败的暗图才需要拉伸

                if (stretch)
                {
                    int loTarget = Math.Max(1, counted * 5 / 100);
                    int hiTarget = Math.Max(1, counted * 95 / 100);
                    long acc = 0;
                    for (int v = 0; v < 256; v++) { acc += hist[v]; if (acc >= loTarget) { p5 = v; break; } }
                    acc = 0;
                    for (int v = 0; v < 256; v++) { acc += hist[v]; if (acc >= hiTarget) { p95 = v; break; } }
                    if (p95 - p5 < 24) p5 = Math.Max(0, p95 - 24); // 防过窄除小数

                    for (int i = 0; i < output.Length; i += 4)
                    {
                        int lum = (output[i] * 299 + output[i + 1] * 587 + output[i + 2] * 114) / 1000;
                        if (lum < 6) continue; // 场景外保持黑
                        int mapped = 25 + (lum - p5) * (215 - 25) / Math.Max(1, p95 - p5);
                        mapped = mapped < 25 ? 25 : (mapped > 215 ? 215 : mapped);
                        double factor = mapped / (double)Math.Max(1, lum);
                        output[i] = ClampByte(output[i] * factor);
                        output[i + 1] = ClampByte(output[i + 1] * factor);
                        output[i + 2] = ClampByte(output[i + 2] * factor);
                    }
                }
            }
            TacticalMapLog.Info("[PhotoMap] photo stats: avgBefore=" + avgBefore.ToString("0.0") +
                " stretch=" + (stretch ? "on" : "off") + " p5=" + p5 + " p95=" + p95 +
                " pixels=" + counted + " publish=" + outW + "x" + outH +
                " (aligned to battle bounds " + _cache.WorldW.ToString("0") + "x" + _cache.WorldH.ToString("0") + ")");

            // 写入跨战斗照片缓存：同场景退出再进直接复用（Start 时签名命中即发布）。
            // 每条约 1~4MB 且进程级永不释放，超限整体清空防止多场战斗累积。
            if (PhotoCache.Count >= 16) PhotoCache.Clear();
            PhotoCache[_cache.BakeSignature] = new CachedPhoto
            {
                Rgba = output,
                Width = outW,
                Height = outH,
                WorldW = _cache.WorldW,
                WorldH = _cache.WorldH
            };

            _cache.ApplyPhotoPixels(output, outW, outH);
        }

        private static int ClampInt(int v, int min, int max)
        {
            return v < min ? min : (v > max ? max : v);
        }

        private static byte ClampByte(double v)
        {
            return (byte)(v < 0 ? 0 : (v > 255 ? 255 : (int)v));
        }

        /// <summary>渲染资源延迟销毁：不立即释放，由 Tick 倒计时到 0 后调 Cleanup（引擎管线排空缓冲）。</summary>
        private void DeferCleanup()
        {
            if (_view != null || _camera != null || _target != null)
                _deferredCleanupFrames = 120;
        }

        private void Cleanup()
        {
            // 清理序列（v4/v5 已验证整场无崩溃）：禁用视图 → 释放相机 → ManualInvalidate(view) → 释放纹理。
            // 不要调用 AddClearTask——它会向引擎渲染队列排入清屏任务，而随后立即 Release 的 view/RT
            // 会让该任务访问已释放资源（2026-09-06 实测：photo applied 后 RayCast AV 闪退）。
            try { if (_view != null) _view.SetEnable(false); } catch { }
            try { _camera?.ReleaseCamera(); } catch { }
            try { if (_view != null) _view.ManualInvalidate(); } catch { }
            try { _target?.Release(); } catch { }
            _view = null;
            _camera = null;
            _target = null;
            _viewEnabled = false;
            _deferredCleanupFrames = -1;
        }

        private void Reset()
        {
            Cleanup();
            IsCompleted = false;
            Failed = false;
            _waitFrames = 0;
        }

        /// <summary>
        /// Mission 结束时的清理，分两种情形：
        /// 1) 拍照已完成（管线已排空）：走原有 500ms 延迟销毁（安全，已实测）。
        /// 2) 拍照进行中（WaitingSceneReady/Settle/Render，view 仍启用即被退场）：
        ///    只 SetEnable(false) 切断渲染引用，**完全不销毁**引擎对象——此时管线正处于
        ///    中间态，无论立即还是延迟销毁都会污染引擎渲染状态（实测：第二场全场 20fps）。
        ///    对象进孤儿队列由静态引用持有（防 GC 终结器跨线程销毁——另一条实测教训），
        ///    引擎在场景卸载时自行解绑该 scene view。代价：每场最多泄漏一张 RT 的 GPU 内存。
        /// </summary>
        public void Shutdown()
        {
            try { if (_view != null) _view.SetEnable(false); } catch { }
            _viewEnabled = false;
            _deferredCleanupFrames = -1;

            if (_view == null && _camera == null && _target == null) return;

            if (IsActive)
            {
                // 拍照进行中被退场：遗弃（不销毁）。IsCompleted/Failed 保持 false，
                // 本实例不再被 Tick 驱动（_stage 仍非 Idle，MissionLogic 已丢弃引用）。
                lock (Orphaned)
                {
                    if (!Orphaned.Contains(this)) Orphaned.Add(this);
                }
                TacticalMapLog.Info("[PhotoMap] capture in progress at mission end; view disabled and orphaned (no engine teardown).");
                return;
            }

            _shutdownDeadlineTicks = DateTime.UtcNow.AddMilliseconds(500).Ticks;
            lock (PendingShutdown)
            {
                if (!PendingShutdown.Contains(this)) PendingShutdown.Add(this);
            }
        }

        /// <summary>拍照进行中被退场的实例（引擎对象遗弃，随场景卸载由引擎解绑）。</summary>
        private static readonly System.Collections.Generic.List<TerrainPhotoCapture> Orphaned =
            new System.Collections.Generic.List<TerrainPhotoCapture>();

        /// <summary>
        /// 静态待清理队列：跨战斗持有待销毁的 capture，防止 GC 终结器跨线程销毁引擎对象
        /// 导致下一场开场冻结（2026-09-06 实测模式）。由游戏主线程每帧驱动。
        /// </summary>
        private static readonly System.Collections.Generic.List<TerrainPhotoCapture> PendingShutdown =
            new System.Collections.Generic.List<TerrainPhotoCapture>();

        private long _shutdownDeadlineTicks;

        /// <summary>每帧驱动（游戏主线程）。到期后执行真正的引擎资源释放。</summary>
        public static void ProcessPendingShutdown()
        {
            lock (PendingShutdown)
            {
                if (PendingShutdown.Count == 0) return;
                long now = DateTime.UtcNow.Ticks;
                for (int i = PendingShutdown.Count - 1; i >= 0; i--)
                {
                    var capture = PendingShutdown[i];
                    if (now < capture._shutdownDeadlineTicks) continue;
                    PendingShutdown.RemoveAt(i);
                    try { capture.Cleanup(); }
                    catch (Exception ex) { TacticalMapLog.Error("[PhotoMap] pending shutdown cleanup failed.", ex); }
                }
            }
        }
    }
}
