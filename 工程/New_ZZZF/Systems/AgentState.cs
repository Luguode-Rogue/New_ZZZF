using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;
using static TaleWorlds.MountAndBlade.Source.Objects.Siege.AgentPathNavMeshChecker;

namespace New_ZZZF
{
    /// <summary>
  /// 状态基类（Buff/Debuff/DOT等）
  /// </summary>
    public abstract class AgentBuff
    {
        public string StateId { get; protected set; }
        /// <summary>
        /// 剩余时间（秒）
        /// </summary>
        private float _duration;
        public float Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                if (value > MaximumDuration) MaximumDuration = value;
            }
        }
        public float MaximumDuration { get; private set; }
        /// <summary>
        /// 状态来源agent（可选）
        /// </summary>
        public Agent SourceAgent { get; set; } 
        /// <summary>
        /// 状态目标agent（可选）
        /// </summary>
        public Agent TargetAgent { get; set; } 

        public abstract void OnApply(Agent agent);    // 状态生效时触发
        public abstract void OnUpdate(Agent agent, float dt); // 每帧更新
        /// <summary>
        /// 先自动进行移除buff，再触发此方法
        /// </summary>
        /// <param name="agent"></param>
        public abstract void OnRemove(Agent agent);   // 状态移除时触发
    }

    /// <summary>
    /// 状态容器（管理Agent所有状态）
    /// </summary>
    public class AgentBuffContainer
    {
        private List<AgentBuff> _activeStates = new List<AgentBuff>();
        public event Action TimersChanged;

        public bool TryGetSkillDuration(SkillBase skill, out float remaining, out float maximum)
        {
            remaining = 0f;
            maximum = 0f;
            if (skill == null) return false;
            foreach (AgentBuff state in _activeStates)
            {
                if (state.Duration <= 0f || !skill.IsHudDurationState(state.StateId) ||
                    state.Duration <= remaining)
                    continue;
                remaining = state.Duration;
                maximum = state.MaximumDuration;
            }
            return remaining > 0f;
        }

        public bool HasState(string stateId)
        {
            foreach (var state in _activeStates)
            {
                if (state.StateId !=null&& state.StateId.ToString().Equals(stateId))
                { return true; }
            }
            return false;
        }
        public void AddState(AgentBuff state)
        {
            AddState(state, null);
        }

        /// <summary>
        /// 添加状态。owner 为该容器所属的 Agent，用于在调用方未设置
        /// <see cref="AgentBuff.TargetAgent"/> 时兜底，避免 OnApply 收到 null。
        /// </summary>
        public void AddState(AgentBuff state, Agent owner)
        {
            if (state == null) return;

            // 兜底：调用方忘记设置 TargetAgent 时使用 owner
            if (state.TargetAgent == null) state.TargetAgent = owner;

            _activeStates.Add(state);
            TimersChanged?.Invoke();

            if (state.TargetAgent == null) return; // 无有效目标则只登记不触发特效

            try { state.OnApply(state.TargetAgent); }
            catch (Exception e) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }
        }

        /// <summary>
        /// 添加一个不可叠加状态。同 StateId 的旧状态会先正常移除，再由新状态
        /// 替换，适用于灼烧等需要刷新持续时间和伤害快照的效果。
        /// </summary>
        public void AddOrReplaceState(AgentBuff state, Agent owner)
        {
            if (state == null) return;
            Agent target = owner ?? state.TargetAgent;
            for (int i = _activeStates.Count - 1; i >= 0; i--)
            {
                AgentBuff existing = _activeStates[i];
                if (!string.Equals(existing.StateId, state.StateId, StringComparison.Ordinal))
                    continue;
                _activeStates.RemoveAt(i);
                TimersChanged?.Invoke();
                if (target != null)
                {
                    try { existing.OnRemove(target); }
                    catch (Exception e) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }
                }
            }
            AddState(state, owner);
        }

        public void UpdateStates(Agent agent, float dt)
        {
            for (int i = _activeStates.Count - 1; i >= 0; i--)
            {
                AgentBuff state = _activeStates[i];
                state.Duration = TaleWorlds.Library.MathF.Clamp(state.Duration - dt, 0f, 100f);

                Agent target = agent ?? state.TargetAgent;
                if (target == null || !target.IsActive())
                {
                    // 目标失效也要执行清理：天启等状态持有独立的场景特效实体。
                    _activeStates.RemoveAt(i);
                    TimersChanged?.Invoke();
                    try { state.OnRemove(target); }
                    catch (Exception e) { /* 状态清理失败不能中断其他状态更新。 */ }
                    continue;
                }

                try { state.OnUpdate(target, dt); }
                catch (Exception e) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }

                if (state.Duration <= 0)
                {
                    _activeStates.RemoveAt(i);
                    TimersChanged?.Invoke();
                    try { state.OnRemove(target); }
                    catch (Exception e) { /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */; }
                }
            }
        }
        public AgentBuff GetState(string stateId)
        {
            AgentBuff state = _activeStates.Find(s => s.StateId == stateId);
            if (state != null)
            {
                return state;
            }
            return null;
        }
        /// <summary>
        /// 移除某个状态。
        /// </summary>
        /// <param name="stateId"></param>
        /// <param name="用于OnRemove函数的agent"></param>
        public void RemoveState(string stateId,Agent agent)
        {
            AgentBuff state = _activeStates.Find(s => s.StateId == stateId);
            if (state != null)
            {
                state.OnRemove(agent);
                _activeStates.Remove(state);
                TimersChanged?.Invoke();
            }
        }
    }
    public class PeriodicMagicDamageState : AgentBuff
    {
        private readonly float _baseDamagePerTick;
        private readonly float _spellPowerCoefficient;
        private readonly float _tickInterval;
        private readonly DamageType _damageType;
        private float _resolvedDamagePerTick;
        private float _timeSinceLastTick;

        public PeriodicMagicDamageState(
            string stateId,
            float duration,
            float tickInterval,
            float baseDamagePerTick,
            float spellPowerCoefficient,
            DamageType damageType,
            Agent source)
        {
            StateId = stateId;
            Duration = MathF.Max(0f, duration);
            _tickInterval = MathF.Max(0.05f, tickInterval);
            _baseDamagePerTick = MathF.Max(0f, baseDamagePerTick);
            _spellPowerCoefficient = MathF.Max(0f, spellPowerCoefficient);
            _damageType = damageType;
            SourceAgent = source;
            _timeSinceLastTick = 0f;
        }

        public override void OnApply(Agent agent)
        {
            // DoT 在挂载时快照魔抗与攻击方增伤；临时免疫在每跳扣血前检查。
            MagicDamageResult result = MagicDamageSystem.Calculate(
                agent, _baseDamagePerTick, _spellPowerCoefficient, _damageType, true);
            _resolvedDamagePerTick = result.FinalDamage *
                MagicDamageSystem.GetOutgoingDamageMultiplier(SourceAgent);
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                return;

            _timeSinceLastTick += dt;
            int elapsedTicks = (int)MathF.Floor(_timeSinceLastTick / _tickInterval);
            if (elapsedTicks <= 0)
                return;

            _timeSinceLastTick -= elapsedTicks * _tickInterval;
            if (agent == null || !agent.IsActive() || _resolvedDamagePerTick <= 0f)
                return;

            // 一次结算积累的全部跳数，避免异常大 dt 导致主线程执行大量循环。
            MagicDamageSystem.ApplyResolvedPeriodicDamage(
                agent, _resolvedDamagePerTick * elapsedTicks);
        }

        public override void OnRemove(Agent agent)
        {
        }
    }

    public class BurningState : PeriodicMagicDamageState
    {
        public BurningState(float duration, float baseDamagePerTick, Agent source)
            : this(
                duration,
                baseDamagePerTick,
                source,
                New_ZZZF.Systems.MagicDamageSystem.GetSpellPowerCoefficient(source))
        {
        }

        public BurningState(
            float duration,
            float baseDamagePerTick,
            Agent source,
            float spellPowerCoefficient)
            : base(
                "fire_burning",
                duration,
                1f,
                baseDamagePerTick,
                spellPowerCoefficient,
                DamageType.FIRE_DAMAGE,
                source)
        {
        }

        public override void OnApply(Agent agent)
        {
            base.OnApply(agent);
            agent.PlayParticleEffect("fire_burning");
        }

        public override void OnRemove(Agent agent)
        {
            agent.StopParticleEffect("fire_burning");
        }
    }
    public class du : AgentBuff
    {
        private readonly float _baseDamagePerSecond;
        private float _resolvedDamagePerSecond;
        private float _timeSinceLastTick;
        public du(float duration, float dps, Agent source)
        {
            StateId = "du";
            Duration = duration;
            _baseDamagePerSecond = dps;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
            _resolvedDamagePerSecond = MagicDamageSystem.Calculate(
                agent,
                _baseDamagePerSecond,
                MagicDamageSystem.GetSpellPowerCoefficient(SourceAgent),
                DamageType.FIRE_DAMAGE, true).FinalDamage *
                MagicDamageSystem.GetOutgoingDamageMultiplier(SourceAgent);
            // 触发燃烧特效
            agent.PlayParticleEffect("du");
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            // 累积伤害时间
            _timeSinceLastTick += dt;

            // 每秒触发一次伤害
            if (_timeSinceLastTick >= 1f)
            {
                if (agent != null && agent.IsActive() && _resolvedDamagePerSecond > 0f)
                    MagicDamageSystem.ApplyResolvedPeriodicDamage(
                        agent, _resolvedDamagePerSecond);

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            // 移除特效
            agent.StopParticleEffect("du");
        }
    }

    /// <summary>
    /// 冰冻减速状态：生效时降低目标移动速度，到期恢复。
    /// 复用模组既有的 agent.AgentDrivenProperties.MaxSpeedMultiplier 覆盖（安全容错）。
    /// </summary>
    public class FreezeState : AgentBuff
    {
        private readonly float _slowFactor; // 0.5 = 减速50%
        private float _originalMul = -1f;
        public FreezeState(float duration, float slowFactor, Agent source)
        {
            StateId = "forge_freeze";
            Duration = duration;
            _slowFactor = TaleWorlds.Library.MathF.Clamp(slowFactor, 0.1f, 0.95f);
            SourceAgent = source;
        }

        public override void OnApply(Agent agent)
        {
            agent.PlayParticleEffect("zzzf_freeze");
            try
            {
                float cur = agent.AgentDrivenProperties.MaxSpeedMultiplier;
                _originalMul = cur;
                agent.AgentDrivenProperties.MaxSpeedMultiplier = cur * (1f - _slowFactor);
            }
            catch { }
        }

        public override void OnUpdate(Agent agent, float dt) { }

        public override void OnRemove(Agent agent)
        {
            agent.StopParticleEffect("zzzf_freeze");
            try
            {
                if (_originalMul > 0f)
                    agent.AgentDrivenProperties.MaxSpeedMultiplier = _originalMul;
            }
            catch { }
        }
    }

    /// <summary>中毒状态：持续造成火焰/毒素伤害（DOT）。</summary>
    public class WeakenState : AgentBuff
    {
        private readonly float _baseDamagePerSecond;
        private float _resolvedDamagePerSecond;
        private float _timeSinceLastTick;
        public WeakenState(float duration, float dps, Agent source)
        {
            StateId = "forge_poison";
            Duration = duration;
            _baseDamagePerSecond = dps;
            SourceAgent = source;
            _timeSinceLastTick = 0;
        }

        public override void OnApply(Agent agent)
        {
            _resolvedDamagePerSecond = MagicDamageSystem.Calculate(
                agent,
                _baseDamagePerSecond,
                MagicDamageSystem.GetSpellPowerCoefficient(SourceAgent),
                DamageType.TOXIN_DAMAGE, true).FinalDamage *
                MagicDamageSystem.GetOutgoingDamageMultiplier(SourceAgent);
            agent.PlayParticleEffect("du");
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            _timeSinceLastTick += dt;
            if (_timeSinceLastTick >= 1f)
            {
                if (agent != null && agent.IsActive() && _resolvedDamagePerSecond > 0f)
                    MagicDamageSystem.ApplyResolvedPeriodicDamage(
                        agent, _resolvedDamagePerSecond);
                _timeSinceLastTick -= 1f;
            }
        }

        public override void OnRemove(Agent agent) => agent.StopParticleEffect("du");
    }

    /// <summary>治疗状态：持续回复生命值（HOT）。</summary>
    public class HealState : AgentBuff
    {
        private readonly Agent _source;
        private float _timeSinceLastTick;
        public HealState(float duration, Agent source)
        {
            StateId = "forge_heal";
            Duration = duration;
            _source = source;
            _timeSinceLastTick = 0;
        }

        public override void OnApply(Agent agent) => agent.PlayParticleEffect("zzzf_heal");

        public override void OnUpdate(Agent agent, float dt)
        {
            _timeSinceLastTick += dt;
            if (_timeSinceLastTick >= 1f)
            {
                float heal = 12f;
                agent.Health = TaleWorlds.Library.MathF.Clamp(agent.Health + heal, 0f, agent.HealthLimit);
                _timeSinceLastTick -= 1f;
            }
        }

        public override void OnRemove(Agent agent) => agent.StopParticleEffect("zzzf_heal");
    }

}
