using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

using New_ZZZF.Systems;
using SandBox.Objects.Usables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Engine.GameEntity;

namespace New_ZZZF
{
    internal class HouYueSheJi : SkillBase
    {
        public HouYueSheJi()
        {
            SkillID = "HouYueSheJi";      // 必须唯一
            Type = SkillType.SubActive;    // 类型必须明确
            Cooldown = 0;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0076}HouYueSheJi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0077}向后跃起，然后坠向目标区域，造成范围伤害。消耗耐力值：30。持续时间：3秒。冷却时间：10秒。");
        }
        public override bool Activate(Agent agent)
        {
            agent.SetActionChannel(1, ActionIndexCache.Create("act_ready_bow"), true, 999UL);
            agent.SetActionChannel(0, ActionIndexCache.Create("act_climb_ladder"), true, 999UL);
            Vec3 tarPos = agent.Position;
            tarPos.z += 5;
            tarPos -= Script.MultiplyVectorByScalar(agent.LookDirection, 2);
            //agent.TeleportToPosition(tarPos);
            if (!SkillSystemBehavior.WoW_AgentRushPos.ContainsKey(agent.Index))
            {
                SkillSystemBehavior.WoW_AgentRushPos.Add(agent.Index, tarPos);
            }
            else
            { return false; }

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new HouYueSheJiBuff(1.3f, agent), new RushToPosBuff(1.3f, 0f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;

        }
        public override bool CheckCondition(Agent caster)
        {
            if (caster.GetTargetAgent() == null) return false;
            if (!Script.CanSeeAgent(caster, caster.GetTargetAgent()))
            { return false; }

            if (caster.HasRangedWeapon(true)) 
            { return true; }
            return false;
        }
        public class HouYueSheJiBuff : AgentBuff
        {
            public HouYueSheJiBuff(float duration, Agent source)
            {
                StateId = "HouYueSheJiBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {//附加实体，并且设定后跃的目标地点。游戏实体添加进移动实体的字典中，让missionTick里去调整实体的位置



            }

            public override void OnUpdate(Agent agent, float dt)
            {//不是很重要的区域，在更新中，可以让玩家选择最后砸向的区域，可以把施法指示器拿过来
             //在这里更新一个玩家的旋转，时刻朝向摄像机




            }

            public override void OnRemove(Agent agent)
            {//差一点的表达效果：直接结束时瞬移，造成伤害
                //好一点的表达效果：增加一段前移，然后再造成伤害。
                agent.SetActionChannel(0, ActionIndexCache.Create("act_none"), true, 999UL);
                agent.SetActionChannel(1, ActionIndexCache.Create("act_none"), true, 999UL);
                Script.AimShoot(agent);
            }
        }
    }

}
