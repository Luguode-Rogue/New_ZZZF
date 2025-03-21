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
        public float Duration { get; set; }  // 剩余时间（秒）
        public Agent SourceAgent { get; set; } // 状态来源（可选）
        public Agent TargetAgent { get; set; } // 状态目标（可选）

        public abstract void OnApply(Agent agent);    // 状态生效时触发
        public abstract void OnUpdate(Agent agent, float dt); // 每帧更新
        public abstract void OnRemove(Agent agent);   // 状态移除时触发
    }

    /// <summary>
    /// 状态容器（管理Agent所有状态）
    /// </summary>
    public class AgentBuffContainer
    {
        private List<AgentBuff> _activeStates = new List<AgentBuff>();

        public void AddState(AgentBuff state)
        {
            _activeStates.Add(state);
            state.OnApply(state.TargetAgent);
        }

        public void UpdateStates(Agent agent, float dt)
        {
            for (int i = _activeStates.Count - 1; i >= 0; i--)
            {
                AgentBuff state = _activeStates[i];
                state.Duration -= dt;
                state.Duration = TaleWorlds.Library.MathF.Clamp(state.Duration, 0f, 100f);
                state.OnUpdate(agent, dt);

                if (state.Duration <= 0)
                {
                    state.OnRemove(agent);
                    _activeStates.RemoveAt(i);
                }
            }
        }

        public void RemoveState(string stateId)
        {
            AgentBuff state = _activeStates.Find(s => s.StateId == stateId);
            if (state != null)
            {
                state.OnRemove(state.SourceAgent);
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
                    "Fire"
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
                    "Fire"
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
}