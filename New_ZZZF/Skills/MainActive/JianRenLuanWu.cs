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
    internal class JianRenLuanWu : SkillBase
    {
        public JianRenLuanWu()
        {
            SkillID = "JianRenLuanWu";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0068}JianRenLuanWu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0069}不断地挥动手中的武器，持续对面前的敌人造成武器伤害。消耗耐力值：60。持续时间：12秒。冷却时间：40秒。");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new JianRenLuanWuBuff(12f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }

        public class JianRenLuanWuBuff : AgentBuff
        {
            public BaseZhanJi baseZhanJi = new BaseZhanJi();
            private float _timeSinceLastTick;
            public JianRenLuanWuBuff(float duration, Agent source)
            {
                StateId = "JianRenLuanWuBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                SkillFactory._skillRegistry.TryGetValue("BaseZhanJi", out var skillBase);
                baseZhanJi = skillBase as BaseZhanJi;
            }

            public override void OnUpdate(Agent agent, float dt)
            {

                // 累积时间
                _timeSinceLastTick += dt;

                //每秒刷一次状态
                if (_timeSinceLastTick >= 0.3f)
                {
                    agent.SetActionChannel(0, ActionIndexCache.Create("act_release_slash_horseback_right"), false, 272UL, 0, 1, -0.2f, 0.4f, 0.25f);
                    baseZhanJi.Activate(SourceAgent);
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
