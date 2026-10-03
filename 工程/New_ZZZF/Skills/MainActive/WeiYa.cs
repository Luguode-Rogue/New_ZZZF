using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class WeiYa : SkillBase
    {
        internal const float BuffDuration = 45f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private static readonly string[] Yells = {
            "event:/voice/combat/male/01/yell", "event:/voice/combat/male/02/yell",
            "event:/voice/combat/male/03/yell", "event:/voice/combat/male/04/yell" };

        public WeiYa()
        {
            SkillID = "WeiYa";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 50f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0031}WeiYa");
            Difficulty = null;
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0032}持续压制20米内较弱的敌人。英雄每5级折算一阶，30级后继续成长；高一阶时降低移速30%、射击精度20%、物理伤害20%，加速时间乘1.5。高三阶时效果为高一阶的两倍，之后增长逐渐放缓。施放时标记15米内友军22.5秒，较弱或同阶友军不会自动重复施放。耐力50，持续45秒，冷却60秒。");
        }

        // 沿用召唤的每5级一阶换算；只为威压取消英雄六阶封顶。
        internal static float GetTier(Agent agent)
        {
            if (agent?.Character == null) return 0f;
            if (agent.IsHero) return Math.Max(1, (agent.Character.Level + 4) / 5);
            CharacterObject troop = agent.Character as CharacterObject;
            return Math.Max(1, troop?.Tier ?? agent.Character.GetBattleTier());
        }

        internal static float GetStrength(Agent caster, Agent enemy)
        {
            float difference = GetTier(caster) - GetTier(enemy);
            return difference > 0f ? 3f * difference / (difference + 2f) : 0f;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster?.Mission == null || caster.Team == null) return false;
            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("WeiYaBuffToSelf")) return false;
            AllyMarker marker = component.StateContainer.GetState("WeiYaAllyMarker") as AllyMarker;
            if (marker != null && marker.MaximumTier >= GetTier(caster)) return false;

            _nearby.Clear();
            caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, 20f, caster.Team, _nearby);
            float now = caster.Mission.CurrentTime;
            bool recentlyHit = now - caster.LastRecievedMeleeHitTime <= 5f ||
                now - caster.LastRecievedRangedHitTime <= 5f;
            bool attacking = IsAttacking(caster);
            foreach (Agent enemy in _nearby)
            {
                if (!CanAffect(caster, enemy)) continue;
                float distance = (enemy.Position - caster.Position).LengthSquared;
                // 坚守等命令均可使用：贴身威胁、正在攻击/瞄准或近期受击即视为交战。
                if (distance <= 64f || recentlyHit || attacking || IsAttacking(enemy)) return true;
            }
            return false;
        }

        private static bool IsAttacking(Agent agent)
        {
            Agent.ActionCodeType action = agent.GetCurrentActionType(1);
            return action == Agent.ActionCodeType.ReadyMelee || action == Agent.ActionCodeType.ReleaseMelee ||
                action == Agent.ActionCodeType.ReadyRanged || action == Agent.ActionCodeType.ReleaseRanged ||
                action == Agent.ActionCodeType.ReleaseThrowing;
        }

        private static bool CanAffect(Agent caster, Agent enemy)
        {
            return enemy != null && enemy.IsActive() && enemy.IsHuman && (caster.IsEnemyOf(enemy) && !SkillTargetProtection.IsProtected(enemy)) &&
                (enemy.Position - caster.Position).LengthSquared <= 400f && GetStrength(caster, enemy) > 0f;
        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            AgentAuraMissionLogic auras = AgentAuraMissionLogic.GetForCurrentMission();
            if (component == null || auras == null || !agent.IsActive() ||
                component.StateContainer.HasState("WeiYaBuffToSelf"))
                return FailActivation("威压已生效或施法者不可用。");
            AgentAuraRequest request = new AgentAuraRequest {
                Caster = agent, Radius = 20f, Duration = BuffDuration, ScanInterval = 1f,
                EnemiesOnly = true, CanAffect = CanAffect,
                OnFirstContact = RefreshEnemy, OnStay = RefreshEnemy };
            if (!auras.TryStart(request, out int auraId)) return FailActivation("威压光环无法启动。");
            component.StateContainer.AddState(new WeiYaBuffToSelf(agent, auraId), agent);

            _nearby.Clear();
            agent.Mission.GetNearbyAgents(agent.Position.AsVec2, 15f, _nearby);
            foreach (Agent ally in _nearby)
            {
                if (ally == null || ally == agent || !ally.IsActive() || !ally.IsHuman ||
                    !ally.IsFriendOf(agent) || (ally.Position - agent.Position).LengthSquared > 225f) continue;
                AgentSkillComponent allyComponent = ally.GetComponent<AgentSkillComponent>();
                if (allyComponent == null) continue;
                AllyMarker marker = allyComponent.StateContainer.GetState("WeiYaAllyMarker") as AllyMarker;
                if (marker == null) {
                    marker = new AllyMarker(agent);
                    allyComponent.StateContainer.AddState(marker, ally);
                }
                marker.Refresh(agent, GetTier(agent), BuffDuration * 0.5f);
            }
            string sound = Yells[MBRandom.RandomInt(Yells.Length)];
            if (agent.IsFemale) sound = sound.Replace("/male/", "/female/");
            try { SoundManager.StartOneShotEvent(sound, agent.Position); }
            catch (Exception) { /* 音效缺失不影响技能；不播放动作。 */ }
            return true;
        }

        private static void RefreshEnemy(Agent caster, Agent enemy, float remaining)
        {
            AgentSkillComponent component = enemy.GetComponent<AgentSkillComponent>();
            if (component == null) return;
            WeiYaBuffToEnemy buff = component.StateContainer.GetState("WeiYaBuffToEnemy") as WeiYaBuffToEnemy;
            if (buff == null) {
                buff = new WeiYaBuffToEnemy(caster);
                component.StateContainer.AddState(buff, enemy);
            }
            buff.Refresh(caster, GetStrength(caster, enemy), Math.Min(2f, remaining), enemy);
        }

        internal static float GetAppliedStrength(Agent agent)
        {
            float strength = (agent?.GetComponent<AgentSkillComponent>()?.StateContainer.GetState("WeiYaBuffToEnemy")
                as WeiYaBuffToEnemy)?.Strength ?? 0f;
            return Math.Max(strength, XieEZuZhou.IsCursed(agent) ? 1f : 0f);
        }

        internal static void ApplyDrivenProperties(Agent agent, AgentDrivenProperties properties)
        {
            float strength = GetAppliedStrength(agent);
            if (strength <= 0f) return;
            float speed = 1f - 0.3f * strength;
            properties.MaxSpeedMultiplier *= speed;
            properties.CombatMaxSpeedMultiplier *= speed;
            properties.TopSpeedReachDuration *= 1f + 0.5f * strength;
            float dispersion = 1f / (1f - 0.2f * strength);
            properties.WeaponInaccuracy *= dispersion;
            properties.WeaponMaxMovementAccuracyPenalty *= dispersion;
            properties.WeaponMaxUnsteadyAccuracyPenalty *= dispersion;
            properties.WeaponRotationalAccuracyPenaltyInRadians *= dispersion;
        }

        internal static void ApplyMountDrivenProperties(Agent mount, AgentDrivenProperties properties)
        {
            float strength = GetAppliedStrength(mount?.RiderAgent);
            if (strength <= 0f) return;
            // 原生 UpdateHorseStats 在坐骑 Agent 上计算这两个字段，按骑手的威压状态取值。
            properties.MountSpeed *= 1f - 0.3f * strength;
            properties.MountDashAccelerationMultiplier /= 1f + 0.5f * strength;
        }

        public sealed class WeiYaBuffToSelf : AgentBuff
        {
        public override bool AffectsOwner => false;
            private readonly int _auraId;
            private WeiYaSoulVisual _visual;
            public WeiYaBuffToSelf(Agent source, int auraId) {
                StateId = "WeiYaBuffToSelf"; Duration = BuffDuration; SourceAgent = source; _auraId = auraId;
            }
            public override void OnApply(Agent agent) { _visual = WeiYaSoulVisual.Create(agent); }
            public override void OnUpdate(Agent agent, float dt) { _visual?.Update(agent, dt); }
            public override void OnRemove(Agent agent) {
                AgentAuraMissionLogic.GetForCurrentMission()?.Stop(_auraId);
                _visual?.Remove(); _visual = null;
            }
        }

        private sealed class Contribution
        {
            public float Value;
            public float Remaining;
        }

        // 同一目标只拥有一个状态；各来源独立到期，取最强值，弱光环不覆盖强光环。
        private sealed class Contributions
        {
            private readonly Dictionary<Agent, Contribution> _sources = new Dictionary<Agent, Contribution>();
            private readonly List<Agent> _expired = new List<Agent>();
            public float Maximum { get; private set; }
            public void Refresh(Agent source, float value, float duration) {
                if (!_sources.TryGetValue(source, out Contribution entry)) {
                    entry = new Contribution(); _sources.Add(source, entry);
                }
                entry.Value = value; entry.Remaining = duration;
                Recalculate();
            }
            public void Tick(float dt) {
                _expired.Clear();
                foreach (var pair in _sources) {
                    pair.Value.Remaining -= dt;
                    if (pair.Value.Remaining <= 0f || !pair.Key.IsActive()) _expired.Add(pair.Key);
                }
                foreach (Agent source in _expired) _sources.Remove(source);
                Recalculate();
            }
            private void Recalculate() {
                Maximum = 0f;
                foreach (Contribution entry in _sources.Values) Maximum = Math.Max(Maximum, entry.Value);
            }
        }

        public sealed class WeiYaBuffToEnemy : AgentBuff
        {
            private readonly Contributions _sources = new Contributions();
            private Agent _lastMount;
            public float Strength => _sources.Maximum;
            public WeiYaBuffToEnemy(Agent source) {
                StateId = "WeiYaBuffToEnemy"; Duration = 2f; SourceAgent = source;
            }
            public void Refresh(Agent source, float strength, float duration, Agent target) {
                float previous = Strength;
                _sources.Refresh(source, strength, duration);
                Duration = Math.Max(Duration, duration);
                if (Math.Abs(previous - Strength) > 0.0001f) RefreshProperties(target);
            }
            public override void OnApply(Agent agent) { }
            public override void OnUpdate(Agent agent, float dt) {
                float previous = Strength;
                if (SkillTargetProtection.IsProtected(agent)) { Duration = 0f; return; }
                _sources.Tick(dt);
                if (Math.Abs(previous - Strength) > 0.0001f || _lastMount != agent.MountAgent)
                    RefreshProperties(agent);
                if (Strength <= 0f) Duration = 0f;
            }
            public override void OnRemove(Agent agent) {
                if (agent != null && agent.IsActive()) RefreshProperties(agent);
                if (_lastMount != null && _lastMount.IsActive()) _lastMount.UpdateAgentProperties();
                _lastMount = null;
            }
            private void RefreshProperties(Agent agent) {
                agent.UpdateAgentProperties();
                Agent mount = agent.MountAgent;
                if (_lastMount != null && _lastMount != mount && _lastMount.IsActive())
                    _lastMount.UpdateAgentProperties();
                _lastMount = mount;
                if (mount != null && mount.IsActive()) mount.UpdateAgentProperties();
            }
        }

        private sealed class AllyMarker : AgentBuff
        {
            public override bool IsMarker => true;
            private readonly Contributions _sources = new Contributions();
            public float MaximumTier => _sources.Maximum;
            public AllyMarker(Agent source) {
                StateId = "WeiYaAllyMarker"; Duration = BuffDuration * 0.5f; SourceAgent = source;
            }
            public void Refresh(Agent source, float tier, float duration) {
                _sources.Refresh(source, tier, duration); Duration = Math.Max(Duration, duration);
            }
            public override void OnApply(Agent agent) { }
            public override void OnUpdate(Agent agent, float dt) {
                _sources.Tick(dt); if (MaximumTier <= 0f) Duration = 0f;
            }
            public override void OnRemove(Agent agent) { }
        }
    }
}
