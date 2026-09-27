using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 鼓舞的金红色自建粒子。用已验证的 prt_shd_sparks 材质绘制小菱片，
    /// 每组独立选择下落起点、横向漂移与弯曲路线。释放 40 片，持续 4 片。
    /// </summary>
    internal sealed class GuWuFallingEmbersVisual
    {
        private const float BurstDuration = 2.3f;
        private const float FallCycle = 1.8f;
        private const int BurstGroups = 4;
        private const int BurstFlakesPerGroup = 10;
        private const int LingeringGroups = 2;
        private const int LingeringFlakesPerGroup = 2;
        private static Mesh _burstMesh;
        private static Mesh _lingeringMesh;

        private readonly Cloud[] _clouds;
        private readonly bool _lingering;
        private float _elapsed;
        private bool _removed;

        private sealed class Cloud
        {
            public GameEntity Entity;
            public int Cycle = -1;
            public float StartX;
            public float StartY;
            public float DriftX;
            public float DriftY;
            public float CurveX;
            public float CurveY;
        }

        private GuWuFallingEmbersVisual(Cloud[] clouds, bool lingering)
        {
            _clouds = clouds;
            _lingering = lingering;
        }

        public static GuWuFallingEmbersVisual Create(Agent agent)
        {
            return Create(agent, false);
        }

        public static GuWuFallingEmbersVisual CreateLingering(Agent agent)
        {
            return Create(agent, true);
        }

        private static GuWuFallingEmbersVisual Create(Agent agent, bool lingering)
        {
            if (agent == null || !agent.IsActive() || agent.Mission?.Scene == null)
                return null;
            Mesh mesh = GetMesh(lingering);
            if (mesh == null)
                return null;

            Cloud[] clouds = new Cloud[lingering ? LingeringGroups : BurstGroups];
            try
            {
                for (int i = 0; i < clouds.Length; i++)
                {
                    GameEntity entity = GameEntity.CreateEmptyDynamic(agent.Mission.Scene, false);
                    if (entity == null)
                        throw new InvalidOperationException("鼓舞粒子实体创建失败。");
                    clouds[i] = new Cloud { Entity = entity };
                    entity.AddMesh(mesh);
                    entity.SetFactorColor(new Color(1f, 1f, 1f, 1f).ToUnsignedInteger());
                    entity.SetVisibilityExcludeParents(true);
                    entity.SetReadyToRender(true);
                }

                GuWuFallingEmbersVisual visual = new GuWuFallingEmbersVisual(clouds, lingering);
                visual.Update(agent, 0f);
                return visual;
            }
            catch (Exception)
            {
                foreach (Cloud cloud in clouds)
                {
                    try { cloud?.Entity?.Remove(0); }
                    catch (Exception) { /* 继续清理其余实体。 */ }
                }
                return null;
            }
        }

        /// <returns>false 表示释放阶段结束，应切换为十分之一密度的持续阶段。</returns>
        public bool Update(Agent agent, float dt)
        {
            if (_removed)
                return false;
            if (agent == null || !agent.IsActive())
            {
                Remove();
                return false;
            }

            _elapsed += dt;
            if (!_lingering && _elapsed >= BurstDuration)
            {
                Remove();
                return false;
            }

            Vec3 eye = agent.GetEyeGlobalPosition();
            for (int i = 0; i < _clouds.Length; i++)
            {
                Cloud cloud = _clouds[i];
                float progress = _elapsed / FallCycle + (float)i / _clouds.Length;
                int cycle = (int)Math.Floor(progress);
                float phase = progress - cycle;
                if (cloud.Cycle != cycle)
                    ChoosePath(cloud, agent.Index, i, cycle);

                // 二次曲线带来方向各异的横向摆动；每次回到顶部都重选路线。
                float bend = 4f * phase * (1f - phase);
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = eye + new Vec3(
                    cloud.StartX + cloud.DriftX * phase + cloud.CurveX * bend,
                    cloud.StartY + cloud.DriftY * phase + cloud.CurveY * bend,
                    0.35f - phase * 1.75f);
                cloud.Entity.SetGlobalFrame(frame);
            }
            return true;
        }

        public void Remove()
        {
            if (_removed)
                return;
            _removed = true;
            foreach (Cloud cloud in _clouds)
            {
                try { cloud?.Entity?.Remove(0); }
                catch (Exception) { /* 单个实体失效不阻碍清理其余粒子。 */ }
            }
        }

        private static Mesh GetMesh(bool lingering)
        {
            Mesh cached = lingering ? _lingeringMesh : _burstMesh;
            if (cached != null && cached.IsValid)
                return cached;

            int count = lingering ? LingeringFlakesPerGroup : BurstFlakesPerGroup;
            Mesh mesh = GoldenSparkMesh.Create(builder =>
            {
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2.399963f;
                    float radius = lingering ? 0.36f : 0.13f + (i * 7 % 9) * 0.048f;
                    Vec3 point = new Vec3(
                        radius * (float)Math.Cos(angle),
                        radius * (float)Math.Sin(angle),
                        lingering ? (i == 0 ? -0.20f : 0.20f) :
                            ((i * 11 % 10) - 4.5f) * 0.055f);
                    uint color = new Color(1f, i % 3 == 0 ? 0.45f : 0.68f,
                        0.14f, 0.95f).ToUnsignedInteger();
                    GoldenSparkMesh.AddDiamond(builder, point,
                        0.025f + (i % 3) * 0.006f, color);
                }
            });
            if (lingering)
                _lingeringMesh = mesh;
            else
                _burstMesh = mesh;
            return mesh;
        }

        private static void ChoosePath(Cloud cloud, int agentIndex, int group, int cycle)
        {
            uint random = unchecked((uint)(agentIndex * 92821 + group * 68917 + cycle * 1013)) | 1u;
            float angle = Next(ref random) * (float)(Math.PI * 2.0);
            float radius = 0.16f + Next(ref random) * 0.30f;
            cloud.StartX = radius * (float)Math.Cos(angle);
            cloud.StartY = radius * (float)Math.Sin(angle);
            cloud.DriftX = (Next(ref random) - 0.5f) * 0.80f;
            cloud.DriftY = (Next(ref random) - 0.5f) * 0.80f;
            cloud.CurveX = (Next(ref random) - 0.5f) * 0.42f;
            cloud.CurveY = (Next(ref random) - 0.5f) * 0.42f;
            cloud.Cycle = cycle;
        }

        private static float Next(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777216f;
        }
    }
}
