using New_ZZZF.Systems;
using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class DunWu : SkillBase
    {
        private const float BuffDuration = 10f;
        private const float ManaPerSecond = 10f;
        private const float AiEngagementRangeSquared = 70f * 70f;

        public DunWu()
        {
            SkillID = "DunWu";
            Type = SPSkillType.MainActive;
            Cooldown = 10f;
            ResourceCost = 90f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0025}DunWu");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0026}开启后每秒回蓝+10，同时清除法术cd。被反射法术伤害时，不会受到反射伤害。消耗耐力：90。持续时间：10秒。冷却时间：10秒。");
        }

        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (component == null || !agent.IsActive())
                return FailActivation("施法者或技能组件不可用。");

            component.StateContainer.AddOrReplaceState(new DunWuBuff(BuffDuration, agent), agent);
            component.ClearSpellCooldowns();

            // 只在成功施法时播放一次短手势和声音，持续期间不创建粒子实体。
            try { agent.SetActionChannel(1, ActionIndexCache.Create("act_horse_command_follow"), true); }
            catch (Exception) { /* 动作不可用不影响效果。 */ }
            try
            {
                string voice = agent.IsFemale
                    ? "event:/voice/combat/female/03/yell"
                    : "event:/voice/combat/male/03/yell";
                SoundManager.StartOneShotEvent(voice, agent.Position);
            }
            catch (Exception) { /* 音效不可用不影响效果。 */ }
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster))
                return false;
            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component == null || component._currentStamina < ResourceCost ||
                component.StateContainer.HasState("DunWuBuff"))
                return false;

            // 没有法术时不浪费90耐力；优先用于补充法力或恢复关键法术冷却。
            bool hasSpell = false;
            bool hasUsefulCooldown = false;
            foreach (SkillBase spell in component.SpellSlots)
            {
                if (spell == null || !spell.IsValid || spell.SkillID == "NullSkill" ||
                    (spell.Type != SPSkillType.Spell &&
                     spell.Type != SPSkillType.Spell_CombatArt))
                    continue;
                hasSpell = true;
                if (component.GetSkillCooldownForDisplay(spell) >= 2f)
                    hasUsefulCooldown = true;
            }
            if (!hasSpell || (!hasUsefulCooldown && component._currentMana > 40f))
                return false;

            Agent target = caster.GetTargetAgent();
            if (target != null && target.IsActive() && (caster.IsEnemyOf(target) && !SkillTargetProtection.IsProtected(target)) &&
                (target.Position.AsVec2 - caster.Position.AsVec2).LengthSquared <=
                    AiEngagementRangeSquared)
                return true;
            Formation formation = caster.Formation?.CachedClosestEnemyFormation?.Formation;
            return formation != null &&
                (formation.CachedMedianPosition.AsVec2 - caster.Position.AsVec2)
                    .LengthSquared <= AiEngagementRangeSquared;
        }

        public sealed class DunWuBuff : AgentBuff
        {
            private float _tickTime;

            public DunWuBuff(float duration, Agent source)
            {
                StateId = "DunWuBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent) { }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (dt <= 0f)
                    return;
                _tickTime += dt;
                int ticks = (int)_tickTime;
                if (ticks <= 0)
                    return;
                _tickTime -= ticks;
                agent?.GetComponent<AgentSkillComponent>()?
                    .ChangeMana(ManaPerSecond * ticks);
            }

            public override void OnRemove(Agent agent) { }
        }
    }
}
