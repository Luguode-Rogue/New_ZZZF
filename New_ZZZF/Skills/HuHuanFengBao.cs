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

namespace New_ZZZF.Skills//（法术）
{
    public class HuHuanFengBao : SkillBase
    {
        public HuHuanFengBao()
        {
            SkillID = "HuHuanFengBao";
            Type = SkillType.MainActive;
            Cooldown = 90;
            ResourceCost = 75;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0047}HuHuanFengBao");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0048}召唤一场闪电风暴，对每一个敌人降下落雷。消耗耐力：75。冷却时间：90秒。");


        }


        public override bool Activate(Agent agent)
        {

            Script.AgentListIFF(agent,Mission.Current.Agents,out var friendAgent,out var foeAgent);
            foreach (var foe in foeAgent) 
            {
                List<Agent> list = Script.FindAgentsWithinSpellRange(foe.Position, 3);
                Script.AgentListIFF(agent, list, out var FriendAgent, out var FoeAgent);
                foreach (var item in FoeAgent)
                {

                    LeiJi.useToAgent(agent, foe);
                }
            }
                return true; 


        }
       
    }
}
