using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.BattleHud;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;
using IoPath = System.IO.Path;
using DrawingColor = System.Drawing.Color;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV=51 全地图拍照底图（开放世界瓦片路线，逐像素视差校正版）。
    ///
    /// REV=50 实机验证：瓦片拍摄→回读→合成→下发全链路可用（28 张 17s coverage 100%），
    /// 但按"格心高度平面"做仿射映射，山坡上的点被采样到错误位置——
    /// 实机症状：瓦片接缝硬切、大块崖壁/土层内容被位移拉进错误位置（红色斑块）。
    ///
    /// REV=52 修正：预计算每个缓冲像素的真实地形高度 h，采样改为
    ///   参考平面（z=格心高）的引擎投影 × 绕瓦片中心缩放 camH/(camZ-h)
    /// 即"过相机与地面点的射线打到像平面"，对任意高度的地形点都成立，
    /// 相邻瓦片对同一地面点给出一致采样位置 → 接缝与位移污染从根上消除。
    /// 关键：参考平面系数由引擎 WorldPointToViewPortPoint 对西南/东南/西北三角反推，
    /// 不假设视口轴向约定（REV=51 因假设 Y 朝上而 maxDelta=1.000 校验失败被回退）。
    ///
    /// 约束（2026-09-06 用户裁决）：不引入独立 SceneView / RenderTarget / Tableau，
    /// 不走整场单图路线；拍摄期间主相机被征用属预期表现。
    /// </summary>
    public sealed class TerrainPhotoTileCapture
    {
        public static readonly TerrainPhotoTileCapture Instance = new TerrainPhotoTileCapture();

        private const int Revision = 53;
        private const float VerticalFov = 1.0471976f;           // 与 REV=40 一致（60° 垂直 FOV）
        private const float TanHalfFov = 0.5773503f;            // tan(30°)
        private const float MinRayLength = 10f;                 // 地形点离相机过近（极端山峰）时放弃采样
        private const int FirstTileMatchingFrames = 8;
        private const int TileMatchingFrames = 4;
        private const int RequiredHiddenUiFrames = 4;
        private const int MaxFramesPerTile = 900;
        private const long TotalTimeoutMs = 120000;
        private const int GroundFillRowsPerFrame = 48;          // 高度场预计算的每帧行数（摊平主线程尖峰）

        private enum Stage { Idle, WaitingAgent, Settling, WaitingFile, Done }

        private Mission _mission;
        private MissionScreen _screen;
        private TerrainCache _cache;
        private TacticalSettings _settings;
        private Camera _camera;
        private Camera _backup;
        private Stage _stage;
        private Stopwatch _watch;

        // 网格与合成参数（Start 时计算一次）
        private int _cols;
        private int _rows;
        private float _camHeight;
        private float _footW;
        private float _footH;
        private float _refZ;
        private float _tileCamZ;
        private int _bufferW;
        private int _bufferH;
        private float _boundsMinX;
        private float _boundsMinY;
        private float _boundsW;
        private float _boundsH;
        private byte[] _accum;
        private float[] _weight;
        private float[] _groundHeights; // 缓冲像素的地形高度场（预计算，行 0 = 南）
        private int _groundFillRow;     // 高度场已填充到的行（增量填充）
        private bool _analyticVerified;

        // 参考平面（z=_refZ）的引擎投影系数 + 瓦片中心视口坐标（视差校正基准）
        private float _worldToViewportX;
        private float _worldToViewportY;
        private float _planeCenterVx;
        private float _planeCenterVy;
        private float _planeVxAtCenter;
        private float _planeVyAtCenter;
        private bool _projectionLogged;

        // 当前瓦片
        private int _tileIndex;
        private int _currentCol;
        private int _currentRow;
        private float _tileCenterX;
        private float _tileCenterY;
        private string _tilePath;
        private int _frames;
        private int _matchingFrames;
        private int _requiredMatchingFrames;
        private long _stableLength;
        private int _stableTicks;

        // 全程一次性状态
        private bool _uiHidden;
        private bool _presentationChanged;
        private bool _previousUiHidden;
        private int _hiddenUiFrames;
        private bool _failed;
        private string _directory;

        // Agent 视觉体隐藏（跨游戏版本 API 有差异，走反射）
        private readonly List<Agent> _hiddenAgents = new List<Agent>();
        private System.Reflection.MethodInfo _agentSetVisibleMethod;

        // 截图回读缓冲
        private int _shotW;
        private int _shotH;
        private byte[] _shotBgra;

        private TerrainPhotoTileCapture() { }

        public bool IsCompleted { get { return _stage == Stage.Done; } }
        public bool IsActive
        {
            get
            {
                return _stage == Stage.WaitingAgent || _stage == Stage.Settling ||
                    _stage == Stage.WaitingFile;
            }
        }
        public bool Failed { get { return _failed; } }

        public void Start(Mission mission, TerrainCache cache, MissionScreen screen)
        {
            ResetState();
            if (mission == null || mission.Scene == null || cache == null || !cache.IsBaked ||
                screen == null || screen.CombatCamera == null)
                return;

            try
            {
                _settings = TacticalSettings.Instance;
                float cellTargetH = Math.Max(30f, _settings.PhotoTileWorldHeight);
                float overlap = Math.Min(0.45f, Math.Max(0f, _settings.PhotoTileOverlap));
                float aspect = Math.Max(0.1f, Screen.AspectRatio);

                _boundsMinX = cache.OriginX;
                _boundsMinY = cache.OriginY;
                _boundsW = cache.WorldW;
                _boundsH = cache.WorldH;

                // 网格：列按 格宽=格高×aspect 摊开，保证相机接近正方形视口的全覆盖
                _cols = Math.Max(1, (int)Math.Ceiling(_boundsW / (cellTargetH * aspect)));
                _rows = Math.Max(1, (int)Math.Ceiling(_boundsH / cellTargetH));
                float cellW = _boundsW / _cols;
                float cellH = _boundsH / _rows;

                // 覆盖尺寸 = 格子 × (1 + 2×overlap)，重叠区供羽化混合、抑制残余错位
                float coverageW = cellW * (1f + 2f * overlap);
                float coverageH = cellH * (1f + 2f * overlap);
                _camHeight = Math.Max(
                    coverageW / (2f * TanHalfFov * aspect),
                    coverageH / (2f * TanHalfFov));
                _footW = 2f * _camHeight * TanHalfFov * aspect;
                _footH = 2f * _camHeight * TanHalfFov;

                // 全局合成缓冲：长边 ≤ PhotoBufferMaxDim，行 0 = 南、列 0 = 西
                float scale = Math.Max(64, _settings.PhotoBufferMaxDim) /
                    Math.Max(_boundsW, _boundsH);
                _bufferW = Math.Max(1, (int)Math.Round(_boundsW * scale));
                _bufferH = Math.Max(1, (int)Math.Round(_boundsH * scale));
                _accum = new byte[_bufferW * _bufferH * 4];
                _weight = new float[_bufferW * _bufferH];
                _groundHeights = new float[_bufferW * _bufferH];
                _groundFillRow = 0;
                _projectionLogged = false;

                _mission = mission;
                _screen = screen;
                _cache = cache;
                _camera = screen.CombatCamera;
                _backup = Camera.CreateCamera();
                if (_backup == null) throw new InvalidOperationException("camera backup failed");
                _backup.FillParametersFrom(_camera);
                _backup.Frame = _camera.Frame;
                _directory = PrepareDirectory();
                _watch = Stopwatch.StartNew();
                _stage = Stage.WaitingAgent;

                TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " START" +
                    " bounds=" + _boundsMinX.ToString("0.0") + "," + _boundsMinY.ToString("0.0") +
                    ".." + (_boundsMinX + _boundsW).ToString("0.0") + "," + (_boundsMinY + _boundsH).ToString("0.0") +
                    " grid=" + _cols + "x" + _rows + " tiles=" + (_cols * _rows) +
                    " coverage=" + coverageW.ToString("0.0") + "x" + coverageH.ToString("0.0") + "m" +
                    " camHeight=" + _camHeight.ToString("0.0") + "m" +
                    " buffer=" + _bufferW + "x" + _bufferH +
                    " parallaxCorrected=True" +
                    " hideAgents=" + _settings.HideAgentsInPhoto +
                    " directory=" + _directory);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " start failed.", ex);
                FailAndRestore("start exception");
            }
        }

        public bool Tick()
        {
            if (!IsActive) return false;
            try
            {
                if (_watch != null && _watch.ElapsedMilliseconds > TotalTimeoutMs)
                    return FailAndRestore("total timeout");
                if (++_frames > MaxFramesPerTile)
                    return FailAndRestore("tile timeout stage=" + _stage + " tile=" + _tileIndex);

                if (_stage == Stage.WaitingAgent)
                {
                    // 高度场预计算分帧进行（与等主 Agent 并行，摊平主线程尖峰）
                    FillGroundHeightsChunk();
                    if (_mission.MainAgent == null || !_mission.MainAgent.IsActive()) return false;
                    if (_groundFillRow < _bufferH) return false;
                    TacticalMapLog.Info("[TilePhoto] REV=" + Revision +
                        " main agent acquired world=" + _mission.MainAgent.Position +
                        " groundHeightField=" + _bufferW + "x" + _bufferH + " ready");
                    BeginTile(0);
                    return false;
                }

                if (_stage == Stage.Settling)
                {
                    TickSettling();
                    return false;
                }

                // WaitingFile：等截图落盘且大小稳定
                ApplyCamera();
                KeepUiHidden();
                if (!File.Exists(_tilePath)) return false;
                long length = new FileInfo(_tilePath).Length;
                if (length <= 0) return false;
                if (length != _stableLength)
                {
                    _stableLength = length;
                    _stableTicks = 0;
                    return false;
                }
                if (++_stableTicks < 2) return false;

                CompositeTile(_tilePath);
                if (_tileIndex + 1 < _cols * _rows)
                {
                    BeginTile(_tileIndex + 1);
                    return false;
                }

                Finish();
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " tick failed.", ex);
                return FailAndRestore("tick exception=" + ex.GetType().Name);
            }
        }

        public void OnMissionEnd()
        {
            RestoreAll();
            ResetState();
        }

        // ---------------- 地面高度场（视差校正输入） ----------------

        private void FillGroundHeightsChunk()
        {
            if (_groundHeights == null || _groundFillRow >= _bufferH) return;
            int endRow = Math.Min(_bufferH, _groundFillRow + GroundFillRowsPerFrame);
            for (int py = _groundFillRow; py < endRow; py++)
            {
                float worldY = _boundsMinY + (py + 0.5f) * _boundsH / _bufferH;
                int rowBase = py * _bufferW;
                for (int px = 0; px < _bufferW; px++)
                {
                    float worldX = _boundsMinX + (px + 0.5f) * _boundsW / _bufferW;
                    _groundHeights[rowBase + px] = _cache.GetHeightAt(new Vec2(worldX, worldY));
                }
            }
            _groundFillRow = endRow;
        }

        // ---------------- 瓦片流程 ----------------

        private void BeginTile(int index)
        {
            _tileIndex = index;
            _currentCol = index % _cols;
            _currentRow = index / _cols;
            _tileCenterX = _boundsMinX + (_currentCol + 0.5f) * _boundsW / _cols;
            _tileCenterY = _boundsMinY + (_currentRow + 0.5f) * _boundsH / _rows;
            // 相机基准高取格心地形高：重叠 + 视差校正吸收高度差
            _refZ = _cache.GetHeightAt(new Vec2(_tileCenterX, _tileCenterY));
            _tileCamZ = _refZ + _camHeight;
            _tilePath = IoPath.Combine(_directory,
                "tile_" + index.ToString("00") + "_" + _currentCol + "_" + _currentRow + ".png");
            TryDelete(_tilePath);
            _frames = 0;
            _matchingFrames = 0;
            _stableLength = 0;
            _stableTicks = 0;
            _requiredMatchingFrames = index == 0 ? FirstTileMatchingFrames : TileMatchingFrames;
            _stage = Stage.Settling;
            ApplyCamera();
            if (index == 0)
            {
                TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " BEGIN grid=" + _cols + "x" + _rows +
                    " camHeight=" + _camHeight.ToString("0.0") + "m");
            }
        }

        private void TickSettling()
        {
            // 复用 REV=40 已验证的稳定判据：最终渲染相机与目标位姿连续数帧一致
            ApplyCamera();
            MatrixFrame rendered = _mission.Scene.LastFinalRenderCameraFrame;
            Vec3 renderedDirection = -rendered.rotation.u;
            Vec3 desired = GetCameraPosition();
            bool ready = _screen.MissionStartedRendering() &&
                _screen.MissionLoadingWindowDisabled() &&
                !LoadingWindow.IsLoadingWindowActive &&
                Utilities.GetNumberOfShaderCompilationsInProgress() == 0;
            bool matches = rendered.origin.DistanceSquared(desired) < 1f &&
                Vec3.DotProduct(renderedDirection, _camera.Direction) > 0.999f;
            if (!ready || !matches)
            {
                _matchingFrames = 0;
                return;
            }
            if (++_matchingFrames < _requiredMatchingFrames) return;

            if (!_uiHidden)
            {
                // 全程只隐藏一次：引擎 UI + 两个 HtmlUI 页面 + Agent 视觉体
                if (!_presentationChanged)
                {
                    _previousUiHidden = MBDebug.DisableAllUI;
                    MBDebug.DisableAllUI = true;
                    try { TacticalMapHtmlUi.Instance.SetCaptureSuspended(true); } catch { }
                    try { BattleHudHtmlUi.Instance.SetCaptureSuspended(true); } catch { }
                    HideAgents();
                    _presentationChanged = true;
                    return;
                }
                _uiHidden = true;
                _hiddenUiFrames = 0;
                return;
            }
            if (++_hiddenUiFrames < RequiredHiddenUiFrames) return;

            CaptureTile();
        }

        private void CaptureTile()
        {
            ApplyCamera();
            KeepUiHidden();
            BuildTileProjection();
            Utilities.TakeScreenshotAsPng(_tilePath);
            TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " CAPTURE tile=" + _tileIndex +
                " (" + (_currentCol + 1) + "/" + _cols + "," + (_currentRow + 1) + "/" + _rows + ")" +
                " camera=" + GetCameraPosition() + " projection=parallaxCorrected");
            _stableLength = 0;
            _stableTicks = 0;
            _frames = 0;
            _stage = Stage.WaitingFile;
        }

        /// <summary>
        /// 首瓦片：用引擎投影对中心+四角建立"参考平面"映射（约定无关：
        /// 不假设视口 Y 朝上/朝下、X 朝东/朝西，全部由引擎投影反推），并记录诊断值。
        /// 逐像素视差校正在此基础上按 camH/(camZ-h) 绕瓦片中心缩放，
        /// 对任意地形高度成立且与视口轴向约定无关。
        /// </summary>
        private void BuildTileProjection()
        {
            float minX = _tileCenterX - 0.5f * _footW;
            float minY = _tileCenterY - 0.5f * _footH;
            Vec3 p00 = new Vec3(minX, minY, _refZ);
            Vec3 p10 = new Vec3(minX + _footW, minY, _refZ);
            Vec3 p01 = new Vec3(minX, minY + _footH, _refZ);
            Vec3 v00 = _camera.WorldPointToViewPortPoint(ref p00);
            Vec3 v10 = _camera.WorldPointToViewPortPoint(ref p10);
            Vec3 v01 = _camera.WorldPointToViewPortPoint(ref p01);

            _worldToViewportX = (v10.X - v00.X) / _footW;
            _worldToViewportY = (v01.Y - v00.Y) / _footH;
            // 中心视口坐标取角点均值：即使存在细微不对称也不会引入偏置
            _planeCenterVx = 0.5f * (v00.X + v10.X);
            _planeCenterVy = 0.5f * (v00.Y + v01.Y);
            _planeVxAtCenter = _planeCenterVx - _worldToViewportX * _tileCenterX;
            _planeVyAtCenter = _planeCenterVy - _worldToViewportY * _tileCenterY;

            if (!_projectionLogged)
            {
                _projectionLogged = true;
                TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " PROJECTION" +
                    " vSW=" + v00.X.ToString("0.0000") + "," + v00.Y.ToString("0.0000") +
                    " vSE=" + v10.X.ToString("0.0000") + "," + v10.Y.ToString("0.0000") +
                    " vNW=" + v01.X.ToString("0.0000") + "," + v01.Y.ToString("0.0000") +
                    " kx=" + _worldToViewportX.ToString("0.000000") +
                    " ky=" + _worldToViewportY.ToString("0.000000") +
                    " center=" + _planeCenterVx.ToString("0.0000") + "," + _planeCenterVy.ToString("0.0000") +
                    " parallaxCorrected=True");
            }
        }

        /// <summary>
        /// 逐像素视差校正：先在参考平面(z=_refZ)上取引擎投影，再按真实地形高度
        /// 把该点绕瓦片中心缩放 camH/(camZ-h)。高度高于基准 → 离相机更近 → 更靠外，反之更靠内。
        /// </summary>
        private void ProjectCorrected(float worldX, float worldY, float groundH,
            out float vx, out float vy)
        {
            float planeVx = _planeVxAtCenter + _worldToViewportX * worldX - _planeCenterVx;
            float planeVy = _planeVyAtCenter + _worldToViewportY * worldY - _planeCenterVy;
            float depth = _tileCamZ - groundH;
            if (depth < MinRayLength) depth = MinRayLength;
            float k = _camHeight / depth;
            vx = _planeCenterVx + planeVx * k;
            vy = _planeCenterVy + planeVy * k;
        }

        /// <summary>截图回读 + 写入全局缓冲（在主线程分瓦片进行，单次约几十毫秒）。</summary>
        private void CompositeTile(string path)
        {
            ReadShot(path);
            if (_shotBgra == null)
            {
                TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " tile=" + _tileIndex +
                    " readback failed, skipped.", null);
                return;
            }

            // 每瓦片重建参考平面映射（相机位姿已确认与渲染一致）
            BuildTileProjection();

            float minX = _tileCenterX - 0.5f * _footW;
            float minY = _tileCenterY - 0.5f * _footH;
            float maxX = minX + _footW;
            float maxY = minY + _footH;

            // 该瓦片覆盖的世界矩形 → 全局缓冲像素 AABB
            int px0 = Math.Max(0, (int)((minX - _boundsMinX) / _boundsW * _bufferW) - 1);
            int px1 = Math.Min(_bufferW - 1, (int)((maxX - _boundsMinX) / _boundsW * _bufferW) + 1);
            int py0 = Math.Max(0, (int)((minY - _boundsMinY) / _boundsH * _bufferH) - 1);
            int py1 = Math.Min(_bufferH - 1, (int)((maxY - _boundsMinY) / _boundsH * _bufferH) + 1);

            int written = 0;
            for (int py = py0; py <= py1; py++)
            {
                float worldY = _boundsMinY + (py + 0.5f) * _boundsH / _bufferH;
                float ny = (worldY - _tileCenterY) / (0.5f * _footH);
                if (ny <= -1f || ny >= 1f) continue;
                float ry = 1f - Math.Abs(ny);
                ry = ry * ry * (3f - 2f * ry); // smoothstep 羽化

                int rowBase = py * _bufferW;
                for (int px = px0; px <= px1; px++)
                {
                    int idx = rowBase + px;
                    float worldX = _boundsMinX + (px + 0.5f) * _boundsW / _bufferW;
                    float nx = (worldX - _tileCenterX) / (0.5f * _footW);
                    if (nx <= -1f || nx >= 1f) continue;
                    float rx = 1f - Math.Abs(nx);
                    rx = rx * rx * (3f - 2f * rx);
                    float weight = rx * ry;

                    // 重叠区取"离格心更近"的瓦片：残余误差随离格心距离增大
                    if (weight <= _weight[idx]) continue;

                    float vx, vy;
                    ProjectCorrected(worldX, worldY, _groundHeights[idx], out vx, out vy);
                    if (vx < 0f || vx >= 1f || vy < 0f || vy >= 1f) continue;

                    int sx = (int)(vx * _shotW);
                    if (sx < 0) sx = 0; else if (sx >= _shotW) sx = _shotW - 1;
                    // 引擎 viewport 的 y 从【底部】起算（官方 MissionFlagMarkerTargetVM /
                    // MultiplayerDuelVM 取到 WorldPointToViewPortPoint 后立即 vec.y = 1f - vec.y
                    // 才当屏幕坐标用），x 不翻（仅在点位于相机后方 z<0 时才翻 x）。
                    // 少了这个 1- 会让每块瓦片上下镜像，相邻瓦片边缘对不上——即"地图不连续"。
                    int sy = (int)((1f - vy) * _shotH);
                    if (sy < 0) sy = 0; else if (sy >= _shotH) sy = _shotH - 1;
                    int s = (sy * _shotW + sx) * 4;
                    int d = idx * 4;
                    // GDI LockBits(Format32bppArgb) 内存序为 BGRA → 回读按 RGBA 重建
                    _accum[d] = _shotBgra[s + 2];
                    _accum[d + 1] = _shotBgra[s + 1];
                    _accum[d + 2] = _shotBgra[s];
                    _accum[d + 3] = 255;
                    _weight[idx] = weight;
                    written++;
                }
            }

            TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " RESULT tile=" + _tileIndex +
                " shot=" + _shotW + "x" + _shotH + " pixels=" + written +
                " elapsed=" + _watch.ElapsedMilliseconds + "ms");
        }

        private void ReadShot(string path)
        {
            _shotBgra = null;
            try
            {
                using (Bitmap bmp = new Bitmap(path))
                {
                    _shotW = bmp.Width;
                    _shotH = bmp.Height;
                    Rectangle rect = new Rectangle(0, 0, _shotW, _shotH);
                    BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        _shotBgra = new byte[_shotW * _shotH * 4];
                        for (int y = 0; y < _shotH; y++)
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride),
                                _shotBgra, y * _shotW * 4, _shotW * 4);
                    }
                    finally { bmp.UnlockBits(data); }
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " shot read failed: " + path, ex);
                _shotBgra = null;
            }
        }

        private void Finish()
        {
            int painted = 0;
            for (int i = 0; i < _weight.Length; i++)
                if (_weight[i] > 0f) painted++;
            float coverageRatio = (float)painted / _weight.Length;

            if (painted <= 0)
            {
                FailAndRestore("composite empty");
                return;
            }

            _cache.ApplyPhotoPixels(_accum, _bufferW, _bufferH);
            _stage = Stage.Done;
            RestoreAll();
            TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " DONE tiles=" + (_cols * _rows) +
                " buffer=" + _bufferW + "x" + _bufferH +
                " coverage=" + (coverageRatio * 100f).ToString("0.0") + "%" +
                " projection=parallaxCorrected" +
                " elapsed=" + (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms");
        }

        // ---------------- 相机 / UI / Agent ----------------

        private Vec3 GetCameraPosition()
        {
            return new Vec3(_tileCenterX, _tileCenterY, _tileCamZ);
        }

        private void ApplyCamera()
        {
            Vec3 position = GetCameraPosition();
            Vec3 raw = new Vec3(0f, 0f, -1f);
            Vec3 up = new Vec3(0f, 1f, 0f);
            _camera.LookAt(position, position + raw * 200f, up);
            _camera.SetFovVertical(VerticalFov, Math.Max(0.1f, Screen.AspectRatio), 0.1f, 1200f);
            _screen.SceneView.SetCamera(_camera);
        }

        private void KeepUiHidden()
        {
            if (_presentationChanged) MBDebug.DisableAllUI = true;
        }

        private void HideAgents()
        {
            if (!_settings.HideAgentsInPhoto) return;
            try
            {
                _hiddenAgents.Clear();
                var agents = _mission.AllAgents;
                if (agents == null) return;
                foreach (Agent agent in agents)
                {
                    if (agent == null) continue;
                    if (SetAgentVisible(agent, false)) _hiddenAgents.Add(agent);
                }
                TacticalMapLog.Info("[TilePhoto] REV=" + Revision +
                    " agents hidden=" + _hiddenAgents.Count);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " hide agents failed.", ex);
            }
        }

        private bool SetAgentVisible(Agent agent, bool visible)
        {
            try
            {
                // 游戏版本间 Agent.AgentVisuals 的具体类型不同（MBAgentVisuals / View.AgentVisuals），
                // 但都有 SetVisible(bool)，走反射保证跨版本可用。
                object visuals = agent.AgentVisuals;
                if (visuals == null) return false;
                if (_agentSetVisibleMethod == null)
                    _agentSetVisibleMethod = visuals.GetType().GetMethod("SetVisible",
                        new Type[] { typeof(bool) });
                if (_agentSetVisibleMethod == null) return false;
                _agentSetVisibleMethod.Invoke(visuals, new object[] { visible });
                return true;
            }
            catch { return false; }
        }

        private void RestoreAgents()
        {
            if (_hiddenAgents.Count == 0) return;
            int restored = 0;
            foreach (Agent agent in _hiddenAgents)
            {
                try { if (agent != null && SetAgentVisible(agent, true)) restored++; }
                catch { }
            }
            _hiddenAgents.Clear();
            TacticalMapLog.Info("[TilePhoto] REV=" + Revision + " agents restored=" + restored);
        }

        private void RestoreAll()
        {
            if (_camera != null && _backup != null)
            {
                try
                {
                    _camera.FillParametersFrom(_backup);
                    _camera.Frame = _backup.Frame;
                    if (_screen != null && _screen.SceneView != null)
                        _screen.SceneView.SetCamera(_camera);
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " camera restore failed.", ex);
                }
            }
            if (_presentationChanged) MBDebug.DisableAllUI = _previousUiHidden;
            _presentationChanged = false;
            _uiHidden = false;
            RestoreAgents();
            try { TacticalMapHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
            try { BattleHudHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
        }

        private bool FailAndRestore(string reason)
        {
            _failed = true;
            RestoreAll();
            _stage = Stage.Idle;
            TacticalMapLog.Error("[TilePhoto] REV=" + Revision + " FAILED " + reason, null);
            return false;
        }

        private static string PrepareDirectory()
        {
            string logDir = IoPath.GetDirectoryName(TacticalMapLog.LogPath);
            string directory = IoPath.Combine(logDir ?? ".", "TilePhoto",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private void ResetState()
        {
            _mission = null;
            _screen = null;
            _cache = null;
            _settings = null;
            _camera = null;
            if (_backup != null)
            {
                try { _backup.ReleaseCameraEntity(); } catch { }
            }
            _backup = null;
            _stage = Stage.Idle;
            _watch = null;
            _cols = 0;
            _rows = 0;
            _tileIndex = 0;
            _accum = null;
            _weight = null;
            _groundHeights = null;
            _groundFillRow = 0;
            _projectionLogged = false;
            _shotBgra = null;
            _uiHidden = false;
            _presentationChanged = false;
            _hiddenUiFrames = 0;
            _failed = false;
            _directory = null;
            _tilePath = null;
            _hiddenAgents.Clear();
            _agentSetVisibleMethod = null;
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

        /// <summary>与 REV=40 相同的保位手段：引擎每帧 CheckForUpdateCamera 后强制回写拍摄相机。</summary>
        internal void ApplyFinalViewCamera(MissionScreen screen)
        {
            if ((_stage == Stage.Settling || _stage == Stage.WaitingFile) &&
                ReferenceEquals(screen, _screen) && _camera != null)
                ApplyCamera();
        }

        [HarmonyPatch(typeof(MissionScreen), "CheckForUpdateCamera")]
        private static class FinalViewCameraPatch
        {
            [HarmonyPostfix]
            private static void Postfix(MissionScreen __instance)
            {
                Instance.ApplyFinalViewCamera(__instance);
            }
        }
    }
}
