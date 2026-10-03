using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>恐虐赐福的原生近战友伤入口；不改变队伍关系或全局友伤选项。</summary>
    internal static class KongNueFriendlyFireRules
    {
        internal static bool IsFriendlySwing(Agent attacker, Agent victim) =>
            KongNueCiFu.IsBlessed(attacker) && attacker != victim &&
            victim != null && victim.IsActive() && attacker.IsFriendOf(victim);

        internal static bool IsExempt(Agent victim) =>
            (victim?.IsMount == true ? victim.RiderAgent : victim)?.GetComponent<AgentSkillComponent>()?.HasSkill("KongNueCiFu") == true;

        internal static bool AllowsDamage(Agent attacker, Agent victim) =>
            (IsFriendlySwing(attacker, victim) && !IsExempt(victim)) || (XieEZuZhou.IsCursed(attacker) && attacker != victim && victim != null && victim.IsActive() && attacker.IsFriendOf(victim));

        internal static float ApplyDamage(Agent attacker, Agent victim, float damage) =>
            XieEZuZhou.IsCursed(attacker) ? damage : (IsFriendlySwing(attacker, victim) ? (IsExempt(victim) ? 0f : damage * 0.5f) : damage);
    }

    [HarmonyPatch(typeof(Mission), "CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase")]
    internal static class KongNueFriendlyDamageGatePatch
    {
        private static void Postfix(Agent attacker, Agent victim, ref bool __result)
        {
            if (KongNueFriendlyFireRules.AllowsDamage(attacker, victim)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class KongNueFriendlyMeleePatch
    {
        private static bool Prefix(Agent attacker, Agent victim, ref AttackCollisionData collisionData,
            ref float inOutMomentumRemaining, ref MeleeCollisionReaction colReaction, ref HitParticleResultData hitParticleResultData, out float __state)
        {
            __state = inOutMomentumRemaining;
            if (!FriendlyMeleePassThroughRules.ShouldSkipFriendlyHit(attacker, victim)) return true;
            // 携带技能即豁免，无需该友军正在开启赐福；继续检查后面的目标。
            hitParticleResultData.Reset();
            collisionData.InflictedDamage = 0;
            colReaction = MeleeCollisionReaction.ContinueChecking;
            return false;
        }

        private static void Postfix(Agent attacker, Agent victim, in AttackCollisionData collisionData,
            ref float inOutMomentumRemaining, float __state)
        {
            if (KongNueFriendlyFireRules.IsFriendlySwing(attacker, victim) &&
                (KongNueFriendlyFireRules.IsExempt(victim) ||
                 collisionData.CollisionResult != CombatCollisionResult.ChamberBlocked))
                inOutMomentumRemaining = __state;
        }
    }
}