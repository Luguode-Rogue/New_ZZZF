using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    internal class JingXia : SkillBase
    {
        internal const float BuffDuration = 20f;
        internal const float Radius = 15f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        internal static readonly Dictionary<Agent, JingXiaBuffToSelf> ActiveAuras = new Dictionary<Agent, JingXiaBuffToSelf>();
        internal static readonly Dictionary<Agent, float> NextEvaluation = new Dictionary<Agent, float>();
        public JingXia() {
            SkillID = "JingXia"; Type = SPSkillType.MainActive; Cooldown = 60f; ResourceCost = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0033}JingXia");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0034}持续20秒，对15米内非英雄敌人判定惊吓：敌人低三阶时50%，同阶27.5%，高三阶时5%，中间线性变化。失败后同一施法者5秒内不再判定。成功后逃跑20秒，受惊时必定满足偷袭条件。光环期间击败敌人将持续时间补满，并获得独立持续10秒的5%惊吓概率加成，可叠加。重叠光环取最高概率。耐力20，冷却60秒。");
        }
        internal static bool IsFrightened(Agent agent) {
            AgentBuff state = agent?.GetComponent<AgentSkillComponent>()?.StateContainer.GetState("JingXiaBuffToEnemy");
            return state != null && state.Duration > 0f;
        }
        internal static float Probability(Agent caster, Agent target) {
            float difference = WeiYa.GetTier(caster) - WeiYa.GetTier(target);
            float bonus = ActiveAuras.TryGetValue(caster, out JingXiaBuffToSelf aura) ? aura.KillBonus : 0f;
            float basic = Math.Max(0f, Math.Min(1f, 0.275f + difference * 0.075f));
            return Math.Min(1f, basic + bonus);
        }
        private static bool CanAffect(Agent caster, Agent target) =>
            SkillTargetProtection.CanSelect(target) && target.IsHuman && !target.IsHero && caster.IsEnemyOf(target) &&
            (caster.Position - target.Position).LengthSquared <= Radius * Radius;
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster.Mission == null || caster.Team == null || ActiveAuras.ContainsKey(caster)) return false;
            _nearby.Clear();
            caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, Radius, caster.Team, _nearby);
            int close = 0;
            foreach (Agent enemy in _nearby) {
                if (!CanAffect(caster, enemy) || Probability(caster, enemy) <= 0f) continue;
                if (caster.HealthLimit > 0f && caster.Health <= caster.HealthLimit * 0.5f) return true;
                if ((enemy.Position - caster.Position).LengthSquared <= 64f && ++close >= 3) return true;
            }
            if (close == 0) return false;
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, 10f, _nearby);
            foreach (Agent ally in _nearby) {
                if (ally == caster || ally == null || !ally.IsActive() || !ally.IsFriendOf(caster)) continue;
                for (int slot = 0; slot < 4; slot++) {
                    ItemObject.ItemTypeEnum? type = ally.Equipment[(EquipmentIndex)slot].Item?.Type;
                    if (type == ItemObject.ItemTypeEnum.Bow || type == ItemObject.ItemTypeEnum.Crossbow) return true;
                }
            }
            return false;
        }
        public override bool Activate(Agent caster) {
            AgentSkillComponent component = caster?.GetComponent<AgentSkillComponent>();
            AgentAuraMissionLogic auras = AgentAuraMissionLogic.GetForCurrentMission();
            if (component == null || auras == null || !caster.IsActive() || ActiveAuras.ContainsKey(caster))
                return FailActivation("惊吓已生效或施法者不可用。");
            var buff = new JingXiaBuffToSelf(BuffDuration, caster);
            ActiveAuras.Add(caster, buff);
            var request = new AgentAuraRequest { Caster = caster, Radius = Radius, Duration = BuffDuration,
                ScanInterval = 0.5f, EnemiesOnly = true, CanAffect = CanAffect, OnFirstContact = Evaluate, OnStay = Evaluate };
            if (!auras.TryStart(request, out int id)) { ActiveAuras.Remove(caster); return FailActivation("惊吓光环无法启动。"); }
            buff.AuraId = id; component.StateContainer.AddState(buff, caster);
            string sex = caster.IsFemale ? "female" : "male";
            try { SoundManager.StartOneShotEvent("event:/voice/combat/" + sex + "/0" + (MBRandom.RandomInt(4) + 1) + "/yell", caster.Position); }
            catch (Exception) { }
            return true;
        }
        private static void Evaluate(Agent caster, Agent target, float remaining) {
            if (IsFrightened(target)) return;
            float now = caster.Mission.CurrentTime;
            if (NextEvaluation.TryGetValue(target, out float next) && now < next) return;
            NextEvaluation[target] = now + 0.45f;
            Agent best = null; float chance = -1f;
            foreach (var pair in ActiveAuras) {
                if (!pair.Key.IsActive() || pair.Value.Duration <= 0f || !CanAffect(pair.Key, target)) continue;
                float candidate = Probability(pair.Key, target);
                if (candidate > chance || candidate == chance && (best == null || pair.Key.Index < best.Index)) { best = pair.Key; chance = candidate; }
            }
            if (best == null || chance <= 0f || !ActiveAuras.TryGetValue(best, out JingXiaBuffToSelf aura)) return;
            caster = best;
            if (aura.FailedUntil.TryGetValue(target, out float retry) && now < retry) return;
            if (MBRandom.RandomFloat >= chance) { aura.FailedUntil[target] = now + 5f; return; }
            target.GetComponent<AgentSkillComponent>()?.StateContainer.AddOrReplaceState(new JingXiaBuffToEnemy(BuffDuration, caster), target);
        }
        internal static void OnEnemyDefeated(Agent victim, Agent killer, AgentState state) {
            if (victim == null || killer == null || !killer.IsActive() || !killer.IsEnemyOf(victim) ||
                (state != AgentState.Killed && state != AgentState.Unconscious) ||
                !ActiveAuras.TryGetValue(killer, out JingXiaBuffToSelf aura) || aura.Duration <= 0f) return;
            aura.Duration = BuffDuration; aura.AddKillBonus(killer.Mission.CurrentTime + 10f);
            AgentAuraMissionLogic.GetForCurrentMission()?.RefreshDuration(aura.AuraId, BuffDuration);
        }
    }
    public class JingXiaBuffToSelf : AgentBuff
    {
        public override bool AffectsOwner => false;
        internal int AuraId;
        internal readonly Dictionary<Agent, float> FailedUntil = new Dictionary<Agent, float>();
        private readonly List<float> _killExpirations = new List<float>();
        private readonly List<Agent> _expiredTargets = new List<Agent>();
        private float _cleanupTimer;
        internal float KillBonus { get {
            float now = SourceAgent.Mission.CurrentTime; int count = 0;
            foreach (float expires in _killExpirations) if (expires > now) count++;
            return count * 0.05f;
        } }
        internal void AddKillBonus(float expires) { _killExpirations.Add(expires); }
        public JingXiaBuffToSelf(float duration, Agent source) { StateId = "JingXiaBuffToSelf"; Duration = duration; SourceAgent = source; }
        public override void OnApply(Agent agent) { }
        public override void OnUpdate(Agent agent, float dt) {
            _cleanupTimer += dt; if (_cleanupTimer < 1f) return; _cleanupTimer = 0f;
            float now = agent.Mission.CurrentTime;
            for (int i = _killExpirations.Count - 1; i >= 0; i--) if (_killExpirations[i] <= now) _killExpirations.RemoveAt(i);
            _expiredTargets.Clear();
            foreach (var pair in FailedUntil) if (pair.Value <= now || !pair.Key.IsActive()) _expiredTargets.Add(pair.Key);
            foreach (Agent target in _expiredTargets) FailedUntil.Remove(target);
        }
        public override void OnRemove(Agent agent) {
            JingXia.ActiveAuras.Remove(SourceAgent); AgentAuraMissionLogic.GetForCurrentMission()?.Stop(AuraId);
            FailedUntil.Clear(); _killExpirations.Clear();
        }
    }
    public class JingXiaBuffToEnemy : AgentBuff
    {
        public override string BattleHudName => "惊吓";
        private float _untilNavigation;
        private Vec3 _lastSourcePosition;
        private bool _ownsNavigation;
        private Vec2 _escapeDirection;
        private FearExclamationVisual _visual;
        public JingXiaBuffToEnemy(float duration, Agent source) {
            StateId = "JingXiaBuffToEnemy"; Duration = duration; SourceAgent = source; _lastSourcePosition = source.Position;
        }
        public override void OnApply(Agent agent) {
            RushMovementMissionLogic.Current?.CancelRush(agent);
            _visual = FearExclamationVisual.Create(agent); agent.SetHasOnAiInputSetCallback(true); Navigate(agent);
        }
        public override void OnUpdate(Agent agent, float dt) {
            if (SkillTargetProtection.IsProtected(agent)) { Duration = 0f; return; }
            _visual?.Update(agent, dt); _untilNavigation -= dt;
            if (_untilNavigation > 0f) return; _untilNavigation = 0.5f; Navigate(agent);
        }
        private void Navigate(Agent agent) {
            if (!agent.IsActive() || agent.IsPlayerControlled) return;
            if (SourceAgent != null && SourceAgent.IsActive()) _lastSourcePosition = SourceAgent.Position;
            Vec2 away = agent.Position.AsVec2 - _lastSourcePosition.AsVec2;
            away = away.LengthSquared > 0.001f ? away.Normalized() : new Vec2(1f, 0f);
            Vec2 position = agent.Position.AsVec2;
            Vec2 goal = agent.FindLongestDirectMoveToPosition(position + away * 10f, true, true, out bool blocked);
            if ((goal - position).LengthSquared < 1f) {
                Vec2 sideways = new Vec2(-away.Y, away.X);
                goal = agent.FindLongestDirectMoveToPosition(position + (away + sideways) * 7f, true, true, out blocked);
                if ((goal - position).LengthSquared < 1f) goal = agent.FindLongestDirectMoveToPosition(position + (away - sideways) * 7f, true, true, out blocked);
            }
            Vec2 direction = goal - position;
            _escapeDirection = direction.LengthSquared > 0.001f ? direction.Normalized() : away;
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive()) {
                // 技能组件只安装在人类上；坐骑必须有自己的输入回调，避免其原生输入覆盖骑手请求。
                if (mount.GetComponent<FearMountInputComponent>() == null)
                    mount.AddComponent(new FearMountInputComponent(mount));
                mount.SetHasOnAiInputSetCallback(true);
            }
            // 原生 AI 继续更新编队与选敌，只在输入回调中暂时覆写移动和攻防。
            // 不启用脚本位置，不关闭编队帧，不关闭自动选敌。
            _ownsNavigation = true;
        }
        internal void ApplyEscapeInput(Agent agent, ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector) {
            if (Duration <= 0f || !_ownsNavigation || agent == null || !agent.IsAIControlled) return;
            movementFlag &= ~(Agent.MovementControlFlag.MoveMask |
                Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask);
            eventFlag &= ~Agent.EventControlFlag.Dismount;
            agent.LookDirection = _escapeDirection.ToVec3();
            agent.SetMaximumSpeedLimit(-1f, false);
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive()) {
                ApplyMountedMovement(mount, ref movementFlag, ref inputVector);
                // 同时提交给实际移动的坐骑；坐骑回调再次覆写时使用同样的方向。
                mount.MovementFlags = movementFlag & Agent.MovementControlFlag.MoveMask;
                mount.MovementInputVector = inputVector;
            } else {
                movementFlag |= Agent.MovementControlFlag.Forward;
                inputVector = new Vec2(0f, 1f);
                agent.SetMovementDirection(_escapeDirection);
            }
        }
        internal void ApplyMountedMovement(Agent mount, ref Agent.MovementControlFlag flags, ref Vec2 input) {
            flags &= ~Agent.MovementControlFlag.MoveMask;
            Vec2 forward = mount.GetMovementDirection();
            if (forward.LengthSquared < 0.001f) forward = mount.Frame.rotation.f.AsVec2;
            forward = forward.Normalized();
            // 原版骑乘输入的 X 是转向，Y 是加速；骑手 LookDirection 并不等于马头方向。
            float lateral = new Vec2(forward.Y, -forward.X).DotProduct(_escapeDirection);
            float alignment = forward.DotProduct(_escapeDirection);
            float steering = alignment < -0.5f ? (lateral >= 0f ? 1f : -1f)
                : Math.Max(-1f, Math.Min(1f, lateral * 2f));
            if (steering > 0.08f) flags |= Agent.MovementControlFlag.TurnRight;
            else if (steering < -0.08f) flags |= Agent.MovementControlFlag.TurnLeft;
            else steering = 0f;
            flags |= Agent.MovementControlFlag.Forward;
            input = new Vec2(steering, 1f);
            mount.SetMaximumSpeedLimit(-1f, false);
        }
        public override void OnRemove(Agent agent) {
            _visual?.Remove(); _visual = null;
            if (agent == null || !agent.IsActive() || !_ownsNavigation) return;
            // 本状态没有开启原生 scripted movement；不能在这里误关其他技能的导航。
            Agent.AIScriptedFrameFlags flagsBefore = agent.GetScriptedFlags();
            agent.SetMaximumSpeedLimit(-1f, false);
            Agent mount = agent.MountAgent;
            if (mount != null && mount.IsActive()) {
                mount.SetMaximumSpeedLimit(-1f, false);
                mount.MovementFlags &= ~Agent.MovementControlFlag.MoveMask;
                mount.MovementInputVector = Vec2.Zero;
            }
            _ownsNavigation = false;
            if (agent.HumanAIComponent != null) {
                agent.HumanAIComponent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Follow);
                agent.HumanAIComponent.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
                if (agent.Formation != null) agent.HumanAIComponent.RefreshBehaviorValues(
                    agent.Formation.GetReadonlyMovementOrderReference().OrderEnum, agent.Formation.ArrangementOrder.OrderEnum);
                agent.HumanAIComponent.SyncBehaviorParamsIfNecessary();
            }
            // 编队帧始终由原生更新维护，结束时不重建编队缓存或改变成员关系。
            agent.ForceAiBehaviorSelection();
            AggressiveAi.AiDefenseThreatAdjustment.RefreshForCurrentTarget(agent);
            if (mount != null && mount.IsActive())
                mount.ForceAiBehaviorSelection();
            if (mount != null && mount.IsActive())
                New_ZZZF.TacticalMap.Diagnostics.TacticalMapLog.Info(
                    "[JingXia] Mounted fear ended: rider=" + agent.Index + ", mount=" + mount.Index +
                    ", control=AIInput, flagsBefore=" + flagsBefore + ", flagsAfter=" + agent.GetScriptedFlags() +
                    ", ai=" + agent.AIStateFlags + ", formationFrame=" + agent.IsFormationFrameEnabled +
                    ", movementLock=" + agent.MovementLockedState);
        }
    }
    // 常驻但无状态：只在当前骑手受惊时覆写坐骑输入，结束后直接让原生 AI 接管。
    internal sealed class FearMountInputComponent : AgentComponent
    {
        internal FearMountInputComponent(Agent agent) : base(agent) { }
        public override void OnAIInputSet(ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector) {
            Agent rider = Agent.RiderAgent;
            if (rider?.GetComponent<AgentSkillComponent>()?.StateContainer.GetState("JingXiaBuffToEnemy")
                is JingXiaBuffToEnemy fear && fear.Duration > 0f) {
                eventFlag &= ~Agent.EventControlFlag.Dismount;
                fear.ApplyMountedMovement(Agent, ref movementFlag, ref inputVector);
            }
        }
    }
}
