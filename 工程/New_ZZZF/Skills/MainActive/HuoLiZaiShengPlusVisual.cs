using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>每个实体只有一个绿色加号，两层材质同步；40个施法粒子转为10个独立持续粒子。</summary>
    internal sealed class HuoLiZaiShengPlusVisual
    {
        private const int CastCount = 40;
        private const int SustainedCount = 10;
        private const float CastDuration = 2.2f;
        private static Mesh _litMesh, _luminousMesh;
        private static uint _generation;
        private readonly Particle[] _particles = new Particle[CastCount];
        private readonly uint _seed;
        private int _count = CastCount;
        private float _elapsed;
        private bool _removed;
        private sealed class Particle
        {
            public GameEntity Entity;
            public int Cycle = -1;
            public float PhaseOffset, Lifetime;
            public float StartX, StartY, DriftX, DriftY, CurveX, CurveY, SwayPhase;
        }
        private HuoLiZaiShengPlusVisual(uint seed) { _seed = seed; }

        internal static HuoLiZaiShengPlusVisual Create(Agent owner)
        {
            if (owner == null || !owner.IsActive() || owner.Mission?.Scene == null) return null;
            Mesh lit = GetMesh(false), luminous = GetMesh(true);
            if (lit == null || luminous == null) return null;
            uint seed = unchecked((uint)owner.Index * 73891u + ++_generation * 54727u) | 1u;
            var visual = new HuoLiZaiShengPlusVisual(seed);
            for (int i = 0; i < CastCount; i++)
            {
                // 随机相位、寿命独立，绝不让一组加号同时整圈上升。
                var particle = new Particle {
                    Entity = DualLayerParticleVisual.Create(owner, lit, luminous),
                    PhaseOffset = Next(ref seed), Lifetime = 1.6f + Next(ref seed) * 1.1f
                };
                visual._particles[i] = particle;
            }
            visual.Update(owner, 0f);
            return visual;
        }

        internal void Update(Agent owner, float dt)
        {
            if (_removed) return;
            if (owner == null || !owner.IsActive() || owner.Health <= 0f) { Remove(); return; }
            if (dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt)) _elapsed += dt;
            if (_count == CastCount && _elapsed >= CastDuration)
            {
                for (int i = SustainedCount; i < CastCount; i++) {
                    DualLayerParticleVisual.Remove(_particles[i].Entity);
                    _particles[i].Entity = null;
                }
                _count = SustainedCount;
            }
            Vec3 anchor = owner.GetEyeGlobalPosition();
            // 单个完整加号朝向渲染相机，避免交叉平面的暗色侧翼。
            MatrixFrame camera = owner.Mission.Scene.LastFinalRenderCameraFrame;
            for (int i = 0; i < _count; i++)
            {
                Particle particle = _particles[i];
                if (particle.Entity == null) continue;
                float progress = _elapsed / particle.Lifetime + particle.PhaseOffset;
                int cycle = (int)Math.Floor(progress);
                float phase = progress - cycle;
                if (particle.Cycle != cycle) ChoosePath(particle, i, cycle);
                float bend = 4f * phase * (1f - phase);
                float sway = (float)Math.Sin(phase * Math.PI * 2f + particle.SwayPhase) * bend * 0.09f;
                MatrixFrame frame = MatrixFrame.Identity;
                // 相机视线为 -u；加号局部 XZ 分别映射相机的右 s 和上 f。
                // 局部 Y 是面法线，不能直接复制相机旋转，否则会侧对镜头。
                frame.rotation = new Mat3(camera.rotation.s, -camera.rotation.u, camera.rotation.f);
                frame.origin = anchor + new Vec3(
                    particle.StartX + particle.DriftX * phase + particle.CurveX * bend + sway,
                    particle.StartY + particle.DriftY * phase + particle.CurveY * bend - sway,
                    -1.25f + phase * 1.9f);
                DualLayerParticleVisual.Update(particle.Entity, frame);
            }
        }

        internal void Remove()
        {
            if (_removed) return;
            _removed = true;
            foreach (Particle particle in _particles) {
                if (particle == null) continue;
                DualLayerParticleVisual.Remove(particle.Entity);
                particle.Entity = null;
            }
        }

        private void ChoosePath(Particle particle, int index, int cycle)
        {
            uint random = unchecked(_seed + (uint)index * 54727u + (uint)cycle * 1213u) | 1u;
            float angle = Next(ref random) * (float)(Math.PI * 2f);
            float radius = 0.45f + Next(ref random) * 0.45f;
            particle.StartX = radius * (float)Math.Cos(angle);
            particle.StartY = radius * (float)Math.Sin(angle);
            particle.DriftX = (Next(ref random) - 0.5f) * 0.55f;
            particle.DriftY = (Next(ref random) - 0.5f) * 0.55f;
            particle.CurveX = (Next(ref random) - 0.5f) * 0.45f;
            particle.CurveY = (Next(ref random) - 0.5f) * 0.45f;
            particle.SwayPhase = Next(ref random) * (float)(Math.PI * 2f);
            particle.Cycle = cycle;
        }

        private static Mesh GetMesh(bool luminous)
        {
            Mesh cached = luminous ? _luminousMesh : _litMesh;
            if (cached != null && cached.IsValid) return cached;
            Mesh mesh = DualLayerParticleVisual.CreateMesh(builder => {
                uint color = new Color(luminous ? 0.78f : 0.55f, 1f, luminous ? 0.82f : 0.62f, 1f).ToUnsignedInteger();
                float size = 0.08f * (luminous ? 0.90f : 1f);
                // 保留受光底层与发光亮芯，但不使用相交的两个加号。
                // 发光面在底层前后各布置两层，每个矩形都显式建立正反面。
                if (luminous)
                {
                    for (int pass = 1; pass <= 2; pass++)
                    {
                        float offset = pass * 0.0025f;
                        AddPlus(builder, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), size, color, offset);
                        AddPlus(builder, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), size, color, -offset);
                    }
                }
                else
                {
                    AddPlus(builder, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), size, color);
                }
            }, luminous);
            if (mesh != null && luminous)
            {
                // 只修改活力再生的材质副本，不改变其他技能共享的双层材质。
                Material bright = Material.GetFromResource("prt_shd_sparks")?.CreateCopy();
                if (bright != null && bright.IsValid)
                {
                    bright.SetAlphaBlendMode(Material.MBAlphaBlendMode.Add);
                    mesh.SetMaterial(bright);
                }
            }
            if (luminous) _luminousMesh = mesh; else _litMesh = mesh;
            return mesh;
        }

        private static void AddPlus(MeshBuilder builder, Vec3 horizontal, Vec3 normal, float size, uint color, float offset = 0f)
        {
            float halfWidth = size * 0.28f;
            AddRect(builder, horizontal, normal, -halfWidth, halfWidth, -size, size, color, offset);
            AddRect(builder, horizontal, normal, -size, -halfWidth, -halfWidth, halfWidth, color, offset);
            AddRect(builder, horizontal, normal, halfWidth, size, -halfWidth, halfWidth, color, offset);
        }
        private static void AddRect(MeshBuilder builder, Vec3 horizontal, Vec3 normal,
            float left, float right, float bottom, float top, uint color, float offset)
        {
            Vec2 uv = new Vec2(0.5f, 0.5f);
            Vec3 center = normal * offset;
            int a = builder.AddFaceCorner(center + horizontal * left + Vec3.Up * bottom, normal, uv, color);
            int b = builder.AddFaceCorner(center + horizontal * right + Vec3.Up * bottom, normal, uv, color);
            int c = builder.AddFaceCorner(center + horizontal * right + Vec3.Up * top, normal, uv, color);
            int d = builder.AddFaceCorner(center + horizontal * left + Vec3.Up * top, normal, uv, color);
            builder.AddFace(a, c, b); builder.AddFace(a, d, c);
            // 关闭网格剔除并不能替代材质需要的反面顶点和法线。
            int backA = builder.AddFaceCorner(center + horizontal * left + Vec3.Up * bottom, -normal, uv, color);
            int backB = builder.AddFaceCorner(center + horizontal * right + Vec3.Up * bottom, -normal, uv, color);
            int backC = builder.AddFaceCorner(center + horizontal * right + Vec3.Up * top, -normal, uv, color);
            int backD = builder.AddFaceCorner(center + horizontal * left + Vec3.Up * top, -normal, uv, color);
            builder.AddFace(backA, backB, backC); builder.AddFace(backA, backC, backD);
        }
        private static float Next(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777216f;
        }
    }
}