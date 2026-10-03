using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 恐虐赐福绕角色旋转的血红色弧线。和天启一样，受光内层负责白天的形状，稍宽的自发光外层负责暗处可见度。
    /// 施放和持续阶段各复用一组静态网格，每个受益者只持有一个双层实体。
    /// </summary>
    internal sealed class KongNueCiFuWhirlVisual
    {
        private const float BurstDuration = 1.8f;
        private const float UpdateInterval = 1f / 30f;
        private static Mesh _burstLitMesh;
        private static Mesh _burstLuminousMesh;
        private static Mesh _lingeringLitMesh;
        private static Mesh _lingeringLuminousMesh;
        private readonly GameEntity _entity;
        private readonly bool _lingering;
        private float _elapsed;
        private float _timeToFrame = UpdateInterval;
        private bool _removed;

        private KongNueCiFuWhirlVisual(GameEntity entity, bool lingering)
        {
            _entity = entity;
            _lingering = lingering;
        }

        public static KongNueCiFuWhirlVisual Create(Agent owner, bool lingering)
        {
            if (owner == null || !owner.IsActive() || owner.Mission?.Scene == null)
                return null;

            Mesh litMesh = GetMesh(lingering, false);
            Mesh luminousMesh = GetMesh(lingering, true);
            if (litMesh == null || luminousMesh == null)
                return null;

            GameEntity entity = DualLayerParticleVisual.Create(owner, litMesh, luminousMesh);
            if (entity == null)
                return null;
            KongNueCiFuWhirlVisual visual = new KongNueCiFuWhirlVisual(entity, lingering);
            visual.Update(owner, 0f);
            return visual;
        }

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
            _timeToFrame += dt;
            if (_timeToFrame < UpdateInterval)
                return true;
            _timeToFrame = 0f;

            Mat3 rotation = Mat3.Identity;
            rotation.RotateAboutAnArbitraryVector(Vec3.Up,
                _elapsed * (_lingering ? 12f : 18f));
            float bob = 0.045f * (float)Math.Sin(_elapsed * 8f);
            Vec3 center = owner.GetEyeGlobalPosition() - new Vec3(0f, 0f, 0.72f - bob);
            MatrixFrame frame = new MatrixFrame(rotation, center);
            DualLayerParticleVisual.Update(_entity, frame);
            return true;
        }

        public void Remove()
        {
            if (_removed)
                return;
            _removed = true;
            DualLayerParticleVisual.Remove(_entity);
        }

        private static Mesh GetMesh(bool lingering, bool luminous)
        {
            Mesh mesh = lingering
                ? (luminous ? _lingeringLuminousMesh : _lingeringLitMesh)
                : (luminous ? _burstLuminousMesh : _burstLitMesh);
            if (mesh != null && mesh.IsValid)
                return mesh;

            int count = lingering ? 4 : 12;
            mesh = DualLayerParticleVisual.CreateMesh(builder =>
            {
                for (int i = 0; i < count; i++)
                {
                    // 每条风线是一段向上卷曲的弧，端点收尖；外层略宽、略外扩。
                    float angle = i * 2.399963f;
                    float radius = 0.48f + (i * 7 % 11) * 0.028f +
                        (luminous ? 0.012f : 0f);
                    float height = ((i * 11 % 19) - 9f) * 0.065f;
                    float arc = 0.48f + (i % 4) * 0.11f;
                    AddWindLine(builder, angle, radius, height, arc, luminous, lingering);
                }
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

        private static void AddWindLine(MeshBuilder builder, float angle, float radius,
            float height, float arc, bool luminous, bool lingering)
        {
            const int segments = 6;
            float width = luminous ? 0.027f : 0.013f;
            float opacity = luminous ? (lingering ? 0.45f : 0.53f)
                : (lingering ? 0.75f : 0.9f);
            for (int segment = 0; segment < segments; segment++)
            {
                float first = (float)segment / segments;
                float second = (float)(segment + 1) / segments;
                Vec3 a = WindPoint(angle, radius, height, arc, first);
                Vec3 b = WindPoint(angle, radius, height, arc, second);
                Vec3 normal = new Vec3((a.X + b.X) * 0.5f,
                    (a.Y + b.Y) * 0.5f, 0.15f);
                normal.Normalize();
                float firstWidth = width * (0.15f + 0.85f *
                    (float)Math.Sin(Math.PI * first));
                float secondWidth = width * (0.15f + 0.85f *
                    (float)Math.Sin(Math.PI * second));
                Vec3 firstOffset = new Vec3(0f, 0f, firstWidth);
                Vec3 secondOffset = new Vec3(0f, 0f, secondWidth);
                uint color = new Color(0.85f, 0.025f, 0.04f, opacity).ToUnsignedInteger();
                Vec2 centerUv = new Vec2(0.5f, 0.5f);
                int v0 = builder.AddFaceCorner(a - firstOffset, normal, centerUv, color);
                int v1 = builder.AddFaceCorner(a + firstOffset, normal, centerUv, color);
                int v2 = builder.AddFaceCorner(b + secondOffset, normal, centerUv, color);
                int v3 = builder.AddFaceCorner(b - secondOffset, normal, centerUv, color);
                builder.AddFace(v0, v1, v2);
                builder.AddFace(v0, v2, v3);
            }
        }

        private static Vec3 WindPoint(float angle, float radius, float height,
            float arc, float progress)
        {
            float direction = angle + arc * (progress - 0.5f);
            float wave = 0.045f * (float)Math.Sin(progress * Math.PI * 2f + angle);
            float currentRadius = radius + wave;
            return new Vec3(currentRadius * (float)Math.Cos(direction),
                currentRadius * (float)Math.Sin(direction),
                height + 0.15f * (progress - 0.5f));
        }
    }
}
