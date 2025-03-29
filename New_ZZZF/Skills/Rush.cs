using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
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
                        RushToAgentBuff rushToAgentBuff = new RushToAgentBuff(5f, 0f, agent); // 新实例
                        rushToAgentBuff.TargetPosition = TAgent.Position;
                        // 每次创建新的状态实例
                        List<AgentBuff> newStates = new List<AgentBuff>
                            {
                               rushToAgentBuff,
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
                            RushToAgentBuff rushToAgentBuff = new RushToAgentBuff(5f, 0f, agent); // 新实例
                            rushToAgentBuff.TargetPosition = agents.Position;
                            // 每次创建新的状态实例
                            List<AgentBuff> newStates = new List<AgentBuff>
                            {
                               rushToAgentBuff,
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
        public Vec3 TargetPosition { get; set; }
        private float _timeSinceLastTick;
        public RushToAgentBuff(float duration, float dps, Agent source)
        {
            StateId = "RushToAgentBuff";
            Duration = duration;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
            agent.SetTargetPosition(TargetPosition.AsVec2);
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
            agent.ClearTargetFrame();
            SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var result);
            if (result != null)
            {
                if (result.StateContainer.HasState("暗影步增伤"))
                {
                    agent.TeleportToPosition(this.TargetAgent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(this.TargetAgent.LookDirection, -2f));
                    MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                    MissionMainAgentController missionMainAgentController = missionScreen.Mission.GetMissionBehavior<MissionMainAgentController>();

                    // 获取 LockedAgent 属性的信息
                    PropertyInfo lockedAgentProperty = typeof(MissionMainAgentController).GetProperty("LockedAgent", BindingFlags.Public | BindingFlags.Instance);
                    if (lockedAgentProperty == null)
                    {
                        throw new Exception("LockedAgent 属性未找到");
                    }

                    // 获取 LockedAgent 属性的私有 setter 方法
                    MethodInfo setMethod = lockedAgentProperty.GetSetMethod(nonPublic: true);
                    if (setMethod == null)
                    {
                        throw new Exception("LockedAgent 的私有 setter 方法未找到");
                    }

                    // 调用私有 setter 方法
                    setMethod.Invoke(missionMainAgentController, new object[] { TargetAgent });

                }
            }
        }
    }
    }

