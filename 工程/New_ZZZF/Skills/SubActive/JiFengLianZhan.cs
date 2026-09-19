using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 疾风连斩：每次接触后重新进入完整的原生近战攻击状态机。
    /// </summary>
    internal sealed class JiFengLianZhan : SkillBase
    {
        public const string Id = "JiFengLianZhan";

        public JiFengLianZhan()
        {
            SkillID = Id;
            Type = SPSkillType.SubActive;
            Cooldown = 8f;
            ResourceCost = 25f;
            Text = new TextObject("{=ZZZF_JI_FENG_LIAN_ZHAN_NAME}疾风连斩");
            Description = new TextObject(
                "{=ZZZF_JI_FENG_LIAN_ZHAN_DESC}立即发动原生近战攻击；具有挥砍伤害的武器只会随机左右挥砍，"
                + "纯突刺武器则使用突刺。命中、盾挡、武器格挡或招架后自动衔接下一击。"
                + "基础为10段；第10段及之后每完成5段并继续命中时，额外消耗5点耐力追加5段，没有总段数上限。"
                + "每段攻击速度提高10%，最高180%。技能持续期间攻击必定突破格挡，"
                + "且突破格挡不会损失攻击动量。消耗耐力：25。冷却时间：8秒。");
        }

        public override bool Activate(Agent casterAgent)
        {
            JiFengLianZhanMissionLogic logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null)
                return FailActivation("当前任务未加载疾风连斩管理器，请确认技能系统已启用。");

            if (!logic.TryStart(casterAgent, out string reason))
                return FailActivation(reason ?? "无法启动疾风连斩。");

            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.IsPlayerControlled)
                return false;

            JiFengLianZhanMissionLogic logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null || logic.IsActive(caster) ||
                !JiFengLianZhanMissionLogic.HasUsableAttackDirection(caster))
                return false;

            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || !caster.IsEnemyOf(target))
                return false;

            MissionWeapon weapon = caster.WieldedWeapon;
            float weaponReach = weapon.CurrentUsageItem == null
                ? 1.5f
                : MathF.Max(1.5f, weapon.CurrentUsageItem.WeaponLength * 0.01f + 0.8f);
            return (target.Position - caster.Position).LengthSquared <= weaponReach * weaponReach;
        }
    }
}
