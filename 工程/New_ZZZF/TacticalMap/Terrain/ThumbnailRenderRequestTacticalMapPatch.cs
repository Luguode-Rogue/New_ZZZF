using System;
using HarmonyLib;
using TaleWorlds.Engine;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// CreateWithoutTexture dereferences the entity argument unconditionally.
    /// REV11 intentionally has no Agent/entity, so supply a plain empty GameEntity
    /// only for TacticalMap's terrain-photo request and retain it until mission end.
    /// The same request is also clamped to a conservative native render-target size.
    /// </summary>
    [HarmonyPatch(typeof(ThumbnailRenderRequest), "CreateWithoutTexture")]
    internal static class ThumbnailRenderRequestTacticalMapPatch
    {
        private const string TacticalMapDebugName = "TacticalMapTerrainPhotoREV11";
        private const int MaxNativeDimension = 2048;
        private const int MinNativeDimension = 64;

        private static GameEntity _photoAnchor;
        private static Scene _photoAnchorScene;

        [HarmonyPrefix]
        private static void Prefix(
            Scene scene,
            ref GameEntity entity,
            string debugName,
            ref int __4,
            ref int __5)
        {
            if (scene == null ||
                !string.Equals(debugName, TacticalMapDebugName, StringComparison.Ordinal))
                return;

            if (entity == null)
            {
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

                entity = _photoAnchor;
            }

            int originalWidth = __4;
            int originalHeight = __5;
            int maxDimension = Math.Max(originalWidth, originalHeight);

            if (maxDimension > MaxNativeDimension)
            {
                double scale = (double)MaxNativeDimension / maxDimension;
                __4 = Math.Max(MinNativeDimension, (int)Math.Round(originalWidth * scale));
                __5 = Math.Max(MinNativeDimension, (int)Math.Round(originalHeight * scale));

                TacticalMapLog.Info(
                    "[PhotoNative] REV=11 clamped native request " +
                    originalWidth + "x" + originalHeight +
                    " -> " + __4 + "x" + __5 +
                    " maxDimension=" + MaxNativeDimension);
            }
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
