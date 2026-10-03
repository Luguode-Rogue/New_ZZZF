using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal sealed class MagicShoot : SkillBase
    {


        public MagicShoot()
        {
            SkillID = "MagicShoot";
            Type = SPSkillType.SubActive;
            Cooldown = 5f;
            ResourceCost = 15f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0007}MagicShoot");
            Difficulty = null;
            Description = new TaleWorlds.Localization.TextObject("{=MagicShoot_DESC}立即沿视线追加一次远程射击，不扣弹药数量。NPC在远程攻击准备时向敌人当前头部射击，不预判移动。消耗耐力15，冷却5秒。");
        }

        public override bool CanActivateWhilePerformingAction => true;

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Equipment == null)
                return FailActivation("施法者不可用。");
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None) return FailActivation("需要手持武器。");
            MissionWeapon weapon = caster.Equipment[slot];
            if (weapon.IsEmpty || weapon.CurrentUsageItem == null) return FailActivation("武器不可用。");
            if (!weapon.CurrentUsageItem.IsRangedWeapon) {
                // 保留近战默认投矛分支，只用恒假条件屏蔽。
                if (1 == 0) {
                    PlayReleasePresentation(caster);
                    return Script.AgentShootTowardsLookDirection(caster, 0f);
                }
                return FailActivation("近战武器射击分支暂时关闭。");
            }
            if (!Script.TryGetActualAmmoWeapon(caster, weapon, out MissionWeapon ammo))
                return FailActivation("没有可用弹药。");
            float speed = weapon.GetModifiedMissileSpeedForCurrentUsage();
            if (SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(caster.Index, out var recorded) && recorded != null)
                foreach (var entry in recorded)
                    if (!entry.Weapon.IsEmpty && entry.Weapon.Item.Id == weapon.Item.Id && entry.MissileSpeed > 0f)
                        speed = entry.MissileSpeed;
            if (speed <= 0f || float.IsNaN(speed) || float.IsInfinity(speed)) return FailActivation("弹速无效。");
            Vec3 start = caster.GetEyeGlobalPosition();
            Vec3 destination;
            bool targetPosition = !caster.IsPlayerControlled;
            if (targetPosition) {
                Agent target = caster.GetTargetAgent();
                if (!IsTarget(caster, target) || !RushMovementMissionLogic.HasLineOfSight(caster, target))
                    return FailActivation("没有有效射击目标。");
                // 当前头部位置，不读取目标速度，不预判飞行时间。
                destination = target.GetEyeGlobalPosition();
                Vec3 solution = Script.CalculateProjectileFiringSolution(start, destination, speed, 9.81f);
                if (!solution.IsValid || solution.LengthSquared < 0.001f) return FailActivation("弹道无法到达目标。");
            } else {
                destination = caster.LookDirection;
                if (!destination.IsValid || destination.LengthSquared < 0.001f) return FailActivation("视线方向无效。");
                destination.Normalize();
            }
            // 触发时直接调用相应 release 动作；创建真实投射物，不扣装备弹药数量。
            PlayReleasePresentation(caster);
            int missile = Script.FireProjectileFromAgentWithWeaponAtPosition(caster, weapon, ammo,
                start, destination, speed, forceTargetPosition: targetPosition);
            return missile > 0 || FailActivation("投射物创建失败。");
        }

        private static bool IsTarget(Agent caster, Agent target) => target != null && target != caster &&
            target.IsHuman && target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target) &&
            !SkillTargetProtection.IsProtected(target);

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.Equipment == null ||
                caster.GetCurrentActionType(1) != Agent.ActionCodeType.ReadyRanged) return false;
            Agent target = caster.GetTargetAgent();
            if (!IsTarget(caster, target)) return false;
            EquipmentIndex slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None) return false;
            MissionWeapon weapon = caster.Equipment[slot];
            return !weapon.IsEmpty && weapon.CurrentUsageItem != null && weapon.CurrentUsageItem.IsRangedWeapon &&
                Script.TryGetActualAmmoWeapon(caster, weapon, out _) && RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        /// <summary>
        /// 自定义投射物不会自动进入原生武器输入链，因此在生成成功后按当前武器
        /// 补播对应释放动作和三维武器声。动作只写上半身通道，不影响移动。
        /// </summary>
        internal static void PlayReleasePresentation(Agent caster, bool playWeaponSound = true)
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
            if (playWeaponSound)
                SoundManager.StartOneShotEvent(soundEvent, caster.Position);
        }

    }
}
