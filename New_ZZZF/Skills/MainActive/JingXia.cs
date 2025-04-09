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
using static New_ZZZF.ZhanYi;

namespace New_ZZZF
{
    internal class JingXia : SkillBase
    {
        public JingXia()
        {
            SkillID = "JingXia";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0033}JingXia");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0034}群体负面状态，持续影响附近敌方单位。受到惊吓的敌人将不受控制的远离施法者。对英雄单位无影响，施法者等级低于敌方太多时将不生效。消耗耐力：20。持续时间：10秒。冷却时间：20");
        }
        public override bool Activate(Agent agent)
        {
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new JingXiaBuffToSelf(10f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;
        }

        
    }
    public class JingXiaBuffToSelf : AgentBuff
    {
        private float _timeSinceLastTick;
        public JingXiaBuffToSelf(float duration, Agent source)
        {
            StateId = "JingXiaBuffToSelf";
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
                            List<AgentBuff> newStates = new List<AgentBuff> { new JingXiaBuffToEnemy(10f, agent), }; // 新实例
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
    public class JingXiaBuffToEnemy : AgentBuff
    {
        private float _timeSinceLastTick;
        public JingXiaBuffToEnemy(float duration, Agent source)
        {
            StateId = "JingXiaBuffToEnemy";
            Duration = duration;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
            Vec3 vec2 = SourceAgent.Position - agent.Position;
            vec2 = vec2.NormalizedCopy();
            Vec3 vec3 = agent.Position - SourceAgent.Position;
            vec3 = vec3.NormalizedCopy();
            vec3 = Script.MultiplyVectorByScalar(vec3, 10f);
            agent.SetTargetPosition((agent.Position + vec3).AsVec2);
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
                Vec3 vec2 = SourceAgent.Position - agent.Position;
                vec2 = vec2.NormalizedCopy();
                Vec3 vec3 = agent.Position - SourceAgent.Position;
                vec3 = vec3.NormalizedCopy();
                vec3 = Script.MultiplyVectorByScalar(vec3, 10f);
                agent.SetTargetPosition((agent.Position + vec3).AsVec2);
                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            agent.ClearTargetFrame();
        }
    }
}
