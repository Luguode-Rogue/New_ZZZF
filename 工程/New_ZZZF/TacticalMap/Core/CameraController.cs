using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 战术地图「点击地点 → 镜头临时飞过去 → 停留 → 自动飞回」的相机控制器。
    ///
    /// 实现原理（参考官方 MissionScreen 源码）：
    /// 官方 MissionScreen.CheckForUpdateCamera 在 CustomCamera != null 时会直接
    /// SetCamera 并 return，短路掉全部跟随/碰撞/边界逻辑。因此我们通过接管
    /// CustomCamera 来获得对相机位置的完全控制权，而不是去劫持官方的私有偏移字段
    /// （_cameraSpecialTargetPositionToAdd 那套是给对话/处决做「相对主角的小幅偏移」用的，
    /// 且每帧会被官方无条件清零，根本无法用于绝对世界坐标的镜头调度）。
    ///
    /// 状态机：Idle → FlyingOut → Holding → FlyingBack → Idle
    /// 全程只写自己的 Camera 对象，退出时把最终位姿通过 MissionScreen.UpdateFreeCamera
    /// 交还给官方，保证反解出的 Bearing/Elevation 与画面一致，不会产生跳变。
    /// </summary>
    public sealed class CameraController
    {
        public enum State
        {
            Idle,
            FlyingOut,
            Holding,
            FlyingBack
        }

        /// <summary>全局单例，由 TacticalMapController 构造时赋值。</summary>
        public static CameraController Instance { get; set; }

        public State CurrentState { get; private set; } = State.Idle;

        /// <summary>是否正在接管相机（供小地图绘制预览标记用）。</summary>
        public bool Active => CurrentState != State.Idle;

        /// <summary>当前预览目标的世界平面坐标（供小地图画标记）。</summary>
        public Vec2 TargetWorldPos { get; private set; }

        /// <summary>预览模式开关（按 C 切换）。关闭时点击地图不会触发镜头飞行。</summary>
        public bool PreviewModeEnabled { get; set; }

        // ---- 可调参数 ----
        /// <summary>飞向目标所需时间（秒）。</summary>
        public float FlyOutDuration = 0.85f;
        /// <summary>在目标点停留时间（秒）。</summary>
        public float HoldDuration = 2.5f;
        /// <summary>飞回原位所需时间（秒）。</summary>
        public float FlyBackDuration = 0.7f;
        /// <summary>相机悬停在目标点上方的高度（米）。</summary>
        public float ViewHeight = 18f;
        /// <summary>相机相对目标点的水平后退距离（米），用于形成俯视角。</summary>
        public float ViewDistance = 14f;
        /// <summary>垂直 FOV（度）。</summary>
        public float FieldOfView = 65f;

        private MissionScreen _missionScreen;
        private Scene _scene;
        private Camera _camera;

        // 起点 / 终点位姿
        private MatrixFrame _fromFrame;
        private MatrixFrame _toFrame;
        private MatrixFrame _currentFrame;
        // 进入预览前的原始位姿，用于飞回
        private MatrixFrame _originalFrame;

        private float _timer;

        /// <summary>
        /// 绑定 MissionScreen / Scene。可每帧安全调用（幂等），
        /// 因为 MissionScreen 要等 ScreenManager.TopScreen 就绪后才拿得到。
        /// </summary>
        public void Initialize(MissionScreen missionScreen, Scene scene)
        {
            if (missionScreen != null) { _missionScreen = missionScreen; }
            if (scene != null) { _scene = scene; }
        }

        /// <summary>
        /// 兼容入口：小地图点击时调用，传入世界平面坐标。
        /// 高度自动取该点地面高度。
        /// </summary>
        public void Enable(Vec2 worldPos)
        {
            TargetWorldPos = worldPos;

            float z = 0f;
            if (_scene != null)
            {
                try
                {
                    float ground = _scene.GetGroundHeightAtPosition(new Vec3(worldPos.X, worldPos.Y, 1000f));
                    if (ground > -1000f && ground < 9999f) { z = ground; }
                }
                catch (Exception) { }
            }
            FocusOn(new Vec3(worldPos.X, worldPos.Y, z));
        }

        /// <summary>兼容入口：关闭预览（平滑飞回原位，而不是硬切）。</summary>
        public void Disable()
        {
            ReturnToOrigin();
        }

        /// <summary>
        /// 请求把镜头临时切到某个世界坐标点。
        /// 若当前已在预览中，则平滑改道到新目标（起点取当前实时位姿，避免跳变）。
        /// </summary>
        public void FocusOn(Vec3 worldPosition)
        {
            if (_missionScreen == null || !PreviewModeEnabled) { return; }
            TargetWorldPos = new Vec2(worldPosition.x, worldPosition.y);

            // 首次进入：记录原始位姿并接管相机
            if (CurrentState == State.Idle)
            {
                if (_missionScreen.CombatCamera == null) { return; }
                _originalFrame = _missionScreen.CombatCamera.Frame;
                _currentFrame = _originalFrame;

                if (_camera == null)
                {
                    _camera = Camera.CreateCamera();
                }
                // 允许玩家在预览期间仍能用鼠标微调（官方同款开关）
                _missionScreen.AllowInputWithCustomCamera = false;
                _missionScreen.CustomCamera = _camera;
            }

            // 改道时从当前实时位置起飞，而不是从原始位置
            _fromFrame = _currentFrame;
            _toFrame = BuildViewFrame(worldPosition);
            _timer = 0f;
            CurrentState = State.FlyingOut;
        }

        /// <summary>立刻开始飞回原位（例如再次按 C 或点击空白处）。</summary>
        public void ReturnToOrigin()
        {
            if (CurrentState == State.Idle || CurrentState == State.FlyingBack) { return; }
            _fromFrame = _currentFrame;
            _toFrame = _originalFrame;
            _timer = 0f;
            CurrentState = State.FlyingBack;
        }

        /// <summary>由 MissionView/Patch 每帧驱动。</summary>
        public void Tick(float dt)
        {
            if (CurrentState == State.Idle || _missionScreen == null) { return; }
            if (dt <= 0f) { dt = 0.0166f; }

            _timer += dt;

            switch (CurrentState)
            {
                case State.FlyingOut:
                {
                    float t = FlyOutDuration <= 0f ? 1f : MBMath.ClampFloat(_timer / FlyOutDuration, 0f, 1f);
                    _currentFrame = LerpFrame(_fromFrame, _toFrame, SmoothStep(t));
                    if (t >= 1f)
                    {
                        _timer = 0f;
                        CurrentState = State.Holding;
                    }
                    break;
                }
                case State.Holding:
                {
                    _currentFrame = _toFrame;
                    // 停留结束后自动交还控制权（文档预期：抵达后自动失活）
                    if (HoldDuration >= 0f && _timer >= HoldDuration)
                    {
                        _fromFrame = _currentFrame;
                        _toFrame = _originalFrame;
                        _timer = 0f;
                        CurrentState = State.FlyingBack;
                    }
                    break;
                }
                case State.FlyingBack:
                {
                    float t = FlyBackDuration <= 0f ? 1f : MBMath.ClampFloat(_timer / FlyBackDuration, 0f, 1f);
                    _currentFrame = LerpFrame(_fromFrame, _toFrame, SmoothStep(t));
                    if (t >= 1f)
                    {
                        Release();
                        return;
                    }
                    break;
                }
            }

            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_camera == null) { return; }
            _camera.Frame = _currentFrame;
            _camera.SetFovVertical(
                FieldOfView * 0.017453292f,
                Screen.AspectRatio,
                0.1f,
                12500f);
        }

        /// <summary>
        /// 归还相机控制权。通过 UpdateFreeCamera 把最终位姿写回，
        /// 官方会据此反解 CameraBearing / CameraElevation，避免松手瞬间画面跳变。
        /// </summary>
        public void Release()
        {
            if (_missionScreen != null)
            {
                _missionScreen.CustomCamera = null;
                _missionScreen.AllowInputWithCustomCamera = false;
                try
                {
                    _missionScreen.UpdateFreeCamera(_currentFrame);
                }
                catch (Exception)
                {
                    // UpdateFreeCamera 依赖 CombatCamera，任务结束阶段可能已释放，忽略即可
                }
            }
            CurrentState = State.Idle;
            _timer = 0f;
        }

        /// <summary>任务结束时调用，释放引擎相机资源。</summary>
        public void Destroy()
        {
            Release();
            if (_camera != null)
            {
                try { _camera.ReleaseCamera(); }
                catch (Exception) { }
                _camera = null;
            }
            _missionScreen = null;
            _scene = null;
        }

        /// <summary>
        /// 由目标点构造一个俯视观察位姿。
        /// 沿用当前镜头的水平朝向，从该方向后退 ViewDistance 并抬高 ViewHeight，
        /// 再 LookAt 目标点，这样切过去的视角与玩家当前朝向连续，不会产生方向感错乱。
        /// </summary>
        private MatrixFrame BuildViewFrame(Vec3 target)
        {
            // 贴合地面：确保目标点不在地形之下
            if (_scene != null)
            {
                try
                {
                    float ground = _scene.GetGroundHeightAtPosition(new Vec3(target.x, target.y, target.z + 100f));
                    if (ground > -1000f && ground < 9999f)
                    {
                        target.z = Math.Max(target.z, ground);
                    }
                }
                catch (Exception) { }
            }

            float bearing = _missionScreen != null ? _missionScreen.CameraBearing : 0f;
            // 相机站位：沿当前朝向的反方向后退，并抬升
            Vec3 eye = new Vec3(
                target.x - (float)Math.Sin(bearing) * -ViewDistance,
                target.y - (float)Math.Cos(bearing) * -ViewDistance,
                target.z + ViewHeight);

            // 防止相机穿地
            if (_scene != null)
            {
                try
                {
                    float eyeGround = _scene.GetGroundHeightAtPosition(new Vec3(eye.x, eye.y, eye.z + 100f));
                    if (eyeGround > -1000f && eyeGround < 9999f && eye.z < eyeGround + 1.5f)
                    {
                        eye.z = eyeGround + 1.5f;
                    }
                }
                catch (Exception) { }
            }

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = eye;

            // 用 LookAt 语义构造旋转：f = 前方(视线)，s = 右，u = 上
            Vec3 forward = (target - eye);
            if (forward.Length < 0.001f) { forward = new Vec3(0f, 1f, 0f); }
            forward.Normalize();
            Vec3 worldUp = new Vec3(0f, 0f, 1f);
            Vec3 side = Vec3.CrossProduct(forward, worldUp);
            if (side.Length < 0.001f) { side = new Vec3(1f, 0f, 0f); }
            side.Normalize();
            Vec3 up = Vec3.CrossProduct(side, forward);
            up.Normalize();

            frame.rotation.s = side;
            frame.rotation.f = forward;
            frame.rotation.u = up;
            return frame;
        }

        private static float SmoothStep(float t)
        {
            // ease-in-out，起步和收尾都平缓，观感接近官方过场
            return t * t * (3f - 2f * t);
        }

        private static MatrixFrame LerpFrame(MatrixFrame a, MatrixFrame b, float t)
        {
            MatrixFrame result = MatrixFrame.Identity;
            result.origin = a.origin * (1f - t) + b.origin * t;

            // 对基向量做插值后重新正交化，避免矩阵退化导致画面扭曲
            Vec3 f = a.rotation.f * (1f - t) + b.rotation.f * t;
            Vec3 u = a.rotation.u * (1f - t) + b.rotation.u * t;
            if (f.Length < 0.0001f) { f = b.rotation.f; }
            f.Normalize();
            if (u.Length < 0.0001f) { u = b.rotation.u; }

            Vec3 s = Vec3.CrossProduct(f, u);
            if (s.Length < 0.0001f) { s = b.rotation.s; }
            s.Normalize();
            u = Vec3.CrossProduct(s, f);
            u.Normalize();

            result.rotation.f = f;
            result.rotation.s = s;
            result.rotation.u = u;
            return result;
        }
    }
}
