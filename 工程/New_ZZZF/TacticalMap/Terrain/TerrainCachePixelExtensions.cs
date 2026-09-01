namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Compatibility helpers for legacy Gauntlet TacticalMap renderers.
    /// Keeps pixel access outside TerrainCache's terrain-generation responsibilities.
    /// </summary>
    public static class TerrainCachePixelExtensions
    {
        public static void GetPixel(
            this TerrainCache cache,
            byte[] buffer,
            int x,
            int y,
            out byte r,
            out byte g,
            out byte b,
            out byte a)
        {
            if (cache == null || buffer == null || cache.Width <= 0 || cache.Height <= 0)
            {
                r = 0;
                g = 0;
                b = 0;
                a = 0;
                return;
            }

            if (x < 0) x = 0;
            else if (x >= cache.Width) x = cache.Width - 1;
            if (y < 0) y = 0;
            else if (y >= cache.Height) y = cache.Height - 1;

            int index = (y * cache.Width + x) * 4;
            if (index < 0 || index + 3 >= buffer.Length)
            {
                r = 0;
                g = 0;
                b = 0;
                a = 0;
                return;
            }

            r = buffer[index];
            g = buffer[index + 1];
            b = buffer[index + 2];
            a = buffer[index + 3];
        }
    }
}
