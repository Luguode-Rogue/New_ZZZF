using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>
    /// 高消耗、短冷却的机动射击副特技。施法者沿后方抛物线跃出，并在最高点
    /// 射击一次；玩家固定以视野反方向位移，AI则远离当前目标。
    /// </summary>
    internal sealed class HouYueSheJi : SkillBase
    {
        private const float LeapDuration = 1.3f;
        private const float LeapDistance = 3.5f;
        private const float LeapHeight = 2.2f;
        private const float NearThreatDistance = 7f;
        private const float LongRangeDistance = 35f;

        public HouYueSheJi()
        {
            SkillID = "HouYueSheJi";
            Type = SPSkillType.SubActive;
            Cooldown = 3f;
            ResourceCost = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0076}后跃射击");
            Difficulty = null;
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0077}向视野后方跃出，并在最高点自动射击。可用于快速位移；空间不足时会撞上障碍。消耗耐力值：20。冷却时间：3秒。");
        }

        // 瞄准属于原生持续动作；允许在瞄准中发动，才能作为射手的紧急脱离技。
        public override bool CanActivateWhilePerformingAction => true;

        public override bool Activate(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return FailActivation("施法者当前不可用。");
            if (agent.MountAgent != null)
                return FailActivation("后跃射击只能在徒步状态下使用。");
            if (!HasUsableRangedWeapon(agent))
                return FailActivation("需要手持远程武器。");

            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null)
                return FailActivation("当前任务未加载强制移动管理器。");

            Agent target = agent.GetTargetAgent();
            Vec2 leapDirection;
            if (agent.IsPlayerControlled)
            {
                // 玩家固定按发动瞬间的视野反方向位移，不依赖锁定目标。
                leapDirection = -agent.LookDirection.AsVec2;
            }
            else if (IsValidEnemy(agent, target))
            {
                leapDirection = agent.Position.AsVec2 - target.Position.AsVec2;
            }
            else
            {
                leapDirection = -agent.LookDirection.AsVec2;
            }

            if (leapDirection.LengthSquared < 0.001f)
                return FailActivation("无法确定后跃方向。");

            ParabolicLeapOptions options = new ParabolicLeapOptions
            {
                Duration = LeapDuration,
                Distance = LeapDistance,
                ApexHeight = LeapHeight,
                FaceTargetDuringLeap = !agent.IsPlayerControlled,
                OnApex = OnLeapApex,
                OnEnded = OnLeapEnded
            };
            if (!movement.TryParabolicLeap(
                    agent, IsValidEnemy(agent, target) ? target : null,
                    leapDirection.Normalized(), options, out string failureReason))
                return FailActivation(failureReason ?? "无法开始后跃。");

            // 保留原技能刻意选用的动作组合；位移和最高点射击由任务级管理器同步。
            agent.SetActionChannel(1, ActionIndexCache.Create("act_ready_bow"), true, (AnimFlags)999UL);
            agent.SetActionChannel(0, ActionIndexCache.Create("act_climb_ladder"), true, (AnimFlags)999UL);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.MountAgent != null ||
                !HasUsableRangedWeapon(caster))
                return false;

            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null || movement.IsRushing(caster))
                return false;

            Agent target = caster.GetTargetAgent();
            if (!IsValidEnemy(caster, target))
                return false;

            float distance = (target.Position - caster.Position).AsVec2.Length;
            if (distance > NearThreatDistance && distance < LongRangeDistance)
                return false;

            bool useHighArc = distance >= LongRangeDistance;
            // 必须有真实弹药且弹道可解才允许 AI 消耗耐力。纯数学/装备检查通过后，
            // 最后才执行本次唯一的场景视线射线。
            return Script.CanShootAtAgentLowCost(caster, target, useHighArc) &&
                   RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static void OnLeapApex(Agent shooter, Agent target)
        {
            if (shooter == null || !shooter.IsActive())
                return;

            bool shotCreated = false;
            // 玩家始终沿自己的视野射击。GetTargetAgent 可能保留一个位于背后的
            // 原生目标，不能用它覆盖玩家的瞄准方向。
            if (shooter.IsPlayerControlled)
            {
                shotCreated = Script.AgentShootTowardsLookDirection(shooter, 0f);
            }
            else if (IsValidEnemy(shooter, target) &&
                RushMovementMissionLogic.HasLineOfSight(shooter, target))
            {
                bool useHighArc = (target.Position - shooter.Position).AsVec2.Length >= LongRangeDistance;
                shotCreated = Script.TryShootAtAgentLowCost(shooter, target, useHighArc);
            }

            if (shotCreated)
                MagicShoot.PlayReleasePresentation(shooter);
        }

        private static void OnLeapEnded(Agent mover, Agent target, RushEndReason reason)
        {
            if (mover == null || !mover.IsActive())
                return;
            mover.SetActionChannel(0, ActionIndexCache.Create("act_none"), true, (AnimFlags)999UL);
            mover.SetActionChannel(1, ActionIndexCache.Create("act_none"), true, (AnimFlags)999UL);
        }

        private static bool HasUsableRangedWeapon(Agent agent)
        {
            if (agent?.Equipment == null)
                return false;
            EquipmentIndex weaponIndex = agent.GetPrimaryWieldedItemIndex();
            if (weaponIndex == EquipmentIndex.None)
                return false;
            MissionWeapon weapon = agent.Equipment[weaponIndex];
            return !weapon.IsEmpty && weapon.CurrentUsageItem != null &&
                   weapon.CurrentUsageItem.IsRangedWeapon;
        }

        private static bool IsValidEnemy(Agent caster, Agent target)
        {
            return caster != null && target != null && target != caster &&
                   target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target);
        }
    }
}
