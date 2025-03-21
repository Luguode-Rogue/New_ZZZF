using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    //统一存放各种需要自己实现的扩展方法
    public static class AgentExtensions
    {
        private static readonly Dictionary<string, GameEntity> _activeParticles = new Dictionary<string, GameEntity>();
        public static bool IsPerformingAction(this Agent agent)
        {
            // 在此实现您的判断逻辑
            return agent.IsActive() && false;
        }
        public static void PlayParticleEffect(this Agent agent, string effectName)
        {
            // 加载预制件
            MatrixFrame attachFrame = agent.AgentVisuals.GetBoneEntitialFrame(0, true);
            Vec3 attachVec3 =agent.GetEyeGlobalPosition();
            GameEntity particleEntity = GameEntity.CreateEmpty(agent.Mission.Scene);

            // 附加到角色胸部骨骼
            particleEntity.SetLocalPosition(attachVec3);
            // 示例路径（根据实际mod路径调整）
            particleEntity.AddParticleSystemComponent("psys_burning_projectile_default_coll"); // 使用游戏内置火焰特效

            if (_activeParticles.TryGetValue(agent.Index + effectName, out var gameEntity))
            { }
            else
            {
                _activeParticles.Add(agent.Index + effectName, particleEntity);
            }
        }

        public static void Damage(this Agent agent, float damageAmount)
        {
            // 在此实现您的逻辑
        }

        public static void StopParticleEffect(this Agent agent, string effectName)
        {
            string key = agent.Index + effectName;
            if (_activeParticles.TryGetValue(key, out GameEntity entity))
            {
                entity.RemoveAllParticleSystems();
                _activeParticles.Remove(key);
            }
        }

        public static void OnDealDamage(this Agent agent, AttackInformation attackInformation)
        {
            // 在此实现您的逻辑
        }
    }
}
