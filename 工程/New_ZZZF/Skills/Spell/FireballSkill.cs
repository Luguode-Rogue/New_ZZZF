using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>沿瞄准方向飞行、接触后造成火焰爆炸并施加灼烧的基础法术。</summary>
    public sealed class FireballSkill : SkillBase
    {
        private const float MinimumAiRange = 8f;
        private const float MaximumAiRange = 80f;
        private const float ProjectileSpeed = 25f;
        private const float ProjectileLifetime = 4f;
        private const float ProjectileHitRadius = 1f;
        private const float ProjectileWorldHitRadius = 0.3f;
        private const float ExplosionRadius = 4f;
        private const float DirectBaseDamage = 30f;
        private const float ExplosionCenterBaseDamage = 20f;
        private const float ExplosionEdgeBaseDamage = 8f;
        private const float BurningDuration = 5f;
        private const float BurningBaseDamagePerTick = 5f;
        private static readonly MBList<Agent> ExplosionTargets = new MBList<Agent>();

        private sealed class FireballSnapshot
        {
            public float SpellPowerCoefficient;
        }

        public FireballSkill()
        {
            SkillID = "Fireball";
            Type = SPSkillType.Spell;
            Cooldown = 8f;
            ResourceCost = 30f;
            Difficulty = null;
            Text = new TextObject("{=12345678}火球");
            Description = new TextObject(
                "快速施法时向80米内视野中的敌人发射火球；按住Shift时改为朝视线指示落点发射。直接命中造成30点基础火焰伤害，随后在4米范围内造成20至8点基础火焰伤害，并施加持续5秒、每秒5点基础伤害的灼烧。灼烧挂载时按目标魔抗降低每秒伤害。所有伤害均乘以施法者的技能法强系数。消耗法力：30。冷却时间：8秒。");
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive())
                return FailActivation("施法者当前不可用。");

            SpellProjectileMissionLogic manager = SpellProjectileMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载法术投射物管理器。");

            if (!SpellTargetingSystem.TryResolveSingleTarget(
                    caster, MaximumAiRange, out SpellTargetingSystem.Result targeting))
                return FailActivation("视野内没有可用目标。");

            Vec3 direction = ResolveCastDirection(caster, targeting);
            SpellProjectileRequest request = new SpellProjectileRequest
            {
                Caster = caster,
                StartPosition = caster.GetEyeGlobalPosition() + direction.NormalizedCopy() * 0.8f,
                Direction = direction,
                Speed = ProjectileSpeed,
                Lifetime = ProjectileLifetime,
                HitRadius = ProjectileHitRadius,
                WorldHitRadius = ProjectileWorldHitRadius,
                CollisionInterval = 0.05f,
                MeshResourceName = null,
                ParticleSystemName = "psys_blaze_vertical_1",
                HitHumanAgentsOnly = true,
                HitEnemiesOnly = true,
                InvokeImpactWhenLifetimeExpires = true,
                OnImpact = OnFireballImpact,
                Payload = new FireballSnapshot
                {
                    SpellPowerCoefficient = MagicDamageSystem.GetSpellPowerCoefficient(caster)
                }
            };
            if (!manager.TrySpawn(request, out string failureReason))
                return FailActivation(failureReason ?? "无法生成火球。");

            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                SpellProjectileMissionLogic.GetForCurrentMission() == null)
                return false;

            Agent target = caster.GetTargetAgent();
            if (!IsValidEnemy(caster, target))
                return false;

            float distance = (target.Position - caster.Position).Length;
            return distance >= MinimumAiRange && distance <= MaximumAiRange &&
                   RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static void OnFireballImpact(SpellProjectileImpact impact)
        {
            if (impact?.Caster == null || Mission.Current == null)
                return;
            FireballSnapshot snapshot = impact.Payload as FireballSnapshot;
            float spellPowerCoefficient = snapshot == null
                ? MagicDamageSystem.GetSpellPowerCoefficient(impact.Caster)
                : snapshot.SpellPowerCoefficient;

            SpellProjectileMissionLogic.GetForCurrentMission()?.SpawnExpandingParticleRing(
                "psys_blaze_vertical_1", "psys_campfire_sparks",
                impact.Position, ExplosionRadius, 12, 0.45f, 0.9f);

            if (impact.DirectTarget != null && impact.DirectTarget.IsActive())
            {
                MagicDamageSystem.Apply(
                    impact.Caster,
                    impact.DirectTarget,
                    DirectBaseDamage,
                    spellPowerCoefficient,
                    DamageType.FIRE_DAMAGE,
                    MagicDamageFlags.Burning,
                    impact.Position);
            }

            if (impact.Caster.Team != null)
            {
                Mission.Current.GetNearbyEnemyAgents(
                    impact.Position.AsVec2,
                    ExplosionRadius,
                    impact.Caster.Team,
                    ExplosionTargets);
            }
            else
            {
                Mission.Current.GetNearbyAgents(
                    impact.Position.AsVec2,
                    ExplosionRadius,
                    ExplosionTargets);
            }

            float radiusSquared = ExplosionRadius * ExplosionRadius;
            foreach (Agent target in ExplosionTargets)
            {
                if (target == null || !target.IsActive() || !target.IsHuman ||
                    target == impact.Caster || !impact.Caster.IsEnemyOf(target))
                    continue;
                float distanceSquared =
                    (target.Position + Vec3.Up - impact.Position).LengthSquared;
                if (distanceSquared > radiusSquared)
                    continue;

                float distanceRatio = MathF.Clamp(
                    MathF.Sqrt(distanceSquared) / ExplosionRadius, 0f, 1f);
                float explosionBaseDamage = ExplosionCenterBaseDamage +
                    (ExplosionEdgeBaseDamage - ExplosionCenterBaseDamage) * distanceRatio;
                MagicDamageSystem.Apply(
                    impact.Caster,
                    target,
                    explosionBaseDamage,
                    spellPowerCoefficient,
                    DamageType.FIRE_DAMAGE,
                    MagicDamageFlags.Area | MagicDamageFlags.Burning,
                    target.Position + Vec3.Up);

                AgentSkillComponent component = target.GetComponent<AgentSkillComponent>();
                if (component == null || !target.IsActive())
                    continue;
                BurningState burning = new BurningState(
                    BurningDuration,
                    BurningBaseDamagePerTick,
                    impact.Caster,
                    spellPowerCoefficient)
                {
                    TargetAgent = target
                };
                // TODO 灼烧粒子的预期表现：在目标腰、胸附近分布两个小型低发射率火焰，
                // 间歇产生少量火星与薄烟。每个目标只保留一个效果，刷新DoT只延长时间，
                // 不重复创建粒子；不使用动态阴影光源，远距离只保留一层小火焰。
                component.StateContainer.AddOrReplaceState(burning, target);
            }
        }

        private static Vec3 ResolveCastDirection(
            Agent caster,
            SpellTargetingSystem.Result targeting)
        {
            Vec3 origin = caster.GetEyeGlobalPosition();
            if (targeting.UsesManualIndicator || targeting.Target == null)
            {
                Vec3 manualDirection = targeting.Position - origin;
                return manualDirection.LengthSquared > 0.001f
                    ? manualDirection.NormalizedCopy()
                    : caster.LookDirection;
            }

            Agent target = targeting.Target;
            Vec3 targetPosition = target.GetEyeGlobalPosition();
            float distance = (targetPosition - origin).Length;
            float travelTime = distance / ProjectileSpeed;
            Vec3 predictedPosition = targetPosition + target.Velocity * travelTime;
            Vec3 direction = predictedPosition - origin;
            return direction.LengthSquared > 0.001f ? direction.NormalizedCopy() : caster.LookDirection;
        }

        private static bool IsValidEnemy(Agent caster, Agent target)
        {
            return caster != null && target != null && target != caster &&
                   target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target);
        }
    }
}
