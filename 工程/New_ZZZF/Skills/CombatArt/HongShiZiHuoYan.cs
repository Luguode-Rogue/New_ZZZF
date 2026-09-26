using System;
using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>向前喷出扇形火焰；伤害采样和表现不再进入旧投射物循环。</summary>
    public class HongShiZiHuoYan : SkillBase
    {
        private const float SampleRadius = 3f;
        private const float ConeLength = 10f;
        private const float HalfAngle = 30f;
        private const float BaseDamage = 60f;
        private const int DirectionCount = 7;
        private readonly MBList<Agent> _nearbyAgents = new MBList<Agent>();

        // 战技由攻击动作后段触发；统一入口的动作禁令不能挡住玩家的战技输入。
        public override bool CanActivateWhilePerformingAction => true;

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            // 实际伤害为七条射线、每米一个半径 3 米的球形采样；不是单个圆形 AOE。
            area = index == 0 ? new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.SampledCone,
                Radius = SampleRadius,
                Length = ConeLength,
                HalfAngleDegrees = HalfAngle
            } : default;
            return index == 0;
        }

        public HongShiZiHuoYan()
        {
            SkillID = "HongShiZiHuoYan";
            Type = SPSkillType.CombatArt;
            Cooldown = 10f;
            ResourceCost = 35f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0062}HongShiZiHuoYan");
            Difficulty = null;
            Description = new TaleWorlds.Localization.TextObject(
                "沿前方10米喷射路径向周围扩散3米的扇形火焰，对范围内每名敌人造成一次60点基础火焰伤害。消耗耐力：35。冷却时间：10秒。");


        }


        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive() || Mission.Current?.Scene == null)
                return FailActivation("施法者或当前场景不可用。");
            if (!TryGetDamageArea(agent, 0, out SkillDamageArea area) || !area.IsValid)
                return FailActivation("喷火范围参数无效。");

            Vec3[] directions = BuildDirections(agent.LookDirection, area.HalfAngleDegrees);
            DamageNearbyEnemies(agent, area, directions);
            ShowFlame(agent.Position, area, directions);
            // 空挥也是一次完整战技；正常消费资源与冷却，避免无目标时无限刷特效。
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            // 当前攻防动作不作为 NPC 使用战技的限制。
            // if (!base.CheckCondition(caster) || caster.IsPerformingAction())
            if (!base.CheckCondition(caster))
                return false;
            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || !target.IsHuman ||
                !caster.IsEnemyOf(target) || !TryGetDamageArea(caster, 0, out SkillDamageArea area))
                return false;
            Vec3 toTarget = target.GetEyeGlobalPosition() - caster.Position;
            float maximumDistance = area.Length + area.Radius;
            if (toTarget.LengthSquared > maximumDistance * maximumDistance)
                return false;
            Vec3[] directions = BuildDirections(caster.LookDirection, area.HalfAngleDegrees);
            return IsInsideSampledFlame(caster.Position, target.GetEyeGlobalPosition(), area, directions) &&
                RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private void DamageNearbyEnemies(Agent caster, SkillDamageArea area, Vec3[] directions)
        {
            _nearbyAgents.Clear();
            float searchRadius = area.Length + area.Radius;
            if (caster.Team != null)
                Mission.Current.GetNearbyEnemyAgents(caster.Position.AsVec2, searchRadius,
                    caster.Team, _nearbyAgents);
            else
                Mission.Current.GetNearbyAgents(caster.Position.AsVec2, searchRadius, _nearbyAgents);

            float spellPower = MagicDamageSystem.GetSpellPowerCoefficient(caster);
            foreach (Agent target in _nearbyAgents)
            {
                if (target == null || target == caster || !target.IsActive() || !target.IsHuman ||
                    !caster.IsEnemyOf(target) || !IsInsideSampledFlame(
                        caster.Position, target.GetEyeGlobalPosition(), area, directions))
                    continue;
                AgentSkillComponent component = Script.GetActiveComponents(target);
                if (component != null && component.StateContainer.HasState("BKBBuff"))
                    continue;
                if (!RushMovementMissionLogic.HasLineOfSight(caster, target))
                    continue;

                Script.CalculateFinalMagicDamage(caster, target, BaseDamage, spellPower,
                    DamageType.FIRE_ENHANCEMENT_BLASTING);
            }
        }

        private static Vec3[] BuildDirections(Vec3 lookDirection, float halfAngleDegrees)
        {
            Vec3 forward = lookDirection.AsVec2.ToVec3();
            if (forward.LengthSquared < 0.001f)
                forward = Vec3.Forward;
            forward.Normalize();
            Vec3[] directions = new Vec3[DirectionCount];
            for (int i = 0; i < DirectionCount; i++)
            {
                Vec3 direction = forward;
                direction.RotateAboutZ((i - 3) * (halfAngleDegrees / 3f) *
                    ((float)Math.PI / 180f));
                directions[i] = direction;
            }
            return directions;
        }

        private static bool IsInsideSampledFlame(
            Vec3 origin, Vec3 targetPoint, SkillDamageArea area, Vec3[] directions)
        {
            // 中心采样原本会误伤身后的近敌；保留近身覆盖，但限制在正前半平面。
            Vec3 toTarget = targetPoint - origin;
            if (Vec3.DotProduct(toTarget.AsVec2.ToVec3(), directions[DirectionCount / 2]) < 0f)
                return false;
            float radiusSquared = area.Radius * area.Radius;
            if ((targetPoint - origin).LengthSquared <= radiusSquared)
                return true;
            for (int i = 0; i < directions.Length; i++)
            {
                for (int distance = 1; distance <= (int)area.Length; distance++)
                {
                    Vec3 sample = origin + directions[i] * distance;
                    if ((targetPoint - sample).LengthSquared <= radiusSquared)
                        return true;
                }
            }
            return false;
        }

        private static void ShowFlame(Vec3 origin, SkillDamageArea area, Vec3[] directions)
        {
            SpellProjectileMissionLogic effects = SpellProjectileMissionLogic.GetForCurrentMission();
            if (effects == null)
                return;
            // 三条表现线各四处粒子；完整七线仅用于伤害判定。
            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex += 3)
            {
                for (int step = 1; step <= 4; step++)
                {
                    Vec3 position = origin + directions[directionIndex] *
                        (area.Length * step / 4f) + Vec3.Up;
                    effects.SpawnTimedParticle("psys_battleground_env_fire", position, 1f);
                }
            }
        }

    }
}
