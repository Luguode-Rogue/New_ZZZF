using HarmonyLib;
using TaleWorlds.Engine;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV12.2 compatibility guard.
    ///
    /// Manually ticking an isolated Scene from managed mission tick can enter unsafe
    /// native engine state and raise AccessViolationException. SceneView already owns
    /// the render lifecycle, so the capture state machine must not call Scene.Tick().
    ///
    /// The original REV12 methods wrap Scene.Tick in try/catch, but AccessViolationException
    /// is a corrupted-state exception and is not reliably catchable. During those two
    /// methods we temporarily hide the managed _photoScene reference. The original
    /// `_photoScene.Tick(...)` therefore becomes a managed NullReferenceException inside
    /// its existing try/catch, while SceneView keeps its own native scene reference and
    /// continues rendering normally.
    /// </summary>
    internal static class TerrainPhotographerRev12SceneTickGuard
    {
        private static readonly System.Reflection.FieldInfo PhotoSceneField =
            AccessTools.Field(typeof(TerrainPhotographerRev11), "_photoScene");

        private static readonly System.Reflection.FieldInfo StageField =
            AccessTools.Field(typeof(TerrainPhotographerRev11), "_stage");

        private static void HideScene(TerrainPhotographerRev11 instance, out Scene state)
        {
            state = PhotoSceneField.GetValue(instance) as Scene;
            if (state != null)
                PhotoSceneField.SetValue(instance, null);
        }

        private static void RestoreScene(TerrainPhotographerRev11 instance, Scene state)
        {
            if (state == null)
                return;

            // Fail()/StopPrivateRenderer() transitions the photographer back to Idle.
            // Do not resurrect the scene reference after cleanup on a failure path.
            object stage = StageField.GetValue(instance);
            if (stage != null && stage.ToString() == "Idle")
                return;

            PhotoSceneField.SetValue(instance, state);
        }

        [HarmonyPatch(typeof(TerrainPhotographerRev11), "TickWarming")]
        private static class TickWarmingPatch
        {
            [HarmonyPrefix]
            private static void Prefix(TerrainPhotographerRev11 __instance, out Scene __state)
            {
                HideScene(__instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(TerrainPhotographerRev11 __instance, Scene __state)
            {
                RestoreScene(__instance, __state);
            }
        }

        [HarmonyPatch(typeof(TerrainPhotographerRev11), "TickCapturing")]
        private static class TickCapturingPatch
        {
            [HarmonyPrefix]
            private static void Prefix(TerrainPhotographerRev11 __instance, out Scene __state)
            {
                HideScene(__instance, out __state);
            }

            [HarmonyPostfix]
            private static void Postfix(TerrainPhotographerRev11 __instance, Scene __state)
            {
                RestoreScene(__instance, __state);
            }
        }
    }
}
