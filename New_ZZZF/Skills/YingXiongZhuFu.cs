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

namespace New_ZZZF
{
    internal class YingXiongZhuFu : SkillBase
    {
        public YingXiongZhuFu()
        {
            SkillID = "YingXiongZhuFu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0035}YingXiongZhuFu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            base.Description = new TaleWorlds.Localization.TextObject("{=ZZZF0036}群体状态，使用后附近士兵获得祝福状态，回复全部血量并获得一次死而复生的机会。提高100%造成的伤害，每秒增加2点耐力。消耗耐力：60。持续时间：60秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            List<Agent> values= Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(),50, true);
            if (values!=null&&values.Count>0)
            {
                foreach (var item in values)
                {
                    item.PlayParticleEffect("fire_burning");
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff> { new YingXiongZhuFuBuff(60f, agent), }; // 新实例
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

        public class YingXiongZhuFuBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public YingXiongZhuFuBuff(float duration, Agent source)
            {
                StateId = "YingXiongZhuFuBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
                agent.Health=agentSkillComponent.MaxHP;
                agentSkillComponent._lifeResurgenceCount += 1;
                agentSkillComponent._shieldStrength += agentSkillComponent.MaxHP;
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
                        agentSkillComponent.ChangeStamina(2);
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
