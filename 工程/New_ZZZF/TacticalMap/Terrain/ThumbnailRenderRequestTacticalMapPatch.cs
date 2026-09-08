using System;
using HarmonyLib;
using TaleWorlds.Engine;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// The native wrapper for ThumbnailRenderRequest.CreateWithoutTexture unconditionally
    /// dereferences GameEntity.Pointer. REV11 intentionally has no visual entity, so supply
    /// one empty, non-physics anchor only for TacticalMap's terrain-photo request.
    /// </summary>
    [HarmonyPatch(typeof(ThumbnailRenderRequest), "CreateWithoutTexture")]
    internal static class ThumbnailRenderRequestTacticalMapPatch
    {
        private const string TacticalMapDebugName = "TacticalMapTerrainPhotoREV11";

        private static GameEntity _photoAnchor;
        private static Scene _photoAnchorScene;

        [HarmonyPrefix]
        private static void Prefix(
            Scene scene,
            GameEntity entity,
            string debugName,
            ref GameEntity __0)
        {
            if (__0 != null || scene == null ||
                !string.Equals(debugName, TacticalMapDebugName, StringComparison.Ordinal))
                return;

            if (_photoAnchor == null || _photoAnchorScene != scene)
            {
                _photoAnchor = GameEntity.CreateEmpty(scene, false, false, false);
                _photoAnchorScene = scene;

                if (_photoAnchor == null)
                {
                    TacticalMapLog.Error(
                        "[PhotoNative] REV=11 failed to create thumbnail anchor entity.", null);
                    return;
                }

                TacticalMapLog.Info(
                    "[PhotoNative] REV=11 created thumbnail anchor pointer=" +
                    _photoAnchor.Pointer);
            }

            __0 = _photoAnchor;
        }

        [HarmonyPatch(typeof(TerrainPhotographerRev11), nameof(TerrainPhotographerRev11.OnMissionEnd))]
        [HarmonyPostfix]
        private static void ReleasePhotoAnchor()
        {
            _photoAnchor = null;
            _photoAnchorScene = null;
        }
    }
}
