using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 自建粒子的通用明暗双层入口：几何由调用方提供，受光材质负责白天，
    /// 原版火花材质负责夜晚和阴影。同一实体持有两层网格，避免增加实体数量。
    /// 调用方可缓存返回的网格，并决定运动轨迹及持续时间。
    /// </summary>
    internal static class DualLayerParticleVisual
    {
        private static Material _litMaterial;
        private static Material _luminousMaterial;

        public static Mesh CreateMesh(Action<MeshBuilder> build, bool selfLuminous)
        {
            Material material = GetMaterial(selfLuminous);
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

        public static GameEntity Create(Agent owner, Mesh litMesh, Mesh luminousMesh)
        {
            if (owner == null || !owner.IsActive() || owner.Mission?.Scene == null ||
                litMesh == null || luminousMesh == null)
                return null;

            GameEntity entity = null;
            try
            {
                entity = GameEntity.CreateEmptyDynamic(owner.Mission.Scene, false);
                if (entity == null)
                    return null;
                entity.AddMesh(litMesh);
                entity.AddMesh(luminousMesh);
                entity.SetFactorColor(new Color(1f, 1f, 1f, 1f).ToUnsignedInteger());
                entity.SetVisibilityExcludeParents(true);
                entity.SetReadyToRender(true);
                AgentAttachedVisualVisibility.Register(owner, entity);
                return entity;
            }
            catch (Exception)
            {
                Remove(entity);
                return null;
            }
        }

        public static void Update(GameEntity entity, MatrixFrame frame)
        {
            entity?.SetGlobalFrame(frame);
        }

        public static void Remove(GameEntity entity)
        {
            if (entity == null)
                return;
            try { AgentAttachedVisualVisibility.Unregister(entity); }
            catch (Exception) { }
            try { entity.Remove(0); }
            catch (Exception) { }
        }

        private static Material GetMaterial(bool selfLuminous)
        {
            Material material = selfLuminous ? _luminousMaterial : _litMaterial;
            if (material != null && material.IsValid)
                return material;

            Material source = Material.GetFromResource(selfLuminous
                ? "prt_shd_sparks" : "vertex_color_lighting");
            if (source == null || !source.IsValid)
                return null;
            material = source.CreateCopy();
            if (material == null || !material.IsValid)
                return null;
            if (!selfLuminous)
                material.SetAlphaBlendMode(Material.MBAlphaBlendMode.Modulate);
            if (selfLuminous) _luminousMaterial = material;
            else _litMaterial = material;
            return material;
        }
    }
}
