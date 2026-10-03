using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    internal static class PhysicalHitBuffRules
    {
        /// <summary>无视比例限制在0..1，返回本次命中的有效护甲，不修改目标本体属性。</summary>
        public static float GetEffectiveArmor(float armor, float ignoreRatio)
        {
            if (float.IsNaN(armor) || float.IsInfinity(armor)) return 0f;
            if (float.IsNaN(ignoreRatio) || float.IsInfinity(ignoreRatio)) ignoreRatio = 0f;
            return Math.Max(0f, armor) * (1f - MathF.Clamp(ignoreRatio, 0f, 1f));
        }
        internal static float GetArmorIgnoreRatio(Agent attacker) =>
            attacker?.GetComponent<AgentSkillComponent>()?.StateContainer.GetLongestStateDuration("NaGouCiFuBuff") > 0f ? 0.5f : 0f;

        internal static float ResolveNaGouImmunity(in AttackInformation info, in AttackCollisionData collision, float damage)
        {
            if (damage <= 0f || collision.CollisionResult != CombatCollisionResult.StrikeAgent || collision.IsFallDamage) return damage;
            AgentSkillComponent component = info.VictimAgent?.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.GetLongestStateDuration("NaGouCiFuBuff") <= 0f || MBRandom.RandomFloat >= 0.5f) return damage;
            // 伤害模型不修改原生Agent；在任务更新安全阶段回复本次原应承受的伤害。
            component.NaGouPendingHealing += MathF.Round(damage);
            return 0f;
        }
    }
}