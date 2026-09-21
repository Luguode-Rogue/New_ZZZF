using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;

namespace New_ZZZF.KillSkillXpMultiplier
{
    [HarmonyPatch(typeof(SkillLevelingManager), nameof(SkillLevelingManager.OnCombatHit))]
    internal static class SkillLevelingManagerOnCombatHitPatch
    {
        private const float KillMultiplier = 5f;
        private const float OneHitKillMultiplier = 10f;

        private static void Prefix(
            CharacterObject affectedCharacter,
            float damageAmount,
            bool isFatal,
            out float __state)
        {
            float multiplier = 1f;
            if (isFatal)
            {
                int maximumHitPoints = affectedCharacter?.MaxHitPoints() ?? 0;
                bool isOneHitKill = maximumHitPoints > 0 && damageAmount >= maximumHitPoints;
                multiplier = isOneHitKill ? OneHitKillMultiplier : KillMultiplier;
            }

            __state = CombatHitXpContext.Set(multiplier);
        }

        private static Exception Finalizer(Exception __exception, float __state)
        {
            CombatHitXpContext.Restore(__state);
            return __exception;
        }
    }

    [HarmonyPatch]
    internal static class CombatXpModelGetXpFromHitPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type modelType = typeof(CombatXpModel);

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null && !type.IsAbstract && modelType.IsAssignableFrom(type))
                .Select(type => AccessTools.DeclaredMethod(type, nameof(CombatXpModel.GetXpFromHit)))
                .Where(method => method != null)
                .Distinct();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        private static void Postfix(ref ExplainedNumber __result)
        {
            float multiplier = CombatHitXpContext.Multiplier;
            if (multiplier > 1f)
            {
                __result.AddFactor(multiplier - 1f);
            }
        }
    }
}
