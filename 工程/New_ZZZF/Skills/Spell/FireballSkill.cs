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
        private const float MaximumAiRange = 45f;
        private const float ProjectileSpeed = 25f;
        private const float ProjectileLifetime = 4f;
        private const float ProjectileHitRadius = 0.75f;
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
                "快速施法时向视野内的敌人发射火球；按住Shift时改为朝视线指示落点发射。直接命中造成30点基础火焰伤害，随后在4米范围内造成20至8点基础火焰伤害，并施加持续5秒、每秒5点基础伤害的灼烧。灼烧挂载时按目标魔抗降低每秒伤害。所有伤害均乘以施法者的技能法强系数。消耗法力：30。冷却时间：8秒。");
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
                // 原版火焰投石器使用的 Fire Pot 弹丸可视网格。
                MeshResourceName = "projectile_pot",
                // TODO 飞行粒子的预期表现（资源完成前保持关闭）：
                // 1. 核心层：贴合 Fire Pot 弹体的黄白色高亮火芯，尺寸稳定，不遮住弹体轮廓。
                // 2. 外焰层：橙红火焰向飞行反方向拉伸，短寿命、连续低密度发射，不形成大块透明叠加。
                // 3. 火星层：每秒少量亮黄火星脱落，带小幅随机侧向速度和轻微重力下坠。
                // 4. 烟尾层：深灰薄烟在世界空间保留，寿命0.15~0.45秒，离开弹体后扩散并迅速淡出。
                // 5. 整体宽度不超过当前0.75米判定半径；不带动态光源，远距离LOD关闭火星和烟雾。
                ParticleSystemName = null,
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

            // 保留施法动作和声音，但投射物不创建粒子；粒子只在命中时生成。
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

            SpellProjectileMissionLogic.GetForCurrentMission()?.SpawnTimedParticle(
                "psys_battleground_env_fire", impact.Position, 0.8f);

            // TODO 命中粒子的预期表现：
            // 1. 0~0.15秒：黄白中心火团瞬间膨胀，配合寿命极短的橙色光晕，强调命中爆点。
            // 2. 0~0.45秒：橙红外焰向四周扩张后回缩，火星以环形高速喷出并受重力下落。
            // 3. 0.1~1.5秒：深灰烟团延迟生成，向上扩散、尺寸增大、透明度逐渐归零。
            // 4. 爆炸的视觉半径与4米伤害范围对齐；不用持续火焰伪装爆炸，结束后只留目标身上的灼烧。

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
