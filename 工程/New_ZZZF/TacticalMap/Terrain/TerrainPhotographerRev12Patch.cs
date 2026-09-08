using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV12.1：修正私有 Scene 初始化并增加场景地形诊断。
    ///
    /// 原 REV12 在 CreatePrivateScene 中主动关闭了 FloraPhysics、TerrainMeshBlending
    /// 和 Oros，但目前没有证据证明这些选项适用于战场地形离屏渲染。
    /// 这里保留 PhysicsMaterials=false，其余使用引擎默认初始化。
    ///
    /// 该类通过 Harmony 覆盖原有私有 CreatePrivateScene，不触碰 Mission.Scene。
    /// </summary>
    [HarmonyPatch]
    internal static class TerrainPhotographerRev12Patch
    {
        private const string TargetMethod = "CreatePrivateScene";

        [HarmonyTargetMethod]
        private static System.Reflection.MethodBase Target()
        {
            return AccessTools.Method(typeof(TerrainPhotographerRev11), TargetMethod);
        }

        [HarmonyPrefix]
        private static bool Prefix(TerrainPhotographerRev11 __instance)
        {
            Mission mission = AccessTools.Field(typeof(TerrainPhotographerRev11), "_mission")
                .GetValue(__instance) as Mission;

            if (mission == null || mission.Scene == null || string.IsNullOrEmpty(mission.SceneName))
                return true;

            try
            {
                Scene scene = Scene.CreateNewScene(
                    false,
                    true,
                    DecalAtlasGroup.Worldmap,
                    "TacticalMapPhotoSceneREV12");

                if (scene == null)
                    throw new InvalidOperationException("Scene.CreateNewScene returned null.");

                SceneInitializationData data = new SceneInitializationData(true);
                data.UsePhysicsMaterials = false;

                scene.Read(mission.SceneName, ref data, "");
                scene.ForceLoadResources(true);
                scene.Tick(0.1f);

                AccessTools.Field(typeof(TerrainPhotographerRev11), "_photoScene")
                    .SetValue(__instance, scene);

                LogProbe(mission, scene);
                return false;
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=12.1 private scene patch failed; fallback to original CreatePrivateScene.", ex);
                return true;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogProbe(Mission mission, Scene privateScene)
        {
            try
            {
                bool liveReady = MissionSceneGuard.IsSceneTerrainReady(mission.Scene);
                bool privateReady = MissionSceneGuard.IsSceneTerrainReady(privateScene);

                TacticalMapLog.Info(
                    "[PhotoNative] REV=12.1 TERRAIN_PROBE " +
                    "liveReady=" + liveReady +
                    " privateReady=" + privateReady +
                    " livePtr=" + mission.Scene.Pointer +
                    " privatePtr=" + privateScene.Pointer +
                    " sceneName=" + mission.SceneName);

                LogTerrainShape("LIVE", mission.Scene, liveReady);
                LogTerrainShape("PRIVATE", privateScene, privateReady);

                TerrainCache cache = AccessTools.Field(typeof(TerrainPhotographerRev11), "_cache")
                    .GetValue(TerrainPhotographerRev11.Instance) as TerrainCache;

                if (cache != null)
                {
                    Vec2 center = new Vec2(
                        cache.OriginX + cache.WorldW * 0.5f,
                        cache.OriginY + cache.WorldH * 0.5f);

                    float liveHeight = mission.Scene.GetTerrainHeight(center, true);
                    float privateHeight = privateScene.GetTerrainHeight(center, true);

                    TacticalMapLog.Info(
                        "[PhotoNative] REV=12.1 COORD_PROBE " +
                        "origin=(" + cache.OriginX + "," + cache.OriginY + ")" +
                        " world=(" + cache.WorldW + "," + cache.WorldH + ")" +
                        " center=(" + center.X + "," + center.Y + ")" +
                        " liveH=" + liveHeight +
                        " privateH=" + privateHeight +
                        " minH=" + cache.MinH +
                        " maxH=" + cache.MaxH);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=12.1 terrain probe failed.", ex);
            }
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LogTerrainShape(string tag, Scene scene, bool ready)
        {
            if (!ready || scene == null)
                return;

            try
            {
                if (scene.GetTerrainMinMaxHeight(out float minH, out float maxH))
                {
                    scene.GetTerrainData(out Vec2i nodeDim, out float nodeSize, out _, out _);
                    TacticalMapLog.Info(
                        "[PhotoNative] REV=12.1 " + tag +
                        "_TERRAIN min=" + minH +
                        " max=" + maxH +
                        " nodeDim=" + nodeDim.X + "x" + nodeDim.Y +
                        " nodeSize=" + nodeSize);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[PhotoNative] REV=12.1 " + tag + " terrain shape probe failed.", ex);
            }
        }
    }
}
