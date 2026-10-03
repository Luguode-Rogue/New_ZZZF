using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>一次剑刃乱舞式超级横扫，沿用原生物理命中，不创建旧测试投射物。</summary>
    public sealed class BaseZhanJi : SkillBase
    {
        public BaseZhanJi() {
            SkillID = "BaseZhanJi";
            Type = SPSkillType.SubActive;
            Cooldown = 3f;
            ResourceCost = 15f;
            Text = new TextObject("{=BaseZhanJi_NAME}基础斩击");
            Description = new TextObject("{=BaseZhanJi_DESC}发动一次剑刃乱舞式右横扫，命中不中断，必定突破格挡并无限贯穿单位。使用原版武器物理伤害，纯戳刺武器使用戳刺伤害，动作速度180%。消耗耐力15，冷却3秒。");
            Difficulty = null;
        }
        public override bool CanActivateWhilePerformingAction => true;
        public override bool Activate(Agent caster) {
            var logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null) return FailActivation("连续攻击管理器不可用。");
            return logic.TryStartSingleBladeSweep(caster, out string reason) || FailActivation(reason);
        }
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || !JiFengLianZhanMissionLogic.HasBladeDanceWeapon(caster) ||
                JingXia.IsFrightened(caster)) return false;
            var logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null || logic.IsActive(caster)) return false;
            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || target.Health <= 0f || !caster.IsEnemyOf(target)) return false;
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None || caster.Equipment == null) return false;
            var usage = caster.Equipment[slot].CurrentUsageItem;
            if (usage == null) return false;
            float reach = MathF.Max(1.5f, usage.WeaponLength * 0.01f + 0.8f);
            return (target.Position - caster.Position).LengthSquared <= reach * reach &&
                RushMovementMissionLogic.HasLineOfSight(caster, target);
        }
    }
}