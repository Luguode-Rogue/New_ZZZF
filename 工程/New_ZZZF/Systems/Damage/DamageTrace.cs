using System;
using System.Collections.Concurrent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF
{
    /// <summary>
    /// 跨伤害阶段的诊断上下文。
    /// 目标：一次命中最终只写一行日志，同时保留 StrikeMagnitude 与最终结算信息。
    /// </summary>
    internal static class DamageTrace
    {
        private sealed class Context
        {
            public int AttackerIndex;
            public int VictimIndex;
            public int MissileIndex;
            public string AttackKind;
            public string DamageType;
            public float StrikeMagnitude;
            public float AdjustedArmor;
            public float DamageBeforeReduction;
            public float DamageAfterNativeWithoutArmor;
            public float DamageAfterStateRules;
            public float FinalDamage;
            public bool FriendlyFire;
            public bool ZhanYi;
            public bool JianRenBuQu;
            public bool TianQi;
            public bool CustomBattle;
        }

        private static readonly ConcurrentDictionary<string, Context> Pending =
            new ConcurrentDictionary<string, Context>();

        public static string BeginStrike(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            string attackKind,
            string damageType,
            float strikeMagnitude)
        {
            string key = BuildKey(attackInformation, collisionData, attackKind, damageType);
            Pending[key] = new Context
            {
                AttackerIndex = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1,
                VictimIndex = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1,
                MissileIndex = collisionData.AffectorWeaponSlotOrMissileIndex,
                AttackKind = attackKind ?? "Unknown",
                DamageType = damageType ?? "Unknown",
                StrikeMagnitude = strikeMagnitude,
                AdjustedArmor = Math.Max(0f, attackInformation.ArmorAmountFloat),
                FriendlyFire = attackInformation.IsFriendlyFire
            };
            return key;
        }

        public static void Complete(
            string key,
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float damageBeforeReduction,
            float damageAfterNativeWithoutArmor,
            float damageAfterStateRules,
            float adjustedArmor,
            float finalDamage,
            bool customBattle,
            string attackKind = null,
            string damageType = null)
        {
            if (string.IsNullOrEmpty(key))
                key = BuildKey(attackInformation, collisionData, attackKind ?? "Unknown", damageType ?? "Unknown");

            Context context;
            if (!Pending.TryRemove(key, out context))
            {
                context = new Context
                {
                    AttackerIndex = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1,
                    VictimIndex = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1,
                    MissileIndex = collisionData.AffectorWeaponSlotOrMissileIndex,
                    AttackKind = attackKind ?? "Unknown",
                    DamageType = damageType ?? "Unknown",
                    AdjustedArmor = Math.Max(0f, adjustedArmor),
                    FriendlyFire = attackInformation.IsFriendlyFire
                };
            }

            context.DamageBeforeReduction = damageBeforeReduction;
            context.DamageAfterNativeWithoutArmor = damageAfterNativeWithoutArmor;
            context.DamageAfterStateRules = damageAfterStateRules;
            context.AdjustedArmor = Math.Max(0f, adjustedArmor);
            context.FinalDamage = finalDamage;
            context.CustomBattle = customBattle;

            Agent attacker = attackInformation.AttackerAgent;
            Agent victim = attackInformation.VictimAgent;

            if (attacker != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attacker.Index, out var attackerComponent))
            {
                context.ZhanYi = attackerComponent.StateContainer.HasState("ZhanYiBuff");
            }

            if (victim != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(victim.Index, out var victimComponent))
            {
                context.JianRenBuQu = victimComponent.StateContainer.HasState("JianRenBuQuuBuff") ||
                                      victimComponent.StateContainer.HasState("JianRenBuQu");
                context.TianQi = victimComponent.StateContainer.HasState("TianQiBuff") ||
                                 victimComponent.StateContainer.HasState("TianQi");
            }

            float minimumRatio = GetMinimumDamageRatio(attacker);
            float armorResult = damageAfterStateRules - context.AdjustedArmor;
            float minimumResult = damageAfterStateRules * minimumRatio;

            TacticalMapLog.Info(string.Format(
                "[DAMAGE] atk={0} vic={1} kind={2} type={3} mode={4} strike={5:F2} n={6:F2} native0={7:F2} state={8:F2} armor={9:F2} armorResult={10:F2} min={11:F2}@{12:P0} final={13:F2} FF={14} ZY={15} JR={16} TQ={17}",
                context.AttackerIndex,
                context.VictimIndex,
                context.AttackKind,
                context.DamageType,
                context.CustomBattle ? "Custom" : "Campaign",
                context.StrikeMagnitude,
                context.DamageBeforeReduction,
                context.DamageAfterNativeWithoutArmor,
                context.DamageAfterStateRules,
                context.AdjustedArmor,
                armorResult,
                minimumResult,
                minimumRatio,
                context.FinalDamage,
                context.FriendlyFire,
                context.ZhanYi,
                context.JianRenBuQu,
                context.TianQi));
        }

        private static string BuildKey(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            string attackKind,
            string damageType)
        {
            int attacker = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1;
            int victim = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1;
            return attacker + ":" + victim + ":" + collisionData.AffectorWeaponSlotOrMissileIndex + ":" +
                   (attackKind ?? "Unknown") + ":" + (damageType ?? "Unknown");
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
