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
                "快速使用时在视野内敌人最密集的位置召唤一个与施法者阶级相同的帝国近战士兵；按住Shift时改为在视线指示落点召唤。召唤物持续30秒，并暂时占用等同其阶级的生命上限。消耗耐力：35。冷却时间：30秒。");
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
            if (!SpellTargetingSystem.TryResolveAreaTarget(
                    agent, MaximumAiCastDistance, 5f,
                    out SpellTargetingSystem.Result targeting))
                return FailActivation("视野内没有可用目标。");
            Vec3 summonPosition = targeting.Position;
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
