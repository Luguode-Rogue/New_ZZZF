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
    internal class ShengZhuangWuBu : SkillBase
    {
        public ShengZhuangWuBu()
        {
            SkillID = "ShengZhuangWuBu";      // 必须唯一
            Type = SkillType.Passive_Spell;    // 类型必须明确
            Cooldown = 0;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0045}ShengZhuangWuBu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0046}低速时，坐骑自动转向为当前视线方向，并且解锁横向移动能力");
        }
        public override bool Activate(Agent agent)
        {
            return true;
        }

    }
}
