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
            public float RelativeHeight;
            public float MovementCost;
            public float HeightBreak;
            public TerrainKind Kind;
            public float Risk;
            public bool IsForest;
            public bool IsCliff;
            public bool IsWater;
            public bool IsHighGround;
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
        public byte[] RiskRGBA => TacticalRGBA;
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
            _scene = scene;
            if (scene == null)
            {
                LastError = "scene 为 null";
                return false;
            }

            if (!MissionSceneGuard.IsSceneTerrainReady(scene))
            {
                LastError = "当前场景无地形数据（酒馆/城镇等室内场景）";
                _baked = false;
                return false;
            }

            try
            {
                scene.GetTerrainData(out Vec2i nodeDim, out float nodeSize, out _, out _);
                if (nodeDim.X <= 0 || nodeDim.Y <= 0 || nodeSize <= 0f)
                {
                    LastError = "地形数据无效(nodeDim/nodeSize)";
                    return false;
                }

                if (!scene.GetTerrainMinMaxHeight(out float minH, out float maxH))
                {
                    LastError = "GetTerrainMinMaxHeight 失败";
                    return false;
                }
                MinH = minH;
                MaxH = maxH;

                float fullWorldW = nodeDim.X * nodeSize;
                float fullWorldH = nodeDim.Y * nodeSize;
                if (!ComputeBattleBounds(scene, out Vec2 battleMin, out Vec2 battleMax))
                {
                    battleMin = Vec2.Zero;
                    battleMax = new Vec2(fullWorldW, fullWorldH);
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

                Cells = new TerrainCell[Width, Height];
                float[,] heights = new float[Width, Height];
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

                TerrainAnalyzer.ClassifyAll(this, heights, _settings);
                BuildBaseRGBA();
                BuildTacticalRGBA();

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
                    var c = Cells[x, y];
                    snapshot.Cells[index++] = new CellSnapshot
                    {
                        Height = c.Height,
                        Normal = c.Normal,
                        Slope = c.Slope,
                        RelativeHeight = c.RelativeHeight,
                        MovementCost = c.MovementCost,
                        HeightBreak = c.HeightBreak,
                        Kind = c.Kind,
                        Risk = c.Risk,
                        IsForest = c.IsForest,
                        IsCliff = c.IsCliff,
                        IsWater = c.IsWater,
                        IsHighGround = c.IsHighGround
                    };
                }
            }

            SceneBakeCache.Remove(scene);
            SceneBakeCache.Add(scene, snapshot);
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
                        RelativeHeight = c.RelativeHeight,
                        MovementCost = c.MovementCost,
                        HeightBreak = c.HeightBreak,
                        Kind = c.Kind,
                        Risk = c.Risk,
                        IsForest = c.IsForest,
                        IsCliff = c.IsCliff,
                        IsWater = c.IsWater,
                        IsHighGround = c.IsHighGround,
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
                return true;
            }

            scene.GetBoundingBox(out Vec3 bbMin, out Vec3 bbMax);
            if (bbMin.IsValid && bbMax.IsValid && bbMax.X > bbMin.X && bbMax.Y > bbMin.Y)
            {
                min = bbMin.AsVec2;
                max = bbMax.AsVec2;
                return true;
            }

            min = Vec2.Zero;
            max = Vec2.Zero;
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
                    float elevationBand = (c.Height - MinH) / range;
                    elevationBand = Clamp(elevationBand, 0f, 1f);
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
                        b = ScaleByte((byte)Math.Max(90, (int)b), 1.25f);
                    }
                    else if (c.IsForest)
                    {
                        r = ScaleByte(r, 0.55f);
                        g = ScaleByte(g, 0.82f);
                        b = ScaleByte(b, 0.55f);
                    }
                    else if (c.IsCliff)
                    {
                        r = ScaleByte((byte)Math.Min(255, (int)r + 26), 0.9f);
                        g = ScaleByte(g, 0.55f);
                        b = ScaleByte(b, 0.55f);
                    }

                    if (c.IsHighGround)
                    {
                        r = ScaleByte(r, 1.08f);
                        g = ScaleByte(g, 1.08f);
                        b = ScaleByte(b, 1.08f);
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
                    var c = Cells[x, y];
                    byte alpha = (byte)Math.Min(190, Math.Max(0, (int)(c.MovementCost * 110f)));
                    byte r = 212, g = 72, b = 52;
                    if (c.IsHighGround)
                    {
                        r = 255; g = 206; b = 72;
                        alpha = (byte)Math.Max((int)alpha, 85);
                    }
                    if (c.IsWater)
                    {
                        r = 55; g = 115; b = 220;
                        alpha = 145;
                    }
                    else if (c.IsForest)
                    {
                        r = 65; g = 190; b = 95;
                        alpha = (byte)Math.Min(150, Math.Max(60, (int)alpha));
                    }
                    else if (!c.IsCliff && !c.IsHighGround && c.MovementCost < 0.18f)
                    {
                        alpha = 0;
                    }
                    SetPixel(TacticalRGBA, x, y, r, g, b, alpha);
                }
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static byte ScaleByte(byte value, float scale)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)(value * scale)));
        }

        public void SetPixel(byte[] buf, int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * Width + x) * 4;
            buf[i] = r; buf[i + 1] = g; buf[i + 2] = b; buf[i + 3] = a;
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
