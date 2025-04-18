﻿using New_ZZZF.Systems;
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
using static New_ZZZF.JingXia;

namespace New_ZZZF
{
    internal class WeiYa : SkillBase
    {
        public WeiYa()
        {
            SkillID = "WeiYa";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0031}WeiYa");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0032}群体负面状态，影响所有敌方单位，但施法者等级低于敌方太多时将不生效。降低敌方50%移动能力/伤害加成/射击精度。消耗耐力：50。持续时间：45秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new WeiYaBuffToSelf(30f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;
        }

        public class WeiYaBuffToSelf : AgentBuff
        {
            private float _timeSinceLastTick;
            public WeiYaBuffToSelf(float duration, Agent source)
            {
                StateId = "WeiYaBuffToSelf";
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
                    List<Agent> values = Mission.Current.Agents;
                    Script.AgentListIFF(agent, values, out var friendAgent, out var foeAgent);
                    int agentLv = agent.Character.Level + 15;
                    if (foeAgent != null && foeAgent.Count > 0)
                    {
                        foreach (var item in foeAgent)
                        {
                            int tarLv = item.Character.Level;
                            if (tarLv < agentLv)
                            {
                                //item.PlayParticleEffect("fire_burning");
                                // 每次创建新的状态实例
                                List<AgentBuff> newStates = new List<AgentBuff> { new WeiYaBuffToEnemy(2f, agent), }; // 新实例
                                foreach (var state in newStates)
                                {
                                    state.TargetAgent = item;
                                    item.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                                }
                            }
                        }

                        return;
                    }
                    _timeSinceLastTick -= 1f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {
                
            }
        }
        public class WeiYaBuffToEnemy : AgentBuff
        {
            private float _timeSinceLastTick;
            public WeiYaBuffToEnemy(float duration, Agent source)
            {
                StateId = "WeiYaBuffToEnemy";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                agent.UpdateAgentProperties();
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
