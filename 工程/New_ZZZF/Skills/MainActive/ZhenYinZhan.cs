using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal sealed class ZhenYinZhan : SkillBase
    {
        private const float Range = 10f;
        private const float Duration = 45f;
        private readonly MBList<Agent> _candidates = new MBList<Agent>();
        public ZhenYinZhan()
        {
            SkillID = "ZhenYinZhan";
            Type = SPSkillType.MainActive;
            Cooldown = 90f;
            ResourceCost = 60f;
            Text = new TextObject("{=ZZZF0072}ZhenYinZhan");
            Description = new TextObject("{=ZZZF0073}45秒内每次近战攻击释放向前扩散的白色弧形剑气，范围10米，速度30米/秒。每道剑气对每个敌人命中一次，按原版武器伤害、增益和护甲计算后取一半。消耗60耐力，冷却90秒。");
            Difficulty = null;
        }
        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            area = index == 0 ? new SkillDamageArea { Shape = SkillDamageAreaShape.Sphere, Radius = Range } : default;
            return index == 0;
        }
        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || !ArcWeaponNativeHit.TryCapture(caster, out _) ||
                Script.GetActiveComponents(caster)?.StateContainer.HasState("ZhenYinZhanBuff") == true ||
                Mission.Current == null) return false;
            _candidates.Clear();
            if (caster.Team != null) Mission.Current.GetNearbyEnemyAgents(caster.Position.AsVec2, Range, caster.Team, _candidates);
            else Mission.Current.GetNearbyAgents(caster.Position.AsVec2, Range, _candidates);
            int count = 0;
            foreach (Agent enemy in _candidates) {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman || enemy.Health <= 0f ||
                    !caster.IsEnemyOf(enemy) || SkillTargetProtection.IsProtected(enemy) ||
                    !RushMovementMissionLogic.HasLineOfSight(caster, enemy)) continue;
                if (++count >= 2) return true;
                if (enemy == caster.GetTargetAgent() && (enemy.Position - caster.Position).LengthSquared <= 36f) return true;
            }
            return false;
        }
        public override bool Activate(Agent caster)
        {
            if (!ArcWeaponNativeHit.TryCapture(caster, out _)) return FailActivation("需要手持近战武器。");
            var component = Script.GetActiveComponents(caster);
            if (component == null || ArcWaveMissionLogic.Current == null) return FailActivation("当前任务无法启用真银斩。");
            AgentBuff existing = component.StateContainer.GetState("ZhenYinZhanBuff");
            if (existing != null) existing.Duration = System.Math.Max(existing.Duration, Duration);
            else component.StateContainer.AddState(new ZhenYinZhanBuff(Duration, caster) { TargetAgent = caster });
            return true;
        }
        public sealed class ZhenYinZhanBuff : AgentBuff
        {
            public override string BattleHudName => "真银斩";
            public ZhenYinZhanBuff(float duration, Agent source)
            {
                StateId = "ZhenYinZhanBuff"; Duration = duration; SourceAgent = source;
            }
            public override void OnApply(Agent agent) { ArcWaveMissionLogic.Current?.Observe(agent); }
            public override void OnUpdate(Agent agent, float dt) { }
            public override void OnRemove(Agent agent) { ArcWaveMissionLogic.Current?.StopObserving(agent); }
        }
    }
}
