using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>后跃旋转光带与暗黄色爆炸碎光；四份缓存网格，每个阶段只创建一个双层实体。</summary>
    internal sealed class TianFaVisual
    {
        private static readonly Mesh[] Meshes = new Mesh[4];
        private GameEntity _entity;
        private Vec3 _origin;
        private bool _burst;
        private float _elapsed;
        internal static TianFaVisual Create(Agent owner, Vec3 origin, bool burst)
        {
            int slot = burst ? 2 : 0;
            for (int i = 0; i < 2; i++)
                if (Meshes[slot + i] == null || !Meshes[slot + i].IsValid) Meshes[slot + i] = Build(burst, i == 1);
            GameEntity entity = DualLayerParticleVisual.Create(owner, Meshes[slot], Meshes[slot + 1]);
            if (entity == null) return null;
            var visual = new TianFaVisual { _entity = entity, _origin = origin, _burst = burst };
            // 世界落地特效不会遮挡角色视线；只有跟随自身的后跃光带按第一人称规则隐藏。
            if (burst) AgentAttachedVisualVisibility.Unregister(entity);
            visual.Update(0f, origin); return visual;
        }
        private static Mesh Build(bool burst, bool glow)
        {
            return DualLayerParticleVisual.CreateMesh(builder => {
                uint color = (burst ? new Color(0.9f, 0.56f, 0.06f, glow ? 0.72f : 0.88f)
                    : new Color(1f, 0.87f, 0.5f, glow ? 0.7f : 0.9f)).ToUnsignedInteger();
                float width = glow ? 0.032f : 0.045f;
                int strips = burst ? 2 : 3;
                for (int strip = 0; strip < strips; strip++)
                    for (int i = 0; i < 80; i++) {
                        float a = i * (float)Math.PI * 2f / 80, b = (i + 1) * (float)Math.PI * 2f / 80;
                        float r = burst ? 1f - strip * 0.16f : 0.7f + strip * 0.17f;
                        float z0 = burst ? 0.04f : strip * 0.65f + i / 80f * 0.55f;
                        float z1 = burst ? 0.04f : strip * 0.65f + (i + 1) / 80f * 0.55f;
                        Vec3 p = new Vec3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, z0);
                        Vec3 q = new Vec3((float)Math.Cos(b) * r, (float)Math.Sin(b) * r, z1);
                        Vec3 w = Vec3.Up * width;
                        Quad(builder, p - w, p + w, q + w, q - w, color);
                        Vec3 side = Vec3.CrossProduct((q - p).NormalizedCopy(), Vec3.Up) * width;
                        Quad(builder, p - side, p + side, q + side, q - side, color);
                    }
                for (int i = 0; i < 72; i++) {
                    float angle = i * 2.399963f;
                    float r = 0.18f + (i % 11) / 11f * 0.72f;
                    Vec3 p = new Vec3((float)Math.Cos(angle) * r, (float)Math.Sin(angle) * r,
                        burst ? (i % 7) * 0.13f : (i % 13) * 0.15f);
                    float size = glow ? 0.032f : 0.043f;
                    Vec3 a = new Vec3(size, 0f, 0f), b = new Vec3(0f, size, 0f);
                    Quad(builder, p - a, p + Vec3.Up * size, p + a, p - Vec3.Up * size, color);
                    Quad(builder, p - a, p + b, p + a, p - b, color);
                }
            }, glow);
        }
        private static void Quad(MeshBuilder builder, Vec3 a, Vec3 b, Vec3 c, Vec3 d, uint color)
        {
            Vec2 uv = new Vec2(0.5f, 0.5f);
            int i0 = builder.AddFaceCorner(a, Vec3.Up, uv, color);
            int i1 = builder.AddFaceCorner(b, Vec3.Up, uv, color);
            int i2 = builder.AddFaceCorner(c, Vec3.Up, uv, color);
            int i3 = builder.AddFaceCorner(d, Vec3.Up, uv, color);
            builder.AddFace(i0, i1, i2); builder.AddFace(i0, i2, i3);
        }
        internal bool Update(float dt, Vec3 position)
        {
            if (_entity == null) return false;
            _elapsed += dt;
            float duration = _burst ? 1.1f : 1.6f;
            if (_elapsed >= duration) { Remove(); return false; }
            if (!_burst && position.IsValid) _origin = position;
            float t = _elapsed / duration;
            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutUp(_elapsed * (_burst ? 3f : 12f));
            float radius = _burst ? 0.3f + TianFaZhiJian.Radius * t : 1f;
            rotation.ApplyScaleLocal(new Vec3(radius, radius, _burst ? 0.5f + 2f * t : 1f));
            _entity.SetFactorColor(new Color(1f, 1f, 1f, _burst ? 1f - t : 1f).ToUnsignedInteger());
            DualLayerParticleVisual.Update(_entity, new MatrixFrame(rotation, _origin + (_burst ? Vec3.Zero : Vec3.Up * 0.25f)));
            return true;
        }
        internal void Remove() { DualLayerParticleVisual.Remove(_entity); _entity = null; }
    }
}
