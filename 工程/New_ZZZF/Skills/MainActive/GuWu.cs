using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class GuWu : SkillBase
    {
        public GuWu()
        {
            SkillID = "GuWu";      // 必须唯一
            Type = SPSkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0021}GuWu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0022}群体状态，使用后附近士兵获得鼓舞状态，提升射击精度，每秒增加1点耐力，并且回复少量已损生命值。持续时间：30秒。冷却时间：60秒。");
        }
        
        /// <summary>
        /// NPC AI逻辑：鼓舞是群体增益技能，当有友军且他们缺少buff时释放
        /// </summary>
        public override bool CheckCondition(Agent caster)
        {
            // 1. 基础条件检查
            if (!base.CheckCondition(caster)) return false;
            
            // 2. 检查周围是否有友军
            List<Agent> allies = Script.GetTargetedInRange(caster, caster.GetEyeGlobalPosition(), 50, true);
            if (allies == null || allies.Count == 0) return false;
            
            // 3. 检查友军是否已有buff（避免重复释放）
            int alliesWithoutBuff = 0;
            foreach (var ally in allies)
            {
                if (ally.IsActive())
                {
                    var skillComponent = ally.GetComponent<AgentSkillComponent>();
                    if (skillComponent != null && !skillComponent.StateContainer.HasState("GuWuBuff"))
                    {
                        alliesWithoutBuff++;
                    }
                }
            }
            
            // 4. 至少有一定数量的友军缺少buff
            return alliesWithoutBuff >= 2;
        }
        
        public override bool Activate(Agent agent)
        {
            List<Agent> values= Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(),50, true);
            if (values!=null&&values.Count>0)
            {
                foreach (var item in values)
                {
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff> { new GuWuBuff(30f, agent), }; // 新实例
                    foreach (var state in newStates)
                    {
                        state.TargetAgent = item;
                        item.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                    }
                }

                return true;
            }

            return false;
        }

        public class GuWuBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public GuWuBuff(float duration, Agent source)
            {
                StateId = "GuWuBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index,out var agentSkillComponent);
                if (agentSkillComponent == null) { return; }
                // 累积时间
                _timeSinceLastTick += dt;

                //每秒刷一次状态
                if (_timeSinceLastTick >= 1f)
                {
                    
                    ZZZF_SandboxAgentStatCalculateModel zZZF_SandboxAgentStatCalculate = MissionGameModels.Current.AgentStatCalculateModel as ZZZF_SandboxAgentStatCalculateModel;
                    if (zZZF_SandboxAgentStatCalculate != null)
                    {
                        zZZF_SandboxAgentStatCalculate._dt = dt;
                        agentSkillComponent.ChangeStamina(1);
                        agent.Health += (agentSkillComponent.MaxHP - agent.Health) * 0.1f;
                        agent.UpdateAgentProperties();
                    }
                    _timeSinceLastTick -= 1f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {
                agent.UpdateAgentProperties();
            }
        }
    }
}
