using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF
{
    /// <summary>
    /// 每次最终命中只输出一行伤害诊断日志。
    /// 这里记录完整的实际结算链：Reduction 输入 → 原版护甲置零后的 Reduction
    /// → New_ZZZF 状态修正 → damage-refactor 护甲/保底 → 最终伤害。
    /// </summary>
    internal static class DamageTrace
    {
        public static void LogFinal(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float damageBeforeReduction,
            float nativeWithoutArmor,
            float stateAdjustedDamage,
            float adjustedArmor,
            float finalDamage,
            bool customBattle)
        {
            Agent attacker = attackInformation.AttackerAgent;
            Agent victim = attackInformation.VictimAgent;

            bool zhanYi = false;
            bool jianRenBuQu = false;
            bool tianQi = false;

            if (attacker != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent))
            {
                zhanYi = attackerComponent.StateContainer.HasState("ZhanYiBuff");
            }

            if (victim != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                jianRenBuQu = victimComponent.StateContainer.HasState("JianRenBuQuuBuff") ||
                              victimComponent.StateContainer.HasState("JianRenBuQu");
                tianQi = victimComponent.StateContainer.HasState("TianQiBuff") ||
                         victimComponent.StateContainer.HasState("TianQi");
            }

            float minimumRatio = GetMinimumDamageRatio(attacker);
            float armorResult = stateAdjustedDamage - Math.Max(0f, adjustedArmor);
            float minimumResult = stateAdjustedDamage * minimumRatio;

            int attackerIndex = attacker != null ? attacker.Index : -1;
            int victimIndex = victim != null ? victim.Index : -1;

            TacticalMapLog.Info(string.Format(
                "[DAMAGE] atk={0} vic={1} mode={2} strike={3} dmgType={4} in={5:F2} native0={6:F2} state={7:F2} armorAdj={8:F2} armorRule={9:F2} min={10:F2}@{11:P0} final={12:F2} FF={13} ZY={14} JR={15} TQ={16}",
                attackerIndex,
                victimIndex,
                customBattle ? "Custom" : "Campaign",
                collisionData.StrikeType,
                collisionData.DamageType,
                damageBeforeReduction,
                nativeWithoutArmor,
                stateAdjustedDamage,
                Math.Max(0f, adjustedArmor),
                armorResult,
                minimumResult,
                minimumRatio,
                finalDamage,
                attackInformation.IsFriendlyFire,
                zhanYi,
                jianRenBuQu,
                tianQi));
        }

        private static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker != null ? attacker.Character : null;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                CharacterObject characterObject = character as CharacterObject;
                Hero hero = characterObject != null ? characterObject.HeroObject : null;
                return hero == null ? 0f : Math.Max(0f, hero.Level * 0.01f);
            }

            return character.IsSoldier ? Math.Max(0f, character.GetBattleTier() * 0.05f) : 0f;
        }
    }
}
