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
        public float Duration { get; set; }  
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

            if (state.TargetAgent == null) return; // 无有效目标则只登记不触发特效

            try { state.OnApply(state.TargetAgent); }
            catch (Exception e) { Debug.Print("[New_ZZZF] AddState.OnApply 异常: " + e.Message); }
        }

        public void UpdateStates(Agent agent, float dt)
        {
            for (int i = _activeStates.Count - 1; i >= 0; i--)
            {
                AgentBuff state = _activeStates[i];
                state.Duration -= dt;
                state.Duration = TaleWorlds.Library.MathF.Clamp(state.Duration, 0f, 100f);

                Agent target = agent ?? state.TargetAgent;
                if (target == null || !target.IsActive())
                {
                    // 目标已失效：直接丢弃状态，避免后续 OnUpdate/OnRemove 触发空引用
                    _activeStates.RemoveAt(i);
                    continue;
                }

                try { state.OnUpdate(target, dt); }
                catch (Exception e) { Debug.Print("[New_ZZZF] OnUpdate 异常: " + e.Message); }

                if (state.Duration <= 0)
                {
                    _activeStates.RemoveAt(i);
                    try { state.OnRemove(target); }
                    catch (Exception e) { Debug.Print("[New_ZZZF] OnRemove 异常: " + e.Message); }
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
            }
        }
    }
    public class BurningState : AgentBuff
    {
        private float _damagePerSecond;
        private float _timeSinceLastTick;
        public BurningState(float duration, float dps, Agent source)
        {
            StateId = "fire_burning";
            Duration = duration;
            _damagePerSecond = dps;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }


        public override void OnApply(Agent agent)
        {
            // 触发燃烧特效
            agent.PlayParticleEffect("fire_burning");
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            // 累积伤害时间
            _timeSinceLastTick += dt;

            // 每秒触发一次伤害
            if (_timeSinceLastTick >= 1f)
            {
                // 使用你的伤害计算逻辑
                Script.CalculateFinalMagicDamage(
                    SourceAgent,
                    agent,
                    _damagePerSecond,
                    DamageType.FIRE_DAMAGE
                );

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            // 移除特效
            agent.StopParticleEffect("fire_burning");
        }
    }
    public class du : AgentBuff
    {
        private float _damagePerSecond;
        private float _timeSinceLastTick;
        public du(float duration, float dps, Agent source)
        {
            StateId = "du";
            Duration = duration;
            _damagePerSecond = dps;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
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
                // 使用你的伤害计算逻辑
                Script.CalculateFinalMagicDamage(
                    SourceAgent,
                    agent,
                    _damagePerSecond,
                    DamageType.FIRE_DAMAGE
                );

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
        private readonly float _damagePerSecond;
        private float _timeSinceLastTick;
        public WeakenState(float duration, float dps, Agent source)
        {
            StateId = "forge_poison";
            Duration = duration;
            _damagePerSecond = dps;
            SourceAgent = source;
            _timeSinceLastTick = 0;
        }

        public override void OnApply(Agent agent) => agent.PlayParticleEffect("du");

        public override void OnUpdate(Agent agent, float dt)
        {
            _timeSinceLastTick += dt;
            if (_timeSinceLastTick >= 1f)
            {
                Script.CalculateFinalMagicDamage(SourceAgent, agent, _damagePerSecond, DamageType.TOXIN_DAMAGE);
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