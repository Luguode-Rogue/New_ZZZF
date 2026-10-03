using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.Systems;

namespace New_ZZZF
{
    internal class DaDiJianTa : SkillBase
    {
        internal const float Radius = 10f;
        internal const string DescriptionText = "跃起践踏，聚拢10米内敌人并控制5秒。基础魔法伤害取当前武器熟练度÷10×2与武器面板伤害÷2的较高值。普通骑兵强制落马，英雄不强制落马。每影响一人回复5耐力。耐力20，冷却20秒。";
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public DaDiJianTa() {
            SkillID = "DaDiJianTa"; Type = SPSkillType.MainActive; ResourceCost = 20f; Cooldown = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0012}大地践踏");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0055}" + DescriptionText);
        }
        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area) {
            area = index == 0 ? new SkillDamageArea { Shape = SkillDamageAreaShape.Sphere, Radius = Radius } : default;
            return index == 0;
        }
        internal static bool CanAffect(Agent caster, Agent enemy) => enemy != null && enemy.IsActive() &&
            enemy.IsHuman && enemy.Health > 0f && caster.IsEnemyOf(enemy) && !SkillTargetProtection.IsProtected(enemy) &&
            (enemy.Position - caster.Position).LengthSquared <= Radius * Radius && enemy.GetComponent<AgentSkillComponent>() != null;
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster?.Mission == null || caster.Team == null) return false;
            _nearby.Clear(); caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, Radius, caster.Team, _nearby);
            int count = 0;
            foreach (Agent enemy in _nearby) if (CanAffect(caster, enemy)) count++;
            return count >= 10 || (count > 0 && caster.Health <= caster.HealthLimit * 0.5f);
        }
        internal static float GetDamage(Agent caster) {
            var usage = caster.WieldedWeapon.CurrentUsageItem;
            if (usage == null) return 0f;
            int proficiency = usage.RelevantSkill == null ? 0 : MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkillForWeapon(caster, usage);
            return Math.Max(proficiency / 10f * 2f, Math.Max(usage.SwingDamage, usage.ThrustDamage) / 2f);
        }
        public override bool Activate(Agent caster) {
            var component = caster?.GetComponent<AgentSkillComponent>();
            if (component == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者不可用。");
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Radius, _nearby);
            bool valid = false;
            foreach (Agent enemy in _nearby) if (CanAffect(caster, enemy)) { valid = true; break; }
            if (!valid) return FailActivation("附近没有有效敌人。");
            if (GetDamage(caster) <= 0f) return FailActivation("当前武器不能提供伤害。");
            component.StateContainer.AddOrReplaceState(new Cast(caster, GetDamage(caster)), caster);
            return true;
        }
        private sealed class Cast : AgentBuff {
            private readonly float _damage;
            private float _elapsed;
            private bool _released;
            private readonly MBList<Agent> _targets = new MBList<Agent>();
            public override bool IsMarker => true;
            public Cast(Agent caster, float damage) { StateId = "DaDiJianTaCast"; SourceAgent = caster; Duration = 0.8f; _damage = damage; }
            public override void OnApply(Agent agent) {
                Agent mount = agent.MountAgent;
                if (mount != null && mount.IsActive()) {
                    // 同一动作索引分别映射坐骑的立起动画和骑手的受惊配套动画。
                    var rear = ActionIndexCache.Create("act_horse_rear_damaged");
                    mount.SetActionChannel(0, rear, true);
                    agent.SetActionChannel(0, rear, true);
                } else agent.SetActionChannel(0, ActionIndexCache.Create("act_jump"), true);
            }
            public override void OnUpdate(Agent agent, float dt) {
                _elapsed += dt;
                if (_released || _elapsed < 0.45f) return;
                _released = true;
                if (!agent.HasMount)
                    agent.SetActionChannel(0, ActionIndexCache.Create("act_jump_end_hard"), true);
                agent.Mission.GetNearbyAgents(agent.Position.AsVec2, Radius, _targets);
                Vec2 forward = agent.LookDirection.AsVec2;
                if (forward.LengthSquared < 0.001f) forward = new Vec2(0f, 1f);
                forward.Normalize();
                Vec3 center = agent.Position + new Vec3(forward.x * 2f, forward.y * 2f, 0f);
                int count = 0;
                float power = MagicDamageSystem.GetSpellPowerCoefficient(agent);
                foreach (Agent enemy in _targets) {
                    if (!CanAffect(agent, enemy)) continue;
                    float angle = count * 2.399963f;
                    float radius = 0.75f + 0.65f * (float)Math.Sqrt(count);
                    Vec3 desired = center + new Vec3((float)Math.Cos(angle) * radius, (float)Math.Sin(angle) * radius, 0f);
                    var flags = MagicDamageFlags.Area;
                    if (enemy.HasMount && !enemy.IsHero) flags |= MagicDamageFlags.ForceDismount;
                    MagicDamageSystem.Apply(agent, enemy, _damage, power, DamageType.None, flags);
                    if (enemy.IsActive() && enemy.Health > 0f) {
                        var movement = RushMovementMissionLogic.Current;
                        if (movement != null && movement.TryGetSafeLandingPosition(desired, out Vec3 landing)) {
                            Agent mount = enemy.MountAgent;
                            if (mount == null) enemy.TeleportToPosition(landing);
                            else if (enemy.IsHero) {
                                Vec3 offset = enemy.Position - mount.Position;
                                mount.TeleportToPosition(landing); enemy.TeleportToPosition(landing + offset);
                            }
                        }
                        var states = enemy.GetComponent<AgentSkillComponent>().StateContainer;
                        var old = states.GetState("DaDiJianTaBuffToEnemy") as DaDiJianTaBuffToEnemy;
                        if (old != null) old.Duration = Math.Max(old.Duration, 5f);
                        else {
                            var control = new DaDiJianTaBuffToEnemy(5f, agent);
                            if (!enemy.IsHero && enemy.HasMount && movement != null && movement.TryGetSafeLandingPosition(desired, out Vec3 dismountedLanding))
                                control.SetDismountedLanding(dismountedLanding);
                            states.AddState(control, enemy);
                        }
                    }
                    count++;
                }
                agent.GetComponent<AgentSkillComponent>()?.ChangeStamina(count * 5f);
                try { SoundManager.StartOneShotEvent("event:/mission/combat/blunt/footstep", agent.Position); } catch (Exception) { }
                var effects = SpellProjectileMissionLogic.GetForCurrentMission();
                for (int i = 0; i < 16; i++) {
                    float angle = i * (float)Math.PI / 8f;
                    effects?.SpawnTimedParticle("psys_campfire_sparks", agent.Position + new Vec3((float)Math.Cos(angle) * 2f, (float)Math.Sin(angle) * 2f, 0.1f), 0.6f, 1f);
                }
            }
            public override void OnRemove(Agent agent) { }
        }
    }
    public class DaDiJianTaBuffToEnemy : AgentBuff
    {
        private bool _paused;
        private Agent _mount;
        private bool _mountPaused;
        private bool _freezeStarted;
        private Vec3 _position;
        private Vec3 _mountPosition;
        private float _elapsed;
        private Vec3? _dismountedLanding;
        internal void SetDismountedLanding(Vec3 landing) { _dismountedLanding = landing; }
        public override string BattleHudName => "大地践踏";
        public DaDiJianTaBuffToEnemy(float duration, Agent source) { StateId = "DaDiJianTaBuffToEnemy"; Duration = duration; SourceAgent = source; }
        public override void OnApply(Agent agent) {
            _paused = agent.IsPaused;
            _mount = agent.MountAgent;
            _mountPaused = _mount?.IsPaused ?? false;
            _position = agent.Position;
            if (_mount != null) _mountPosition = _mount.Position;
            if (agent.IsAIControlled) agent.SetIsAIPaused(true);
            if (_mount != null && _mount.IsAIControlled) _mount.SetIsAIPaused(true);
        }
        public override void OnUpdate(Agent agent, float dt) {
            if (SkillTargetProtection.IsProtected(agent)) { Duration = 0f; return; }
            _elapsed += dt;
            // 让原生落马反应先执行；骑手落地后才冻结踉跄动作。
            if (!_freezeStarted && (_elapsed >= 0.35f && (agent.IsHero || !agent.HasMount))) {
                _freezeStarted = true;
                if (_dismountedLanding.HasValue) agent.TeleportToPosition(_dismountedLanding.Value);
                _position = agent.Position;
                agent.SetActionChannel(0, ActionIndexCache.act_stagger_backward, true);
                agent.SetActionChannel(1, ActionIndexCache.act_stagger_backward, true);
            }
            if (agent.IsAIControlled) agent.SetIsAIPaused(true);
            if (_freezeStarted) {
                agent.SetCurrentActionSpeed(0, 0f); agent.SetCurrentActionSpeed(1, 0f);
                if (agent.MountAgent == null && (agent.Position - _position).LengthSquared > 0.01f) agent.TeleportToPosition(_position);
            }
            if (_mount != null && _mount.IsActive() && agent.MountAgent == _mount) {
                if (_mount.IsAIControlled) _mount.SetIsAIPaused(true);
                if ((_mount.Position - _mountPosition).LengthSquared > 0.01f) _mount.TeleportToPosition(_mountPosition);
            }
        }
        public override void OnRemove(Agent agent) {
            if (agent != null && agent.IsActive()) {
                if (_freezeStarted) { agent.SetCurrentActionSpeed(0, 1f); agent.SetCurrentActionSpeed(1, 1f); }
                if (agent.IsAIControlled && !_paused) agent.SetIsAIPaused(false);
            }
            if (_mount != null && _mount.IsActive() && _mount.IsAIControlled && !_mountPaused) _mount.SetIsAIPaused(false);
        }
    }
}