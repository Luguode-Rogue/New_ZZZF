using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace New_ZZZF
{
    // 与中文翻译表的 *_Desc / *_Learn 同步维护；fallback 也提供完整说明。
    // 这里只解释已落地的战斗规则，不改变熟练度、面板、伤害或 perk。
    internal static class WeaponMasteryTexts
    {
        internal const string Common = @"共通特性
戳刺撞到盾、武器或墙时不会弹刀；刺中颈部，伤害提高25%。
单手武器除锤外，可在攻击释放的最后30%变招，方向不限；受击硬直时不可，完全打空的斧和挥砍长杆仍受末段收招限制。
";
        internal const string Miss = @"斧、锤和长杆挥砍完全打空时，释放末段无法收招且动作减慢；武器所属精通越高，惩罚越轻。命中身体、盾、武器或墙均不算打空。
";
        internal const string ArmorBreak = @"破甲：实际造成伤害，且减伤前伤害超过受击部位原始护甲时，每次降低该部位“武器阶数＋品质加值”点护甲，最多3层，队友攻击同部位也受益。受伤刷新持续时间，连续10秒未受伤后恢复。品质加值：普通及以下0、精品1、大师2、传奇3。
";
        internal const string Shield = @"举盾负担：投掷斧、标枪命中正在格挡的盾，每次增加10%的举盾耗时，无叠加上限。负担逐渐消退，积累越多，恢复越慢；已举好的盾仍可格挡。
";
        internal const string Knife = @"飞刀衔接：携带飞刀时，取消起手或变招会自动投出一把；AI先投刀再接近战。每次攻击最多一把，消耗弹药。
";
        internal const string Sword = @"灵活衔接，快速出手。
单手剑、双手剑与飞刀共享精通及熟练度加成。
每点熟练额外提高0.2%的挥砍速度、准备速度和操控性。
剑的挥砍被格挡后，自身硬直减半。
";
        internal const string Axe = @"强力劈砍，破盾削甲。
单手斧、双手斧与投掷斧共享精通。
近战斧每点熟练额外增伤0.3%；双手斧对盾伤害翻倍，并能削弱受击部位的护甲。
斧击格挡物时有机会缴械：基础概率10%，每领先1点熟练增加0.1个百分点，每领先1阶增加5个百分点，落后时相应降低；概率限制在5%～60%。
投掷斧切换近战时补偿面板伤害折损；投掷命中盾会增加举盾负担。
";
        internal const string Hammer = @"穿透护甲，重击控制。
单手锤、双手锤与投掷锤共享精通。
近战穿甲＝10＋武器阶数＋品质加值＋士兵阶级。品质加值：普通及以下0、精品1、大师2、传奇3。
被格挡时无额外自身硬直。原有突破判定失败后再次尝试：概率＝10%＋熟练度×0.15%，最高50%；成功突破后至少保留“20%＋熟练度×0.2%”的动量，最高80%。
身体伤害超过30时，击中持械手或持盾手有机会缴械；钝击足部可减速25%，持续20秒，再次触发刷新时间。
投掷锤也能触发部位控制；对坐骑伤害提高50%，对盾伤害减半。
";
        internal const string Spear = @"长距压制，连续追刺。
长杆戳刺、挥砍与标枪均使用矛精通。
长杆每点熟练额外提高0.3%的准备速度。近战戳刺无视低于20的部位原始护甲；刺中手臂或腿，伤害降低25%。
戳刺在护甲结算后，立即造成30%的伤害，随后3秒造成90%，合计120%；延迟伤害按首次结算锁定。
首次戳刺实际扣血，且受击部位原始护甲高于武器戳刺面板时，沿命中方向将徒步目标随枪尖推向本次动作的最远伸展处；墙、地形和人物会阻挡推开。
推开期间，原戳刺动作仍在释放时，每点击一次攻击，可按当前动作进度再次判定伤害。连刺间隔＝0.32÷（1＋熟练度÷200）秒，最低0.12秒；AI每50熟练获得一次尝试，每次有50%概率，动作结束后未用的次数作废。
长杆挥砍每点矛精通额外增伤0.3%，对盾伤害翻倍，并能破甲；完全打空时有末段收招惩罚。以上挥砍效果不用于戳刺。标枪命中盾会增加举盾负担。
";
        internal const string SwordLearn = "用单手剑、双手剑或飞刀攻击敌人，提升剑精通。";
        internal const string AxeLearn = "用单手斧、双手斧或投掷斧攻击敌人，提升斧精通。";
        internal const string HammerLearn = "用单手锤、双手锤或投掷锤攻击敌人，提升锤精通。";
        internal const string SpearLearn = "用单手、双手、低握长杆的戳刺或挥砍，或用标枪攻击敌人，提升矛精通。";

        internal static string Description(string kind)
        {
            switch (kind)
            {
                case "Sword": return Sword + Common + Knife;
                case "Axe": return Axe + ArmorBreak + Miss + Shield + Common + Knife;
                case "Hammer": return Hammer + Miss + Common + Knife;
                case "Spear": return Spear + ArmorBreak + Shield + Common + Knife;
                default: return string.Empty;
            }
        }
    }

    // 覆盖 getter 而不是只改 SkillVM，使其他调用学习文本的界面也得到正确分类。
    [HarmonyPatch(typeof(SkillObject), "get_HowToLearnSkillText")]
    internal static class WeaponMasteryLearningTextPatch
    {
        private static bool Prefix(SkillObject __instance, ref TextObject __result)
        {
            string kind, fallback;
            if (__instance == DefaultSkills.OneHanded) { kind = "Sword"; fallback = WeaponMasteryTexts.SwordLearn; }
            else if (__instance == DefaultSkills.TwoHanded) { kind = "Axe"; fallback = WeaponMasteryTexts.AxeLearn; }
            else if (__instance == DefaultSkills.Polearm) { kind = "Hammer"; fallback = WeaponMasteryTexts.HammerLearn; }
            else if (__instance == DefaultSkills.Throwing) { kind = "Spear"; fallback = WeaponMasteryTexts.SpearLearn; }
            else return true;
            __result = new TextObject("{=ZZZF_" + kind + "Mastery_Learn}" + fallback);
            return false;
        }
    }
}
