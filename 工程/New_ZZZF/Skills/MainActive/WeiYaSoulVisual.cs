using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>血红冤魂由180道在5秒内淡出至20道；共享网格分组旋转。</summary>
    internal sealed class WeiYaSoulVisual
    {
        private const int Groups = 18;
        private const int SoulsPerGroup = 10;
        private const int SustainedGroups = 2;
        private const float ThinningDuration = 5f;
        private const float UpdateInterval = 1f / 30f;
        private static readonly Mesh[] LitMeshes = new Mesh[Groups];
        private static readonly Mesh[] GlowMeshes = new Mesh[Groups];
        private readonly GameEntity[] _entities = new GameEntity[Groups];
        private float _elapsed;
        private float _untilUpdate;
        private bool _removed;

        public static WeiYaSoulVisual Create(Agent owner)
        {
            if (owner == null || !owner.IsActive() || owner.Mission?.Scene == null) return null;
            WeiYaSoulVisual visual = new WeiYaSoulVisual();
            try {
                for (int group = 0; group < Groups; group++) {
                    Mesh lit = GetMesh(group, false);
                    Mesh glow = GetMesh(group, true);
                    visual._entities[group] = DualLayerParticleVisual.Create(owner, lit, glow);
                    if (visual._entities[group] == null) { visual.Remove(); return null; }
                }
                visual.Update(owner, 0f);
                return visual;
            }
            catch (Exception) { visual.Remove(); return null; }
        }

        public void Update(Agent owner, float dt)
        {
            if (_removed) return;
            if (owner == null || !owner.IsActive()) { Remove(); return; }
            _elapsed += dt;
            _untilUpdate -= dt;
            if (_untilUpdate > 0f) return;
            _untilUpdate = UpdateInterval;
            Vec3 center = owner.GetEyeGlobalPosition() - new Vec3(0f, 0f, 0.65f);
            // 180道分成18组；5秒内逐组淡出并移除，最终只留下20道，不重建网格。
            float removedGroups = Math.Min(1f, _elapsed / ThinningDuration) * (Groups - SustainedGroups);
            for (int group = 0; group < Groups; group++) {
                if (_entities[group] == null) continue;
                float opacity = group < SustainedGroups ? 1f :
                    Math.Max(0f, Math.Min(1f, group - SustainedGroups + 1f - removedGroups));
                if (opacity <= 0f) {
                    DualLayerParticleVisual.Remove(_entities[group]);
                    _entities[group] = null;
                    continue;
                }
                _entities[group].SetFactorColor(new Color(1f, 1f, 1f, opacity).ToUnsignedInteger());
                Mat3 rotation = Mat3.Identity;
                int motionGroup = group % 6;
                float speed = (0.55f + motionGroup * 0.12f) * (motionGroup % 2 == 0 ? 1f : -1f);
                rotation.RotateAboutAnArbitraryVector(Vec3.Up, _elapsed * speed);
                float bob = 0.28f * (float)Math.Sin(_elapsed * (1.1f + motionGroup * 0.13f) + group);
                DualLayerParticleVisual.Update(_entities[group],
                    new MatrixFrame(rotation, center + new Vec3(0f, 0f, bob)));
            }
        }

        public void Remove()
        {
            if (_removed) return;
            _removed = true;
            for (int group = 0; group < Groups; group++) {
                DualLayerParticleVisual.Remove(_entities[group]);
                _entities[group] = null;
            }
        }

        private static Mesh GetMesh(int group, bool luminous)
        {
            Mesh[] cache = luminous ? GlowMeshes : LitMeshes;
            if (cache[group] != null && cache[group].IsValid) return cache[group];
            cache[group] = DualLayerParticleVisual.CreateMesh(builder => {
                for (int i = 0; i < SoulsPerGroup; i++) {
                    int seed = group * SoulsPerGroup + i;
                    float angle = seed * 2.399963f;
                    float radius = 2.2f + (seed * 13 % 31) * 0.24f;
                    float height = -0.6f + (seed * 17 % 29) * 0.085f;
                    float size = 0.36f + (seed % 5) * 0.045f;
                    AddSoul(builder, angle, radius, height, size, luminous, seed);
                }
            }, luminous);
            return cache[group];
        }

        private static void AddSoul(MeshBuilder builder, float angle, float radius,
            float height, float size, bool luminous, int seed)
        {
            // 自发光几何略窄，防止填满骷髅镂空；保持血红，不使用白色核心。
            float layerScale = luminous ? 0.9f : 1f;
            float opacity = luminous ? 0.58f : 0.82f;
            uint color = new Color(luminous ? 1f : 0.72f, 0.015f, 0.035f, opacity).ToUnsignedInteger();
            Vec3 radial = new Vec3((float)Math.Cos(angle), (float)Math.Sin(angle), 0f);
            Vec3 tangent = new Vec3(-radial.Y, radial.X, 0f);
            Vec3 head = radial * (radius + (luminous ? -0.008f : 0f)) + new Vec3(0f, 0f, height);
            // 7×7的头骨轮廓，两个眼窝、鼻腔和分开的牙齿均是真实几何空洞。
            string[] skull = { "0011100", "0111110", "1101011", "1101011",
                "0110110", "0011100", "0010100" };
            float cell = size * layerScale / 7f;
            for (int row = 0; row < skull.Length; row++)
                for (int column = 0; column < skull[row].Length; column++) {
                    if (skull[row][column] != '1') continue;
                    Vec3 origin = head + tangent * ((column - 3.5f) * cell) +
                        new Vec3(0f, 0f, (3.5f - row) * cell);
                    AddQuad(builder, origin, origin + tangent * cell,
                        origin + tangent * cell - Vec3.Up * cell, origin - Vec3.Up * cell,
                        radial, color);
                }

            // 大块弧形长条拖尾，向后渐窄，两个交叉面保证不同视角可见。
            const int segments = 12;
            float arc = (1.7f + (seed % 4) * 0.35f) / radius;
            for (int segment = 0; segment < segments; segment++) {
                float a = segment / (float)segments;
                float b = (segment + 1f) / segments;
                Vec3 first = TailPoint(angle, radius, height, arc, a, seed, luminous);
                Vec3 second = TailPoint(angle, radius, height, arc, b, seed, luminous);
                float firstWidth = size * 0.32f * layerScale * (1f - a);
                float secondWidth = size * 0.32f * layerScale * (1f - b);
                uint tailColor = new Color(luminous ? 1f : 0.72f, 0.015f, 0.035f,
                    opacity * (1f - a * 0.85f)).ToUnsignedInteger();
                AddQuad(builder, first - Vec3.Up * firstWidth, first + Vec3.Up * firstWidth,
                    second + Vec3.Up * secondWidth, second - Vec3.Up * secondWidth, radial, tailColor);
                AddQuad(builder, first - radial * firstWidth, first + radial * firstWidth,
                    second + radial * secondWidth, second - radial * secondWidth, Vec3.Up, tailColor);
            }
        }

        private static Vec3 TailPoint(float angle, float radius, float height,
            float arc, float progress, int seed, bool luminous)
        {
            float direction = angle - arc * progress;
            float r = radius + 0.12f * (float)Math.Sin(progress * 6f + seed) * progress -
                (luminous ? 0.008f : 0f);
            return new Vec3(r * (float)Math.Cos(direction), r * (float)Math.Sin(direction),
                height - 0.3f * progress + 0.16f * (float)Math.Sin(progress * 5f + seed) * progress);
        }

        private static void AddQuad(MeshBuilder builder, Vec3 a, Vec3 b, Vec3 c, Vec3 d,
            Vec3 normal, uint color)
        {
            Vec2 uv = new Vec2(0.5f, 0.5f);
            int v0 = builder.AddFaceCorner(a, normal, uv, color);
            int v1 = builder.AddFaceCorner(b, normal, uv, color);
            int v2 = builder.AddFaceCorner(c, normal, uv, color);
            int v3 = builder.AddFaceCorner(d, normal, uv, color);
            builder.AddFace(v0, v1, v2); builder.AddFace(v0, v2, v3);
        }
    }
}
