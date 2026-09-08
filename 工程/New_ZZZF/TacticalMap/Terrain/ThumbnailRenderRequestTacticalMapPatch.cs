using System;
using HarmonyLib;
using TaleWorlds.Engine;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// REV12 no longer creates a secondary Scene. This patch is retained only as a
    /// defensive guard for legacy calls carrying the TacticalMap REV11 debug name.
    /// </summary>
    [HarmonyPatch(typeof(ThumbnailRenderRequest), "CreateWithoutTexture")]
    internal static class ThumbnailRenderRequestTacticalMapPatch
    {
        private const string LegacyDebugName = "TacticalMapTerrainPhotoREV11";

        [HarmonyPrefix]
        private static void Prefix(
            Scene scene,
            ref GameEntity entity,
            string debugName,
            ref int __4,
            ref int __5)
        {
            if (scene == null ||
                !string.Equals(debugName, LegacyDebugName, StringComparison.Ordinal))
                return;

            if (entity == null)
            {
                entity = GameEntity.CreateEmpty(scene, false, false, false);
                if (entity == null)
                {
                    TacticalMapLog.Error(
                        "[PhotoNative] REV=12 failed to create legacy thumbnail guard entity.", null);
                    return;
                }
            }

            int maxDimension = Math.Max(__4, __5);
            const int maxNativeDimension = 2048;
            const int minNativeDimension = 64;
            if (maxDimension > maxNativeDimension)
            {
                double scale = (double)maxNativeDimension / maxDimension;
                __4 = Math.Max(minNativeDimension, (int)Math.Round(__4 * scale));
                __5 = Math.Max(minNativeDimension, (int)Math.Round(__5 * scale));
            }
        }
    }
}
