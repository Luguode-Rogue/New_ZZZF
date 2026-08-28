using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>Projects the engine AI navigation mesh onto the tactical-map raster.</summary>
    public sealed class NavMeshMap
    {
        private readonly TerrainCache _cache;
        private bool[] _walkable;
        private byte[] _rgba;
        private int _version;

        public NavMeshMap(TerrainCache cache) { _cache = cache; }
        public bool IsBuilt => _walkable != null && _rgba != null;
        public int Version => _version;
        public byte[] RGBA => _rgba;

        public bool IsWalkable(int x, int y)
        {
            if (_walkable == null || x < 0 || x >= _cache.Width || y < 0 || y >= _cache.Height) return false;
            return _walkable[y * _cache.Width + x];
        }

        public void Build(Scene scene)
        {
            if (_cache == null || scene == null || !_cache.IsBaked) return;
            int width = _cache.Width, height = _cache.Height;
            if (width <= 0 || height <= 0) return;

            try
            {
                _walkable = new bool[width * height];
                _rgba = new byte[width * height * 4];
                int walkableCells = 0;
                int faceCount = 0;
                try { faceCount = scene.GetNavMeshFaceCount(); } catch { }

                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    Vec2 pos = _cache.CellCenter(x, y);
                    float z = _cache.GetHeightAt(pos);
                    PathFaceRecord face = PathFaceRecord.NullFaceRecord;
                    try { scene.GetNavMeshFaceIndex(ref face, new Vec3(pos.X, pos.Y, z), true); }
                    catch { face = PathFaceRecord.NullFaceRecord; }

                    bool walkable = face.IsValid();
                    _walkable[y * width + x] = walkable;
                    if (walkable)
                    {
                        walkableCells++;
                        SetPixel(x, y, 0, 0, 0, 0);
                    }
                    else
                    {
                        // Do not paint every non-walkable cell as a solid block. At 256x256,
                        // a one-cell-wide fence would otherwise become much thicker than an agent.
                        // We keep a subtle interior tint and emphasize only the NavMesh boundary.
                        SetPixel(x, y, 120, 28, 28, 42);
                    }
                }

                // Boundary pass: only non-walkable cells adjacent to walkable space receive a
                // strong edge. This makes fences/walls read as thin boundaries while solid
                // building interiors remain visually subdued.
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (_walkable[y * width + x]) continue;
                        if (!TouchesWalkable(x, y, width, height)) continue;
                        SetPixel(x, y, 165, 42, 38, 150);
                    }
                }

                _version++;
                TacticalMapLog.Info("NavMeshMap build complete. Faces=" + faceCount +
                                     " WalkableCells=" + walkableCells +
                                     " TotalCells=" + (width * height) +
                                     " Coverage=" + ((float)walkableCells / Math.Max(1, width * height)).ToString("F3"));
            }
            catch (Exception ex)
            {
                _walkable = null;
                _rgba = null;
                TacticalMapLog.Error("NavMeshMap.Build failed.", ex);
            }
        }

        private bool TouchesWalkable(int x, int y, int width, int height)
        {
            if (x > 0 && _walkable[y * width + (x - 1)]) return true;
            if (x + 1 < width && _walkable[y * width + (x + 1)]) return true;
            if (y > 0 && _walkable[(y - 1) * width + x]) return true;
            if (y + 1 < height && _walkable[(y + 1) * width + x]) return true;
            return false;
        }

        private void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * _cache.Width + x) * 4;
            _rgba[i] = r;
            _rgba[i + 1] = g;
            _rgba[i + 2] = b;
            _rgba[i + 3] = a;
        }
    }
}
