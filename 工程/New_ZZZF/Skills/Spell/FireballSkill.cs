using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class FireballSkill : SkillBase
    {
        // 缓存机制：每Agent的目标检测结果，避免频繁遍历
        private static Dictionary<int, (float time, List<Agent> enemies)> _targetCache = new Dictionary<int, (float, List<Agent>)>();
        
        public FireballSkill()
        {
            SkillID = "Fireball";
            Type = SPSkillType.Spell;
            Cooldown = 100f;
            ResourceCost = 100f;
            Text = new TaleWorlds.Localization.TextObject("{=12345678}Fireball");

        }
        
        /// <summary>
        /// NPC AI逻辑：智能判断是否应该释放火球术
        /// 触发条件（满足任一即可）：
        /// 1. 敌人群体密集（3个以上敌人在15米内）→ AOE收益最大化
        /// 2. 有高价值目标（Hero/精英兵）在射程内 → 优先击杀威胁目标
        /// 3. 自身血量低（<30%）且有敌人在射程内 → 绝望反击
        /// 4. 敌人正在使用远程武器 → 优先打断
        /// </summary>
        public override bool CheckCondition(Agent caster)
        {
            // 1. 基础条件检查（Agent活跃且非坐骑）
            if (!base.CheckCondition(caster)) return false;
            
            // 2. 性能优化：缓存机制（每2秒重新检测）
            float currentTime = (float)Mission.Current.CurrentTime;
            if (!_targetCache.TryGetValue(caster.Index, out var cached) ||
                currentTime - cached.time > 2f)
            {
                var detectedEnemies = Script.GetTargetedInRange(caster, caster.GetEyeGlobalPosition(), 15);
                _targetCache[caster.Index] = (currentTime, detectedEnemies);
            }

            // 3. 获取缓存的敌人列表
            var enemies = _targetCache[caster.Index].enemies;
            if (enemies == null || enemies.Count == 0) return false;
            
            // 4. 战术判断
            // 条件1：群体密集（AOE收益）
            if (enemies.Count >= 3) return true;
            
            // 条件2：高价值目标（Hero单位）
            foreach (var enemy in enemies)
            {
                if (enemy.IsHero) return true;
            }
            
            // 条件3：自身血量低（ desperation）
            var skillComponent = caster.GetComponent<AgentSkillComponent>();
            if (skillComponent != null && caster.Health < skillComponent.MaxHP * 0.3f) return true;
            
            // 条件4：敌人使用远程武器（优先打断）
            foreach (var enemy in enemies)
            {
                var weapon = enemy.WieldedWeapon.CurrentUsageItem;
                if (weapon != null && weapon.IsRangedWeapon) return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 清理指定Agent的缓存（避免内存泄漏）
        /// </summary>
        public static void CleanCache(int agentIndex)
        {
            _targetCache.Remove(agentIndex);
        }


        public override bool Activate(Agent agent)
        {
            Agent target = FindTarget(agent);
            if (target == null || !target.IsActive()) return false;

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff>
                {
                    new BurningState(5f, 1f, agent), // 新实例
                    new du(5f, 1f, agent)            // 新实例
                };

            foreach (var state in newStates)
            {
                state.TargetAgent = target;
                target.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;
        }
        private Agent FindTarget(Agent agent)
        {
           // Script.GetTargetedInRange( agent,);
            Agent castAgent = agent;
            List<Agent> list = Script.FindAgentsWithinSpellRange(agent.GetEyeGlobalPosition(), 15);
            List<Agent> FriendAgent = new List<Agent>();
            List<Agent> FoeAgent = new List<Agent>();
            Script.AgentListIFF(castAgent, list, out FriendAgent, out FoeAgent);
            Agent outAgent = Script.FindClosestAgentToCaster(agent, FoeAgent);
            if (outAgent != agent)
            {
                return outAgent;
            }
            return null;
        }
    }
}
