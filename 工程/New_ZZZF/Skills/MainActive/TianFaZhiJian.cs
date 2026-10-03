using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal sealed class TianFaZhiJian : SkillBase
    {
        internal const float Range = 60f, Radius = 5f, BaseDamage = 50f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public TianFaZhiJian()
        {
            SkillID = "TianFaZhiJian"; Type = SPSkillType.MainActive;
            Cooldown = 10f; ResourceCost = 30f; Difficulty = null;
            Text = new TextObject("{=ZZZF0074}TianFaZhiJian");
            Description = new TextObject("{=ZZZF0075}玩家施法距离不限，NPC范围60米；优先选择敌人密集区域，后跃腾空期间免疫伤害，随后瞬移引爆5米范围，造成50点基础无属性法术伤害。骑乘时直接瞬移引爆。受魔法免疫影响。耐力30，冷却10秒。");
            CastSoundEvent = "event:/mission/combat/missile/foley/sling_release";
        }
        internal static bool IsTarget(Agent caster, Agent target) =>
            target != null && target.IsActive() && target.IsHuman && target.Health > 0f &&
            caster.IsEnemyOf(target) && !SkillTargetProtection.IsProtected(target);
        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = index == 0 ? new SkillDamageArea { Shape = SkillDamageAreaShape.Sphere, Radius = Radius } : default;
            return index == 0;
        }
        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || TianFaZhiJianMissionLogic.Current == null ||
                RushMovementMissionLogic.Current == null || RushMovementMissionLogic.Current.IsRushing(caster) ||
                TianFaZhiJianMissionLogic.IsLeaping(caster)) return false;
            // AI 预判只查附近是否有目标；最密集落点仅在真正发动时计算一次。
            _nearby.Clear();
            if (caster.Team != null) caster.Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, Range, caster.Team, _nearby);
            else caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);
            foreach (Agent enemy in _nearby)
                if (IsTarget(caster, enemy) && (enemy.Position - caster.Position).LengthSquared <= Range * Range) return true;
            return false;
        }
        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || !caster.IsHuman) return FailActivation("施法者不可用。");
            var logic = TianFaZhiJianMissionLogic.Current;
            if (logic == null) return FailActivation("天罚之剑管理器未加载。");
            if (!SpellTargetingSystem.TryResolveDenseAreaTarget(caster, caster.IsPlayerControlled ? float.PositiveInfinity : Range, Radius, out var result))
                return FailActivation("指示点周围或视野内没有有效目标区域。");
            return logic.Begin(caster, result.Position, out string reason) || FailActivation(reason);
        }
    }
}
