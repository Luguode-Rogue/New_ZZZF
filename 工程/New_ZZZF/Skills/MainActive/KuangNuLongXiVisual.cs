using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    // 原版火焰系统保持材质；12处表现实体只创建一次，持续随施法者位置和朝向移动。
    internal sealed class KuangNuLongXiVisual
    {
        private readonly GameEntity[] _entities = new GameEntity[12];
        internal static KuangNuLongXiVisual Create(Agent owner) {
            if (owner?.Mission?.Scene == null) return null;
            var visual = new KuangNuLongXiVisual();
            try {
                for (int i = 0; i < visual._entities.Length; i++) {
                    var entity = GameEntity.CreateEmpty(owner.Mission.Scene);
                    visual._entities[i] = entity;
                    entity.AddParticleSystemComponent("psys_battleground_env_fire");
                    AgentAttachedVisualVisibility.Register(owner, entity);
                }
                visual.Update(owner);
                return visual;
            } catch (Exception) { visual.Remove(); return null; }
        }
        internal void Update(Agent owner) {
            Vec3 forward = owner.LookDirection.AsVec2.ToVec3();
            if (forward.LengthSquared < 0.001f) forward = Vec3.Forward;
            forward.Normalize();
            for (int row = 0; row < 3; row++) {
                Vec3 direction = forward;
                direction.RotateAboutZ((row - 1) * (float)Math.PI / 6f);
                for (int step = 0; step < 4; step++) {
                    var frame = MatrixFrame.Identity;
                    frame.origin = owner.Position + direction * (2.5f * (step + 1)) + Vec3.Up;
                    _entities[row * 4 + step]?.SetGlobalFrame(frame);
                }
            }
        }
        internal void Remove() {
            for (int i = 0; i < _entities.Length; i++) {
                var entity = _entities[i];
                _entities[i] = null;
                if (entity == null) continue;
                AgentAttachedVisualVisibility.Unregister(entity);
                try { entity.Remove(1); } catch (Exception) { }
            }
        }
    }
}