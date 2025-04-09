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
    internal class DaoShan : SkillBase
    {
        public DaoShan()
        {
            SkillID = "DaoShan";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 3f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0010}DaoShan");
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
            Script.AgentListIFF(agent, Mission.Current.Agents, out var friendAgent, out var foeAgent);
            foreach (var TAgent in foeAgent)
            {

                if (Script.AgentShotAgent(agent, TAgent) != 0)
                {
                    return true;
                }

            }
            return false;
        }
    }
}
