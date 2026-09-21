using New_ZZZF.Skills;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 灵马哨笛：在战场中生成角色装备的坐骑并走原版上马流程；
    /// 骑乘时再次使用则走原版下马流程，完成后让坐骑渐隐。
    /// </summary>
    internal sealed class lingmashaodi : SkillBase
    {
        public lingmashaodi()
        {
            SkillID = "lingmashaodi";
            Type = SPSkillType.Spell;
            Cooldown = 3f;
            ResourceCost = 0f;
            Text = new TextObject("{=ZZZF0009}灵马哨笛");
            Description = new TextObject(
                "{=ZZZF0009_DESC}吹响哨笛，召来角色当前装备的坐骑并正常上马。骑乘时再次使用会正常下马，随后坐骑渐隐。上下马过程中不能重复吹哨。消耗法力值：0。冷却时间：3秒。");
            Difficulty = null;
        }

        // 必须允许骑乘状态下发动；具体的上下马过渡锁由任务管理器负责。
        public override bool CanActivateWhilePerformingAction => true;

        public override bool Activate(Agent agent)
        {
            SpiritSteedMissionLogic manager = SpiritSteedMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载灵马管理器。");

            if (!manager.TryToggleMount(agent, out string failureReason))
                return FailActivation(failureReason);

            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.IsMount)
                return false;

            SpiritSteedMissionLogic manager = SpiritSteedMissionLogic.GetForCurrentMission();
            if (manager == null || manager.IsTransitioning(caster))
                return false;

            // AI 只用哨笛上马，避免每次冷却结束后反复上下马。
            return caster.IsPlayerControlled || caster.MountAgent == null;
        }
    }
}
