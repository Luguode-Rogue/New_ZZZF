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
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    internal class NaGouCiFu : SkillBase
    {
        public NaGouCiFu()
        {
            SkillID = "NaGouCiFu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0039}NaGouCiFu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0040}群体状态，使用后附近士兵获得瘟疫赐福，每秒扣除10%最大血量并转化为护盾，但获得3次死而复生的机会。扣除生命值不会致死，且转化护盾会继续持续5秒。" +
                "提高50%造成的伤害，降低50%移动速度，受到物理伤害时50%免疫此次伤害。消耗耐力：60。持续时间：60秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            List<Agent> values= Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(),50, true);
            if (values!=null&&values.Count>0)
            {
                foreach (var item in values)
                {
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff> { new NaGouCiFuBuff(60f, agent), }; // 新实例
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

        public class NaGouCiFuBuff : AgentBuff
        {
            private int _timeCount = 0;
            private float _timeSinceLastTick;
            public NaGouCiFuBuff(float duration, Agent source)
            {
                StateId = "NaGouCiFuBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
                agentSkillComponent._lifeResurgenceCount += 3;
                
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
                        if (_timeCount < 5)
                        {
                            agentSkillComponent._shieldStrength += agentSkillComponent.MaxHP * 0.1f;
                            agent.Health = MathF.Clamp(agent.Health - agentSkillComponent.MaxHP * 0.1f, 1, agentSkillComponent.MaxHP);
                            
                        }
                        if (agent.Health <= 1)
                        { _timeCount += 1; }
                        else
                        { _timeCount = 0; }
                        zZZF_SandboxAgentStatCalculate._dt = dt;
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
