using New_ZZZF.Skills;
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
    internal class KuangNuLongXi : SkillBase
    {
        public KuangNuLongXi()
        {
            SkillID = "KuangNuLongXi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0064}KuangNuLongXi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0065}持续喷射火焰，焚烧前方锥形区域的敌人。消耗耐力值：80。持续时间：12秒。冷却时间：20秒。");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new KuangNuLongXiBuff(3f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }

        public class KuangNuLongXiBuff : AgentBuff
        {
            public HongShiZiHuoYan hongShiZiHuoYan =new HongShiZiHuoYan();
            private float _timeSinceLastTick;
            public KuangNuLongXiBuff(float duration, Agent source)
            {
                StateId = "KuangNuLongXiBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                SkillFactory._skillRegistry.TryGetValue("HongShiZiHuoYan", out var skillBase);
                hongShiZiHuoYan = skillBase as HongShiZiHuoYan;
            }

            public override void OnUpdate(Agent agent, float dt)
            {

                // 累积时间
                _timeSinceLastTick += dt;

                //每秒刷一次状态
                if (_timeSinceLastTick >= 0.3f)
                {
                    hongShiZiHuoYan.Activate(SourceAgent);
                    _timeSinceLastTick -= 0.3f; // 重置计时器
                }
            }

            public override void OnRemove(Agent agent)
            {
                agent.UpdateAgentProperties();
            }
        }
    }
}
