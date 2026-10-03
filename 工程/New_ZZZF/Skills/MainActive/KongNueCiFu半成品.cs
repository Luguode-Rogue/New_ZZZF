using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class KongNueCiFu : SkillBase
    {
        internal const float StrengthMultiplier = 1f + 2f / 3f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public KongNueCiFu()
        {
            SkillID = "KongNueCiFu";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 50f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0037}KongNueCiFu");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0038}近战攻击突破普通格挡，贯穿与破格挡不损失动量。近战伤害、攻速及移速+66.7%，护甲+50%，远程能力大幅降低；伤害未携带此技能的友军时仅造成50%伤害且不损失动量。每秒耐力+10、魔力-10；击杀回复损失生命的50%、额外恢复5耐力并延长1秒。累计击杀等级达到888获得1次通用复活。耐力50，持续30秒，冷却60秒。");
        }

        internal static bool IsBlessed(Agent agent) => agent != null && agent.IsActive() &&
            agent.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration("KongNueCiFuBuff") > 0f;

        internal static bool HasMeleeWeapon(Agent agent)
        {
            if (agent == null) return false;
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None) return false;
            WeaponComponentData usage = agent.Equipment[slot].CurrentUsageItem;
            return usage != null && usage.IsMeleeWeapon && !usage.IsRangedWeapon;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.Mission == null || !HasMeleeWeapon(caster)) return false;
            if (caster.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration("KongNueCiFuBuff") > 5f) return false;
            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, 12f, _nearby);
            int enemies = 0;
            bool close = false;
            bool stronger = false;
            foreach (Agent enemy in _nearby)
            {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman || !caster.IsEnemyOf(enemy)) continue;
                float distance = (enemy.Position - caster.Position).LengthSquared;
                if (distance > 144f) continue;
                enemies++;
                close |= distance <= 36f;
                stronger |= enemy.Character != null && caster.Character != null && enemy.Character.Level >= caster.Character.Level;
            }
            return enemies >= 2 || close || enemies > 0 &&
                (stronger || caster.Health <= caster.HealthLimit * 0.7f || caster.IsPerformingAction());
        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (agent == null || !agent.IsActive() || component == null) return FailActivation("施法者不可用。");
            float duration = Math.Max(30f, component.StateContainer.GetLongestStateDuration("KongNueCiFuBuff"));
            component.StateContainer.AddOrReplaceState(new KongNueCiFuBuff(duration, agent), agent);
            try
            {
                string sex = agent.IsFemale ? "female" : "male";
                SoundManager.StartOneShotEvent("event:/voice/combat/" + sex + "/0" + (MBRandom.RandomInt(4) + 1) + "/yell", agent.Position);
            }
            catch (Exception) { }
            return true;
        }

        public class KongNueCiFuBuff : AgentBuff
        {
            private float _timer;
            private KongNueCiFuWhirlVisual _visual;
            public override string BattleHudName => "恐虐赐福";
            public KongNueCiFuBuff(float duration, Agent source)
            {
                StateId = "KongNueCiFuBuff"; Duration = duration; SourceAgent = source;
            }
            public void ExtendAfterKill() { Duration += 1f; }
            public override void OnApply(Agent agent)
            {
                agent.UpdateAgentProperties();
                _visual = KongNueCiFuWhirlVisual.Create(agent, true);
            }
            public override void OnUpdate(Agent agent, float dt)
            {
                _visual?.Update(agent, dt);
                _timer += dt;
                if (_timer < 1f) return;
                int seconds = (int)_timer; _timer -= seconds;
                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
                component?.ChangeStamina(10f * seconds);
                component?.ChangeMana(-10f * seconds);
            }
            public override void OnRemove(Agent agent)
            {
                _visual?.Remove(); _visual = null;
                if (agent != null && agent.IsActive()) agent.UpdateAgentProperties();
            }
        }
    }
}