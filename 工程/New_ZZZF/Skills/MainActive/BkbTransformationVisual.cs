using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>原生单位缩放；白膜只替换本单位 Mesh 的材质引用，保留原来的蒙皮 Shader。</summary>
    internal sealed class BkbTransformationVisual
    {
        private static readonly MethodInfo SetScale = typeof(Agent).GetMethod("SetInitialAgentScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Texture _whiteTexture;
        private float _originalScale;
        private bool _scaled;
        internal float OriginalScale => _originalScale;
        internal static void ApplyScale(Agent agent, float scale)
        {
            if (agent != null && agent.IsActive() && SetScale != null)
                SetScale.Invoke(agent, new object[] { scale });
        }
        private readonly List<MeshMaterialRecord> _meshes = new List<MeshMaterialRecord>();
        private readonly Dictionary<UIntPtr, Material> _materials = new Dictionary<UIntPtr, Material>();
        private readonly HashSet<UIntPtr> _seen = new HashSet<UIntPtr>();
        private sealed class MeshMaterialRecord { public Mesh Mesh; public Material Original; public Material White; public uint Color; }

        internal void Apply(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            _originalScale = agent.AgentScale;
            if (SetScale != null) {
                SetScale.Invoke(agent, new object[] { _originalScale * 1.5f });
                _scaled = true;
            }
            Refresh(agent);
        }

        internal void Refresh(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return;
            GameEntity root = agent.AgentVisuals?.GetEntity();
            if (root == null) return;
            Texture white = GetWhiteTexture();
            if (white == null) return;
            var entities = new List<GameEntity>();
            root.GetChildrenRecursive(ref entities);
            entities.Add(root);
            foreach (GameEntity entity in entities) {
                for (int i = 0; i < entity.MultiMeshComponentCount; i++) Paint(entity.GetMetaMesh(i), white);
                for (int i = 0; i < entity.ClothSimulatorComponentCount; i++) Paint(entity.GetClothSimulator(i).GetFirstMetaMesh(), white);
            }
        }

        private void Paint(MetaMesh meta, Texture texture)
        {
            if (meta == null) return;
            for (int i = 0; i < meta.MeshCount; i++) {
                Mesh mesh = meta.GetMeshAtIndex(i);
                if (mesh == null || !mesh.IsValid || _seen.Contains(mesh.Pointer)) continue;
                Material original = mesh.GetMaterial();
                if (original == null) continue;
                if (!_materials.TryGetValue(original.Pointer, out Material white)) {
                    white = original.CreateCopy();
                    white.SetTexture(Material.MBTextureType.DiffuseMap, texture);
                    white.SetTexture(Material.MBTextureType.DiffuseMap2, texture);
                    _materials.Add(original.Pointer, white);
                }
                _meshes.Add(new MeshMaterialRecord { Mesh = mesh, Original = original, White = white, Color = mesh.Color });
                _seen.Add(mesh.Pointer);
                mesh.SetMaterial(white);
                mesh.Color = 0xffffffff;
            }
        }

        internal void Remove(Agent agent)
        {
            // Agent 离场后不能再解引用它的原生 Visuals/网格。
            if (agent != null && agent.IsActive()) {
                foreach (MeshMaterialRecord record in _meshes) {
                    if (!record.Mesh.IsValid) continue;
                    Material current = record.Mesh.GetMaterial();
                    if (current == null || current.Pointer != record.White.Pointer) continue;
                    record.Mesh.SetMaterial(record.Original);
                    record.Mesh.Color = record.Color;
                }
                if (_scaled && SetScale != null) SetScale.Invoke(agent, new object[] { _originalScale });
            }
            _scaled = false;
            _meshes.Clear();
            _materials.Clear();
            _seen.Clear();
        }

        private static Texture GetWhiteTexture()
        {
            if (_whiteTexture != null) return _whiteTexture;
            // 一个白色像素的标准未压缩 RGBA DDS；运行时创建，不需要新资源文件。
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream)) {
                writer.Write(0x20534444); // DDS magic
                writer.Write(124); writer.Write(0x100f); // header size / flags
                writer.Write(1); writer.Write(1); writer.Write(4); // height / width / pitch
                writer.Write(0); writer.Write(0); // depth / mipmaps
                for (int i = 0; i < 11; i++) writer.Write(0);
                writer.Write(32); writer.Write(0x41); writer.Write(0); writer.Write(32);
                writer.Write(0x00ff0000); writer.Write(0x0000ff00); writer.Write(0x000000ff); writer.Write(unchecked((int)0xff000000));
                writer.Write(0x1000); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
                writer.Write(unchecked((int)0xffffffff));
                _whiteTexture = Texture.CreateFromMemory(stream.ToArray());
            }
            return _whiteTexture;
        }
    }
}
