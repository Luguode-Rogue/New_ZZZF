using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// 自建网格使用原版 prt_shd_sparks 材质。鼓舞的第四组对照已验证它在暗处可见。
    /// 不再以 vertex_color_lighting 配点光源来模拟粒子自发光。
    /// </summary>
    internal static class GoldenSparkMesh
    {
        private static Material _material;

        public static Mesh Create(Action<MeshBuilder> build)
        {
            Material material = GetMaterial();
            if (material == null || build == null)
                return null;

            MeshBuilder builder = new MeshBuilder();
            build(builder);
            Mesh mesh = builder.Finalize();
            if (mesh == null || !mesh.IsValid)
                return null;
            mesh.SetMaterial(material);
            mesh.Color = new Color(1f, 1f, 1f, 1f).ToUnsignedInteger();
            mesh.CullingMode = MBMeshCullingMode.None;
            mesh.UpdateBoundingBox();
            return mesh;
        }

        public static void AddDiamond(MeshBuilder builder, Vec3 center, float size, uint color)
        {
            AddFace(builder, center, new Vec3(size, 0f, 0f),
                new Vec3(0f, 1f, 0f), size, color);
            AddFace(builder, center, new Vec3(0f, size, 0f),
                new Vec3(1f, 0f, 0f), size, color);
        }

        private static Material GetMaterial()
        {
            if (_material != null && _material.IsValid)
                return _material;

            Material source = Material.GetFromResource("prt_shd_sparks");
            if (source == null || !source.IsValid)
                return null;
            _material = source.CreateCopy();
            return _material != null && _material.IsValid ? _material : null;
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
