using System;
using New_ZZZF.Skills;
using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class KuangNuLongXi : SkillBase
    {
        internal const float BuffDuration = 12f;
        internal const float PulseInterval = 0.5f;
        private const string BuffId = "KuangNuLongXiBuff";
        private readonly HongShiZiHuoYan _flame = new HongShiZiHuoYan();
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        internal const string DescriptionText = "持续向前喷射火焰12秒，每0.5秒对火焰范围内敌人造成60点基础火焰爆炸伤害。喷射路径长10米，沿途扩散3米，可转向扫射。重施刷新较长持续时间，不叠加喷射。耐力80，冷却20秒。";
        public KuangNuLongXi() {
            SkillID = "KuangNuLongXi"; Type = SPSkillType.MainActive; ResourceCost = 80f; Cooldown = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0064}狂怒龙息");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0065}" + DescriptionText);
        }
        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area) =>
            _flame.TryGetDamageArea(caster, index, out area);
        private bool IsStrong(Agent caster, Agent enemy) => enemy.IsHero || WeiYa.GetTier(enemy) >= WeiYa.GetTier(caster);
        private bool HasSuitableTargets(Agent caster, Vec3 facing) {
            int count = 0;
            foreach (Agent enemy in _nearby) {
                if (!_flame.CanHitFromDirection(caster, enemy, facing)) continue;
                if (IsStrong(caster, enemy) || ++count >= 3) return true;
            }
            return false;
        }
        private bool TryGetAiFacing(Agent caster, out Vec3 facing) {
            facing = caster.LookDirection;
            _nearby.Clear(); caster.Mission.GetNearbyAgents(caster.Position.AsVec2, 13f, _nearby);
            if (HasSuitableTargets(caster, facing)) return true;
            foreach (Agent enemy in _nearby) {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman || !caster.IsEnemyOf(enemy) ||
                    SkillTargetProtection.IsProtected(enemy)) continue;
                Vec3 direction = (enemy.Position - caster.Position).AsVec2.ToVec3();
                if (direction.LengthSquared < 0.001f) continue;
                direction.Normalize();
                if (HasSuitableTargets(caster, direction)) { facing = direction; return true; }
            }
            return false;
        }
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster?.Mission == null ||
                caster.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration(BuffId) > 0f) return false;
            return TryGetAiFacing(caster, out _);
        }
        public override bool Activate(Agent caster) {
            var component = caster?.GetComponent<AgentSkillComponent>();
            if (component == null || !caster.IsActive() || caster.Mission?.Scene == null) return FailActivation("施法者不可用。");
            if (caster.IsAIControlled && !caster.IsMainAgent) {
                if (!TryGetAiFacing(caster, out Vec3 facing)) return FailActivation("没有合适的龙息目标。");
                caster.LookDirection = facing;
            }
            var existing = component.StateContainer.GetState(BuffId) as KuangNuLongXiBuff;
            if (existing != null) existing.Duration = Math.Max(existing.Duration, BuffDuration);
            else component.StateContainer.AddState(new KuangNuLongXiBuff(BuffDuration, caster), caster);
            return true;
        }
        public class KuangNuLongXiBuff : AgentBuff
        {
            private readonly HongShiZiHuoYan _flame = new HongShiZiHuoYan();
            private readonly float _spellPower;
            private float _elapsed;
            private KuangNuLongXiVisual _visual;
            public override string BattleHudName => "狂怒龙息";
            public KuangNuLongXiBuff(float duration, Agent source) {
                StateId = BuffId; Duration = duration; SourceAgent = source;
                _spellPower = MagicDamageSystem.GetSpellPowerCoefficient(source);
            }
            public override void OnApply(Agent agent) {
                _visual = KuangNuLongXiVisual.Create(agent);
                try {
                    SoundManager.StartOneShotEvent("event:/voice/combat/" + (agent.IsFemale ? "female" : "male") + "/01/yell", agent.Position);
                } catch (Exception) { /* 音效缺失不影响喷射。 */ }
            }
            public override void OnUpdate(Agent agent, float dt) {
                if (agent == null || !agent.IsActive() || agent.Health <= 0f) { Duration = 0f; return; }
                _visual?.Update(agent);
                _elapsed += dt;
                if (_elapsed < PulseInterval) return;
                // 保留时间相位，但每帧至多喷射一次，避免长帧集中补发伤害。
                _elapsed %= PulseInterval;
                _flame.ReleaseContinuousDamage(agent, _spellPower);
            }
            public override void OnRemove(Agent agent) { _visual?.Remove(); _visual = null; }
        }
    }
}