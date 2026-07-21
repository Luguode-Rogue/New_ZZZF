using System;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace New_ZZZF.TacticalMap.Terrain
{
    /// <summary>
    /// 在战斗开局把 Scene 地形烘焙成一张低分辨率战术栅格。
    /// 只做一次（或场景变化时），之后由 MinimapCompositor 复用。
    /// 所有坐标约定：uv(0..1) -> 世界 (uv.X*WorldW, uv.Y*WorldH)，原点在场景 (0,0)。
    /// </summary>
    public sealed class TerrainCache
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public float WorldW { get; private set; }
        public float WorldH { get; private set; }
        public float CellStep { get; private set; }
        public float MinH { get; private set; }
        public float MaxH { get; private set; }

        public TerrainCell[,] Cells { get; private set; }

        // 烘焙一次的静态底图（高度色 + 材质着色）与风险层（半透明叠加）
        public byte[] TerrainBaseRGBA { get; private set; }
        public byte[] RiskRGBA { get; private set; }
        // 动态单位层：每单位一个彩色点（透明背景），由 AgentTracker 每帧节流刷新，
        // 整体烘焙成纹理绘制（单 draw call，清晰呈现成千上万单位的真实分布）。
        public byte[] AgentRGBA { get; private set; }

        private readonly TacticalMap.Config.TacticalSettings _settings;
        private Scene _scene;
        private bool _baked;

        public TerrainCache(TacticalMap.Config.TacticalSettings settings)
        {
            _settings = settings;
        }

        public bool TryBake(Scene scene)
        {
            _scene = scene;
            try
            {
                scene.GetTerrainData(out Vec2i nodeDim, out float nodeSize, out _, out _);
                if (nodeDim.X <= 0 || nodeDim.Y <= 0 || nodeSize <= 0f) { LastError = "地形数据无效(nodeDim/nodeSize)"; return false; }
                if (!scene.GetTerrainMinMaxHeight(out float minH, out float maxH)) { LastError = "GetTerrainMinMaxHeight 失败"; return false; }

                WorldW = nodeDim.X * nodeSize;
                WorldH = nodeDim.Y * nodeSize;
                MinH = minH;
                MaxH = maxH;

                int res = _settings.BakeResolution;
                Width = res;
                Height = res;
                CellStep = WorldW / res; // 假设近正方形；非正方形场景按比例缩放也可，这里取 X

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
                BuildRiskRGBA();
                AgentRGBA = new byte[Width * Height * 4]; // 初始全 0 = 全透明
                _baked = true;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Console.WriteLine("[TacticalMap] TerrainCache.TryBake failed: " + ex.Message);
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 地形烘焙失败: {ex.GetType().Name}: {ex.Message}"));
                _baked = false;
                return false;
            }
        }

        public bool IsBaked => _baked;
        public string LastError { get; private set; }

        public Vec2 CellCenter(int x, int y)
        {
            return new Vec2((x + 0.5f) * CellStep, (y + 0.5f) * CellStep);
        }

        public Vec2 UVToWorld(Vec2 uv)
        {
            return new Vec2(uv.X * WorldW, uv.Y * WorldH);
        }

        public Vec2 WorldToUV(Vec2 world)
        {
            return new Vec2(world.X / WorldW, world.Y / WorldH);
        }

        public float GetHeightAt(Vec2 world)
        {
            if (!_baked || _scene == null) return 0f;
            try { return _scene.GetTerrainHeight(world, true); }
            catch { return 0f; }
        }

        // --- 颜色工具 ---
        private void BuildBaseRGBA()
        {
            TerrainBaseRGBA = new byte[Width * Height * 4];
            float range = Math.Max(0.001f, MaxH - MinH);
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    var c = Cells[x, y];
                    float t = (c.Height - MinH) / range; // 0..1
                    // 高度色带：低绿 -> 中棕 -> 高灰 -> 顶白
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

        // --- 动态单位层维护 ---
        public void ClearAgents()
        {
            if (AgentRGBA != null) Array.Clear(AgentRGBA, 0, AgentRGBA.Length);
        }

        // 在 (gx,gy) 周围 radius 范围内画一个不透明点（默认 3x3），双线性拉伸后仍清晰可辨。
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
