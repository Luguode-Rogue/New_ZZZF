using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// Bakes the battlefield terrain into a compact tactical grid.
    /// The baked data is deliberately centered on gameplay geometry: elevation, relief, slope,
    /// movement difficulty and terrain breaks.
    /// </summary>
    public sealed class TerrainCache
    {
        private sealed class CachedBake
        {
            public int Signature;
            public int Width;
            public int Height;
            public float WorldW;
            public float WorldH;
            public float OriginX;
            public float OriginY;
            public float CellStep;
            public float MinH;
            public float MaxH;
            public CellSnapshot[] Cells;
            public byte[] TerrainBaseRGBA;
            public byte[] TacticalRGBA;
        }

        private struct CellSnapshot
        {
            public float Height;
            public Vec3 Normal;
            public float Slope;
            public TerrainKind Kind;
            public float MovementCost;
            public float RelativeHeight;
            public float HighGround;
            public float HeightBreak;
            public bool IsForest;
            public bool IsCliff;
            public bool IsWater;
        }

        private static readonly ConditionalWeakTable<Scene, CachedBake> SceneBakeCache =
            new ConditionalWeakTable<Scene, CachedBake>();

        public int Width { get; private set; }
        public int Height { get; private set; }
        public float WorldW { get; private set; }
        public float WorldH { get; private set; }
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }
        public float CellStep { get; private set; }
        public float MinH { get; private set; }
        public float MaxH { get; private set; }

        public TerrainCell[,] Cells { get; private set; }
        public byte[] TerrainBaseRGBA { get; private set; }
        public byte[] TacticalRGBA { get; private set; }

        // Compatibility surface for old HTML state consumers.
        public byte[] RiskRGBA => TacticalRGBA;

        // Kept for legacy code paths; HTMLUI now derives agent points from AgentSnapshots.
        public byte[] AgentRGBA { get; private set; }

        private readonly TacticalMap.Config.TacticalSettings _settings;
        private Scene _scene;
        private bool _baked;

        public TerrainCache(TacticalMap.Config.TacticalSettings settings)
        {
            _settings = settings;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public bool TryBake(Scene scene)
        {
            var watch = Stopwatch.StartNew();
            TacticalMapLog.Section("TERRAIN BAKE");
            string assemblyPath = typeof(TerrainCache).Assembly.Location;
            TacticalMapLog.Info(
                "BuildFingerprint: Assembly=" + typeof(TerrainCache).Assembly.FullName
                + ", LastWriteUtc=" + (string.IsNullOrWhiteSpace(assemblyPath) ? "<unknown>" : File.GetLastWriteTimeUtc(assemblyPath).ToString("O")));
            TacticalMapLog.Info("TryBake entered. Scene=" + (scene == null ? "null" : scene.GetType().FullName));
            _scene = scene;
            if (scene == null)
            {
                LastError = "scene 为 null";
                TacticalMapLog.Warn("Terrain bake aborted: scene is null.");
                return false;
            }

            if (!MissionSceneGuard.IsSceneTerrainReady(scene))
            {
                LastError = "当前场景无地形数据（酒馆/城镇等室内场景）";
                _baked = false;
                TacticalMapLog.Warn("Terrain bake aborted: scene terrain is not ready.");
                return false;
            }

            try
            {
                scene.GetTerrainData(out Vec2i nodeDim, out float nodeSize, out _, out _);
                TacticalMapLog.Info("TerrainData: nodeDim=" + nodeDim.X + "x" + nodeDim.Y + ", nodeSize=" + nodeSize);
                if (nodeDim.X <= 0 || nodeDim.Y <= 0 || nodeSize <= 0f)
                {
                    LastError = "地形数据无效(nodeDim/nodeSize)";
                    TacticalMapLog.Warn("Terrain data invalid.");
                    return false;
                }

                if (!scene.GetTerrainMinMaxHeight(out float minH, out float maxH))
                {
                    LastError = "GetTerrainMinMaxHeight 失败";
                    TacticalMapLog.Warn("GetTerrainMinMaxHeight failed.");
                    return false;
                }
                MinH = minH;
                MaxH = maxH;
                TacticalMapLog.Info("Terrain height range: min=" + minH + ", max=" + maxH);

                float fullWorldW = nodeDim.X * nodeSize;
                float fullWorldH = nodeDim.Y * nodeSize;
                if (!ComputeBattleBounds(scene, out Vec2 battleMin, out Vec2 battleMax))
                {
                    battleMin = Vec2.Zero;
                    battleMax = new Vec2(fullWorldW, fullWorldH);
                    TacticalMapLog.Warn("Battle bounds fallback to full terrain bounds.");
                }
                else
                {
                    TacticalMapLog.Info("Battle bounds: min=(" + battleMin.X + "," + battleMin.Y + ") max=(" + battleMax.X + "," + battleMax.Y + ")");
                }

                int res = Math.Max(1, _settings.BakeResolution);
                Width = res;
                Height = res;
                OriginX = battleMin.X;
                OriginY = battleMin.Y;
                WorldW = Math.Max(1f, battleMax.X - battleMin.X);
                WorldH = Math.Max(1f, battleMax.Y - battleMin.Y);
                CellStep = Math.Max(WorldW, WorldH) / res;

                int cacheSignature = ComputeCacheSignature(nodeDim, nodeSize, minH, maxH, battleMin, battleMax);
                if (SceneBakeCache.TryGetValue(scene, out var cached) && cached != null && cached.Signature == cacheSignature)
                {
                    ApplyCachedBake(cached, scene);
                    watch.Stop();
                    TacticalMapLog.Info("Terrain bake CACHE HIT. Reused Scene bake in " + watch.ElapsedMilliseconds + " ms.");
                    return true;
                }

                TacticalMapLog.Info("Bake resolution=" + Width + "x" + Height + ", world=" + WorldW + "x" + WorldH + ", cellStep=" + CellStep);

                Cells = new TerrainCell[Width, Height];
                float[,] heights = new float[Width, Height];
                var sampleWatch = Stopwatch.StartNew();

                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        Vec2 pos = CellCenter(x, y);
                        float h = scene.GetTerrainHeight(pos, true);
                        heights[x, y] = h;
                        scene.GetTerrainHeightAndNormal(pos, out _, out Vec3 normal);

                        int nodeX = (int)(pos.X / nodeSize);
                        int nodeY = (int)(pos.Y / nodeSize);
                        nodeX = Math.Max(0, Math.Min(nodeDim.X - 1, nodeX));
                        nodeY = Math.Max(0, Math.Min(nodeDim.Y - 1, nodeY));
                        short[] mat = scene.GetTerrainPhysicsMaterialIndexData(nodeX, nodeY);

                        Cells[x, y] = new TerrainCell
                        {
                            Height = h,
                            Normal = normal,
                            MaterialLayers = mat ?? new short[0]
                        };
                    }
                }

                sampleWatch.Stop();
                TacticalMapLog.Info("Terrain samples completed in " + sampleWatch.ElapsedMilliseconds + " ms.");

                var classifyWatch = Stopwatch.StartNew();
                TerrainAnalyzer.ClassifyAll(this, heights, _settings);
                classifyWatch.Stop();
                TacticalMapLog.Info("TerrainAnalyzer.ClassifyAll completed in " + classifyWatch.ElapsedMilliseconds + " ms. Cells=" + (Width * Height));

                var rgbaWatch = Stopwatch.StartNew();
                BuildBaseRGBA();
                BuildTacticalRGBA();
                rgbaWatch.Stop();
                TacticalMapLog.Info("Tactical terrain RGBA build completed in " + rgbaWatch.ElapsedMilliseconds + " ms. TerrainBytes=" + (TerrainBaseRGBA == null ? 0 : TerrainBaseRGBA.Length) + ", TacticalBytes=" + (TacticalRGBA == null ? 0 : TacticalRGBA.Length));

                AgentRGBA = new byte[Width * Height * 4];
                _baked = true;
                LastError = null;

                StoreCachedBake(scene, cacheSignature);

                watch.Stop();
                TacticalMapLog.Info("Terrain bake SUCCESS. Total=" + watch.ElapsedMilliseconds + " ms.");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _baked = false;
                TacticalMapLog.Error("TerrainCache.TryBake failed.", ex);
                Console.WriteLine("[TacticalMap] TerrainCache.TryBake failed: " + ex.Message);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 地形烘焙失败: {ex.GetType().Name}: {ex.Message}"));
                return false;
            }
        }

        private int ComputeCacheSignature(Vec2i nodeDim, float nodeSize, float minH, float maxH, Vec2 battleMin, Vec2 battleMax)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + nodeDim.X;
                hash = hash * 31 + nodeDim.Y;
                hash = hash * 31 + nodeSize.GetHashCode();
                hash = hash * 31 + minH.GetHashCode();
                hash = hash * 31 + maxH.GetHashCode();
                hash = hash * 31 + battleMin.X.GetHashCode();
                hash = hash * 31 + battleMin.Y.GetHashCode();
                hash = hash * 31 + battleMax.X.GetHashCode();
                hash = hash * 31 + battleMax.Y.GetHashCode();
                hash = hash * 31 + _settings.BakeResolution;
                hash = hash * 31 + _settings.CliffSlopeThreshold.GetHashCode();
                hash = hash * 31 + _settings.CliffHeightJump.GetHashCode();
                hash = hash * 31 + _settings.WaterHeightFraction.GetHashCode();
                hash = hash * 31 + _settings.HighGroundReferenceHeight.GetHashCode();
                var forestMaterials = _settings.ForestMaterialIndices;
                if (forestMaterials != null)
                {
                    hash = hash * 31 + forestMaterials.Length;
                    for (int i = 0; i < forestMaterials.Length; i++)
                        hash = hash * 31 + forestMaterials[i];
                }
                return hash;
            }
        }

        private void StoreCachedBake(Scene scene, int signature)
        {
            var snapshot = new CachedBake
            {
                Signature = signature,
                Width = Width,
                Height = Height,
                WorldW = WorldW,
                WorldH = WorldH,
                OriginX = OriginX,
                OriginY = OriginY,
                CellStep = CellStep,
                MinH = MinH,
                MaxH = MaxH,
                Cells = new CellSnapshot[Width * Height],
                TerrainBaseRGBA = TerrainBaseRGBA == null ? null : (byte[])TerrainBaseRGBA.Clone(),
                TacticalRGBA = TacticalRGBA == null ? null : (byte[])TacticalRGBA.Clone()
            };

            int index = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    TerrainCell c = Cells[x, y];
                    snapshot.Cells[index++] = new CellSnapshot
                    {
                        Height = c.Height,
                        Normal = c.Normal,
                        Slope = c.Slope,
                        Kind = c.Kind,
                        MovementCost = c.MovementCost,
                        RelativeHeight = c.RelativeHeight,
                        HighGround = c.HighGround,
                        HeightBreak = c.HeightBreak,
                        IsForest = c.IsForest,
                        IsCliff = c.IsCliff,
                        IsWater = c.IsWater
                    };
                }
            }

            SceneBakeCache.Remove(scene);
            SceneBakeCache.Add(scene, snapshot);
            TacticalMapLog.Info("Scene terrain cache stored. Signature=" + signature);
        }

        private void ApplyCachedBake(CachedBake cached, Scene scene)
        {
            _scene = scene;
            Width = cached.Width;
            Height = cached.Height;
            WorldW = cached.WorldW;
            WorldH = cached.WorldH;
            OriginX = cached.OriginX;
            OriginY = cached.OriginY;
            CellStep = cached.CellStep;
            MinH = cached.MinH;
            MaxH = cached.MaxH;

            Cells = new TerrainCell[Width, Height];
            int index = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    CellSnapshot c = cached.Cells[index++];
                    Cells[x, y] = new TerrainCell
                    {
                        Height = c.Height,
                        Normal = c.Normal,
                        Slope = c.Slope,
                        Kind = c.Kind,
                        MovementCost = c.MovementCost,
                        RelativeHeight = c.RelativeHeight,
                        HighGround = c.HighGround,
                        HeightBreak = c.HeightBreak,
                        Risk = c.MovementCost,
                        IsForest = c.IsForest,
                        IsCliff = c.IsCliff,
                        IsWater = c.IsWater,
                        MaterialLayers = new short[0],
                        DensityAgentCount = 0
                    };
                }
            }

            TerrainBaseRGBA = cached.TerrainBaseRGBA == null ? null : (byte[])cached.TerrainBaseRGBA.Clone();
            TacticalRGBA = cached.TacticalRGBA == null ? null : (byte[])cached.TacticalRGBA.Clone();
            AgentRGBA = new byte[Width * Height * 4];
            _baked = true;
            LastError = null;
        }

        public bool IsBaked => _baked;
        public string LastError { get; private set; }

        public Vec2 CellCenter(int x, int y)
        {
            return new Vec2(OriginX + (x + 0.5f) * CellStep, OriginY + (y + 0.5f) * CellStep);
        }

        public Vec2 UVToWorld(Vec2 uv)
        {
            return new Vec2(OriginX + (1f - uv.X) * WorldW, OriginY + uv.Y * WorldH);
        }

        public Vec2 WorldToUV(Vec2 world)
        {
            return new Vec2(1f - (world.X - OriginX) / WorldW, (world.Y - OriginY) / WorldH);
        }

        public float GetHeightAt(Vec2 world)
        {
            if (!_baked || _scene == null) return 0f;
            try { return _scene.GetTerrainHeight(world, true); }
            catch { return 0f; }
        }

        private bool ComputeBattleBounds(Scene scene, out Vec2 min, out Vec2 max)
        {
            int softCount = scene.GetSoftBoundaryVertexCount();
            if (softCount > 0)
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                for (int i = 0; i < softCount; i++)
                {
                    Vec2 v = scene.GetSoftBoundaryVertex(i);
                    if (v.X < minX) minX = v.X;
                    if (v.Y < minY) minY = v.Y;
                    if (v.X > maxX) maxX = v.X;
                    if (v.Y > maxY) maxY = v.Y;
                }
                float mx = (maxX - minX) * 0.1f;
                float my = (maxY - minY) * 0.1f;
                min = new Vec2(minX - mx, minY - my);
                max = new Vec2(maxX + mx, maxY + my);
                TacticalMapLog.Info("ComputeBattleBounds: using soft boundary vertices=" + softCount);
                return true;
            }

            scene.GetBoundingBox(out Vec3 bbMin, out Vec3 bbMax);
            if (bbMin.IsValid && bbMax.IsValid && bbMax.X > bbMin.X && bbMax.Y > bbMin.Y)
            {
                min = bbMin.AsVec2;
                max = bbMax.AsVec2;
                TacticalMapLog.Info("ComputeBattleBounds: using scene bounding box.");
                return true;
            }

            min = Vec2.Zero;
            max = Vec2.Zero;
            TacticalMapLog.Warn("ComputeBattleBounds: no usable bounds.");
            return false;
        }

        private void BuildBaseRGBA()
        {
            TerrainBaseRGBA = new byte[Width * Height * 4];
            float range = Math.Max(0.001f, MaxH - MinH);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    TerrainCell c = Cells[x, y];
                    float elevation = Clamp01((c.Height - MinH) / range);
                    float elevationBand = elevation < 0.5f
                        ? elevation / 0.5f
                        : 0.5f + (elevation - 0.5f) / 0.5f;

                    byte r = (byte)(52 + elevationBand * 140f);
                    byte g = (byte)(82 + elevationBand * 118f);
                    byte b = (byte)(54 + elevationBand * 125f);

                    float light = Clamp(c.Normal.x * -0.45f + c.Normal.y * -0.15f + c.Normal.z * 0.92f, 0f, 1f);
                    float shade = 0.72f + light * 0.42f;
                    r = ScaleByte(r, shade);
                    g = ScaleByte(g, shade);
                    b = ScaleByte(b, shade);

                    if (c.IsWater)
                    {
                        r = ScaleByte(r, 0.45f);
                        g = ScaleByte(g, 0.65f);
                        b = ScaleByte((byte)Math.Max(90, b), 1.25f);
                    }
                    else if (c.IsForest)
                    {
                        r = ScaleByte(r, 0.55f);
                        g = ScaleByte(g, 0.82f);
                        b = ScaleByte(b, 0.55f);
                    }
                    else if (c.IsCliff)
                    {
                        r = ScaleByte((byte)Math.Min(255, r + 26), 0.9f);
                        g = ScaleByte(g, 0.55f);
                        b = ScaleByte(b, 0.55f);
                    }

                    // Rough contour lines make elevation changes readable without depending on color alone.
                    float contourInterval = Math.Max(0.5f, range / 12f);
                    float phase = Math.Abs((c.Height / contourInterval) - (float)Math.Round(c.Height / contourInterval));
                    if (phase < 0.045f)
                    {
                        r = ScaleByte(r, 0.68f);
                        g = ScaleByte(g, 0.68f);
                        b = ScaleByte(b, 0.68f);
                    }

                    SetPixel(TerrainBaseRGBA, x, y, r, g, b, 255);
                }
            }
        }

        private void BuildTacticalRGBA()
        {
            TacticalRGBA = new byte[Width * Height * 4];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    TerrainCell c = Cells[x, y];
                    if (c.IsCliff)
                    {
                        SetPixel(TacticalRGBA, x, y, 235, 70, 55, 205);
                    }
                    else if (c.IsWater)
                    {
                        SetPixel(TacticalRGBA, x, y, 40, 110, 230, 185);
                    }
                    else
                    {
                        // Red: movement difficulty. Cyan: locally advantageous high ground.
                        float difficulty = c.MovementCost;
                        float high = c.HighGround;
                        byte r = (byte)(210f * difficulty);
                        byte g = (byte)(110f * (1f - difficulty) + 80f * high);
                        byte b = (byte)(105f + 125f * high);
                        byte a = (byte)(Math.Max(0f, difficulty * 155f + high * 70f));
                        if (a < 12) a = 0;
                        SetPixel(TacticalRGBA, x, y, r, g, b, a);
                    }
                }
            }
        }

        private static byte ScaleByte(byte value, float factor)
        {
            return (byte)Clamp(value * factor, 0f, 255f);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void SetPixel(byte[] buf, int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * Width + x) * 4;
            buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; buf[i + 3] = a;
        }

        public void GetPixel(byte[] buf, int x, int y, out byte r, out byte g, out byte b, out byte a)
        {
            int i = (y * Width + x) * 4;
            r = buf[i]; g = buf[i + 1]; b = buf[i + 2]; a = buf[i + 3];
        }

        public void ClearAgents()
        {
            if (AgentRGBA != null) Array.Clear(AgentRGBA, 0, AgentRGBA.Length);
        }

        public void PaintAgent(int gx, int gy, byte r, byte g, byte b, int radius = 1)
        {
            if (AgentRGBA == null) return;
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                int x = gx + dx, y = gy + dy;
                if (x < 0 || x >= Width || y < 0 || y >= Height) continue;
                SetPixel(AgentRGBA, x, y, r, g, b, 255);
            }
        }
    }
}
