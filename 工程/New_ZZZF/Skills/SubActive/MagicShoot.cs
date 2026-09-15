using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal sealed class MagicShoot : SkillBase
    {
        // 约 10 度瞄准锥；必须已经由原生 AI 锁定并面向目标，不能把技能当自瞄使用。
        private const float MinimumAimDot = 0.985f;

        public MagicShoot()
        {
            SkillID = "MagicShoot";
            Type = SPSkillType.SubActive;
            Cooldown = 5f;
            ResourceCost = 0f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0007}MagicShoot");
            Difficulty = null;
        }

        public override bool CanActivateWhilePerformingAction => true;

        public override bool Activate(Agent casterAgent)
        {
            if (!Script.AgentShootTowardsLookDirection(casterAgent, 0f))
                return FailActivation("没有可用于魔法射击的武器、弹药或有效弹道。");

            PlayReleasePresentation(casterAgent);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;

            Agent target = caster.GetTargetAgent();
            if (target == null || target == caster || !target.IsHuman || target.IsMount ||
                !target.IsActive() || target.Health <= 0f || !caster.IsEnemyOf(target))
                return false;

            EquipmentIndex weaponIndex = caster.GetPrimaryWieldedItemIndex();
            if (weaponIndex == EquipmentIndex.None)
                return false;
            MissionWeapon weapon = caster.Equipment[weaponIndex];
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null || !weapon.CurrentUsageItem.IsRangedWeapon)
                return false;
            Vec3 toTarget = target.GetEyeGlobalPosition() - caster.GetEyeGlobalPosition();
            Vec3 lookDirection = caster.LookDirection;
            if (toTarget.LengthSquared < 0.01f || lookDirection.LengthSquared < 0.01f)
                return false;
            if (Vec3.DotProduct(toTarget.NormalizedCopy(), lookDirection.NormalizedCopy()) < MinimumAimDot)
                return false;

            // 角度合格后才发射射线；AI 冷却期间连本方法都不会进入。
            return RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        /// <summary>
        /// 自定义投射物不会自动进入原生武器输入链，因此在生成成功后按当前武器
        /// 补播对应释放动作和三维武器声。动作只写上半身通道，不影响移动。
        /// </summary>
        internal static void PlayReleasePresentation(Agent caster)
        {
            if (caster == null || !caster.IsActive())
                return;

            EquipmentIndex weaponIndex = caster.GetPrimaryWieldedItemIndex();
            if (weaponIndex == EquipmentIndex.None)
                return;
            MissionWeapon weapon = caster.Equipment[weaponIndex];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (weapon.IsEmpty || usage == null)
                return;

            bool hasShield = !caster.WieldedOffhandWeapon.IsEmpty &&
                             caster.WieldedOffhandWeapon.CurrentUsageItem != null &&
                             caster.WieldedOffhandWeapon.CurrentUsageItem.IsShield;
            string actionName;
            string soundEvent;

            switch (usage.WeaponClass)
            {
                case WeaponClass.Bow:
                    actionName = caster.MountAgent != null
                        ? "act_release_bow_horseback"
                        : "act_release_bow";
                    soundEvent = "event:/mission/combat/missile/foley/bowrelease";
                    break;
                case WeaponClass.Crossbow:
                    actionName = "act_release_crossbow";
                    soundEvent = "event:/mission/combat/missile/foley/crossbowrelease";
                    break;
                case WeaponClass.ThrowingKnife:
                    actionName = hasShield
                        ? "act_release_throwing_knife_with_shield"
                        : "act_release_throwing_knife";
                    soundEvent = "event:/mission/combat/missile/foley/javelinrelease";
                    break;
                case WeaponClass.ThrowingAxe:
                    actionName = hasShield
                        ? "act_release_throwing_axe_with_shield"
                        : "act_release_throwing_axe";
                    soundEvent = "event:/mission/combat/missile/foley/javelinrelease";
                    break;
                case WeaponClass.Stone:
                    actionName = hasShield
                        ? "act_release_stone_with_shield"
                        : "act_release_stone";
                    soundEvent = "event:/mission/combat/missile/foley/javelinrelease";
                    break;
                case WeaponClass.Sling:
                    actionName = hasShield
                        ? "act_release_sling_with_shield"
                        : "act_release_sling";
                    soundEvent = "event:/mission/combat/missile/foley/sling_release";
                    break;
                case WeaponClass.Javelin:
                    actionName = hasShield
                        ? "act_release_javelin_with_shield"
                        : "act_release_javelin";
                    soundEvent = "event:/mission/combat/missile/foley/javelinrelease";
                    break;
                default:
                    // 近战武器会由投射物逻辑改用默认投矛；未知远程武器则沿用
                    // 弩的短促上半身释放动作，避免播放近战攻击判定。
                    actionName = usage.IsRangedWeapon
                        ? "act_release_crossbow"
                        : (hasShield ? "act_release_javelin_with_shield" : "act_release_javelin");
                    soundEvent = usage.IsRangedWeapon
                        ? "event:/mission/combat/missile/foley/crossbowrelease"
                        : "event:/mission/combat/missile/foley/javelinrelease";
                    break;
            }

            caster.SetActionChannel(1, ActionIndexCache.Create(actionName));
            SoundManager.StartOneShotEvent(soundEvent, caster.Position);
        }

    }
}
