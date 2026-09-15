using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 一次召唤调用的通用参数。所有通过召唤管理器执行的召唤技能都会先经过被动修饰器。
    /// </summary>
    public sealed class SummonInvocation
    {
        public Agent Summoner { get; }
        public string SourceSkillId { get; }
        public int Count { get; set; }
        public bool DismissExistingSummons { get; set; }

        public SummonInvocation(Agent summoner, string sourceSkillId, int baseCount)
        {
            if (baseCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(baseCount));

            Summoner = summoner;
            SourceSkillId = sourceSkillId ?? string.Empty;
            Count = baseCount;
        }
    }

    /// <summary>
    /// 召唤类通用被动契约。以后新增召唤技能时无需判断具体被动ID，
    /// 只需通过 SummonManagerMissionLogic 创建并执行召唤调用。
    /// </summary>
    public interface ISummonInvocationModifier
    {
        void ModifySummonInvocation(SummonInvocation invocation);
    }

    /// <summary>
    /// 召唤技能共用的 AI 目标检查。只接受原生 AI 已锁定的当前敌人，不扫描全场；
    /// 各召唤技能只需提供符合自身定位的交战距离。
    /// </summary>
    internal static class SummonSkillAi
    {
        public static bool HasSuitableVisibleTarget(
            Agent caster,
            float minimumDistance,
            float maximumDistance,
            out Agent target)
        {
            target = null;
            if (caster == null || !caster.IsActive() || caster.IsMount || caster.Team == null ||
                Mission.Current == null || SummonManagerMissionLogic.GetForCurrentMission() == null)
                return false;

            Agent currentTarget = caster.GetTargetAgent();
            if (currentTarget == null || currentTarget == caster || !currentTarget.IsHuman ||
                currentTarget.IsMount || !currentTarget.IsActive() || currentTarget.Health <= 0f ||
                !caster.IsEnemyOf(currentTarget))
                return false;

            float distance = (currentTarget.Position - caster.Position).Length;
            if (distance < minimumDistance || distance > maximumDistance)
                return false;

            // 把射线放在廉价的状态和距离判断之后；只有技能确实可能发动时才检查视线。
            if (!RushMovementMissionLogic.HasLineOfSight(caster, currentTarget))
                return false;

            target = currentTarget;
            return true;
        }
    }
}
