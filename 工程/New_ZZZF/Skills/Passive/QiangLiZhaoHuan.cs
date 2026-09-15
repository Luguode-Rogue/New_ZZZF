using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;

namespace New_ZZZF.Skills
{
    /// <summary>通用召唤强化：召唤数量乘五，并在新批次成功后遣散旧召唤物。</summary>
    public sealed class QiangLiZhaoHuan : SkillBase, ISummonInvocationModifier
    {
        public const int SummonMultiplier = 5;

        public QiangLiZhaoHuan()
        {
            SkillID = "QiangLiZhaoHuan";
            Type = SPSkillType.Passive;
            Cooldown = 0f;
            ResourceCost = 0f;
            Difficulty = null;
            Text = new TextObject("强力召唤");
            Description = new TextObject(
                "被动技能。触发任意召唤类技能时，召唤数量变为原本的5倍；新一批召唤成功后，遣散该施法者此前仍然存在的召唤物。");
        }

        public override bool Activate(Agent agent)
        {
            // 被动本身不主动施法；效果由召唤管理器的通用修饰管线调用。
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            // 即使以后 AI 调度器开始遍历被动栏，也不能把该修饰器当主动技能释放。
            return false;
        }

        public void ModifySummonInvocation(SummonInvocation invocation)
        {
            if (invocation == null)
                return;

            invocation.Count = checked(invocation.Count * SummonMultiplier);
            invocation.DismissExistingSummons = true;
        }
    }
}
