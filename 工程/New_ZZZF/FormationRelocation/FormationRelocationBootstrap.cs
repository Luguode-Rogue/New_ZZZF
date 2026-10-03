using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.FormationRelocation
{
    // All patches and mission state for this feature are owned by this folder.
    internal static class FormationRelocationBootstrap
    {
        private const string HarmonyId = "New_ZZZF.FormationRelocation";
        private static Harmony _harmony;

        internal static bool IsInstalled => _harmony != null;

        internal static void Install()
        {
            if (_harmony != null) return;

            var harmony = new Harmony(HarmonyId);
            try
            {
                Patch(harmony, typeof(OrderController), nameof(OrderController.SetOrderWithTwoPositions),
                    new[] { typeof(OrderType), typeof(TaleWorlds.Engine.WorldPosition), typeof(TaleWorlds.Engine.WorldPosition) },
                    nameof(FormationRelocationPatches.BeforeLineMove));
                Patch(harmony, typeof(OrderController), nameof(OrderController.SetOrderWithPosition),
                    new[] { typeof(OrderType), typeof(TaleWorlds.Engine.WorldPosition) },
                    nameof(FormationRelocationPatches.BeforePointMove));
                Patch(harmony, typeof(HumanAIComponent), nameof(HumanAIComponent.GetDesiredSpeedInFormation),
                    new[] { typeof(bool) },
                    nameof(FormationRelocationPatches.BeforeMountedFormationSpeed));
                _harmony = harmony;
            }
            catch
            {
                harmony.UnpatchAll(HarmonyId);
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (_harmony == null) return;
            _harmony.UnpatchAll(HarmonyId);
            _harmony = null;
        }

        private static void Patch(Harmony harmony, Type targetType, string methodName,
            Type[] parameters, string patchName)
        {
            MethodInfo original = AccessTools.Method(targetType, methodName, parameters);
            MethodInfo prefix = AccessTools.Method(typeof(FormationRelocationPatches), patchName);
            if (original == null || prefix == null)
                throw new MissingMethodException(targetType.FullName, methodName);
            harmony.Patch(original, prefix: new HarmonyMethod(prefix));
        }
    }
}
