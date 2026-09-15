using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    /// <summary>在施法者身边召唤帝国远程兵；六阶使用保留坐骑的帝国骑射手。</summary>
    public sealed class SheShouFuJi : SkillBase
    {
        private const float SpawnBesideDistance = 2.5f;
        private const float MinimumAiCastDistance = 10f;
        private const float MaximumAiCastDistance = 55f;

        private static readonly string[] EmpireRangedTroopIds =
        {
            null,
            "imperial_archer",
            "imperial_archer",
            "imperial_trained_archer",
            "imperial_veteran_archer",
            "imperial_palatine_guard",
            "bucellarii"
        };

        public SheShouFuJi()
        {
            SkillID = "SheShouFuJi";
            Type = SPSkillType.MainActive;
            Cooldown = 30f;
            ResourceCost = 35f;
            Difficulty = null;
            Text = new TextObject("射手伏击");
            Description = new TextObject(
                "在自己身边召唤一个对应阶级的帝国远程士兵，持续30秒；六阶召唤帝国骑射手。每个召唤物暂时占用等同其阶级的生命上限。消耗耐力：35。冷却时间：30秒。");
        }

        public override bool Activate(Agent agent)
        {
            if (agent == null)
                return FailActivation("施法者为空。");
            if (!agent.IsActive())
                return FailActivation("施法者已失效或不在战场中。");
            if (Game.Current?.ObjectManager == null)
                return FailActivation("游戏对象管理器尚未初始化。");

            SummonManagerMissionLogic manager = SummonManagerMissionLogic.GetForCurrentMission();
            if (manager == null)
                return FailActivation("当前任务未加载召唤物管理器，请确认技能系统已启用。");

            int tier = SummonManagerMissionLogic.ResolveSummonTier(agent);
            string troopId = EmpireRangedTroopIds[tier];
            CharacterObject troop = Game.Current.ObjectManager.GetObject<CharacterObject>(troopId);
            if (troop == null)
                return FailActivation("未找到阶级 " + tier + " 对应的帝国远程兵种：" + troopId + "。");

            Vec2 forward = agent.LookDirection.AsVec2;
            if (forward.LengthSquared < 0.001f)
                forward = new Vec2(0f, 1f);
            forward.Normalize();
            Vec2 right = new Vec2(forward.y, -forward.x);
            Vec3 spawnCenter = agent.Position + new Vec3(
                right.x * SpawnBesideDistance,
                right.y * SpawnBesideDistance,
                0f);

            int summonedCount;
            string failureReason;
            if (!manager.TrySummonTroopBatch(
                agent,
                spawnCenter,
                troop,
                tier,
                1,
                SkillID,
                tier == 6,
                out summonedCount,
                out failureReason))
                return FailActivation(failureReason);

            if (agent.IsMainAgent)
                InformationManager.DisplayMessage(new InformationMessage(
                    "[射手伏击] 发动成功，本次召唤 " + summonedCount + " 个单位。"));
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || Game.Current?.ObjectManager == null)
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
