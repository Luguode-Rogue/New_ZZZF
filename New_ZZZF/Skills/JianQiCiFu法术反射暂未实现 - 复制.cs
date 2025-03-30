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

namespace New_ZZZF
{
    internal class JianQiCiFu : SkillBase
    {
        public JianQiCiFu()
        {
            SkillID = "JianQiCiFu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0025}JianQiCiFu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            base.Description = new TaleWorlds.Localization.TextObject("{=ZZZF0026}开启后每秒回蓝+10，每秒耐力-10，同时获得元素法术大师被动。" +
                "使用副特技时，消耗魔法而非耐力。被反射法术伤害时，不会受到反射伤害。持续时间：60秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {                    
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new JianQiCiFuBuff(60f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            

            return true;
        }

        public class JianQiCiFuBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public JianQiCiFuBuff(float duration, Agent source)
            {
                StateId = "JianQiCiFuBuff";
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
                    agentSkillComponent.ChangeMana(10);
                    agentSkillComponent.ChangeStamina(-10);
                    _timeSinceLastTick -= 1f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {

            }
        }


    }
}
