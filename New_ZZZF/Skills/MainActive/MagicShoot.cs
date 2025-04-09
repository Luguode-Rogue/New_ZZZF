using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal class MagicShoot : SkillBase
    {
        public MagicShoot()
        {
            SkillID = "MagicShoot";      // 必须唯一
            Type = SkillType.SubActive;    // 类型必须明确
            Cooldown = 5f;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0007}MagicShoot");
            Difficulty = null;// new List<SkillDifficulty> {new SkillDifficulty(80,"bow"), new SkillDifficulty(80, "nu") };//技能装备的需求
        }
        public override bool Activate(Agent casterAgent)
        {
            if (Script.AgentShootTowardsLookDirection(casterAgent, 0f))
                return true;
            else
                return false;
        }
        
    }
}
