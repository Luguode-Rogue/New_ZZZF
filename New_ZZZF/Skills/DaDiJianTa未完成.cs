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
    internal class DaDiJianTa : SkillBase
    {
        public DaDiJianTa()
        {
            SkillID = "DaDiJianTa";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 3f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0012}DaDiJianTa");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool Activate(Agent agent)
        {
            if (AgentDaoShan(agent))
            { return true; }
             return false;
        }
        public bool AgentDaoShan(Agent agent)
        {
            foreach (var TAgent in Mission.Current.Agents)
            {
                if ((TAgent.IsActive() && !TAgent.IsFriendOf(agent))||!TAgent.IsHuman)
                {
                    if (Script.AgentShotAgent(agent, TAgent) != 0)
                    { 
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
