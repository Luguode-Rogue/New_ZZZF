using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF.Skills
{
    /// <summary>标记剑气登记的物理打击；暴击逻辑位于 SkillSystemBehavior.OnAgentHit。</summary>
    internal static class JianQiHitContext
    {
        [ThreadStatic] private static Agent _attacker;
        [ThreadStatic] private static Agent _victim;
        [ThreadStatic] private static bool _criticalFollowup;
        [ThreadStatic] private static bool _primaryConsumed;

        internal static bool TryConsumePrimaryHit(Agent attacker, Agent victim)
        {
            if (_criticalFollowup || _primaryConsumed ||
                !ReferenceEquals(_attacker, attacker) || !ReferenceEquals(_victim, victim))
                return false;
            _primaryConsumed = true;
            return true;
        }

        internal static void Begin(Agent attacker, Agent victim)
        {
            _attacker = attacker;
            _victim = victim;
            _primaryConsumed = false;
        }

        internal static void End()
        {
            _attacker = null;
            _victim = null;
            _criticalFollowup = false;
            _primaryConsumed = false;
        }

        internal static void BeginCriticalFollowup() { _criticalFollowup = true; }
        internal static void EndCriticalFollowup() { _criticalFollowup = false; }

        internal static void ReportCombatLog(Agent attacker, Agent victim, int damage,
            int absorbedByArmor, DamageTypes damageType)
        {
            Mission mission = Mission.Current;
            if (mission == null || attacker == null || victim == null || damage <= 0)
                return;
            bool attackerHasRider = attacker.RiderAgent != null;
            bool victimHasRider = victim.RiderAgent != null;
            CombatLogData log = new CombatLogData(
                attacker == victim, attacker.IsHuman, attacker.IsMine,
                attackerHasRider, attackerHasRider && attacker.RiderAgent.IsMine,
                attacker.IsMount, victim.IsHuman, victim.IsMine,
                victim.Health - damage < 1f, victimHasRider,
                victimHasRider && victim.RiderAgent.IsMine, victim.IsMount,
                null, victim.RiderAgent == attacker, false, false, 0f)
            {
                InflictedDamage = damage,
                AbsorbedDamage = absorbedByArmor,
                DamageType = damageType,
                BodyPartHit = BoneBodyPartType.Abdomen
            };
            log.SetVictimAgent(victim);
            mission.AddCombatLogSafe(attacker, victim, log);
        }
    }

    internal sealed class JianQi : SkillBase
    {
        private const float Range = 15f;
        private const float Speed = 60f;
        private const float BaseRadius = 2f;
        private const float RadiusPerBonusHit = 0.25f;
        private const float DistanceBetweenHits = 1f;
        private const float AiSingleTargetMinimumRange = 5f;
        private const float AiRangedPressureDistance = 8f;
        private readonly MBList<Agent> _aiCandidates = new MBList<Agent>();

        public JianQi()
        {
            SkillID = "JianQi";
            Type = SPSkillType.MainActive;
            Cooldown = 5f;
            ResourceCost = 40f;
            Text = new TextObject("{=12345676}JianQi");
            Description = new TextObject("朝视野方向发出穿透敌人的15米剑气。每次命中按原版武器伤害、增益和护甲计算后取五分之一；武器长度与对应武器精通扩大伤害判定范围；范围内可多次命中，不设置单体命中次数上限。冷却5秒，消耗40体力。");
            Difficulty = null;
        }

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = default;
            if (index != 0 || !TryGetWeapon(caster, out _, out MissionWeapon weapon))
                return false;
            int bonus = GetBonusHits(caster, weapon);
            area = new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.Sphere,
                Radius = BaseRadius + RadiusPerBonusHit * bonus
            };
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                !TryGetWeapon(caster, out _, out MissionWeapon weapon) ||
                Mission.Current == null)
                return false;
            Vec3 direction = caster.LookDirection;
            if (direction.LengthSquared < 0.001f)
                return false;
            direction.Normalize();
            Vec3 start = caster.GetEyeGlobalPosition();
            Vec3 end = start + direction * Range;
            float radius = BaseRadius + RadiusPerBonusHit * GetBonusHits(caster, weapon);
            Agent primaryTarget = caster.GetTargetAgent();
            DamageTypes damageType = weapon.CurrentUsageItem.SwingDamage > 0
                ? weapon.CurrentUsageItem.SwingDamageType
                : weapon.CurrentUsageItem.ThrustDamageType;
            if (damageType == DamageTypes.Invalid)
                damageType = DamageTypes.Blunt;

            _aiCandidates.Clear();
            Vec3 middle = (start + end) * 0.5f;
            float searchRadius = Range * 0.5f + radius + 1f;
            if (caster.Team != null)
                Mission.Current.GetNearbyEnemyAgents(middle.AsVec2,
                    searchRadius, caster.Team, _aiCandidates);
            else
                Mission.Current.GetNearbyAgents(middle.AsVec2,
                    searchRadius, _aiCandidates);

            int hittableEnemies = 0;
            float totalExpectedDamage = 0f;
            bool usefulSingleTarget = false;
            float baseDamage = weapon.CurrentUsageItem.SwingDamage > 0 ? weapon.GetModifiedSwingDamageForCurrentUsage() : weapon.GetModifiedThrustDamageForCurrentUsage();
            foreach (Agent target in _aiCandidates)
            {
                if (target == null || !target.IsActive() || !target.IsHuman ||
                    target.Health <= 0f || (!caster.IsEnemyOf(target) || SkillTargetProtection.IsProtected(target)) ||
                    !TryGetInsideInterval(start, end, target.GetEyeGlobalPosition(),
                        radius, out float entry, out float exit) ||
                    !RushMovementMissionLogic.HasLineOfSight(caster, target))
                    continue;

                int expectedHits = 1 + (int)((Range * (exit - entry) + 0.0001f) / DistanceBetweenHits);
                float perHit = MissionGameModels.Current.StrikeMagnitudeModel.ComputeRawDamage(
                    damageType, baseDamage,
                    target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Abdomen),
                    target.Monster.AbsorbedDamageRatio) / 5f;
                if (perHit < 1f)
                    continue;
                float expectedDamage = perHit * expectedHits;
                hittableEnemies++;
                totalExpectedDamage += expectedDamage;

                if (target == primaryTarget)
                {
                    float distance = (target.GetEyeGlobalPosition() - start).Length;
                    usefulSingleTarget = distance >= AiSingleTargetMinimumRange &&
                        (expectedDamage >= target.Health * 0.8f ||
                         (distance >= AiRangedPressureDistance && expectedHits >= 2 &&
                          expectedDamage >= baseDamage * 0.75f));
                }
            }

            // 40 体力优先换取穿透群体收益；单目标只在远距压制或有望收割时使用。
            return hittableEnemies >= 2 && totalExpectedDamage >= baseDamage ||
                   usefulSingleTarget;
        }

        public override bool Activate(Agent caster)
        {
            if (!ArcWeaponNativeHit.TryCapture(caster, out var cast))
                return FailActivation("需要手持近战武器。");
            ArcWaveMissionLogic manager = ArcWaveMissionLogic.Current;
            if (manager == null) return FailActivation("当前任务未加载弧形剑气管理器。");
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = caster.IsMainAgent && screen != null ? screen.CombatCamera.Direction : caster.LookDirection;
            if (direction.LengthSquared < 0.001f) direction = caster.LookDirection;
            float radius = BaseRadius + RadiusPerBonusHit * GetBonusHits(caster, cast.Weapon);
            if (!manager.TrySpawn(cast, caster.GetEyeGlobalPosition() - Vec3.Up * 0.45f,
                direction, false, radius, Speed, Range, out string reason)) return FailActivation(reason);
            PlayReleaseAction(caster, cast.Weapon.CurrentUsageItem, cast.Strike);
            return true;
        }

        private static void PlayReleaseAction(Agent caster, WeaponComponentData usage,
            StrikeType strikeType)
        {
            bool twoHanded = usage.IsTwoHanded;
            bool mounted = caster.HasMount;
            string actionName;
            if (strikeType == StrikeType.Thrust)
                actionName = twoHanded
                    ? (mounted ? "act_release_thrust_2h_horseback" : "act_release_thrust_2h")
                    : (mounted ? "act_release_thrust_1h_horseback" : "act_release_thrust_1h");
            else
                actionName = twoHanded
                    ? (mounted ? "act_release_slash_2h_horseback_left" : "act_release_slashleft_2h")
                    : (mounted ? "act_release_slash_horseback_left" : "act_release_slashleft_1h");
            caster.SetActionChannel(1, ActionIndexCache.Create(actionName), true);
        }

        private static bool TryGetWeapon(Agent caster, out EquipmentIndex slot, out MissionWeapon weapon)
        {
            slot = EquipmentIndex.None;
            weapon = MissionWeapon.Invalid;
            if (!ArcWeaponNativeHit.TryCapture(caster, out var cast)) return false;
            slot = cast.Slot; weapon = cast.Weapon;
            return true;
        }

        private static int GetBonusHits(Agent caster, MissionWeapon weapon)
        {
            WeaponComponentData usage = weapon.CurrentUsageItem;
            // GetRealWeaponLength 已经以米为单位，3 米正好取得 3 次长度加成。
            int lengthBonus = Math.Min(3, Math.Max(0,
                (int)(usage.GetRealWeaponLength() + 0.0001f)));
            int proficiency = usage.RelevantSkill == null ? 0 :
                caster.Character.GetSkillValue(usage.RelevantSkill);
            int proficiencyBonus = Math.Min(3, Math.Max(0, proficiency / 100));
            return lengthBonus + proficiencyBonus;
        }

        private static bool TryGetInsideInterval(Vec3 start, Vec3 end, Vec3 center,
            float radius, out float entry, out float exit)
        {
            entry = exit = 0f;
            Vec3 travel = end - start;
            Vec3 offset = start - center;
            float a = travel.LengthSquared;
            float b = 2f * Vec3.DotProduct(offset, travel);
            float c = offset.LengthSquared - radius * radius;
            float discriminant = b * b - 4f * a * c;
            if (a < 0.000001f || discriminant < 0f)
                return false;
            float root = MathF.Sqrt(discriminant);
            entry = MathF.Max(0f, (-b - root) / (2f * a));
            exit = MathF.Min(1f, (-b + root) / (2f * a));
            return exit > entry;
        }

    }
}
