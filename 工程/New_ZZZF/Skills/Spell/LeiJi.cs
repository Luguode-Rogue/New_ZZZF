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
            CastSoundEvent = "event:/mission/combat/missile/foley/sling_release";
            Text = new TextObject("{=ZZZF0043}雷击");
            Description = new TextObject(
                "在指定地点召雷，以12根雷柱标示3米范围，对范围内敌人造成30点基础电击伤害，并受法强与魔抗影响。首次施放消耗50法力、开启25秒引雷窗口及30秒个人冷却；窗口内再次施放不消耗法力、不重置个人冷却，只受法术公共冷却限制。玩家快速施法选择160米内视野中敌人最密集的地点，按住Shift时使用视线指示落点。");
        }

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = index == 0 ? new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.Sphere,
                Radius = Radius
            } : default;
            return index == 0;
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
            if (!TryGetDamageArea(caster, 0, out SkillDamageArea damageArea) ||
                !damageArea.IsValid)
                return FailActivation("雷击范围参数无效。");
            if (caster.IsPlayerControlled)
            {
                if (!SpellTargetingSystem.TryResolveAreaTarget(
                        caster, CastRange, damageArea.Radius, out targeting))
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

            int hitCount = StrikeArea(caster, targeting.Position, damageArea.Radius);
            if (hitCount == 0 && !targeting.UsesManualIndicator)
                return FailActivation("目标已离开雷击范围。");

            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component != null && !SkillRecastWindowState.IsActive(caster, SkillID))
                component.StateContainer.AddState(
                    new SkillRecastWindowState(SkillID, CallWindowDuration, caster), caster);

            ShowLightningArea(targeting.Position, damageArea.Radius);
            MagicShoot.PlayReleasePresentation(caster, playWeaponSound: false);
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

        private static int StrikeArea(Agent caster, Vec3 center, float radius)
        {
            MBList<Agent> nearby = new MBList<Agent>();
            if (caster.Team != null)
                Mission.Current.GetNearbyEnemyAgents(center.AsVec2, radius, caster.Team, nearby);
            else
                Mission.Current.GetNearbyAgents(center.AsVec2, radius, nearby);

            int hitCount = 0;
            float spellPowerCoefficient = MagicDamageSystem.GetSpellPowerCoefficient(caster);
            foreach (Agent target in nearby)
            {
                if (!IsValidVictim(caster, target) ||
                    (target.Position + Vec3.Up - center).LengthSquared > radius * radius)
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

        private static void ShowLightningArea(Vec3 center, float radius)
        {
            SpellProjectileMissionLogic effects = SpellProjectileMissionLogic.GetForCurrentMission();
            if (effects == null)
                return;

            // 原生短促的弩炮发射声暂代远距离落雷冲击；整次施法只播一声。
            effects.QueueOneShotSound("event:/mission/siege/ballista/fire", center);
            // 中心一根、实际伤害半径边缘十一根；仅雷击范围法术使用此布局。
            ShowLightningColumn(effects, center);
            for (int i = 0; i < 11; i++)
            {
                double angle = 2.0 * System.Math.PI * i / 11;
                Vec3 position = center + new Vec3(
                    (float)System.Math.Cos(angle) * radius,
                    (float)System.Math.Sin(angle) * radius, 0f);
                ShowLightningColumn(effects, position);
            }
        }

        private static void ShowLightning(Vec3 impactPosition)
        {
            SpellProjectileMissionLogic effects = SpellProjectileMissionLogic.GetForCurrentMission();
            if (effects == null)
                return;

            ShowLightningColumn(effects, impactPosition);
        }

        private static void ShowLightningColumn(SpellProjectileMissionLogic effects, Vec3 impactPosition)
        {
            // 原版没有完整的落雷粒子；沿原有七个折点插值到三十五处火花。
            for (int i = 0; i < 35; i++)
            {
                float path = 6f * i / 34f;
                int from = (int)path;
                int to = from < 6 ? from + 1 : 6;
                float fromSide = from == 0 || from == 6 ? 0f : (from % 2 == 0 ? 0.25f : -0.25f);
                float toSide = to == 0 || to == 6 ? 0f : (to % 2 == 0 ? 0.25f : -0.25f);
                float side = fromSide + (toSide - fromSide) * (path - from);
                float height = 9f * (1f - i / 34f);
                effects.SpawnTimedParticle(
                    "psys_game_sparkle_a",
                    impactPosition + new Vec3(side, -side, height), 0.22f, 15f);
            }
            // 落点由一处扩为五处，避免五层粒子完全重叠。
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition, 0.35f, 15f);
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition + new Vec3(0.5f, 0f, 0f), 0.35f, 15f);
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition + new Vec3(-0.5f, 0f, 0f), 0.35f, 15f);
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition + new Vec3(0f, 0.5f, 0f), 0.35f, 15f);
            effects.SpawnTimedParticle("psys_campfire_sparks", impactPosition + new Vec3(0f, -0.5f, 0f), 0.35f, 15f);
        }
    }
}
