using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class LeiJi : SkillBase
    {
        public LeiJi()
        {
            SkillID = "LeiJi";
            Type = SkillType.Spell;
            Cooldown = 3;
            ResourceCost = 15;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0043}LeiJi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0044}召唤一道闪电，轰击目标地点，造成施法者等级点伤害。消耗法力值：15。冷却时间：3秒。");


        }


        public override bool Activate(Agent agent)
        {
            List<Agent> target = FindTarget(agent);
            if (target != null && target.Count > 0)
            {
                foreach (var item in target)
                {
                    if (item == null || !item.IsActive()) continue;
                    Script.CalculateFinalMagicDamage(agent, item, 30, DamageType.ELECTRICITY_DAMAGE);
                    item.SetActionChannel(0, ActionIndexCache.Create("act_jump_loop"));
                }
                return true;
            }
            else
                return false;


        }
        private List<Agent> FindTarget(Agent agent)
        {

            Agent tarAgent = Script.FindOptimalConflictPos(agent, Script.AgentLookPos(agent), 30);
            if (tarAgent != null)
            {

                List<Agent> list = Script.FindAgentsWithinSpellRange(tarAgent.Position, 3);
                Script.AgentListIFF(agent, list, out var FriendAgent, out var FoeAgent);
                return FoeAgent;
            }
            else return null;
        }
        public static void useToAgent(Agent caster, Agent vimAgent)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(vimAgent.Index, out var ActiveComponents);
            if (ActiveComponents ==null|| ActiveComponents._beHitCount <= 5)
            {

                if (vimAgent == null || !vimAgent.IsActive()) return;
                Script.CalculateFinalMagicDamage(caster, vimAgent, 30, DamageType.ELECTRICITY_DAMAGE);
                vimAgent.SetActionChannel(0, ActionIndexCache.Create("act_jump_end"));

            }
        }
    }
}
