using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    internal sealed class FearExclamationVisual
    {
        private static Mesh _lit;
        private static Mesh _glow;
        private GameEntity _entity;
        private float _untilUpdate;
        public static FearExclamationVisual Create(Agent owner) {
            if (_lit == null || !_lit.IsValid) _lit = Build(false);
            if (_glow == null || !_glow.IsValid) _glow = Build(true);
            GameEntity entity = DualLayerParticleVisual.Create(owner, _lit, _glow);
            if (entity == null) return null;
            var visual = new FearExclamationVisual { _entity = entity }; visual.Update(owner, 0f); return visual;
        }
        public void Update(Agent owner, float dt) {
            if (_entity == null) return;
            if (owner == null || !owner.IsActive()) { Remove(); return; }
            _untilUpdate -= dt; if (_untilUpdate > 0f) return; _untilUpdate = 1f / 20f;
            // 交叉面感叹号从各个视角均可见，无需摄像机查询。
            DualLayerParticleVisual.Update(_entity,
                new MatrixFrame(Mat3.Identity, owner.GetEyeGlobalPosition() + new Vec3(0f, 0f, 0.3f)));
        }
        public void Remove() { DualLayerParticleVisual.Remove(_entity); _entity = null; }
        private static Mesh Build(bool luminous) => DualLayerParticleVisual.CreateMesh(builder => {
            uint color = new Color(1f, 0.025f, 0.025f, luminous ? 0.65f : 0.95f).ToUnsignedInteger();
            float width = luminous ? 0.042f : 0.055f;
            for (int plane = 0; plane < 2; plane++) {
                Vec3 side = plane == 0 ? new Vec3(width, 0f, 0f) : new Vec3(0f, width, 0f);
                Vec3 normal = plane == 0 ? new Vec3(0f, 1f, 0f) : new Vec3(1f, 0f, 0f);
                AddQuad(builder, side, normal, 0.15f, 0.58f, color);
                AddQuad(builder, side, normal, 0f, 0.09f, color);
            }
        }, luminous);
        private static void AddQuad(MeshBuilder builder, Vec3 side, Vec3 normal, float bottom, float top, uint color) {
            Vec2 uv = new Vec2(0.5f, 0.5f);
            int a = builder.AddFaceCorner(-side + Vec3.Up * bottom, normal, uv, color);
            int b = builder.AddFaceCorner(side + Vec3.Up * bottom, normal, uv, color);
            int c = builder.AddFaceCorner(side + Vec3.Up * top, normal, uv, color);
            int d = builder.AddFaceCorner(-side + Vec3.Up * top, normal, uv, color);
            builder.AddFace(a, b, c); builder.AddFace(a, c, d);
        }
    }
}
