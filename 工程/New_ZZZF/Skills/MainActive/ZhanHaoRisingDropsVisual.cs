using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>战嚎的红色水滴由下往上漂浮；受光内层与自发光外层共用一个实体。</summary>
    internal sealed class ZhanHaoRisingDropsVisual
    {
        private const float BurstDuration = 2.3f;
        private const int BurstDropCount = 12;
        private const int LingeringDropCount = 4;
        private static Mesh _burstLitMesh;
        private static Mesh _burstLuminousMesh;
        private static Mesh _lingeringLitMesh;
        private static Mesh _lingeringLuminousMesh;

        private readonly Drop[] _drops;
        private readonly Mesh _litMesh;
        private readonly Mesh _luminousMesh;
        private readonly bool _lingering;
        private float _elapsed;
        private bool _removed;

        private sealed class Drop
        {
            public GameEntity Entity;
            public int Cycle = -1;
            public float SpawnDelay;
            public float RiseDuration;
            public float StartX;
            public float StartY;
            public float DriftX;
            public float DriftY;
            public float CurveX;
            public float CurveY;
        }

        private ZhanHaoRisingDropsVisual(Drop[] drops, Mesh litMesh,
            Mesh luminousMesh, bool lingering)
        {
            _drops = drops;
            _litMesh = litMesh;
            _luminousMesh = luminousMesh;
            _lingering = lingering;
        }

        public static ZhanHaoRisingDropsVisual Create(Agent owner, bool lingering)
        {
            if (owner == null || !owner.IsActive() || owner.Mission?.Scene == null)
                return null;
            Mesh litMesh = GetMesh(lingering, false);
            Mesh luminousMesh = GetMesh(lingering, true);
            if (litMesh == null || luminousMesh == null)
                return null;

            Drop[] drops = new Drop[lingering ? LingeringDropCount : BurstDropCount];
            for (int i = 0; i < drops.Length; i++)
                drops[i] = new Drop
                {
                    SpawnDelay = i * (lingering ? 0.38f : 0.12f),
                    RiseDuration = 1.45f + (i * 7 % 6) * 0.11f
                };
            ZhanHaoRisingDropsVisual visual = new ZhanHaoRisingDropsVisual(
                drops, litMesh, luminousMesh, lingering);
            visual.Update(owner, 0f);
            return visual;
        }

        /// <returns>false 表示施放阶段结束，可以切换到稀疏的持续阶段。</returns>
        public bool Update(Agent owner, float dt)
        {
            if (_removed)
                return false;
            if (owner == null || !owner.IsActive())
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

            Vec3 eye = owner.GetEyeGlobalPosition();
            for (int i = 0; i < _drops.Length; i++)
            {
                Drop drop = _drops[i];
                if (_elapsed < drop.SpawnDelay)
                    continue;
                if (drop.Entity == null)
                {
                    drop.Entity = DualLayerParticleVisual.Create(owner, _litMesh, _luminousMesh);
                    if (drop.Entity == null)
                        continue;
                }
                float progress = (_elapsed - drop.SpawnDelay) / drop.RiseDuration;
                int cycle = (int)Math.Floor(progress);
                float phase = progress - cycle;
                if (drop.Cycle != cycle)
                    ChoosePath(drop, owner.Index, i, cycle);

                float bend = 4f * phase * (1f - phase);
                MatrixFrame frame = MatrixFrame.Identity;
                frame.origin = eye + new Vec3(
                    drop.StartX + drop.DriftX * phase + drop.CurveX * bend,
                    drop.StartY + drop.DriftY * phase + drop.CurveY * bend,
                    -1.35f + phase * 1.75f);
                DualLayerParticleVisual.Update(drop.Entity, frame);
            }
            return true;
        }

        public void Remove()
        {
            if (_removed)
                return;
            _removed = true;
            foreach (Drop drop in _drops)
                DualLayerParticleVisual.Remove(drop.Entity);
        }

        private static Mesh GetMesh(bool lingering, bool luminous)
        {
            Mesh mesh = lingering
                ? (luminous ? _lingeringLuminousMesh : _lingeringLitMesh)
                : (luminous ? _burstLuminousMesh : _burstLitMesh);
            if (mesh != null && mesh.IsValid)
                return mesh;

            mesh = DualLayerParticleVisual.CreateMesh(builder =>
            {
                uint color = new Color(1f, 0.16f, 0.08f,
                    luminous ? 0.78f : 0.84f).ToUnsignedInteger();
                float size = (lingering ? 0.034f : 0.039f) *
                    (luminous ? 1.12f : 1f);
                AddDrop(builder, Vec3.Zero, size, color);
            }, luminous);
            if (mesh == null)
                return null;
            if (lingering)
            {
                if (luminous) _lingeringLuminousMesh = mesh;
                else _lingeringLitMesh = mesh;
            }
            else
            {
                if (luminous) _burstLuminousMesh = mesh;
                else _burstLitMesh = mesh;
            }
            return mesh;
        }

        private static void AddDrop(MeshBuilder builder, Vec3 center, float size, uint color)
        {
            AddDropFace(builder, center, size, new Vec3(1f, 0f, 0f),
                new Vec3(0f, 1f, 0f), color);
            AddDropFace(builder, center, size, new Vec3(0f, 1f, 0f),
                new Vec3(1f, 0f, 0f), color);
        }

        private static void AddDropFace(MeshBuilder builder, Vec3 center, float size,
            Vec3 horizontal, Vec3 normal, uint color)
        {
            // 上端尖、下端圆宽，截面与鼓舞的菱片明显不同。
            Vec2 uv = new Vec2(0.5f, 0.5f);
            int tip = builder.AddFaceCorner(center + new Vec3(0f, 0f, size * 1.65f),
                normal, uv, color);
            int rightUpper = builder.AddFaceCorner(center + horizontal * (size * 0.25f) +
                new Vec3(0f, 0f, size * 0.48f), normal, uv, color);
            int right = builder.AddFaceCorner(center + horizontal * size -
                new Vec3(0f, 0f, size * 0.24f), normal, uv, color);
            int bottomRight = builder.AddFaceCorner(center + horizontal * (size * 0.57f) -
                new Vec3(0f, 0f, size * 0.78f), normal, uv, color);
            int bottom = builder.AddFaceCorner(center - new Vec3(0f, 0f, size),
                normal, uv, color);
            int bottomLeft = builder.AddFaceCorner(center - horizontal * (size * 0.57f) -
                new Vec3(0f, 0f, size * 0.78f), normal, uv, color);
            int left = builder.AddFaceCorner(center - horizontal * size -
                new Vec3(0f, 0f, size * 0.24f), normal, uv, color);
            int leftUpper = builder.AddFaceCorner(center - horizontal * (size * 0.25f) +
                new Vec3(0f, 0f, size * 0.48f), normal, uv, color);
            builder.AddFace(tip, rightUpper, right);
            builder.AddFace(tip, right, bottomRight);
            builder.AddFace(tip, bottomRight, bottom);
            builder.AddFace(tip, bottom, bottomLeft);
            builder.AddFace(tip, bottomLeft, left);
            builder.AddFace(tip, left, leftUpper);
        }

        private static void ChoosePath(Drop drop, int agentIndex, int index, int cycle)
        {
            uint random = unchecked((uint)(agentIndex * 73891 + index * 54727 + cycle * 1213)) | 1u;
            float angle = Next(ref random) * (float)(Math.PI * 2.0);
            float radius = 0.25f + Next(ref random) * 0.30f;
            drop.StartX = radius * (float)Math.Cos(angle);
            drop.StartY = radius * (float)Math.Sin(angle);
            drop.DriftX = (Next(ref random) - 0.5f) * 0.60f;
            drop.DriftY = (Next(ref random) - 0.5f) * 0.60f;
            drop.CurveX = (Next(ref random) - 0.5f) * 0.35f;
            drop.CurveY = (Next(ref random) - 0.5f) * 0.35f;
            drop.Cycle = cycle;
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
