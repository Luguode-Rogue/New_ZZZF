using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.BattleHud;
using New_ZZZF.TacticalMap.Diagnostics;
using New_ZZZF.TacticalMap.UI;
using IoPath = System.IO.Path;
using DrawingColor = System.Drawing.Color;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV=40: retain only the verified downward direction. Keep the camera at player XY and
    /// player Z + 20 m, and capture with the engine direction vector (0,0,-1).
    /// </summary>
    public sealed class SinglePhotoProofProbe
    {
        public static readonly SinglePhotoProofProbe Instance = new SinglePhotoProofProbe();

        private const int Revision = 40;
        private const float HeightOffset = 20f;
        private const int RequiredMatchingFrames = 8;
        private const int RequiredHiddenUiFrames = 4;
        private const int MaxWaitFrames = 7200;

        private enum Stage { Idle, WaitingAgent, Settling, WaitingFile, Done }

        private sealed class DirectionVariant
        {
            public string Label;
            public Vec3 RawDirection;
        }

        private static readonly DirectionVariant[] Directions =
        {
            new DirectionVariant { Label = "minus_Z", RawDirection = new Vec3(0f, 0f, -1f) }
        };

        private Mission _mission;
        private MissionScreen _screen;
        private TerrainCache _cache;
        private Camera _camera;
        private Camera _backup;
        private Stage _stage;
        private Vec2 _center;
        private float _cameraZ;
        private int _directionIndex;
        private readonly System.Collections.Generic.List<string> _completedPaths =
            new System.Collections.Generic.List<string>();
        private int _frames;
        private int _matchingFrames;
        private int _hiddenUiFrames;
        private long _stableLength = -1;
        private int _stableTicks;
        private bool _presentationChanged;
        private bool _previousUiHidden;
        private bool _failed;
        private string _directory;
        private string _path;
        private Stopwatch _watch;

        private SinglePhotoProofProbe() { }

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
                TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision +
                    " START waitingForRealMainAgent=true directions=1 verifiedMinusZOnly=true" +
                    " fixedCameraXY=true heightOffset=+20m rawVectors=true" +
                    " agentsVisible=true directory=" + _directory);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[SinglePhotoProof] REV=" + Revision + " start failed.", ex);
                FailAndRestore("start exception");
            }
        }

        public bool Tick()
        {
            if (!IsActive) return false;
            try
            {
                if (++_frames > MaxWaitFrames) return FailAndRestore("timeout stage=" + _stage);

                if (_stage == Stage.WaitingAgent)
                {
                    if (_mission.MainAgent == null || !_mission.MainAgent.IsActive()) return false;
                    TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision +
                        " REAL MAIN AGENT acquired world=" + _mission.MainAgent.Position);
                    BeginDirection(0);
                    return false;
                }

                if (_stage == Stage.Settling)
                {
                    // Follow the real agent until the exact final render camera has matched for
                    // several consecutive frames. This prevents using a pre-spawn fallback point.
                    _center = _mission.MainAgent.Position.AsVec2;
                    _cameraZ = _mission.MainAgent.Position.Z + HeightOffset;
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
                        return false;
                    }
                    if (++_matchingFrames < RequiredMatchingFrames) return false;

                    if (!_presentationChanged)
                    {
                        _previousUiHidden = MBDebug.DisableAllUI;
                        MBDebug.DisableAllUI = true;
                        TacticalMapHtmlUi.Instance.SetCaptureSuspended(true);
                        BattleHudHtmlUi.Instance.SetCaptureSuspended(true);
                        _presentationChanged = true;
                        return false;
                    }
                    MBDebug.DisableAllUI = true;
                    if (++_hiddenUiFrames < RequiredHiddenUiFrames) return false;

                    LogProjection();
                    Utilities.TakeScreenshotAsPng(_path);
                    TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision +
                        " CAPTURE requested path=" + _path);
                    _stage = Stage.WaitingFile;
                    _frames = 0;
                    return false;
                }

                ApplyCamera();
                MBDebug.DisableAllUI = true;
                if (!File.Exists(_path)) return false;
                long length = new FileInfo(_path).Length;
                if (length <= 0) return false;
                if (length != _stableLength)
                {
                    _stableLength = length;
                    _stableTicks = 0;
                    return false;
                }
                if (++_stableTicks < 2) return false;

                string stats = Analyze(_path);
                _completedPaths.Add(_path);
                TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision + " RESULT direction=" +
                    Directions[_directionIndex].Label + " " + stats + " path=" + _path);
                if (_directionIndex + 1 < Directions.Length)
                {
                    BeginDirection(_directionIndex + 1);
                    return false;
                }

                BuildContactSheet();
                _stage = Stage.Done;
                RestoreAll();
                TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision + " DONE directions=" +
                    Directions.Length + " elapsed=" +
                    (_watch == null ? -1 : _watch.ElapsedMilliseconds) + "ms" +
                    " validationOnly=true noApplyPhotoPixels=true");
                return true;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[SinglePhotoProof] REV=" + Revision + " tick failed.", ex);
                return FailAndRestore("tick exception=" + ex.GetType().Name);
            }
        }

        public void OnMissionEnd()
        {
            RestoreAll();
            ResetState();
        }

        private void ApplyCamera()
        {
            Vec3 position = GetCameraPosition();
            Vec3 raw = Directions[_directionIndex].RawDirection;
            Vec3 up = Math.Abs(raw.Z) > 0.5f ? new Vec3(0f, 1f, 0f) : Vec3.Up;
            _camera.LookAt(position, position + raw * 100f, up);
            _camera.SetFovVertical(1.0471976f, Math.Max(0.1f, Screen.AspectRatio), 0.1f, 1200f);
            _screen.SceneView.SetCamera(_camera);
        }

        private Vec3 GetCameraPosition()
        {
            return new Vec3(_center.X, _center.Y, _cameraZ);
        }

        private void BeginDirection(int index)
        {
            _directionIndex = index;
            _center = _mission.MainAgent.Position.AsVec2;
            _cameraZ = _mission.MainAgent.Position.Z + HeightOffset;
            _path = IoPath.Combine(_directory, index.ToString("00") + "_" +
                Directions[index].Label + ".png");
            TryDelete(_path);
            _frames = 0;
            _matchingFrames = 0;
            _hiddenUiFrames = 0;
            _stableLength = -1;
            _stableTicks = 0;
            _stage = Stage.Settling;
            ApplyCamera();
            TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision + " BEGIN " +
                (index + 1) + "/" + Directions.Length + " direction=" +
                Directions[index].Label + " player=" + _mission.MainAgent.Position +
                " camera=" + GetCameraPosition() + " rawDirection=" +
                Directions[index].RawDirection);
        }

        private void LogProjection()
        {
            Vec3 player = _mission.MainAgent.Position;
            Vec3 viewport = _camera.WorldPointToViewPortPoint(ref player);
            MatrixFrame finalFrame = _mission.Scene.LastFinalRenderCameraFrame;
            TacticalMapLog.Info("[SinglePhotoProof] REV=" + Revision +
                " PROOF direction=" + Directions[_directionIndex].Label +
                " rawVector=" + Directions[_directionIndex].RawDirection +
                " playerWorld=" + player + " expectedViewport=" + viewport +
                " requestedCamera=" + _camera.Frame.origin +
                " finalRenderCamera=" + finalFrame.origin +
                " requestedDirection=" + _camera.Direction +
                " finalRenderDirection=" + (-finalFrame.rotation.u));
        }

        private static string Analyze(string path)
        {
            using (Bitmap bmp = new Bitmap(path))
            {
                long sum = 0;
                double sum2 = 0;
                int count = 0;
                int min = 255;
                int max = 0;
                Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    for (int y = 0; y < bmp.Height; y += 4)
                    {
                        for (int x = 0; x < bmp.Width; x += 4)
                        {
                            IntPtr p = IntPtr.Add(data.Scan0, y * data.Stride + x * 4);
                            int b = System.Runtime.InteropServices.Marshal.ReadByte(p, 0);
                            int g = System.Runtime.InteropServices.Marshal.ReadByte(p, 1);
                            int r = System.Runtime.InteropServices.Marshal.ReadByte(p, 2);
                            int lum = (r * 299 + g * 587 + b * 114) / 1000;
                            sum += lum;
                            sum2 += (double)lum * lum;
                            min = Math.Min(min, lum);
                            max = Math.Max(max, lum);
                            count++;
                        }
                    }
                }
                finally { bmp.UnlockBits(data); }
                double avg = count == 0 ? 0 : (double)sum / count;
                double variance = count == 0 ? 0 : Math.Max(0, sum2 / count - avg * avg);
                return "png=" + bmp.Width + "x" + bmp.Height +
                    " avg=" + avg.ToString("0.0") +
                    " variance=" + variance.ToString("0.0") +
                    " min=" + min + " max=" + max;
            }
        }

        private void BuildContactSheet()
        {
            try
            {
                const int cellW = 420;
                const int imageH = 263;
                const int labelH = 28;
                using (Bitmap sheet = new Bitmap(cellW, imageH + labelH,
                    PixelFormat.Format24bppRgb))
                using (Graphics graphics = Graphics.FromImage(sheet))
                {
                    graphics.Clear(DrawingColor.Black);
                    for (int i = 0; i < _completedPaths.Count; i++)
                    {
                        int x = 0;
                        int y = 0;
                        using (Bitmap source = new Bitmap(_completedPaths[i]))
                            graphics.DrawImage(source, new Rectangle(x, y, cellW, imageH));
                        graphics.DrawString(Directions[i].Label, SystemFonts.DefaultFont,
                            Brushes.White, new PointF(x + 4, y + imageH + 5));
                    }
                    sheet.Save(IoPath.Combine(_directory, "00_contact_sheet.png"), ImageFormat.Png);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[SinglePhotoProof] REV=" + Revision +
                    " contact sheet failed.", ex);
            }
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
                    TacticalMapLog.Error("[SinglePhotoProof] REV=" + Revision+
                        " camera restore failed.", ex);
                }
            }
            if (_presentationChanged) MBDebug.DisableAllUI = _previousUiHidden;
            _presentationChanged = false;
            try { TacticalMapHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
            try { BattleHudHtmlUi.Instance.SetCaptureSuspended(false); } catch { }
        }

        private bool FailAndRestore(string reason)
        {
            _failed = true;
            RestoreAll();
            _stage = Stage.Idle;
            TacticalMapLog.Error("[SinglePhotoProof] REV=" + Revision + " FAILED " + reason, null);
            return false;
        }

        private static string PrepareDirectory()
        {
            string logDir = IoPath.GetDirectoryName(TacticalMapLog.LogPath);
            string directory = IoPath.Combine(logDir ?? ".", "SinglePhotoProof",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private void ResetState()
        {
            _mission = null;
            _screen = null;
            _cache = null;
            _camera = null;
            if (_backup != null)
            {
                try { _backup.ReleaseCameraEntity(); } catch { }
            }
            _backup = null;
            _stage = Stage.Idle;
            _center = Vec2.Zero;
            _cameraZ = 0f;
            _directionIndex = 0;
            _completedPaths.Clear();
            _frames = 0;
            _matchingFrames = 0;
            _hiddenUiFrames = 0;
            _stableLength = -1;
            _stableTicks = 0;
            _presentationChanged = false;
            _failed = false;
            _directory = null;
            _path = null;
            _watch = null;
        }

        private static void TryDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

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
