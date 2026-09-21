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
        // 旧版骑乘操控被动，实际效果仍由 SkillSystemBehavior 中的遗留逻辑执行。
        // 本轮暂缓重构；后续处理时应将每帧 SetInitialFrame 迁移为独立的原生移动逻辑。
        public ShengZhuangWuBu()
        {
            SkillID = "ShengZhuangWuBu";      // 必须唯一
            Type = SPSkillType.Passive_Spell;    // 类型必须明确
            Cooldown = 0;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0045}ShengZhuangWuBu");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0046}被动技能。骑乘时，坐骑朝向会随当前视野方向调整；配合方向键可以改变移动朝向。");
        }
        public override bool Activate(Agent agent)
        {
            return true;
        }

    }
}
