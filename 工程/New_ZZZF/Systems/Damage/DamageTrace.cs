using System;
using System.Collections.Concurrent;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF
{
    /// <summary>
    /// 一次攻击的跨阶段诊断上下文。
    /// StrikeMagnitude 阶段记录输入/中间结果，ApplyDamageReductions 阶段补齐最终结果，最终只输出一行。
    /// </summary>
    internal static class DamageTrace
    {
        private sealed class Context
        {
            public DateTime CreatedAt;
            public int AttackerIndex;
            public int VictimIndex;
            public int MissileIndex;
            public StrikeType StrikeType;
            public DamageType DamageType;
            public float StrikeMagnitude;
            public float NativeArmor;
            public float AdjustedArmor;
            public float DamageBeforeReduction;
            public float DamageAfterNativeWithoutArmor;
            public float DamageAfterStateRules;
            public float ArmorResult;
            public float MinimumResult;
            public float FinalDamage;
            public bool FriendlyFire;
            public bool ZhanYi;
            public bool JianRenBuQu;
            public bool TianQi;
            public bool CustomBattle;
            public string Source;
        }

        private static readonly ConcurrentDictionary<string, Context> Pending =
            new ConcurrentDictionary<string, Context>();

        public static string BeginStrike(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            StrikeType strikeType,
            DamageType damageType,
            float strikeMagnitude,
            float armor)
        {
            string key = BuildKey(attackInformation, collisionData, strikeType, damageType);
            Pending[key] = new Context
            {
                CreatedAt = DateTime.UtcNow,
                AttackerIndex = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1,
                VictimIndex = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1,
                MissileIndex = collisionData.AffectorWeaponSlotOrMissileIndex,
                StrikeType = strikeType,
                DamageType = damageType,
                StrikeMagnitude = strikeMagnitude,
                NativeArmor = armor,
                AdjustedArmor = armor,
                FriendlyFire = attackInformation.IsFriendlyFire,
                Source = "StrikeMagnitude"
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
            bool customBattle)
        {
            if (string.IsNullOrEmpty(key))
                key = BuildKey(attackInformation, collisionData, attackInformation.StrikeType, collisionData.DamageType);

            Context context;
            if (!Pending.TryRemove(key, out context))
            {
                context = new Context
                {
                    CreatedAt = DateTime.UtcNow,
                    AttackerIndex = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1,
                    VictimIndex = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1,
                    MissileIndex = collisionData.AffectorWeaponSlotOrMissileIndex,
                    StrikeType = attackInformation.StrikeType,
                    DamageType = collisionData.DamageType,
                    NativeArmor = adjustedArmor,
                    AdjustedArmor = adjustedArmor
                };
            }

            context.DamageBeforeReduction = damageBeforeReduction;
            context.DamageAfterNativeWithoutArmor = damageAfterNativeWithoutArmor;
            context.DamageAfterStateRules = damageAfterStateRules;
            context.AdjustedArmor = adjustedArmor;
            context.ArmorResult = damageAfterStateRules - adjustedArmor;
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
            context.MinimumResult = damageAfterStateRules * minimumRatio;

            TacticalMapLog.Info(
                string.Format(
                    "[DAMAGE] atk={0} vic={1} type={2}/{3} src={4} strike={5:F2} armor={6:F2} armorAdj={7:F2} native0Armor={8:F2} state={9:F2} armorRule={10:F2} min={11:F2}@{12:P0} final={13:F2} FF={14} ZhanYi={15} JianRen={16} TianQi={17}",
                    context.AttackerIndex,
                    context.VictimIndex,
                    context.StrikeType,
                    context.DamageType,
                    context.CustomBattle ? "Custom" : "Campaign",
                    context.StrikeMagnitude,
                    context.NativeArmor,
                    context.AdjustedArmor,
                    context.DamageAfterNativeWithoutArmor,
                    context.DamageAfterStateRules,
                    context.ArmorResult,
                    context.MinimumResult,
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
            StrikeType strikeType,
            DamageType damageType)
        {
            int attacker = attackInformation.AttackerAgent != null ? attackInformation.AttackerAgent.Index : -1;
            int victim = attackInformation.VictimAgent != null ? attackInformation.VictimAgent.Index : -1;
            return attacker + ":" + victim + ":" + collisionData.AffectorWeaponSlotOrMissileIndex + ":" + (int)strikeType + ":" + (int)damageType;
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
