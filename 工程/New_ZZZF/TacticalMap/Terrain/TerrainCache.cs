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
    /// 在战斗开局把 Scene 地形烘焙成一张低分辨率战术栅格。
    /// Scene 级缓存会复用已经采样/分类完成的数据，避免同一 Scene 重复调用引擎地形 API。
    /// 所有坐标约定：uv(0..1) -> 世界 (OriginX + uv.X*WorldW, OriginY + uv.Y*WorldH)。
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
            public byte[] RiskRGBA;
        }

        private struct CellSnapshot
        {
            public float Height;
            public Vec3 Normal;
            public float Slope;
            public TerrainKind Kind;
            public float Risk;
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
        public byte[] RiskRGBA { get; private set; }
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
                BuildRiskRGBA();
                rgbaWatch.Stop();
                TacticalMapLog.Info("Terrain RGBA build completed in " + rgbaWatch.ElapsedMilliseconds + " ms. TerrainBytes=" + (TerrainBaseRGBA == null ? 0 : TerrainBaseRGBA.Length) + ", RiskBytes=" + (RiskRGBA == null ? 0 : RiskRGBA.Length));

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
                RiskRGBA = RiskRGBA == null ? null : (byte[])RiskRGBA.Clone()
            };

            int index = 0;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = Cells[x, y];
                    snapshot.Cells[index++] = new CellSnapshot
                    {
                        Height = c.Height,
                        Normal = c.Normal,
                        Slope = c.Slope,
                        Kind = c.Kind,
                        Risk = c.Risk,
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
                    var c = cached.Cells[index++];
                    Cells[x, y] = new TerrainCell
                    {
                        Height = c.Height,
                        Normal = c.Normal,
                        Slope = c.Slope,
                        Kind = c.Kind,
                        Risk = c.Risk,
                        IsForest = c.IsForest,
                        IsCliff = c.IsCliff,
                        IsWater = c.IsWater,
                        MaterialLayers = new short[0],
                        DensityAgentCount = 0
                    };
                }
            }

            TerrainBaseRGBA = cached.TerrainBaseRGBA == null ? null : (byte[])cached.TerrainBaseRGBA.Clone();
            RiskRGBA = cached.RiskRGBA == null ? null : (byte[])cached.RiskRGBA.Clone();
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
                    var c = Cells[x, y];
                    float t = (c.Height - MinH) / range;
                    byte r, g, b;
                    if (t < 0.5f)
                    {
                        float k = t / 0.5f;
                        r = (byte)(60 + k * 60); g = (byte)(120 + k * (-10)); b = (byte)(40 + k * 30);
                    }
                    else
                    {
                        float k = (t - 0.5f) / 0.5f;
                        r = (byte)(120 + k * 115); g = (byte)(110 + k * 125); b = (byte)(70 + k * 165);
                    }

                    if (c.IsWater) { r = 40; g = 90; b = 200; }
                    else if (c.IsForest) { r = (byte)(r * 0.6f); g = (byte)(g * 0.85f); b = (byte)(b * 0.6f); }
                    else if (c.IsCliff) { r = (byte)(r * 0.9f + 40); g = (byte)(g * 0.5f); b = (byte)(b * 0.5f); }

                    SetPixel(TerrainBaseRGBA, x, y, r, g, b, 255);
                }
            }
        }

        private void BuildRiskRGBA()
        {
            RiskRGBA = new byte[Width * Height * 4];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = Cells[x, y];
                    if (c.IsCliff) SetPixel(RiskRGBA, x, y, 210, 50, 50, 150);
                    else if (c.IsWater) SetPixel(RiskRGBA, x, y, 50, 110, 220, 140);
                    else if (c.IsForest) SetPixel(RiskRGBA, x, y, 50, 180, 70, 100);
                    else SetPixel(RiskRGBA, x, y, 0, 0, 0, 0);
                }
            }
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
