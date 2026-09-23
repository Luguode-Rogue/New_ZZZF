using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>在目标地点生成一道固定火墙，通过通用持续区域管理器结算伤害与回收表现。</summary>
    public sealed class FireWallSkill : SkillBase
    {
        private const float Duration = 10f;
        private const float TickInterval = 0.5f;
        private const float BaseDamagePerSecond = 20f;
        private const float BaseDamagePerTick = BaseDamagePerSecond * TickInterval;
        private const float WallLength = 15f; // 包含两端圆帽的实际总长度
        // 胶囊形区域的 Radius 是半宽，2.5米对应火墙总宽5米。
        private const float WallRadius = 2.5f;
        private const float MaximumCastDistance = 160f;
        private const float MinimumAiRange = 8f;
        private const float MaximumAiRange = 160f;

        private sealed class FireWallSnapshot
        {
            public float SpellPowerCoefficient;
        }

        public FireWallSkill()
        {
            SkillID = "FireWall";
            Type = SPSkillType.Spell;
            Cooldown = 10f;
            ResourceCost = 50f;
            Difficulty = null;
            Text = new TextObject("火墙术");
            Description = new TextObject(
                "快速施法时在160米内视野中敌人最密集的位置生成火墙；按住Shift时改为在视线指示落点生成。火墙长15米、宽5米，持续10秒，每0.5秒对范围内的敌人造成10点基础火焰伤害，即每秒20点。伤害乘以施法者的技能法强系数并受目标魔抗减免。消耗法力：50。冷却时间：10秒。");
        }

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = index == 0 ? new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.Capsule,
                Radius = WallRadius,
                // 管理器 Length 表示中轴线，两端各再延伸一个 Radius。
                Length = WallLength - 2f * WallRadius,
                HeightTolerance = 2.75f
            } : default;
            return index == 0;
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || Mission.Current?.Scene == null)
                return FailActivation("施法者或当前场景不可用。");

            SpellAreaMissionLogic manager = SpellAreaMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载持续法术区域管理器。");

            if (!SpellTargetingSystem.TryResolveAreaTarget(
                    caster, MaximumCastDistance, 5f,
                    out SpellTargetingSystem.Result targeting))
                return FailActivation("视野内没有可用目标。");

            if (!TryGetDamageArea(caster, 0, out SkillDamageArea damageArea) ||
                !damageArea.IsValid)
                return FailActivation("火墙范围参数无效。");

            Vec3 midpoint = targeting.Position;
            // targeting.Position 在指示模式下已是摄像机射线命中的真实场景表面。
            // 禁止再用 GetGroundHeightAtPosition 覆盖 Z：该接口会取下方地形高度，
            // 无法表示城墙、房顶和木台等上层碰撞面，会导致火墙沉到地下。
            Vec3 forward = ResolveHorizontalForward(caster, midpoint);
            Vec3 wallDirection = new Vec3(-forward.y, forward.x, 0f);
            wallDirection.Normalize();
            Vec3 start = midpoint - wallDirection * (damageArea.Length * 0.5f);

            SpellAreaRequest request = new SpellAreaRequest
            {
                Caster = caster,
                Center = start,
                Direction = wallDirection,
                Shape = SpellAreaShape.Capsule,
                Radius = damageArea.Radius,
                Length = damageArea.Length,
                HeightTolerance = damageArea.HeightTolerance,
                Duration = Duration,
                TickInterval = TickInterval,
                TickImmediately = false,
                HitHumanAgentsOnly = true,
                HitEnemiesOnly = true,
                ParticleSystemName = "psys_blaze_vertical_1",
                SecondaryParticleSystemName = "psys_campfire_sparks",
                ParticleSpacing = 1.25f,
                Payload = new FireWallSnapshot
                {
                    SpellPowerCoefficient = MagicDamageSystem.GetSpellPowerCoefficient(caster)
                },
                OnAffectTarget = AffectTarget
            };

            if (!manager.TryCreate(request, out _, out string failureReason))
                return FailActivation(failureReason ?? "无法创建火墙。");

            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                SpellAreaMissionLogic.GetForCurrentMission() == null)
                return false;

            Agent target = caster.GetTargetAgent();
            if (target == null || target == caster || !target.IsActive() ||
                !caster.IsEnemyOf(target))
                return false;

            float distance = (target.Position - caster.Position).Length;
            return distance >= MinimumAiRange && distance <= MaximumAiRange &&
                   RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static void AffectTarget(SpellAreaTargetContext context)
        {
            if (context.Caster == null || context.Target == null ||
                !context.Target.IsActive())
                return;

            FireWallSnapshot snapshot = context.Payload as FireWallSnapshot;
            float spellPowerCoefficient = snapshot == null
                ? MagicDamageSystem.GetSpellPowerCoefficient(context.Caster)
                : snapshot.SpellPowerCoefficient;
            MagicDamageSystem.Apply(
                context.Caster,
                context.Target,
                BaseDamagePerTick,
                spellPowerCoefficient,
                DamageType.FIRE_DAMAGE,
                MagicDamageFlags.Area | MagicDamageFlags.Burning,
                context.Target.Position + Vec3.Up);
        }

        private static Vec3 ResolveHorizontalForward(Agent caster, Vec3 targetPosition)
        {
            Vec3 forward = targetPosition - caster.Position;
            forward.z = 0f;
            if (forward.LengthSquared < 0.001f)
                forward = Vec3.Forward;
            forward.Normalize();
            return forward;
        }
    }
}
