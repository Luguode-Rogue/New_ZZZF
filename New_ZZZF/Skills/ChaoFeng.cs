using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class ChaoFeng : SkillBase
    {
        public ChaoFeng()
        {
            SkillID = "ChaoFeng";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0023}ChaoFeng");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0024}嘲讽附近敌方单位，并持续大幅回复自身血量。受到嘲讽的单位会持续靠近施法者。持续时间：30秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {                    
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new ChaoFengBuffApplyToSelf(30f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            List<Agent> values= Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(),30);
            if (values!=null&&values.Count>0)
            {
                foreach (var item in values)
                {
                    // 每次创建新的状态实例
                    newStates = new List<AgentBuff> { new ChaoFengBuffApplyToEnemy(30f, agent), }; // 新实例
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
        public override bool CheckCondition(Agent caster)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(caster.Index, out var agentSkill);
            if (agentSkill == null) { return false; }
            if (caster.Health/ agentSkill.MaxHP<=0.5f)
            {
                return true;
            }
            List<Agent> FoeList = Script.GetTargetedInRange(caster, caster.GetEyeGlobalPosition(), 30);
            List<Agent> FriendList = Script.GetTargetedInRange(caster, caster.GetEyeGlobalPosition(), 30,true);
            if (FoeList.Count>0)
            {
                if (FoeList.Count > 5)
                {
                    return true;
                }
                else if (FoeList.Count>2&&FriendList.Count>0)
                { 
                    return true;
                }
            }
            // 默认条件：Agent存活且非坐骑
            return false;
        }

        public class ChaoFengBuffApplyToEnemy : AgentBuff
        {
            private float _timeSinceLastTick;
            public ChaoFengBuffApplyToEnemy(float duration, Agent source)
            {
                StateId = "ChaoFengBuffApplyToEnemy";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                agent.SetTargetAgent(SourceAgent);
                agent.SetTargetPosition(SourceAgent.Position.AsVec2);
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
                if (agentSkillComponent == null) { return; }
                // 累积时间
                _timeSinceLastTick += dt;

                //每秒刷一次状态
                if (_timeSinceLastTick >= 1f)
                {
                    agent.SetTargetAgent(SourceAgent);
                    agent.SetTargetPosition(SourceAgent.Position.AsVec2);
                    _timeSinceLastTick -= 1f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {
                agent.ClearTargetFrame();
                agent.InvalidateTargetAgent();
            }
        }
        public class ChaoFengBuffApplyToSelf : AgentBuff
        {
            private float _timeSinceLastTick;
            public ChaoFengBuffApplyToSelf(float duration, Agent source)
            {
                StateId = "ChaoFengBuffApplyToSelf";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
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
                        agent.Health += (agentSkillComponent.MaxHP - agent.Health) * 0.5f;
                        
                    }
                    _timeSinceLastTick -= 1f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {
                
            }
        }
    }
}
