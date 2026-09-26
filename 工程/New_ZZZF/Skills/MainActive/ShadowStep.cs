using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    // 近距离直接闪现，远距离接近后闪现；背刺、回血和短暂踉跄只在闪现成功后触发。
    internal class ShadowStep : SkillBase
    {//
        public ShadowStep()
        {
            SkillID = "ShadowStep";      // 必须唯一
            Type = SPSkillType.MainActive;    // 类型必须明确
            Cooldown = 15;             // 冷却时间（秒）
            ResourceCost = 40f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0011}ShadowStep");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF_SHADOW_STEP_DESC}闪现至目标背后并恢复全部生命，令目标短暂踉跄，随后发动一次原生右砍。5.75秒内对目标造成双倍伤害，受到该目标的伤害减半。消耗耐力：40。冷却时间：15秒。");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool IsHudDurationState(string stateId)
        {
            return string.Equals(stateId, "暗影步增伤", StringComparison.Ordinal);
        }
        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return FailActivation("施法者不可用。");
            if (UseShadowStep(agent))
            { return true; }
            return false;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || !AiBattleOrderGate.AllowsAggressiveSkill(caster))
                return false;
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            Agent target = SelectTarget(caster);
            return (movement == null || !movement.IsRushing(caster)) &&
                   IsValidTarget(caster, target);
        }

        private static bool IsValidTarget(Agent caster, Agent target)
        {
            return target != null && target != caster && target.IsHuman && !target.IsMount &&
                   target.MountAgent == null &&
                   target.IsActive() && target.Health > 0f &&
                   caster.IsEnemyOf(target);
        }

        private static bool TryGetLandingPosition(
            RushMovementMissionLogic movement, Agent target, out Vec3 landing)
        {
            Vec3 desired = target.Position - target.LookDirection * 2f;
            // 导航网格只能作为优先落点，不能让场景中无导航的敌人使技能失效。
            if (movement != null && movement.TryGetSafeLandingPosition(desired, out landing))
                return true;
            landing = desired;
            landing.z = target.Position.z;
            return landing.IsValid;
        }

        private static Agent SelectTarget(Agent caster)
        {
            Agent current = caster.GetTargetAgent();
            if (caster.IsAIControlled && IsValidTarget(caster, current))
                return current;

            Vec3 lookPoint = caster.IsAIControlled ? Vec3.Invalid : Script.AgentLookPos(caster);
            Agent best = null;
            float bestScore = float.MaxValue;
            foreach (Agent candidate in Mission.Current.Agents)
            {
                if (!IsValidTarget(caster, candidate))
                    continue;
                float distance = (candidate.Position - caster.Position).Length;
                bool nearLook = lookPoint.IsValid &&
                    candidate.GetEyeGlobalPosition().Distance(lookPoint) <= 10f;
                WeaponComponentData weapon = candidate.WieldedWeapon.CurrentUsageItem;
                float score = (nearLook ? 0f : 100f) +
                              (weapon != null && weapon.IsRangedWeapon ? 0f : 40f) + distance * 0.01f;
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
            return best ?? (IsValidTarget(caster, current) ? current : null);
        }
        public bool UseShadowStep(Agent agent)
        {
            Agent vimagent = SelectTarget(agent);
            if (!IsValidTarget(agent, vimagent))
                return FailActivation("无有效目标。");
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement != null && movement.IsRushing(agent))
                return FailActivation("当前无法发动暗影步位移。");
            if (movement == null)
                return FinishShadowStep(agent, vimagent);
            // 近目标直接闪现；远目标冲刺结束后闪现，不要求在 0.75 秒内跑完全程。
            if ((vimagent.GetEyeGlobalPosition() - agent.GetEyeGlobalPosition()).Length > 10)
            {
                agent.SetTargetAgent(vimagent);
                // 旧的玩家锁定反射保留但停用；新的结束回调会安全恢复目标锁定。
#if false
                MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                MissionMainAgentController missionMainAgentController = missionScreen.Mission.GetMissionBehavior<MissionMainAgentController>();

                // 获取 LockedAgent 属性的信息
                PropertyInfo lockedAgentProperty = typeof(MissionMainAgentController).GetProperty("LockedAgent", BindingFlags.Public | BindingFlags.Instance);
                if (lockedAgentProperty == null)
                {
                    throw new Exception("LockedAgent 属性未找到");
                }

                // 获取 LockedAgent 属性的私有 setter 方法
                MethodInfo setMethod = lockedAgentProperty.GetSetMethod(nonPublic: true);
                if (setMethod == null)
                {
                    throw new Exception("LockedAgent 的私有 setter 方法未找到");
                }

                // 调用私有 setter 方法
                setMethod.Invoke(missionMainAgentController, new object[] { vimagent });
#endif
                RushMovementOptions movementOptions = new RushMovementOptions
                {
                    Duration = 0.75f,
                    StopDistance = 1.5f,
                    SpeedLimit = 30.2f,
                    SpeedLimitIsMultiplier = false,
                    AllowMounted = true,
                    OnEnded = (mover, target, reason) =>
                    {
                        if (mover != null && mover.IsActive() && IsValidTarget(mover, target))
                            FinishShadowStep(mover, target);
                    }
                };
                if (!movement.TryRushToAgent(agent, vimagent, movementOptions, out string failureReason))
                    return FinishShadowStep(agent, vimagent)
                        ? true : FailActivation(failureReason ?? "无法接近暗影步目标。");
                return true;
            }
            else
            {
                if (!FinishShadowStep(agent, vimagent))
                    return FailActivation("暗影步落点已经失效。");
                // 旧重复锁定反射保留但停用；FinishShadowStep 已完成同一职责并带空值保护。
#if false
                MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                MissionMainAgentController missionMainAgentController= missionScreen.Mission.GetMissionBehavior<MissionMainAgentController>();
                
                // 获取 LockedAgent 属性的信息
                PropertyInfo lockedAgentProperty = typeof(MissionMainAgentController).GetProperty("LockedAgent", BindingFlags.Public | BindingFlags.Instance);
                if (lockedAgentProperty == null)
                {
                    throw new Exception("LockedAgent 属性未找到");
                }

                // 获取 LockedAgent 属性的私有 setter 方法
                MethodInfo setMethod = lockedAgentProperty.GetSetMethod(nonPublic: true);
                if (setMethod == null)
                {
                    throw new Exception("LockedAgent 的私有 setter 方法未找到");
                }

                // 调用私有 setter 方法
                setMethod.Invoke(missionMainAgentController, new object[] { vimagent });
#endif

                return true;
            }
            return false;
        }

        private static bool FinishShadowStep(Agent agent, Agent target)
        {
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (!IsValidTarget(agent, target) ||
                !TryGetLandingPosition(movement, target, out Vec3 landing))
                return false;
            agent.TeleportToPosition(landing);
            agent.Health = agent.HealthLimit;
            agent.SetTargetAgent(target);
            AgentSkillComponent casterComponent = agent.GetComponent<AgentSkillComponent>();
            casterComponent?.StateContainer.AddOrReplaceState(
                new 暗影步增伤(5.75f, agent, target), agent);
            AgentSkillComponent targetComponent = target.GetComponent<AgentSkillComponent>();
            targetComponent?.StateContainer.AddOrReplaceState(
                new ShadowStepStagger(0.75f, agent), target);
            if (movement != null)
                movement.QueueRightAttack(agent, target);
            else
                agent.MovementFlags |= Agent.MovementControlFlag.AttackRight;
            if (!agent.IsMainAgent)
                return true;

            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            MissionMainAgentController controller = missionScreen?.Mission?
                .GetMissionBehavior<MissionMainAgentController>();
            PropertyInfo lockedAgentProperty = typeof(MissionMainAgentController)
                .GetProperty("LockedAgent", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo setMethod = lockedAgentProperty?.GetSetMethod(true);
            if (controller != null && setMethod != null)
                setMethod.Invoke(controller, new object[] { target });
            return true;
        }

    }
    public class 暗影步增伤 : AgentBuff
    {
        public Agent MarkedTarget { get; }
        public 暗影步增伤(float duration, Agent source, Agent markedTarget)
        {
            StateId = "暗影步增伤";
            Duration = duration;
            SourceAgent = source;
            MarkedTarget = markedTarget;
        }

        public override void OnApply(Agent agent)
        {
        }

        public override void OnUpdate(Agent agent, float dt)
        {
        }

        public override void OnRemove(Agent agent)
        {
        }
    }

    /// <summary>闪现后的短暂控制：原生踉跄动作并暂停 NPC 战斗输入。</summary>
    internal sealed class ShadowStepStagger : AgentBuff
    {
        private bool _wasPaused;

        public ShadowStepStagger(float duration, Agent source)
        {
            StateId = "ShadowStepStagger";
            Duration = duration;
            SourceAgent = source;
        }

        public override void OnApply(Agent agent)
        {
            _wasPaused = agent.IsPaused;
            agent.SetActionChannel(0, ActionIndexCache.act_stagger_backward, true);
            agent.SetActionChannel(1, ActionIndexCache.act_stagger_backward, true);
            if (agent.IsAIControlled)
                agent.SetIsAIPaused(true);
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            if (agent.IsAIControlled)
                agent.SetIsAIPaused(true);
        }

        public override void OnRemove(Agent agent)
        {
            if (agent.IsAIControlled && !_wasPaused)
                agent.SetIsAIPaused(false);
        }
    }
}
