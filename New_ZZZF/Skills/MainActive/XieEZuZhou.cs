﻿using New_ZZZF.Systems;
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
using static New_ZZZF.JingXia;

namespace New_ZZZF
{
    internal class XieEZuZhou : SkillBase
    {
        public XieEZuZhou()
        {
            SkillID = "XieEZuZhou";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0053}XieEZuZhou");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0054}群体负面状态，持续影响附近敌方单位。受影响单位每秒生命值减少1%，并且禁用远程武器，对英雄单位无影响。消耗耐力：60。持续时间：60秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new XieEZuZhouBuffToSelf(60f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;
        }


    }
    public class XieEZuZhouBuffToSelf : AgentBuff
    {
        private float _timeSinceLastTick;
        public XieEZuZhouBuffToSelf(float duration, Agent source)
        {
            StateId = "XieEZuZhouBuffToSelf";
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
                if (foeAgent != null && foeAgent.Count > 0)
                {
                    foreach (var item in foeAgent)
                    {

                        // 每次创建新的状态实例
                        List<AgentBuff> newStates = new List<AgentBuff> { new XieEZuZhouBuffToEnemy(2F, agent), }; // 新实例
                        foreach (var state in newStates)
                        {
                            state.TargetAgent = item;
                            item.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
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
    public class XieEZuZhouBuffToEnemy : AgentBuff
    {
        private float _timeSinceLastTick;
        private List<EquipmentIndex> itemsIndex = new List<EquipmentIndex>();
        private List<MissionWeapon> weapon = new List<MissionWeapon>();
        public XieEZuZhouBuffToEnemy(float duration, Agent source)
        {
            StateId = "XieEZuZhouBuffToEnemy";
            Duration = duration;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {

        }

        public override void OnUpdate(Agent agent, float dt)
        {

            // 累积时间
            _timeSinceLastTick += dt;

            //每秒刷一次状态
            if (_timeSinceLastTick >= 1f)
            {

                agent.UpdateAgentProperties();

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            agent.UpdateAgentProperties();
        }
    }
}
