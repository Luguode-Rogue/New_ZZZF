using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    /// <summary>一次施法一个双层圆环实体，固定施法位置，扩散后自动清理。</summary>
    internal sealed class BlessingNovaVisual
    {
        private static Mesh _lit, _glow;
        private GameEntity _entity;
        private Vec3 _origin;
        private float _elapsed;
        internal static BlessingNovaVisual Create(Agent owner) {
            if (_lit == null || !_lit.IsValid) _lit = Build(false);
            if (_glow == null || !_glow.IsValid) _glow = Build(true);
            GameEntity entity = DualLayerParticleVisual.Create(owner, _lit, _glow);
            if (entity == null) return null;
            var visual = new BlessingNovaVisual { _entity = entity, _origin = owner.Position + Vec3.Up * 0.35f };
            visual.Update(0f); return visual;
        }
        private static Mesh Build(bool glow) {
            return DualLayerParticleVisual.CreateMesh(builder => {
                uint color = new Color(1f, 0.72f, 0.18f, glow ? 0.9f : 0.75f).ToUnsignedInteger();
                for (int i = 0; i < 96; i++) {
                    float a = i * (float)(2 * Math.PI / 96), b = (i + 1) * (float)(2 * Math.PI / 96);
                    float width = glow ? 0.018f : 0.013f;
                    Vec3 p = new Vec3((float)Math.Cos(a), (float)Math.Sin(a), 0f);
                    Vec3 q = new Vec3((float)Math.Cos(b), (float)Math.Sin(b), 0f);
                    int i0 = builder.AddFaceCorner(p * (1f - width), Vec3.Up, new Vec2(0f, 0f), color);
                    int i1 = builder.AddFaceCorner(q * (1f - width), Vec3.Up, new Vec2(1f, 0f), color);
                    int i2 = builder.AddFaceCorner(q * (1f + width), Vec3.Up, new Vec2(1f, 1f), color);
                    int i3 = builder.AddFaceCorner(p * (1f + width), Vec3.Up, new Vec2(0f, 1f), color);
                    builder.AddFace(i0, i1, i2); builder.AddFace(i0, i2, i3);
                }
            }, glow);
        }
        internal bool Update(float dt) {
            if (_entity == null) return false;
            _elapsed += dt; if (_elapsed >= 1.5f) { Remove(); return false; }
            float t = _elapsed / 1.5f;
            MatrixFrame frame = MatrixFrame.Identity; frame.origin = _origin;
            frame.rotation.ApplyScaleLocal(0.5f + t * 49.5f);
            _entity.SetFactorColor(new Color(1f, 1f, 1f, 1f - t).ToUnsignedInteger());
            DualLayerParticleVisual.Update(_entity, frame);
            return true;
        }
        internal void Remove() { DualLayerParticleVisual.Remove(_entity); _entity = null; }
    }
}
