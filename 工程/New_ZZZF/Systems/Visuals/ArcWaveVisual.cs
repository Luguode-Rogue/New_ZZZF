using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal static class ArcWaveVisual
    {
        private static Mesh _flyingLit, _flyingLight, _expandingLit, _expandingLight;
        internal static GameEntity Create(Agent owner, bool expanding)
        {
            return DualLayerParticleVisual.Create(owner, GetMesh(expanding, false), GetMesh(expanding, true));
        }
        internal static void Update(GameEntity entity, Vec3 origin, Mat3 rotation, float radius)
        {
            rotation.ApplyScaleLocal(new Vec3(radius, radius, 1f));
            DualLayerParticleVisual.Update(entity, new MatrixFrame(rotation, origin));
        }
        private static Mesh GetMesh(bool expanding, bool luminous)
        {
            Mesh cached = expanding ? (luminous ? _expandingLight : _expandingLit) : (luminous ? _flyingLight : _flyingLit);
            if (cached != null && cached.IsValid) return cached;
            Mesh mesh = DualLayerParticleVisual.CreateMesh(builder => {
                int trails = expanding ? 2 : 9;
                for (int trail = 0; trail < trails; trail++) {
                    float offset = expanding ? trail * 0.04f : trail * 0.25f;
                    float opacity = expanding ? 0.85f - trail * 0.3f : 0.92f * (1f - trail / 10f);
                    if (luminous) opacity *= 0.65f;
                    const int segments = 48;
                    for (int i = 0; i < segments; i++) {
                        float a = -(float)Math.PI / 2f + i * (float)Math.PI / segments;
                        float b = -(float)Math.PI / 2f + (i + 1) * (float)Math.PI / segments;
                        float r = expanding ? 1f - offset : 1f;
                        Vec3 p = new Vec3((float)Math.Sin(a) * r, (float)Math.Cos(a) * r - (expanding ? 0f : 1f + offset), 0f);
                        Vec3 q = new Vec3((float)Math.Sin(b) * r, (float)Math.Cos(b) * r - (expanding ? 0f : 1f + offset), 0f);
                        float taper = 0.2f + 0.8f * (float)Math.Sin(Math.PI * (i + 0.5f) / segments);
                        Vec3 width = new Vec3(0f, 0f, (luminous ? 0.055f : 0.035f) * taper);
                        uint color = new Color(1f, 1f, 1f, opacity).ToUnsignedInteger();
                        Vec2 uv = new Vec2(0.5f, 0.5f);
                        int v0 = builder.AddFaceCorner(p - width, Vec3.Up, uv, color);
                        int v1 = builder.AddFaceCorner(p + width, Vec3.Up, uv, color);
                        int v2 = builder.AddFaceCorner(q + width, Vec3.Up, uv, color);
                        int v3 = builder.AddFaceCorner(q - width, Vec3.Up, uv, color);
                        builder.AddFace(v0, v1, v2); builder.AddFace(v0, v2, v3);
                        // 水平薄带与竖直薄带交叉，从高处和人物视角都能看见。
                        Vec3 across = (q - p).NormalizedCopy();
                        across = Vec3.CrossProduct(across, Vec3.Up) * width.z;
                        v0 = builder.AddFaceCorner(p - across, Vec3.Up, uv, color);
                        v1 = builder.AddFaceCorner(p + across, Vec3.Up, uv, color);
                        v2 = builder.AddFaceCorner(q + across, Vec3.Up, uv, color);
                        v3 = builder.AddFaceCorner(q - across, Vec3.Up, uv, color);
                        builder.AddFace(v0, v1, v2); builder.AddFace(v0, v2, v3);
                    }
                }
            }, luminous);
            if (expanding) { if (luminous) _expandingLight = mesh; else _expandingLit = mesh; }
            else { if (luminous) _flyingLight = mesh; else _flyingLit = mesh; }
            return mesh;
        }
    }
}
