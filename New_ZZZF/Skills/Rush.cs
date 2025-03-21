using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal class Rush : SkillBase
    {
        public Rush()
        {
            SkillID = "Rush";      // 必须唯一
            Type = SkillType.SubActive;    // 类型必须明确
            Cooldown = 10f;             // 冷却时间（秒）
            ResourceCost = 10f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0005}Rush");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool Activate(Agent agent)
        {

            Agent TAgent = null;
            if (agent == Agent.Main)
            {
                TAgent = Script.FindTargetedLockableAgent(agent);
                if (TAgent != null)
                {
                    if (!SkillSystemBehavior.WoW_AgentRushAgent.ContainsKey(agent.Index))
                    {
                        SkillSystemBehavior.WoW_AgentRushAgent.Add(agent.Index, TAgent);
                        // 每次创建新的状态实例
                        List<AgentBuff> newStates = new List<AgentBuff>
                        {
                            new RushToAgentBuff(5f, 0f, agent), // 新实例
                        };
                        foreach (var state in newStates)
                        {
                            state.TargetAgent = agent;
                            agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                        }
                        return true;
                    }
                }
            }
            if (TAgent == null || agent != Agent.Main)
                foreach (Agent agents in Mission.Current.Agents)
                {
                    if (!agents.IsFriendOf(agent) && agents.IsActive())
                    {
                        if (!SkillSystemBehavior.WoW_AgentRushAgent.ContainsKey(agent.Index))
                        {
                            SkillSystemBehavior.WoW_AgentRushAgent.Add(agent.Index, agents);
                            // 每次创建新的状态实例
                            List<AgentBuff> newStates = new List<AgentBuff>
                            {
                                new RushToAgentBuff(5f, 0f, agent), // 新实例
                            };
                            foreach (var state in newStates)
                            {
                                state.TargetAgent = agent;
                                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                            }
                            return true;
                        }
                        else return false;
                    }
                }
            return false;
        }
    }

    public class RushToAgentBuff : AgentBuff
    {
        private float _damagePerSecond;
        private float _timeSinceLastTick;
        public RushToAgentBuff(float duration, float dps, Agent source)
        {
            StateId = "RushToAgentBuff";
            Duration = duration;
            _damagePerSecond = 0;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            // 累积伤害时间
            _timeSinceLastTick += dt;

            // 每秒触发一次伤害
            if (_timeSinceLastTick >= 1f)
            {
                InformationManager.DisplayMessage(new InformationMessage("RushToAgentBuff"));

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {

        }
    }
}
