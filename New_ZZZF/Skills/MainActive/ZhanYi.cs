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
    internal class ZhanYi : SkillBase
    {
        public ZhanYi()
        {
            SkillID = "ZhanYi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0015}ZhanYi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0016}开启后获得战意状态，每秒回复5耐力，并且击杀敌方单位后，恢复50%自身已损失血量。按当前耐力值增加等量的伤害加成与速度加成。基础持续8秒，每次击杀敌方单位后，持续时间重置为8秒。如果持续时间大于8秒时造成击杀，则持续时间加1秒。消耗耐力：0。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new ZhanYiBuff(8f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }

        public class ZhanYiBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public ZhanYiBuff(float duration, Agent source)
            {
                StateId = "ZhanYiBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (dt == 0f)
                {
                    if (this.Duration <= 8)
                    { this.Duration = 8; }
                    else
                    { this.Duration += 1; }
                }
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
