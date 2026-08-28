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
                        // Visible mask for areas the AI navigation mesh does not cover.
                        SetPixel(x, y, 150, 32, 32, 135);
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
