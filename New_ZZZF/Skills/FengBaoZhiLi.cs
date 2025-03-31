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
using static New_ZZZF.GuWu;

namespace New_ZZZF
{
    internal class FengBaoZhiLi : SkillBase
    {
        public FengBaoZhiLi()
        {
            SkillID = "FengBaoZhiLi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 60;             // 冷却时间（秒）
            ResourceCost = 60f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0027}FengBaoZhiLi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0028}群体状态，使用后附近士兵获得风暴之力状态，提升200%射击精度与远程伤害，并有概率额外附加50伤害。消耗耐力：60。持续时间：60秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            List<Agent> values = Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(), 50, true);
            if (values != null && values.Count > 0)
            {
                foreach (var item in values)
                {
                    item.PlayParticleEffect("fire_burning");
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff> { new FengBaoZhiLiBuff(60f, agent), }; // 新实例
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

        public class FengBaoZhiLiBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public FengBaoZhiLiBuff(float duration, Agent source)
            {
                StateId = "FengBaoZhiLiBuff";
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
