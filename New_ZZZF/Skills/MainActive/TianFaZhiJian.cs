using New_ZZZF.Systems;
using SandBox.Objects.Usables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem.Extensions;
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
    internal class TianFaZhiJian : SkillBase
    {
        public TianFaZhiJian()
        {
            SkillID = "TianFaZhiJian";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 3;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0074}TianFaZhiJian");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0075}向后跃起，然后坠向目标区域，造成范围伤害。消耗耐力值：30。持续时间：3秒。冷却时间：10秒。");
        }
        public override bool Activate(Agent agent)
        {
            agent.SetActionChannel(0, ActionIndexCache.Create("act_ready_overswing_spear"), true, 512UL); //Name = "act_ready_overswing_spear"
            agent.SetActionChannel(1, ActionIndexCache.Create("act_jump_forward"), true); //Name = "act_climb_ladder"
            Vec3 tarPos = agent.Position;
            tarPos.z += 15;
            tarPos -= Script.MultiplyVectorByScalar(agent.LookDirection, 15);
            if (!SkillSystemBehavior.WoW_AgentRushPos.ContainsKey(agent.Index))
            {
                SkillSystemBehavior.WoW_AgentRushPos.Add(agent.Index, tarPos);
            }
            else
            { return false; }
            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new TianFaZhiJianBuff(1.5f, agent), new RushToPosBuff(1.5f, 7f, agent), }; // 新实例
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

            List<Agent> agents = new List<Agent>();
            if ((caster.Position - caster.GetTargetAgent().Position).Length < 30 && Script.FindTarAgents(caster, 5, out agents))
            {
                if (agents.Count > 0)
                {
                    AgentSkillComponent agentSkillComponent = Script.GetActiveComponents(caster);
                    if (agentSkillComponent != null && agentSkillComponent.StateContainer.HasState("TianFaZhiJianBuff"))
                    {
                        TianFaZhiJianBuff tianFaZhiJianBuff = agentSkillComponent.StateContainer.GetState("TianFaZhiJianBuff") as TianFaZhiJianBuff;
                        tianFaZhiJianBuff.tarAgents = agents;
                    }
                    return true;
                }
            }
            else
                return false;
            return base.CheckCondition(caster);
        }

        public class TianFaZhiJianBuff : AgentBuff
        {
            public List<Agent> tarAgents = new List<Agent>();
            public TianFaZhiJianBuff(float duration, Agent source)
            {
                StateId = "TianFaZhiJianBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {//附加实体，并且设定后跃的目标地点。游戏实体添加进移动实体的字典中，让missionTick里去调整实体的位置





            }

            public override void OnUpdate(Agent agent, float dt)
            {//不是很重要的区域，在更新中，可以让玩家选择最后砸向的区域，可以把施法指示器拿过来
             //在这里更新一个玩家的旋转，时刻朝向摄像机

                MatrixFrame frame = Mission.Current.GetCameraFrame();
                agent.SetInitialFrame(agent.Position, frame.rotation.f.AsVec2);
                List<Agent> agents = new List<Agent>();
                if (Script.FindTarAgents(agent, 5, out agents))
                {
                    if (agents.Count > 0)
                        tarAgents = agents;
                }


            }

            public override void OnRemove(Agent agent)
            {//差一点的表达效果：直接结束时瞬移，造成伤害
             //好一点的表达效果：增加一段前移，然后再造成伤害。
             // agent.SetActionChannel(0, ActionIndexCache.Create("act_none"), true, 999UL); 
                agent.SetActionChannel(0, ActionIndexCache.Create("act_none"), true, 512UL);
                agent.SetActionChannel(1, ActionIndexCache.Create("act_quick_release_overswing_spear_left_stance"), true);//Name = "act_quick_release_overswing_spear_left_stance"
                foreach (var item in tarAgents)
                {
                    Script.CalculateFinalMagicDamage(agent, item, 50, DamageType.None);
                    Vec3 vec3 = item.Position;
                    vec3 -= Script.MultiplyVectorByScalar(agent.LookDirection, 1);
                    agent.TeleportToPosition(vec3);
                    item.PlayParticleEffect("fire_burning");
                }

            }
        }
    }

}
