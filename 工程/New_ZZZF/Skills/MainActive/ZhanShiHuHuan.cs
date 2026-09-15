using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;

namespace New_ZZZF.Skills
{
    /// <summary>
    /// 在施法者目视落点附近召唤与施法者阶级相同的帝国临时士兵。
    /// 召唤、持续时间和生命上限债务统一交给 SummonManagerMissionLogic 管理。
    /// </summary>
    public sealed class ZhanShiHuHuan : SkillBase
    {
        private const float MinimumAiCastDistance = 5f;
        private const float MaximumAiCastDistance = 25f;

        public ZhanShiHuHuan()
        {
            SkillID = "ZhanShiHuHuan";
            Type = SPSkillType.MainActive;
            Cooldown = 30f;
            ResourceCost = 35f;
            Text = new TextObject("战士呼唤");
            Difficulty = null;
            Description = new TextObject(
                "在目视落点召唤一个与施法者阶级相同的帝国近战士兵，持续30秒。每个召唤物暂时占用等同其阶级的生命上限。消耗耐力：35。冷却时间：30秒。");
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null)
                return FailActivation("施法者为空。");
            if (!agent.IsActive())
                return FailActivation("施法者已失效或不在战场中。");

            SummonManagerMissionLogic manager = SummonManagerMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载召唤物管理器，请确认技能系统已启用。");

            int summonedCount;
            string failureReason;
            Agent aiTarget = !agent.IsMainAgent ? agent.GetTargetAgent() : null;
            Vec3 summonPosition = aiTarget != null && aiTarget.IsActive()
                ? aiTarget.Position
                : Script.AgentLookPos(agent);
            if (!manager.TrySummonBatch(
                agent,
                summonPosition,
                1,
                SkillID,
                out summonedCount,
                out failureReason))
                return FailActivation(failureReason);

            if (agent.IsMainAgent)
                InformationManager.DisplayMessage(new InformationMessage(
                    "[战士呼唤] 发动成功，本次召唤 " + summonedCount + " 个单位。"));
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;

            Agent ignoredTarget;
            return SummonSkillAi.HasSuitableVisibleTarget(
                caster,
                MinimumAiCastDistance,
                MaximumAiCastDistance,
                out ignoredTarget);
        }
    }
}
