using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class ChaoFeng : SkillBase
    {
        private const float BuffDuration = 30f;
        private const float AuraRadius = 30f;
        private const float AllyProtectionRadius = 10f;
        private const int FrontlineEnemyCount = 6;
        private static readonly EquipmentIndex[] WeaponSlots =
        {
            EquipmentIndex.WeaponItemBeginSlot, EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2, EquipmentIndex.Weapon3
        };
        private static readonly string[] MaleYells =
        {
            "event:/voice/combat/male/01/yell", "event:/voice/combat/male/02/yell",
            "event:/voice/combat/male/03/yell", "event:/voice/combat/male/04/yell"
        };
        private static readonly string[] FemaleYells =
        {
            "event:/voice/combat/female/01/yell", "event:/voice/combat/female/02/yell",
            "event:/voice/combat/female/03/yell", "event:/voice/combat/female/04/yell"
        };

        // SkillFactory 共用技能实例；AI 轮询顺序执行，复用列表避免每次判断分配。
        private readonly MBList<Agent> _nearbyEnemies = new MBList<Agent>();
        private readonly MBList<Agent> _nearbyAllies = new MBList<Agent>();

        public ChaoFeng()
        {
            SkillID = "ChaoFeng";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0023}ChaoFeng");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0024}嘲讽附近敌方单位，并持续大幅回复自身血量。受到嘲讽的单位会持续靠近施法者。消耗耐力：20。持续时间：30秒。冷却时间：60秒。");
        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            AgentAuraMissionLogic auras = AgentAuraMissionLogic.GetForCurrentMission();
            if (component == null || auras == null || !agent.IsActive() ||
                component.StateContainer.HasState("ChaoFengBuffApplyToSelf"))
                return FailActivation("嘲讽光环已生效或技能组件不可用。");

            AgentAuraRequest request = new AgentAuraRequest
            {
                Caster = agent,
                Radius = AuraRadius,
                Duration = BuffDuration,
                ScanInterval = 0.5f,
                EnemiesOnly = true,
                CanAffect = CanTaunt,
                OnFirstContact = ApplyTaunt,
                OnStay = RestoreTauntIfMissing,
                OnAuraEndedForTarget = RemoveTaunt
            };
            if (!auras.TryStart(request, out int auraId))
                return FailActivation("嘲讽光环无法启动。");

            component.StateContainer.AddState(new ChaoFengBuffApplyToSelf(BuffDuration, agent, auraId), agent);
            PlayCastPresentation(agent);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;
            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("ChaoFengBuffApplyToSelf"))
                return false;

            float maximumHealth = caster.HealthLimit > 0f ? caster.HealthLimit : component.MaxHP;
            // 自救优先；不要求施放瞬间已有敌人，光环会持续接纳后来进入的目标。
            if (maximumHealth > 0f && caster.Health <= maximumHealth * 0.5f)
                return true;

            CollectNearby(caster, AuraRadius, _nearbyEnemies, true);
            int enemyCount = 0;
            foreach (Agent enemy in _nearbyEnemies)
            {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman || !caster.IsEnemyOf(enemy))
                    continue;
                enemyCount++;
                if (enemyCount >= FrontlineEnemyCount)
                    return true;
            }
            if (enemyCount == 0)
                return false;

            // 保护装备弓或弩的友军；单持投掷武器不属于远程兵种。
            CollectNearby(caster, AllyProtectionRadius, _nearbyAllies, false);
            foreach (Agent ally in _nearbyAllies)
                if (ally != null && ally != caster && ally.IsActive() && ally.IsHuman &&
                    ally.IsFriendOf(caster) && !ally.IsEnemyOf(caster) && HasBowOrCrossbow(ally))
                    return true;
            return false;
        }

        private static void CollectNearby(Agent caster, float radius, MBList<Agent> result,
            bool enemiesOnly)
        {
            result.Clear();
            if (caster?.Mission == null)
                return;
            if (enemiesOnly && caster.Team != null)
                caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, radius,
                    caster.Team, result);
            else
                caster.Mission.GetNearbyAgents(caster.Position.AsVec2, radius, result);
        }

        private static bool HasBowOrCrossbow(Agent ally)
        {
            foreach (EquipmentIndex slot in WeaponSlots)
            {
                ItemObject.ItemTypeEnum? type = ally.Equipment[slot].Item?.Type;
                if (type == ItemObject.ItemTypeEnum.Bow ||
                    type == ItemObject.ItemTypeEnum.Crossbow)
                    return true;
            }
            return false;
        }

        private static bool CanTaunt(Agent caster, Agent target)
        {
            return target.IsHuman && !target.IsHero &&
                target.GetComponent<AgentSkillComponent>() != null && caster.IsEnemyOf(target);
        }

        private static void ApplyTaunt(Agent caster, Agent target, float remaining)
        {
            AgentSkillComponent component = target.GetComponent<AgentSkillComponent>();
            if (component == null)
                return;
            component.StateContainer.AddOrReplaceState(
                new ChaoFengBuffApplyToEnemy(remaining, caster), target);
        }

        private static void RestoreTauntIfMissing(Agent caster, Agent target, float remaining)
        {
            AgentBuffContainer states = target.GetComponent<AgentSkillComponent>()?.StateContainer;
            // 后施放者优先；其嘲讽结束后，仍在旧光环内的单位可以重新受到旧光环影响。
            if (states != null && !states.HasState("ChaoFengBuffApplyToEnemy"))
                ApplyTaunt(caster, target, remaining);
        }

        private static void RemoveTaunt(Agent caster, Agent target)
        {
            if (target == null || !target.IsActive())
                return;
            AgentBuffContainer states = target.GetComponent<AgentSkillComponent>()?.StateContainer;
            if (states?.GetState("ChaoFengBuffApplyToEnemy") is ChaoFengBuffApplyToEnemy taunt &&
                taunt.SourceAgent == caster)
            {
                states.RemoveState("ChaoFengBuffApplyToEnemy", target);
                // RemoveState 先调 OnRemove 再移出容器；移除后再重算一次才能恢复原生攻防值。
                if (!target.IsPlayerControlled)
                    AggressiveAi.AiDefenseThreatAdjustment.RefreshForCurrentTarget(target);
            }
        }

        private static void PlayCastPresentation(Agent agent)
        {
            string[] yells = agent.IsFemale ? FemaleYells : MaleYells;
            try { SoundManager.StartOneShotEvent(yells[MBRandom.RandomInt(yells.Length)], agent.Position); }
            catch (Exception) { /* 音效故障不影响技能。 */ }
            WeaponClass weaponClass = agent.WieldedWeapon.CurrentUsageItem?.WeaponClass ?? WeaponClass.Undefined;
            string action = weaponClass == WeaponClass.Bow || weaponClass == WeaponClass.Crossbow
                ? "act_taunt_cheer_1_bow" : "act_taunt_cheer_1";
            try { agent.SetActionChannel(1, ActionIndexCache.Create(action), true); }
            catch (Exception) { /* 动作资源缺失时保留光环效果。 */ }
        }

        /// <summary>敌人身上的显式嘲讽标记，后续友伤判定可直接查询 StateId。</summary>
        public sealed class ChaoFengBuffApplyToEnemy : AgentBuff
        {
            private float _targetRefreshTimer;

            public ChaoFengBuffApplyToEnemy(float duration, Agent source)
            {
                StateId = "ChaoFengBuffApplyToEnemy";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                RefreshTarget(agent);
                if (agent != null && agent.IsActive() && !agent.IsPlayerControlled)
                {
                    agent.SetHasOnAiInputSetCallback(true);
                    agent.UpdateAgentProperties();
                }
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                _targetRefreshTimer -= dt;
                if (_targetRefreshTimer > 0f)
                    return;
                _targetRefreshTimer = 0.5f;
                if (SourceAgent == null || !SourceAgent.IsActive() || agent == null ||
                    !agent.IsActive() || !agent.IsEnemyOf(SourceAgent))
                {
                    Duration = 0f;
                    return;
                }
                RefreshTarget(agent);
            }

            public override void OnRemove(Agent agent)
            {
                if (agent == null || !agent.IsActive())
                    return;
                if (agent.GetTargetAgent() == SourceAgent)
                {
                    agent.ClearTargetFrame();
                    agent.InvalidateTargetAgent();
                }
                if (!agent.IsPlayerControlled)
                    AggressiveAi.AiDefenseThreatAdjustment.RefreshForCurrentTarget(agent);
            }

            private void RefreshTarget(Agent agent)
            {
                if (agent == null || SourceAgent == null || !SourceAgent.IsActive())
                    return;
                agent.SetTargetAgent(SourceAgent);
                agent.SetTargetPosition(SourceAgent.Position.AsVec2);
            }
        }

        public sealed class ChaoFengBuffApplyToSelf : AgentBuff
        {
            private readonly int _auraId;
            private float _timeSinceLastTick;

            public ChaoFengBuffApplyToSelf(float duration, Agent source, int auraId)
            {
                StateId = "ChaoFengBuffApplyToSelf";
                Duration = duration;
                SourceAgent = source;
                _auraId = auraId;
            }

            public override void OnApply(Agent agent) { }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (agent == null || !agent.IsActive() || dt <= 0f)
                    return;
                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
                if (component == null)
                    return;
                float maximumHealth = agent.HealthLimit > 0f ? agent.HealthLimit : component.MaxHP;
                _timeSinceLastTick += dt;
                while (_timeSinceLastTick >= 1f)
                {
                    _timeSinceLastTick -= 1f;
                    agent.Health = MathF.Min(maximumHealth,
                        agent.Health + MathF.Max(0f, maximumHealth - agent.Health) * 0.5f);
                }
            }

            public override void OnRemove(Agent agent)
            {
                AgentAuraMissionLogic.GetForCurrentMission()?.Stop(_auraId);
            }
        }
    }
}
