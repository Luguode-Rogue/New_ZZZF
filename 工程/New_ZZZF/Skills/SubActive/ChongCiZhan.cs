using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 冲刺斩：徒步时导航接敌并请求一次原生右砍；骑乘时沿初始方向穿阵，
    /// 技能期间的原生马匹冲撞会强制击倒敌方步兵。
    /// </summary>
    internal sealed class ChongCiZhan : SkillBase
    {
        private const float RushDuration = 5f;
        private const float MinimumFootRushDistance = 6f;
        private const float MinimumMountedRushDistance = 10f;
        private const float MinimumMountedForwardDot = 0.35f;

        public ChongCiZhan()
        {
            SkillID = "ChongCiZhan";
            Type = SPSkillType.SubActive;
            Cooldown = 10f;
            ResourceCost = 10f;
            Text = new TextObject("{=ZZZF_CHONG_CI_ZHAN_NAME}冲刺斩");
            Description = new TextObject(
                "{=ZZZF_CHONG_CI_ZHAN_DESC}冲向选中的敌人。徒步时高速接近目标，并在进入攻击距离后发动一次右侧挥砍；骑乘时沿冲锋方向穿过敌阵，撞倒接触到的敌方步兵。敌方步行枪兵的有效突刺可以中断骑乘冲锋。持续时间：5秒。消耗耐力：10。冷却时间：10秒。");
            Difficulty = null;
        }

        public override bool Activate(Agent agent)
        {
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null)
                return FailActivation("当前任务未加载强制移动管理器，请确认技能系统已启用。");
            if (movement.IsRushing(agent))
                return FailActivation("当前已经处于强制移动状态。");

            Agent target = FindTarget(agent);
            if (target == null)
                return FailActivation("没有找到可见的敌方目标。");

            bool mounted = agent.MountAgent != null;
            if (!mounted && !HasUsableMeleeWeapon(agent))
                return FailActivation("徒步使用冲刺斩时需要手持近战武器。");

            RushMovementOptions options = new RushMovementOptions
            {
                Duration = RushDuration,
                // 冲刺期间把人物（骑乘时连同坐骑）的最高移动速度提高到基础值的 500%。
                SpeedLimit = 5f,
                SpeedLimitIsMultiplier = true,
                RequireLineOfSight = !mounted,
                SightCheckInterval = 0.2f,
                LostSightGrace = 0.75f,
                AllowMounted = mounted,
                IsChongCiZhan = true,
                TriggerRightAttackOnEnd = !mounted,
                UseSafeKinematicMovement = !mounted,
                KinematicSpeed = 30.2f,
                StopDistance = mounted ? 1f : GetAttackStopDistance(agent)
            };

            string failureReason;
            bool started;
            if (mounted)
            {
                Vec2 direction = target.Position.AsVec2 - agent.Position.AsVec2;
                started = movement.TryDirectionalCharge(agent, target, direction, options, out failureReason);
            }
            else
            {
                started = movement.TryRushToAgent(agent, target, options, out failureReason);
            }

            if (!started)
                return FailActivation(failureReason ?? "无法建立冲刺路径。");

            agent.YellAfterDelay(0f);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;

            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null || movement.IsRushing(caster))
                return false;

            // AI 只对原生战斗系统已经选定的当前目标施放，不在条件检查中遍历全场抢目标。
            Agent target = caster.GetTargetAgent();
            if (!IsValidEnemy(caster, target))
                return false;

            Vec2 toTarget = target.Position.AsVec2 - caster.Position.AsVec2;
            float distance = toTarget.Length;
            bool mounted = caster.MountAgent != null;
            if (!mounted)
            {
                if (!HasUsableMeleeWeapon(caster))
                    return false;

                // 已经进入普通交战距离时不浪费技能；武器越长，保留的起跑距离也略大。
                float minimumDistance = MathF.Max(
                    MinimumFootRushDistance,
                    GetAttackStopDistance(caster) + 3f);
                if (distance < minimumDistance)
                    return false;
            }
            else
            {
                if (!caster.MountAgent.IsActive() || distance < MinimumMountedRushDistance)
                    return false;

                // 骑兵只会向当前朝向前方的目标发动，避免瞬间掉头或横向穿阵。
                Vec2 forward = caster.LookDirection.AsVec2;
                if (forward.LengthSquared < 0.01f)
                    forward = caster.Frame.rotation.f.AsVec2;
                if (forward.LengthSquared < 0.01f ||
                    Vec2.DotProduct(forward.Normalized(), toTarget.Normalized()) < MinimumMountedForwardDot)
                    return false;
            }

            // 距离和方向均合格后才做射线，降低大量低级士兵同时检查时的开销。
            return RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static Agent FindTarget(Agent caster)
        {
            if (caster == null || Mission.Current == null)
                return null;

            Agent preferred = caster.IsMainAgent
                ? Script.FindTargetedLockableAgent(caster)
                : caster.GetTargetAgent();
            if (IsValidVisibleEnemy(caster, preferred))
                return preferred;

            foreach (Agent candidate in Mission.Current.Agents
                         .Where(candidate => candidate != null && candidate != caster &&
                                             candidate.IsHuman && !candidate.IsMount &&
                                             candidate.IsActive() && candidate.Health > 0f &&
                                             caster.IsEnemyOf(candidate))
                         .OrderBy(candidate => (candidate.Position - caster.Position).LengthSquared))
            {
                if (RushMovementMissionLogic.HasLineOfSight(caster, candidate))
                    return candidate;
            }
            return null;
        }

        private static bool IsValidVisibleEnemy(Agent caster, Agent target)
        {
            return IsValidEnemy(caster, target) && RushMovementMissionLogic.HasLineOfSight(caster, target);
        }

        private static bool IsValidEnemy(Agent caster, Agent target)
        {
            return target != null && target != caster && target.IsHuman && !target.IsMount &&
                   target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target);
        }

        private static bool HasUsableMeleeWeapon(Agent agent)
        {
            MissionWeapon weapon = agent.WieldedWeapon;
            return !weapon.IsEmpty && weapon.CurrentUsageItem != null && weapon.CurrentUsageItem.IsMeleeWeapon;
        }

        private static float GetAttackStopDistance(Agent agent)
        {
            WeaponComponentData usage = agent.WieldedWeapon.CurrentUsageItem;
            if (usage == null)
                return 1.5f;
            return MathF.Clamp(usage.GetRealWeaponLength() * 0.01f + 0.45f, 1.25f, 2.35f);
        }
    }
}
