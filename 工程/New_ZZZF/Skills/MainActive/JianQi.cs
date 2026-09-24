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

        private sealed class TargetHits
        {
            public float DistanceInside;
            public int Count;
        }

        private sealed class CastState
        {
            public Agent Caster;
            public EquipmentIndex WeaponSlot;
            public ItemObject WeaponItem;
            public MissionWeapon Weapon;
            public float BaseDamage;
            public float Radius;
            public int MaximumHits;
            public StrikeType StrikeType;
            public readonly Dictionary<Agent, TargetHits> Hits = new Dictionary<Agent, TargetHits>();
            public readonly MBList<Agent> NearbyAgents = new MBList<Agent>();
        }

        public JianQi()
        {
            SkillID = "JianQi";
            Type = SPSkillType.MainActive;
            Cooldown = 5f;
            ResourceCost = 40f;
            Text = new TextObject("{=12345676}JianQi");
            Description = new TextObject("朝视野方向发出穿透敌人的15米剑气。每次命中的基础物理伤害为(30+等级)/2；武器每满1米、对应武器精通每满100级，各增加1次单体命中上限，分别最多增加3次，总上限7次。伤害范围随命中上限扩大。冷却5秒，消耗40体力。");
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
            int maximumHits = 1 + GetBonusHits(caster, weapon);
            float radius = BaseRadius + RadiusPerBonusHit * (maximumHits - 1);
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
            float baseDamage = (30f + caster.Character.Level) / 2f;
            foreach (Agent target in _aiCandidates)
            {
                if (target == null || !target.IsActive() || !target.IsHuman ||
                    target.Health <= 0f || !caster.IsEnemyOf(target) ||
                    !TryGetInsideInterval(start, end, target.GetEyeGlobalPosition(),
                        radius, out float entry, out float exit) ||
                    !RushMovementMissionLogic.HasLineOfSight(caster, target))
                    continue;

                int expectedHits = Math.Min(maximumHits,
                    1 + (int)((Range * (exit - entry) + 0.0001f) / DistanceBetweenHits));
                float perHit = MissionGameModels.Current.StrikeMagnitudeModel.ComputeRawDamage(
                    damageType, baseDamage,
                    target.GetBaseArmorEffectivenessForBodyPart(BoneBodyPartType.Abdomen),
                    target.Monster.AbsorbedDamageRatio);
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
            if (!TryGetWeapon(caster, out EquipmentIndex slot, out MissionWeapon weapon))
                return FailActivation("需要手持近战武器。");
            SpellProjectileMissionLogic manager = SpellProjectileMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载投射物管理器。");
            Vec3 start = caster.GetEyeGlobalPosition();
            MissionScreen screen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = caster.IsMainAgent && screen != null
                ? screen.CombatCamera.Direction
                : caster.LookDirection;
            if (direction.LengthSquared < 0.001f)
                direction = caster.LookDirection;
            if (direction.LengthSquared < 0.001f)
                return FailActivation("剑气方向无效。");
            direction.Normalize();

            int bonus = GetBonusHits(caster, weapon);
            CastState state = new CastState
            {
                Caster = caster,
                WeaponSlot = slot,
                WeaponItem = weapon.Item,
                Weapon = weapon,
                BaseDamage = (30f + caster.Character.Level) / 2f,
                Radius = BaseRadius + RadiusPerBonusHit * bonus,
                MaximumHits = 1 + bonus,
                StrikeType = weapon.CurrentUsageItem.SwingDamage > 0
                    ? StrikeType.Swing : StrikeType.Thrust
            };
            SpellProjectileRequest request = new SpellProjectileRequest
            {
                Caster = caster,
                StartPosition = start,
                Direction = direction,
                Speed = Speed,
                Lifetime = 1f,
                MaxTravelDistance = Range,
                HitRadius = state.Radius,
                WorldHitRadius = 0.05f,
                CollisionInterval = 0.05f,
                PrefabResourceName = "weapon_heap_sword_a",
                PiercesAgents = true,
                InvokeImpactWhenLifetimeExpires = false,
                OnTravelSegment = (from, to) => ApplyTravelDamage(state, from, to)
            };
            if (!manager.TrySpawn(request, out string reason))
                return FailActivation(reason ?? "无法生成剑气。");
            PlayReleaseAction(caster, weapon.CurrentUsageItem, state.StrikeType);
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
            if (caster == null || !caster.IsActive() || caster.Character == null)
                return false;
            slot = caster.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None)
                return false;
            weapon = caster.Equipment[slot];
            return !weapon.IsEmpty && weapon.Item != null &&
                   weapon.CurrentUsageItem != null &&
                   !weapon.CurrentUsageItem.IsRangedWeapon &&
                   !weapon.CurrentUsageItem.IsShield &&
                   (weapon.CurrentUsageItem.SwingDamage > 0 ||
                    weapon.CurrentUsageItem.ThrustDamage > 0);
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

        private static void ApplyTravelDamage(CastState state, Vec3 start, Vec3 end)
        {
            Mission mission = Mission.Current;
            if (mission == null || state.Caster == null || !state.Caster.IsActive())
                return;
            float segmentLength = (end - start).Length;
            if (segmentLength < 0.0001f)
                return;

            Vec3 middle = (start + end) * 0.5f;
            float queryRadius = segmentLength * 0.5f + state.Radius + 1f;
            state.NearbyAgents.Clear();
            if (state.Caster.Team != null)
                mission.GetNearbyEnemyAgents(middle.AsVec2, queryRadius,
                    state.Caster.Team, state.NearbyAgents);
            else
                mission.GetNearbyAgents(middle.AsVec2, queryRadius, state.NearbyAgents);

            foreach (Agent target in state.NearbyAgents)
            {
                if (target == null || !target.IsActive() || !target.IsHuman ||
                    !state.Caster.IsEnemyOf(target))
                    continue;
                if (!TryGetInsideInterval(start, end, target.GetEyeGlobalPosition(),
                        state.Radius, out float entry, out float exit))
                    continue;
                if (!state.Hits.TryGetValue(target, out TargetHits hits))
                {
                    hits = new TargetHits();
                    state.Hits.Add(target, hits);
                }
                if (hits.Count >= state.MaximumHits)
                    continue;
                float previousDistance = hits.DistanceInside;
                hits.DistanceInside += segmentLength * (exit - entry);
                int allowed = Math.Min(state.MaximumHits,
                    1 + (int)((hits.DistanceInside + 0.0001f) / DistanceBetweenHits));
                while (hits.Count < allowed && target.IsActive() && target.Health > 0f)
                {
                    float distanceFromEntry = hits.Count == 0 ? 0f :
                        MathF.Max(0f, hits.Count * DistanceBetweenHits - previousDistance);
                    float progress = MathF.Min(exit, entry + distanceFromEntry / segmentLength);
                    Vec3 hitPosition = start + (end - start) * progress;
                    hits.Count++;
                    RegisterPhysicalHit(state, target, hitPosition);
                }
            }
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

        private static void RegisterPhysicalHit(CastState state, Agent target, Vec3 position)
        {
            Agent caster = state.Caster;
            if (caster.GetPrimaryWieldedItemIndex() != state.WeaponSlot ||
                caster.Equipment[state.WeaponSlot].Item != state.WeaponItem ||
                caster.Equipment[state.WeaponSlot].ItemModifier != state.Weapon.ItemModifier)
                return;

            WeaponComponentData usage = state.Weapon.CurrentUsageItem;
            DamageTypes damageType = state.StrikeType == StrikeType.Swing
                ? usage.SwingDamageType : usage.ThrustDamageType;
            if (damageType == DamageTypes.Invalid)
                damageType = DamageTypes.Blunt;
            sbyte handBone = caster.Monster.MainHandItemBoneIndex;
            Vec3 direction = target.Position - caster.Position;
            if (direction.LengthSquared < 0.001f)
                direction = caster.LookDirection;
            direction.Normalize();

            AttackCollisionData collision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false, false, false, true, false, false, false, false,
                false, false, false, false, CombatCollisionResult.StrikeAgent,
                (int)state.WeaponSlot, (int)state.StrikeType, (int)damageType,
                0, BoneBodyPartType.Abdomen, handBone,
                state.StrikeType == StrikeType.Swing
                    ? Agent.UsageDirection.AttackLeft : Agent.UsageDirection.AttackDown,
                -1, CombatHitResultFlags.NormalHit,
                0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Up, direction, position, Vec3.Zero, Vec3.Zero,
                target.Velocity, Vec3.Up);
            AttackInformation attack = new AttackInformation(
                caster, target, WeakGameEntity.Invalid, in collision, in state.Weapon);
            // 原版先按武器伤害类型和目标装备护甲计算，再施加 AgentApplyDamageModel 的修正。
            var damageModel = MissionGameModels.Current.AgentApplyDamageModel;
            var strikeModel = MissionGameModels.Current.StrikeMagnitudeModel;
            float armor = strikeModel.CalculateAdjustedArmorForBlow(
                in attack, in collision, attack.ArmorAmountFloat,
                attack.AttackerAgentCharacter, attack.AttackerCaptainCharacter,
                attack.VictimAgentCharacter, attack.VictimCaptainCharacter, usage);
            float bodyMultiplier = damageModel.GetDamageMultiplierForBodyPart(
                BoneBodyPartType.Abdomen, damageType, target.IsHuman, false);
            float multiplier = bodyMultiplier * attack.CombatDifficultyMultiplier;
            float damageBeforeModifiers = strikeModel.ComputeRawDamage(
                damageType, state.BaseDamage, armor,
                attack.VictimAgentAbsorbedDamageRatio) * multiplier;
            int armorReducedDamage = Math.Min(2000, Math.Max(0,
                (int)MathF.Ceiling(damageBeforeModifiers)));
            int damageWithoutArmor = Math.Min(2000, Math.Max(0,
                (int)MathF.Ceiling(strikeModel.ComputeRawDamage(
                    damageType, state.BaseDamage, 0f,
                    attack.VictimAgentAbsorbedDamageRatio) * multiplier)));
            collision.BaseMagnitude = state.BaseDamage;
            collision.AbsorbedByArmor = Math.Max(0, damageWithoutArmor - armorReducedDamage);
            collision.InflictedDamage = armorReducedDamage;
            int inflictedDamage = armorReducedDamage > 0
                ? Math.Max(0, (int)MathF.Round(damageModel.CalculateDamage(
                    in attack, in collision, armorReducedDamage)))
                : 0;
            collision.InflictedDamage = inflictedDamage;
            if (inflictedDamage <= 0)
                return;

            Blow blow = new Blow(caster.Index)
            {
                DamageType = damageType,
                StrikeType = state.StrikeType,
                AttackType = AgentAttackType.Standard,
                BoneIndex = 0,
                VictimBodyPart = BoneBodyPartType.Abdomen,
                BaseMagnitude = state.BaseDamage,
                InflictedDamage = inflictedDamage,
                GlobalPosition = position,
                SwingDirection = direction,
                Direction = direction,
                DamageCalculated = true,
                BlowFlag = BlowFlags.None
            };
            blow.WeaponRecord.FillAsMeleeBlow(
                state.WeaponItem, usage, (int)state.WeaponSlot, handBone);
            blow.WeaponRecord.CurrentPosition = position;
            blow.WeaponRecord.StartingPosition = position;

            JianQiHitContext.ReportCombatLog(caster, target, inflictedDamage,
                collision.AbsorbedByArmor, damageType);
            JianQiHitContext.Begin(caster, target);
            try { target.RegisterBlow(blow, collision); }
            finally { JianQiHitContext.End(); }

            // RegisterBlow 已触发 OnAgentHit/OnScoreHit；OnMeleeHit 需按原生顺序单独分发。
            if (Mission.Current != null)
            {
                foreach (MissionBehavior behavior in Mission.Current.MissionBehaviors)
                    behavior.OnMeleeHit(caster, target, false, collision);
            }

            AgentSkillComponent component = Script.GetActiveComponents(target);
            if (component != null)
            {
                component._beHitCount++;
                component._beHitTime = MathF.Clamp(component._beHitTime + 0.3f, 0f, 0.3f);
            }
        }
    }
}
