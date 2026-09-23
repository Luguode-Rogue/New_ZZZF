using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using New_ZZZF.Systems;

namespace New_ZZZF.Skills//（法术）
{
    public class HuHuanFengBao : SkillBase
    {
        public HuHuanFengBao()
        {
            SkillID = "HuHuanFengBao";
            Type = SPSkillType.MainActive;
            Cooldown = 90;
            ResourceCost = 75;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0047}HuHuanFengBao");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0048}召唤一场闪电风暴，对每一个敌人降下落雷。消耗耐力：75。冷却时间：90秒。");


        }


        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive() || Mission.Current == null)
                return FailActivation("施法者或当前任务不可用。");

            // 每名敌人只受一次落雷；限制同时存在的表现实体，伤害仍覆盖全部敌人。
            List<Agent> enemies = new List<Agent>();
            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (candidate != null && candidate.IsActive() && candidate.IsHuman &&
                    candidate != agent && agent.IsEnemyOf(candidate))
                    enemies.Add(candidate);
            }
            int visualCount = 0;
            float spellPowerCoefficient = MagicDamageSystem.GetSpellPowerCoefficient(agent);
            foreach (Agent foe in enemies)
            {
                if (LeiJi.StrikeSingleTarget(
                        agent, foe, spellPowerCoefficient, visualCount < 24) && visualCount < 24)
                    visualCount++;
            }
            return true;
        }
       
    }
}
