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
    internal class KongNueCiFu : SkillBase
    {
        public KongNueCiFu()
        {
            SkillID = "KongNueCiFu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0037}KongNueCiFu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            base.Description = new TaleWorlds.Localization.TextObject("{=ZZZF0038}开启后，攻击必定突破格挡，" +
                "并且可以贯穿多人，移除贯穿减伤。耐力回复+10，魔力回复-10，提升200%伤害与近战攻速/移速。" +
                "造成击杀时，回复已损失血量的50%，且额外获得5耐力，并延长持续时间1秒。" +
                "累计击杀888等级的敌人后，获得1次复活。消耗耐力：50。持续时间：30秒。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new KongNueCiFuBuff(3f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }

        public class KongNueCiFuBuff : AgentBuff
        {
            public int carnageRankCounter = 0;
            private float _timeSinceLastTick;
            public KongNueCiFuBuff(float duration, Agent source)
            {
                StateId = "KongNueCiFuBuff";
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
                    if (this.Duration <= 30)
                    { this.Duration += 2; }
                    else
                    { this.Duration += 1; }
                }
                // 累积时间
                _timeSinceLastTick += dt;

                //每秒刷一次状态
                if (_timeSinceLastTick >= 1f)
                {
                    SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
                    ZZZF_SandboxAgentStatCalculateModel zZZF_SandboxAgentStatCalculate = MissionGameModels.Current.AgentStatCalculateModel as ZZZF_SandboxAgentStatCalculateModel;
                    if (zZZF_SandboxAgentStatCalculate != null)
                    {
                        agentSkillComponent.ChangeStamina(10);
                        agentSkillComponent.ChangeMana(-10);
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
