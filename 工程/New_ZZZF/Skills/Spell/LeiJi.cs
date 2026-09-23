using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>首次召雷开启施法窗口；窗口内再次引雷只受法术公共冷却限制。</summary>
    public sealed class LeiJi : SkillBase
    {
        private const float CallWindowDuration = 25f;
        private const float CastRange = 160f;
        private const float MinimumAiRange = 8f;
        private const float Radius = 3f;
        private const float BaseDamage = 30f;

        public LeiJi()
        {
            SkillID = "LeiJi";
            Type = SPSkillType.Spell;
            Cooldown = 30f;
            ResourceCost = 50f;
            Text = new TextObject("{=ZZZF0043}雷击");
            Description = new TextObject(
                "在指定地点召雷，对3米范围内敌人造成30点基础电击伤害，并受法强与魔抗影响。首次施放消耗50法力、开启25秒引雷窗口及30秒个人冷却；窗口内再次施放不消耗法力、不重置个人冷却，只受法术公共冷却限制。玩家快速施法选择160米内视野中敌人最密集的地点，按住Shift时使用视线指示落点。");
        }

        public override SkillActivationPolicy GetActivationPolicy(Agent caster)
        {
            return SkillRecastWindowState.IsActive(caster, SkillID)
                ? new SkillActivationPolicy(0f, true, 0f)
                : base.GetActivationPolicy(caster);
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || Mission.Current == null)
                return FailActivation("施法者或当前任务不可用。");

            SpellTargetingSystem.Result targeting;
            if (caster.IsPlayerControlled)
            {
                if (!SpellTargetingSystem.TryResolveAreaTarget(
                        caster, CastRange, Radius, out targeting))
                    return FailActivation("视野内没有可用目标。");
            }
            else
            {
                // AI 已在 CheckCondition 检查可见目标；再施法直接取当前目标，
                // 避免每秒重复运行范围选点的全场聚集度搜索。
                Agent target = caster.GetTargetAgent();
                if (!IsValidVictim(caster, target) ||
                    (target.Position - caster.Position).LengthSquared > CastRange * CastRange)
                    return FailActivation("当前目标已不可用。");
                targeting = new SpellTargetingSystem.Result
                {
                    Target = target,
                    Position = target.Position
                };
            }

            int hitCount = StrikeArea(caster, targeting.Position);
            if (hitCount == 0 && !targeting.UsesManualIndicator)
                return FailActivation("目标已离开雷击范围。");

            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component != null && !SkillRecastWindowState.IsActive(caster, SkillID))
                component.StateContainer.AddState(
                    new SkillRecastWindowState(SkillID, CallWindowDuration, caster), caster);

            ShowLightning(targeting.Position);
            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;
            Agent target = caster.GetTargetAgent();
            if (target == null || !target.IsActive() || target.Health <= 0f ||
                !caster.IsEnemyOf(target))
                return false;
            float distanceSquared = (target.Position - caster.Position).LengthSquared;
            return distanceSquared >= MinimumAiRange * MinimumAiRange &&
                distanceSquared <= CastRange * CastRange &&
                RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static int StrikeArea(Agent caster, Vec3 center)
        {
            MBList<Agent> nearby = new MBList<Agent>();
            if (caster.Team != null)
                Mission.Current.GetNearbyEnemyAgents(center.AsVec2, Radius, caster.Team, nearby);
            else
                Mission.Current.GetNearbyAgents(center.AsVec2, Radius, nearby);

            int hitCount = 0;
            float spellPowerCoefficient = MagicDamageSystem.GetSpellPowerCoefficient(caster);
            foreach (Agent target in nearby)
            {
                if (!IsValidVictim(caster, target) ||
                    (target.Position + Vec3.Up - center).LengthSquared > Radius * Radius)
                    continue;
                ApplyDamage(caster, target, spellPowerCoefficient);
                hitCount++;
            }
            return hitCount;
        }

        /// <summary>供“呼唤风暴”共用同一电击伤害与落雷表现，每名敌人只调用一次。</summary>
        public static bool StrikeSingleTarget(
            Agent caster, Agent target, float spellPowerCoefficient, bool showVisual)
        {
            if (!IsValidVictim(caster, target))
                return false;
            ApplyDamage(caster, target, spellPowerCoefficient);
            if (showVisual)
                ShowLightning(target.Position + Vec3.Up);
            return true;
        }

        private static bool IsValidVictim(Agent caster, Agent target)
        {
            return caster != null && target != null && target != caster &&
                target.IsActive() && target.IsHuman && target.Health > 0f &&
                caster.IsEnemyOf(target);
        }

        private static void ApplyDamage(Agent caster, Agent target, float spellPowerCoefficient)
        {
            MagicDamageSystem.Apply(
                caster, target, BaseDamage, spellPowerCoefficient,
                DamageType.ELECTRICITY_DAMAGE, MagicDamageFlags.Area,
                target.Position + Vec3.Up);
        }

        private static void ShowLightning(Vec3 impactPosition)
        {
            SpellProjectileMissionLogic effects = SpellProjectileMissionLogic.GetForCurrentMission();
            if (effects == null)
                return;

            // 原版没有完整的落雷粒子；短寿命火花沿折线排布，后续可替换美术资源。
            for (int i = 0; i < 7; i++)
            {
                float height = 9f * (6 - i) / 6f;
                float side = i == 0 || i == 6 ? 0f : (i % 2 == 0 ? 0.25f : -0.25f);
                effects.SpawnTimedParticle(
                    "psys_game_sparkle_a",
                    impactPosition + new Vec3(side, -side, height), 0.22f);
            }
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition, 0.35f);
        }
    }
}
