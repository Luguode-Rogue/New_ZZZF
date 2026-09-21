using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class LeiJi : SkillBase
    {
        public LeiJi()
        {
            SkillID = "LeiJi";
            Type = SPSkillType.Spell;
            Cooldown = 3;
            ResourceCost = 15;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0043}LeiJi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0044}快速施法时轰击视野内敌人最密集的地点；按住Shift时轰击视线指示落点。对3米范围内的敌人造成30点基础电击伤害。消耗法力值：15。冷却时间：3秒。");


        }


        public override bool Activate(Agent agent)
        {
            if (!SpellTargetingSystem.TryResolveAreaTarget(
                    agent, 30f, 3f, out SpellTargetingSystem.Result targeting))
                return FailActivation("视野内没有可用目标。");

            Script.AgentListIFF(
                agent,
                Script.FindAgentsWithinSpellRange(targeting.Position, 3),
                out _,
                out List<Agent> target);

            if (target != null && target.Count > 0)
            {
                foreach (var item in target)
                {
                    if (item == null || !item.IsActive()) continue;
                    Script.CalculateFinalMagicDamage(agent, item, 30, DamageType.ELECTRICITY_DAMAGE);
                    item.SetActionChannel(0, ActionIndexCache.Create("act_jump_loop"));
                    item.PlayParticleEffect("fire_burning");
                }
                return true;
            }
            // 指示施法允许玩家预先封锁空地；快速施法则在统一选点阶段已保证有目标。
            return targeting.UsesManualIndicator;
        }
        public static void useToAgent(Agent caster, Agent vimAgent)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(vimAgent.Index, out var ActiveComponents);
            if (ActiveComponents == null || ActiveComponents._beHitCount <= 5)
            {

                if (vimAgent == null || !vimAgent.IsActive()) return;
                Script.CalculateFinalMagicDamage(caster, vimAgent, 30, DamageType.ELECTRICITY_DAMAGE);
                vimAgent.SetActionChannel(0, ActionIndexCache.Create("act_jump_end"));

            }
        }
    }
}
