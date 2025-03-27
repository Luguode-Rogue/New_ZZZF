using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class TianQi : SkillBase
    {
        public TianQi()
        {
            SkillID = "TianQi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0019}TianQi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            base.Description = new TaleWorlds.Localization.TextObject("{=ZZZF0020}开启后回满血量，并获得近乎无敌的状态，免疫物理与魔法伤害，并获得100%的伤害提升。濒死时自动无消耗触发，但持续时间减半，冷却时间加倍。消耗耐力：50。持续时间：30秒。冷却时间：60秒。非英雄使用时，持续与冷却减半");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new TianQiBuff(30f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }

        public class TianQiBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public TianQiBuff(float duration, Agent source)
            {
                StateId = "TianQiBuff";
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

                    ZZZF_SandboxAgentStatCalculateModel zZZF_SandboxAgentStatCalculate = MissionGameModels.Current.AgentStatCalculateModel as ZZZF_SandboxAgentStatCalculateModel;
                    if (zZZF_SandboxAgentStatCalculate != null)
                    {
                        zZZF_SandboxAgentStatCalculate._dt = dt;
                        //MissionGameModels.Current.AgentStatCalculateModel.UpdateAgentStats(agent, agent.AgentDrivenProperties);
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
