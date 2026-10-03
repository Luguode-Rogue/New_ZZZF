using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// 金红色菱片的几何生成；明暗材质和实体生命周期由双层粒子通用入口处理。
    /// </summary>
    internal static class GoldenSparkMesh
    {
        public static Mesh Create(Action<MeshBuilder> build, bool selfLuminous = true)
        {
            return DualLayerParticleVisual.CreateMesh(build, selfLuminous);
        }

        public static void AddDiamond(MeshBuilder builder, Vec3 center, float size, uint color)
        {
            AddFace(builder, center, new Vec3(size, 0f, 0f),
                new Vec3(0f, 1f, 0f), size, color);
            AddFace(builder, center, new Vec3(0f, size, 0f),
                new Vec3(1f, 0f, 0f), size, color);
        }

        private static void AddFace(MeshBuilder builder, Vec3 center, Vec3 horizontal,
            Vec3 normal, float size, uint color)
        {
            Vec3 vertical = new Vec3(0f, 0f, size * 1.35f);
            int top = builder.AddFaceCorner(center + vertical, normal, new Vec2(0.5f, 0f), color);
            int right = builder.AddFaceCorner(center + horizontal, normal, new Vec2(1f, 0.5f), color);
            int bottom = builder.AddFaceCorner(center - vertical, normal, new Vec2(0.5f, 1f), color);
            int left = builder.AddFaceCorner(center - horizontal, normal, new Vec2(0f, 0.5f), color);
            builder.AddFace(top, right, bottom);
            builder.AddFace(top, bottom, left);
        }
    }
}
