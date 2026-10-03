using New_ZZZF.Skills;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class HuoYanTuXi : SkillBase
    {
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public HuoYanTuXi()
        {
            SkillID = "HuoYanTuXi"; Type = SPSkillType.MainActive; Cooldown = 30f; ResourceCost = 35f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0051}HuoYanTuXi");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0052}火焰突袭（无施法距离限制）：步行10米内优先突进，沿途留下持续10秒的火墙，每秒20点基础火伤；远距离、骑乘或无可用路径时改为瞬移。抵达后释放一次红狮子火焰，造成60点基础火伤。不要求目标视线通畅。耐力35，冷却30秒。");
        }
        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area) =>
            new HongShiZiHuoYan().TryGetDamageArea(caster, index, out area);
        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || !AiBattleOrderGate.AllowsAggressiveSkill(caster) || caster.Mission == null ||
                RushMovementMissionLogic.Current == null || RushMovementMissionLogic.Current.IsRushing(caster)) return false;
            foreach (Agent enemy in caster.Mission.Agents)
                if (IsTarget(caster, enemy)) return true;
            return false;
        }
        internal static bool IsTarget(Agent caster, Agent target) => target != null && target.IsActive() && target.IsHuman &&
            target.Health > 0f && caster.IsEnemyOf(target) && !SkillTargetProtection.IsProtected(target);
        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者不可用。");
            if (!SpellTargetingSystem.TryResolveAreaTarget(caster, float.PositiveInfinity, 5f, out SpellTargetingSystem.Result result, false))
                return FailActivation("没有有效突袭目标。");
            Agent target = result.Target;
            if (result.UsesManualIndicator)
            {
                _nearby.Clear(); caster.Mission.GetNearbyAgents(result.Position.AsVec2, 5f, _nearby);
                float best = float.MaxValue;
                foreach (Agent enemy in _nearby)
                    if (IsTarget(caster, enemy) && (enemy.Position - result.Position).LengthSquared < best)
                    { target = enemy; best = (enemy.Position - result.Position).LengthSquared; }
            }
            if (!IsTarget(caster, target)) return FailActivation("指示点附近没有有效敌人。");
            FlameRushMissionLogic logic = caster.Mission.GetMissionBehavior<FlameRushMissionLogic>();
            if (logic == null) return FailActivation("火焰突袭管理器不可用。");
            return logic.Begin(caster, target, out string reason) || FailActivation(reason);
        }
    }
}