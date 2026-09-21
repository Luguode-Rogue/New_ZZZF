using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Systems
{
    /// <summary>只描述魔法伤害的表现/来源特征，不参与法强和魔抗数值计算。</summary>
    [Flags]
    public enum MagicDamageFlags
    {
        None = 0,
        Area = 1,
        Periodic = 2,
        Burning = 4
    }

    /// <summary>
    /// 一次魔法伤害的完整结算明细。保留各层原始值，方便以后制作战斗日志、
    /// 伤害飘字和属性面板，而不需要在调用方重新推导计算过程。
    /// </summary>
    public struct MagicDamageResult
    {
        /// <summary>技能定义的基础伤害，不包含任何攻击方增益。</summary>
        public float BaseDamage;
        /// <summary>本次技能在施法时快照的法强系数。</summary>
        public float SpellPowerCoefficient;
        /// <summary>基础伤害乘以法强系数后的抗性前伤害。</summary>
        public float DamageBeforeResistance;
        /// <summary>由角色基础属性换算出的通用魔抗。</summary>
        public float AttributeResistance;
        /// <summary>装备、技能、Buff等特殊来源提供的通用魔抗。</summary>
        public float SpecialUniversalResistance;
        /// <summary>特殊来源中仅针对本次伤害元素的专项抗性。</summary>
        public float SpecialElementResistance;
        /// <summary>三部分抗性合计后的最终有效魔抗。</summary>
        public float EffectiveResistance;
        /// <summary>经过魔抗公式后实际扣除的生命值。</summary>
        public float FinalDamage;
        public bool WasImmune;
        public bool WasFatal;
    }

    /// <summary>
    /// 全部魔法伤害的唯一结算入口。抗性前伤害严格采用：
    /// 技能基础伤害 × 技能法强系数。
    /// </summary>
    public static class MagicDamageSystem
    {
        // 允许负抗性产生易伤，但限制最低值，避免错误配置造成无限倍率。
        private const float MinimumResistance = -100f;

        /// <summary>
        /// 读取施法者当前的法强系数。没有技能组件的原生单位使用1倍，确保旧技能
        /// 和未接入属性系统的单位仍能造成其技能基础伤害。
        /// </summary>
        public static float GetSpellPowerCoefficient(Agent caster)
        {
            AgentSkillComponent component = caster?.GetComponent<AgentSkillComponent>();
            return component == null ? 1f : MathF.Max(0f, component.MagicPowerCoefficient);
        }

        /// <summary>
        /// 结算并施加一次魔法伤害。
        ///
        /// 固定结算顺序：
        /// 1. 技能基础伤害 × 技能法强系数；
        /// 2. 检查完全魔法免疫；
        /// 3. 属性通用魔抗 + 特殊通用魔抗 + 对应元素专项抗性；
        /// 4. 根据有效魔抗计算最终伤害；
        /// 5. 扣除生命并保留施法者的击杀归属。
        ///
        /// 调用方应在施法或创建持续状态时快照 spellPowerCoefficient，避免投射物
        /// 飞行期间或持续伤害每次跳伤时因装备变化而改变已经释放技能的伤害。
        /// </summary>
        public static MagicDamageResult Apply(
            Agent caster,
            Agent victim,
            float baseDamage,
            float spellPowerCoefficient,
            DamageType damageType,
            MagicDamageFlags flags = MagicDamageFlags.None,
            Vec3? impactPosition = null)
        {
            MagicDamageResult result = Calculate(
                victim, baseDamage, spellPowerCoefficient, damageType);

            if (result.FinalDamage <= 0f)
                return result;

            // RegisterBlow 只接受整数伤害。正数伤害至少登记1点，确保极低伤害也能
            // 进入原生受击、飘字和监听链；Result同步为真正交给引擎的伤害值。
            int inflictedDamage = Math.Max(1, (int)Math.Round(result.FinalDamage));
            result.FinalDamage = inflictedDamage;
            float healthBefore = victim.Health;
            RegisterNativeMagicBlow(
                caster, victim, inflictedDamage, flags, impactPosition);
            result.WasFatal = healthBefore > 0f && victim.Health < 1f;

            return result;
        }

        /// <summary>
        /// 只计算一次魔法伤害，不修改目标生命。持续伤害在状态挂载时调用本方法，
        /// 将当时的法强和目标魔抗一次性快照为固定每跳伤害。
        /// </summary>
        public static MagicDamageResult Calculate(
            Agent victim,
            float baseDamage,
            float spellPowerCoefficient,
            DamageType damageType)
        {
            MagicDamageResult result = new MagicDamageResult
            {
                BaseDamage = MathF.Max(0f, baseDamage),
                SpellPowerCoefficient = MathF.Max(0f, spellPowerCoefficient)
            };
            result.DamageBeforeResistance = result.BaseDamage * result.SpellPowerCoefficient;

            if (victim == null || !victim.IsActive() || result.DamageBeforeResistance <= 0f)
                return result;

            AgentSkillComponent victimComponent = victim.GetComponent<AgentSkillComponent>();
            if (victimComponent != null && victimComponent.StateContainer.HasState("TianQiBuff"))
            {
                result.WasImmune = true;
                return result;
            }

            // 魔抗严格拆成两个来源层级：
            // AttributeResistance 是角色属性产生的通用抗性；其余两项都属于
            // 特殊来源，其中通用抗性作用于全部元素，专项抗性只作用于对应元素。
            if (victimComponent != null)
            {
                result.AttributeResistance = victimComponent.AttributeMagicResistance;
                result.SpecialUniversalResistance = victimComponent.SpecialUniversalMagicResistance;
                result.SpecialElementResistance = victimComponent.GetSpecialElementResistance(damageType);
            }

            result.EffectiveResistance = MathF.Max(
                MinimumResistance,
                result.AttributeResistance +
                result.SpecialUniversalResistance +
                result.SpecialElementResistance);

            // 正抗性采用递减收益：100抗性承受50%伤害。
            // 负抗性使用连续的易伤公式：-100抗性承受150%伤害。
            float multiplier = result.EffectiveResistance >= 0f
                ? 100f / (100f + result.EffectiveResistance)
                : 2f - 100f / (100f - result.EffectiveResistance);
            result.FinalDamage = MathF.Max(0f, result.DamageBeforeResistance * multiplier);

            return result;
        }

        /// <summary>
        /// 按 Mission 中区域破坏/投石器伤害所使用的模式，构造一个已完成伤害
        /// 计算的 Blow，并通过 Agent.RegisterBlow 进入原生受击链。
        /// DamageCalculated=true 表示数值已经过本系统的法强和魔抗结算，原生物理
        /// 护甲模型不得再次削减；DamageTypes.Blunt 仅作为引擎所需的表现类型。
        /// </summary>
        private static void RegisterNativeMagicBlow(
            Agent caster,
            Agent victim,
            int inflictedDamage,
            MagicDamageFlags flags,
            Vec3? impactPosition)
        {
            int ownerId = caster == null ? -1 : caster.Index;
            sbyte attackBoneIndex = caster == null
                ? (sbyte)-1
                : caster.Monster.MainHandItemBoneIndex;

            Vec3 globalPosition = impactPosition ?? (victim.Position + Vec3.Up);
            Vec3 direction = caster == null
                ? victim.LookDirection
                : victim.Position - caster.Position;
            if (direction.LengthSquared < 0.001f)
                direction = victim.LookDirection;
            if (direction.LengthSquared < 0.001f)
                direction = Vec3.Up;
            direction.Normalize();

            Blow blow = new Blow(ownerId)
            {
                DamageType = DamageTypes.Blunt,
                StrikeType = StrikeType.Swing,
                AttackType = AgentAttackType.Standard,
                BoneIndex = 0,
                VictimBodyPart = BoneBodyPartType.Abdomen,
                BaseMagnitude = inflictedDamage,
                InflictedDamage = inflictedDamage,
                GlobalPosition = globalPosition,
                SwingDirection = direction,
                Direction = direction,
                DamageCalculated = true,
                BlowFlag = flags.HasFlag(MagicDamageFlags.Periodic)
                    ? BlowFlags.NoSound
                    : BlowFlags.None
            };
            blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, attackBoneIndex);
            blow.WeaponRecord.WeaponFlags |= WeaponFlags.NoBlood;
            if (flags.HasFlag(MagicDamageFlags.Area))
                blow.WeaponRecord.WeaponFlags |= WeaponFlags.AffectsArea;
            if (flags.HasFlag(MagicDamageFlags.Burning))
                blow.WeaponRecord.WeaponFlags |= WeaponFlags.Burning;
            blow.WeaponRecord.CurrentPosition = globalPosition;
            blow.WeaponRecord.StartingPosition = globalPosition;

            AttackCollisionData collisionData =
                AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                    false, false, false, true,
                    false, false, false, false,
                    false, false, false, false,
                    CombatCollisionResult.StrikeAgent,
                    -1,
                    (int)StrikeType.Swing,
                    (int)DamageTypes.Blunt,
                    blow.BoneIndex,
                    BoneBodyPartType.Abdomen,
                    attackBoneIndex,
                    Agent.UsageDirection.AttackLeft,
                    -1,
                    CombatHitResultFlags.NormalHit,
                    0.5f,
                    1f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f,
                    Vec3.Up,
                    direction,
                    globalPosition,
                    Vec3.Zero,
                    Vec3.Zero,
                    victim.Velocity,
                    Vec3.Up);

            victim.RegisterBlow(blow, collisionData);

            // RegisterBlow 负责原生扣血、受击回调、击杀归属与士气等逻辑，
            // 但直接调用它不会经过 Mission.PrintAttackCollisionResults，因此不会
            // 自动生成玩家的伤害数字。按 Mission 中溺水等特殊伤害的做法，
            // 在原生受击完成后单独补一条 CombatLogData。
            AddMagicCombatLog(caster, victim, inflictedDamage);
        }

        private static void AddMagicCombatLog(Agent caster, Agent victim, int inflictedDamage)
        {
            Mission mission = Mission.Current;
            if (mission == null || caster == null || victim == null || inflictedDamage <= 0)
                return;

            bool attackerHasRider = caster.RiderAgent != null;
            bool victimHasRider = victim.RiderAgent != null;
            CombatLogData combatLog = new CombatLogData(
                caster == victim,
                caster.IsHuman,
                caster.IsMine,
                attackerHasRider,
                attackerHasRider && caster.RiderAgent.IsMine,
                caster.IsMount,
                victim.IsHuman,
                victim.IsMine,
                victim.Health <= 0f,
                victimHasRider,
                victimHasRider && victim.RiderAgent.IsMine,
                victim.IsMount,
                null,
                victim.RiderAgent == caster,
                false,
                false,
                0f)
            {
                // 魔法伤害已经完成法强与魔抗结算，这里只向 HUD 报告最终整数。
                InflictedDamage = inflictedDamage,
                IsSpecialDamage = true,
                DamageType = DamageTypes.Blunt,
                BodyPartHit = BoneBodyPartType.None
            };
            combatLog.SetVictimAgent(victim);
            mission.AddCombatLogSafe(caster, victim, combatLog);
        }
    }
}
