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
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    internal class JianRenBuQu : SkillBase
    {
        public JianRenBuQu()
        {
            SkillID = "JianRenBuQu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0060}JianRenBuQu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0061}群体状态，使自己与友军受到的普攻伤害、物理伤害、爆炸伤害降低为1。消耗耐力：60。持续时间：45秒。冷却时间：30秒。");
        }
        public override bool Activate(Agent agent)
        {
            List<Agent> values= Script.GetTargetedInRange(agent, agent.GetEyeGlobalPosition(),50, true);
            if (values!=null&&values.Count>0)
            {
                foreach (var item in values)
                {
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff> { new JianRenBuQuuBuff(60f, agent), }; // 新实例
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

        public class JianRenBuQuuBuff : AgentBuff
        {
            private int _timeCount = 0;
            private float _timeSinceLastTick;
            public JianRenBuQuuBuff(float duration, Agent source)
            {
                StateId = "JianRenBuQuuBuff";
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
                    _timeSinceLastTick -= 1f; // 重置计时器
                    
                }
            }

            public override void OnRemove(Agent agent)
            {

            }
        }
    }
}
