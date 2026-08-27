using System;
using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using New_ZZZF.TacticalMap.Terrain;
using New_ZZZF.TacticalMap.Tracking;

namespace New_ZZZF.TacticalMap.UI
{
    public sealed class MinimapWidget : Widget
    {
        public TacticalMapController Controller { get; set; }

        public MinimapWidget(UIContext context) : base(context) { }

        private static bool _warnedNoCtrl, _warnedNotBaked, _warnedArea, _warnedDrawn, _warnedRenderErr;
        private static bool _warnedNoWhite, _warnedNoTerrain, _warnedNoRisk, _warnedNoForm, _warnedNoPlayer;
        private static int _renderErrDiagCount;
        private static readonly bool UseBakedTexture = false;
        private static Texture _whiteTex;
        private static TaleWorlds.TwoDimension.Texture _terrainTex;
        private static TaleWorlds.TwoDimension.Texture _riskTex;
        private static TaleWorlds.Engine.Texture _terrainETex;
        private static TaleWorlds.Engine.Texture _riskETex;
        private static TaleWorlds.TwoDimension.Texture _agentTex;
        private static TaleWorlds.Engine.Texture _agentETex;
        private static int _agentTexVer = -1;
        private static TacticalMapController _texCtrl;

        private static void WarnOnce(ref bool flag, string msg)
        {
            if (flag) return;
            flag = true;
            InformationManager.DisplayMessage(new InformationMessage(msg));
        }

        private static void Diag(string msg)
        {
            try { TaleWorlds.Library.Debug.Print("[TMap] " + msg); } catch { }
            try
            {
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "tmap_diag.log");
                System.IO.File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\n");
            }
            catch { }
        }

        private static string TopFrame(Exception ex)
        {
            try
            {
                string st = ex.StackTrace;
                if (string.IsNullOrEmpty(st)) return "(无堆栈)";
                foreach (var l in st.Split('\n'))
                {
                    string s = l.Trim();
                    if (s.StartsWith("at ")) return s;
                }
                return st.Split('\n')[0].Trim();
            }
            catch { return "(取堆栈失败)"; }
        }

        private static string WidgetSizeStr(Widget w)
        {
            try
            {
                var t = w.GetType();
                var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var pw = t.GetProperty("Width", bf) ?? t.GetProperty("width", bf);
                var ph = t.GetProperty("Height", bf) ?? t.GetProperty("height", bf);
                string fw = "?";
                if (pw != null) { try { fw = System.Convert.ToSingle(pw.GetValue(w)).ToString("F1"); } catch { } }
                string fh = "?";
                if (ph != null) { try { fh = System.Convert.ToSingle(ph.GetValue(w)).ToString("F1"); } catch { } }
                return fw + "," + fh;
            }
            catch { return "?"; }
        }

        private static void EnsureWhiteTexture(UIContext uiContext)
        {
            if (_whiteTex != null) return;
            try
            {
                byte[] white = new byte[] { 255, 255, 255, 255 };
                var eTex = TaleWorlds.Engine.Texture.CreateFromByteArray(white, 1, 1);
                if (eTex != null)
                {
                    eTex.SetTextureAsAlwaysValid();
                    _whiteTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
                    Diag("WHT 已用字节数组创建纯白纹理");
                    return;
                }
            }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 白纹理(字节数组)创建失败: {ex.Message}")); Diag("WHT 字节创建失败: " + ex.Message); }

            if (uiContext == null) return;
            try
            {
                var cbf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var type = uiContext.GetType();
                Diag($"WHT ctxType={type.FullName}");
                var sdProp = type.GetProperty("SpriteData", cbf);
                if (sdProp == null) { Diag("WHT no SpriteData prop on UIContext"); return; }
                object spriteData = sdProp.GetValue(uiContext);
                if (spriteData == null) { Diag("WHT SpriteData is null"); return; }
                Diag($"WHT spriteData={spriteData.GetType().FullName}");
                var getSprite = spriteData.GetType().GetMethod("GetSprite", cbf);
                if (getSprite == null) { Diag("WHT no GetSprite method"); return; }
                foreach (var name in new[] { "blank", "Blank", "white", "White", "ui/blank", "BlankWhite", "blank_white" })
                {
                    object sprite = null;
                    try { sprite = getSprite.Invoke(spriteData, new object[] { name }); } catch { sprite = null; }
                    if (sprite == null) continue;
                    var texProp = sprite.GetType().GetProperty("Texture", cbf);
                    var tex = texProp?.GetValue(sprite) as Texture;
                    if (tex != null && tex.IsValid) { _whiteTex = tex; Diag($"WHT got white via '{name}'"); return; }
                }
                var spProp = spriteData.GetType().GetProperty("Sprites", cbf);
                var dict = spProp?.GetValue(spriteData) as System.Collections.IDictionary;
                if (dict != null)
                {
                    var validNames = new System.Collections.Generic.List<string>();
                    Texture fallback = null;
                    foreach (System.Collections.DictionaryEntry e in dict)
                    {
                        var sprite = e.Value;
                        var texProp = sprite.GetType().GetProperty("Texture", cbf);
                        var tex = texProp?.GetValue(sprite) as Texture;
                        string key = e.Key?.ToString() ?? "";
                        if (tex != null && tex.IsValid)
                        {
                            if (key.IndexOf("blank", System.StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("white", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            { _whiteTex = tex; Diag($"WHT got white via enum '{key}'"); return; }
                            if (fallback == null) fallback = tex;
                            validNames.Add(key);
                        }
                    }
                    if (fallback != null) { _whiteTex = fallback; Diag("WHT fallback non-white texture. validSprites=" + string.Join(",", validNames)); return; }
                }
                Diag("WHT no usable texture found");
            }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] 白纹理(反射)获取失败: {ex.Message}")); Diag($"WHT ex={ex.Message}"); }
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);
            try
            {
                var ctrl = Controller;
                if (ctrl == null) { WarnOnce(ref _warnedNoCtrl, "[TMap] OnRender: Controller 为空"); return; }
                if (!ctrl.Cache.IsBaked) { WarnOnce(ref _warnedNotBaked, $"[TMap] OnRender: 地形未烘焙 ({ctrl.Cache.Width}x{ctrl.Cache.Height})"); return; }

                Rectangle2D area = this.AreaRect;
                var box = area.GetBoundingBox();
                float ox = box.X;
                float oy = box.Y;
                float w = box.Width;
                float h = box.Height;
                if (w <= 0f || h <= 0f) { WarnOnce(ref _warnedArea, "[TMap] OnRender: Size 无效"); return; }

                var cache = ctrl.Cache;
                var s = TacticalSettings.Instance;

                EnsureWhiteTexture(this.Context);
                if (_whiteTex == null) WarnOnce(ref _warnedNoWhite, "[TMap] 白色纹理为空：矩形/标记可能无法显示或报错");
                DrawRect(drawContext, ox, oy, w, h, new Color(0.04f, 0.06f, 0.09f, 0.85f));
                WarnOnce(ref _warnedDrawn, "[TMap] OnRender: 正在绘制小地图");
                DrawRect(drawContext, ox - 3f, oy - 3f, w + 6f, h + 6f, new Color(0f, 0f, 0f, 0.35f));
                DrawRectFrame(drawContext, ox - 2f, oy - 2f, w + 4f, h + 4f, 2f, new Color(0.02f, 0.03f, 0.04f, 0.9f));
                DrawRectFrame(drawContext, ox, oy, w, h, Math.Max(1.5f, w * 0.006f), new Color(0.78f, 0.72f, 0.55f, 0.85f));

                EnsureTerrainTexture(ctrl);
                if (_terrainTex == null) WarnOnce(ref _warnedNoTerrain, "[TMap] 地形纹理创建失败：已降级为逐像素绘制（可能卡/不显示）");
                if (_terrainTex != null)
                {
                    DrawTexture(drawContext, _terrainTex, ox, oy, w, h);
                    if (s.EnableRiskOverlay)
                    {
                        EnsureRiskTexture(ctrl);
                        if (_riskTex == null) WarnOnce(ref _warnedNoRisk, "[TMap] 风险纹理创建失败：不显示风险叠加层");
                        if (_riskTex != null) DrawTexture(drawContext, _riskTex, ox, oy, w, h);
                    }
                }
                else
                {
                    int cols = Math.Min(48, cache.Width);
                    int step = Math.Max(1, cache.Width / cols);
                    float cw = w / (cache.Width / (float)step);
                    float ch = h / (cache.Height / (float)step);
                    bool showRisk = s.EnableRiskOverlay;
                    bool showDensity = s.EnableDensityHeatmap;
                    bool showAgents = s.EnableAgentMarkers;
                    var agentData = showAgents ? ctrl.AgentRGBA : null;

                    for (int x = 0; x < cache.Width; x += step)
                    for (int y = 0; y < cache.Height; y += step)
                    {
                        cache.GetPixel(cache.TerrainBaseRGBA, x, y, out byte r, out byte g, out byte b, out _);
                        float rf = r / 255f, gf = g / 255f, bf = b / 255f;
                        if (showRisk)
                        {
                            cache.GetPixel(cache.RiskRGBA, x, y, out byte rr, out byte rg, out byte rb, out byte ra);
                            if (ra > 0)
                            {
                                float ka = ra / 255f;
                                float red = rr / 255f;
                                rf = rf + (red - rf) * ka;
                                gf = gf * (1f - ka * 0.85f);
                                bf = bf * (1f - ka * 0.85f);
                            }
                        }
                        if (showDensity)
                        {
                            int dens = cache.Cells[x, y].DensityAgentCount;
                            if (dens > 0)
                            {
                                float t = Math.Min(1f, dens / 12f);
                                float ka = Math.Min(0.35f, 0.08f + t * 0.22f);
                                float tr = 1f;
                                float tg = 0.55f + t * 0.4f;
                                float tb = 0.15f + t * 0.55f;
                                rf = rf * (1f - ka) + tr * ka;
                                gf = gf * (1f - ka) + tg * ka;
                                bf = bf * (1f - ka) + tb * ka;
                            }
                        }
                        if (showAgents && agentData != null)
                        {
                            cache.GetPixel(agentData, x, y, out byte ar, out byte ag, out byte ab, out byte aa);
                            if (aa > 0)
                            {
                                float ka = aa / 255f;
                                rf = rf * (1f - ka) + (ar / 255f) * ka;
                                gf = gf * (1f - ka) + (ag / 255f) * ka;
                                bf = bf * (1f - ka) + (ab / 255f) * ka;
                            }
                        }
                        float px = ox + (x / (float)cache.Width) * w;
                        float py = oy + (y / (float)cache.Height) * h;
                        DrawRect(drawContext, px, py, cw + 0.5f, ch + 0.5f, new Color(rf, gf, bf, 1f));
                    }
                }

                if (s.EnableUnitMarkers)
                {
                    var snaps = ctrl.FormationSnapshots;
                    if (snaps == null) WarnOnce(ref _warnedNoForm, "[TMap] 编队快照为空：看不到编队标记（数据未就绪/Controller 异常）");
                    if (snaps != null)
                    {
                        float fs = Math.Max(9f, w * 0.04f);
                        float ft = Math.Max(1.5f, w * 0.008f);
                        foreach (var f in snaps)
                        {
                            if (f == null) continue;
                            Vec2 uv = cache.WorldToUV(f.AveragePosition);
                            if (uv.X < 0f || uv.X > 1f || uv.Y < 0f || uv.Y > 1f) continue;
                            float px = ox + uv.X * w;
                            float py = oy + uv.Y * h;
                            Color frame;
                            if (f.IsPlayer) frame = new Color(1f, 1f, 1f, 0.95f);
                            else if (f.IsEnemy) frame = new Color(1f, 0.2f, 0.2f, 0.95f);
                            else frame = new Color(0.25f, 1f, 0.35f, 0.95f);
                            Color c = Color.FromUint(f.Color);
                            Vec2 dir = f.Facing.LengthSquared > 1E-4f ? f.Facing.Normalized() : new Vec2(0f, 1f);
                            Vec2 fwd = dir;
                            Vec2 right = new Vec2(-dir.Y, dir.X);
                            Vec2 tip = new Vec2(px + fwd.X * fs * 0.6f, py + fwd.Y * fs * 0.6f);
                            Vec2 bl = new Vec2(px - fwd.X * fs * 0.45f + right.X * fs * 0.45f, py - fwd.Y * fs * 0.45f + right.Y * fs * 0.45f);
                            Vec2 br = new Vec2(px - fwd.X * fs * 0.45f - right.X * fs * 0.45f, py - fwd.Y * fs * 0.45f - right.Y * fs * 0.45f);
                            float glow = ft * 2.5f;
                            DrawLine(drawContext, tip.X + fwd.X * glow, tip.Y + fwd.Y * glow, bl.X - right.X * glow, bl.Y - right.Y * glow, new Color(frame.Red, frame.Green, frame.Blue, 0.22f), glow);
                            DrawLine(drawContext, bl.X - right.X * glow, bl.Y - right.Y * glow, br.X + right.X * glow, br.Y + right.Y * glow, new Color(frame.Red, frame.Green, frame.Blue, 0.22f), glow);
                            DrawLine(drawContext, br.X + right.X * glow, br.Y + right.Y * glow, tip.X + fwd.X * glow, tip.Y + fwd.Y * glow, new Color(frame.Red, frame.Green, frame.Blue, 0.22f), glow);
                            DrawLine(drawContext, tip.X, tip.Y, bl.X, bl.Y, c, Math.Max(ft, fs * 0.5f));
                            DrawLine(drawContext, bl.X, bl.Y, br.X, br.Y, c, Math.Max(ft, fs * 0.5f));
                            DrawLine(drawContext, br.X, br.Y, tip.X, tip.Y, c, Math.Max(ft, fs * 0.5f));
                            DrawRect(drawContext, px - fs * 0.18f, py - fs * 0.18f, fs * 0.36f, fs * 0.36f, c);
                            DrawLine(drawContext, tip.X + fwd.X * ft, tip.Y + fwd.Y * ft, bl.X - right.X * ft, bl.Y - right.Y * ft, frame, ft);
                            DrawLine(drawContext, bl.X - right.X * ft, bl.Y - right.Y * ft, br.X + right.X * ft, br.Y + right.Y * ft, frame, ft);
                            DrawLine(drawContext, br.X + right.X * ft, br.Y + right.Y * ft, tip.X + fwd.X * ft, tip.Y + fwd.Y * ft, frame, ft);
                            DrawLine(drawContext, tip.X, tip.Y, px + fwd.X * fs * 1.4f, py + fwd.Y * fs * 1.4f, frame);
                        }
                    }
                }

                if (!ctrl.PlayerPos.HasValue) WarnOnce(ref _warnedNoPlayer, "[TMap] 玩家位置为空：不显示玩家标记");
                if (ctrl.PlayerPos.HasValue)
                {
                    Vec2 uv = cache.WorldToUV(ctrl.PlayerPos.Value);
                    if (uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f)
                    {
                        float px = ox + uv.X * w;
                        float py = oy + uv.Y * h;
                        float pr = Math.Max(10f, w * 0.05f);
                        // 只显示玩家实际世界朝向，不使用 TacticalMap 相机目标。
                        Vec2 pdir = ctrl.PlayerFacing.LengthSquared > 1E-4f
                            ? ctrl.PlayerFacing.Normalized()
                            : new Vec2(0f, 1f);
                        DrawRectFrame(drawContext, px - pr / 2f - 2f, py - pr / 2f - 2f, pr + 4f, pr + 4f, Math.Max(2f, w * 0.01f), new Color(0f, 1f, 1f, 0.3f));
                        DrawRectFrame(drawContext, px - pr / 2f, py - pr / 2f, pr, pr, Math.Max(2.5f, w * 0.012f), new Color(0f, 1f, 1f, 1f));
                        DrawLine(drawContext, px, py, px + pdir.X * pr * 0.7f, py + pdir.Y * pr * 0.7f, new Color(1f, 1f, 0.2f, 1f), Math.Max(2.5f, w * 0.012f));
                        DrawRect(drawContext, px - 3f, py - 3f, 6f, 6f, new Color(1f, 1f, 0.2f, 1f));
                    }
                }

                if (ctrl.CameraTarget.HasValue)
                {
                    Vec2 uv = cache.WorldToUV(ctrl.CameraTarget.Value);
                    float px = ox + uv.X * w;
                    float py = oy + uv.Y * h;
                    float d = Math.Max(6f, w * 0.03f);
                    DrawDiamond(drawContext, px, py, d, new Color(1f, 0.8f, 0.2f, 1f), 3f);
                }
            }
            catch (Exception ex)
            {
                string where = TopFrame(ex);
                WarnOnce(ref _warnedRenderErr, $"[TMap] OnRender 异常: {ex.GetType().Name}: {ex.Message} @ {where}");
                if (_renderErrDiagCount < 5)
                {
                    _renderErrDiagCount++;
                    Diag("OnRender EXCEPTION 完整堆栈:\n" + ex.ToString());
                }
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, System.Reflection.MemberInfo> _rectMembers =
            new System.Collections.Generic.Dictionary<string, System.Reflection.MemberInfo>();

        private static System.Reflection.MemberInfo ResolveRectMember(string name)
        {
            System.Reflection.MemberInfo m;
            if (_rectMembers.TryGetValue(name, out m)) return m;
            var t = typeof(TaleWorlds.TwoDimension.Rectangle2D);
            var bf = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            System.Reflection.FieldInfo fi = t.GetField(name, bf) ?? t.GetField(char.ToLowerInvariant(name[0]) + name.Substring(1), bf);
            if (fi != null)
                m = fi;
            else
            {
                var pi = t.GetProperty(name, bf);
                m = (pi != null && pi.CanWrite) ? (System.Reflection.MemberInfo)pi : null;
            }
            _rectMembers[name] = m;
            return m;
        }

        private static void SetRectMember(ref Rectangle2D rect, string name, object value)
        {
            var m = ResolveRectMember(name);
            if (m == null) return;
            object boxed = rect;
            System.Type targetType = (m is System.Reflection.FieldInfo fi) ? fi.FieldType
                : (m is System.Reflection.PropertyInfo pi) ? pi.PropertyType : null;
            object coerced = (targetType != null) ? CoerceToTargetType(targetType, value) : value;
            if (m is System.Reflection.FieldInfo fi2)
                fi2.SetValue(boxed, coerced);
            else if (m is System.Reflection.PropertyInfo pi2)
                pi2.SetValue(boxed, coerced);
            rect = (Rectangle2D)boxed;
        }

        private static object CoerceToTargetType(System.Type targetType, object value)
        {
            if (value is System.Numerics.Vector2 v
                && targetType.FullName == "System.Numerics.Vector2"
                && targetType.Assembly != typeof(System.Numerics.Vector2).Assembly)
            {
                return System.Activator.CreateInstance(targetType, v.X, v.Y);
            }
            return value;
        }

        private static void SetRectPosition(ref Rectangle2D rect, Vector2 pos)
        {
            SetRectMember(ref rect, "LocalPosition", pos);
        }

        private static void DrawRect(TwoDimensionDrawContext ctx, float x, float y, float w, float h, Color color)
        {
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x, y));
            SetRectMember(ref r, "LocalScale", new Vector2(w, h));
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = _whiteTex;
            mat.Color = color;
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }

        private static void DrawLine(TwoDimensionDrawContext ctx, float x1, float y1, float x2, float y2, Color color, float width = 2f)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.001f) return;
            float ang = (float)Math.Atan2(dy, dx);
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x1, y1));
            SetRectMember(ref r, "LocalScale", new Vector2(len, width));
            SetRectMember(ref r, "LocalRotation", ang);
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = _whiteTex;
            mat.Color = color;
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }

        private static void DrawDiamond(TwoDimensionDrawContext ctx, float cx, float cy, float d, Color color, float width = 3f)
        {
            float tx = cx, ty = cy - d;
            float rx = cx + d, ry = cy;
            float bx = cx, by = cy + d;
            float lx = cx - d, ly = cy;
            DrawLine(ctx, tx, ty, rx, ry, color, width);
            DrawLine(ctx, rx, ry, bx, by, color, width);
            DrawLine(ctx, bx, by, lx, ly, color, width);
            DrawLine(ctx, lx, ly, tx, ty, color, width);
        }

        private static void DrawRectFrame(TwoDimensionDrawContext ctx, float x, float y, float w, float h, float t, Color color)
        {
            DrawRect(ctx, x, y, w, t, color);
            DrawRect(ctx, x, y + h - t, w, t, color);
            DrawRect(ctx, x, y, t, h, color);
            DrawRect(ctx, x + w - t, y, t, h, color);
        }

        private static TaleWorlds.Engine.Texture TryCreateEngineTexture(byte[] rgba, int w, int h, bool swapRB)
        {
            try
            {
                byte[] data;
                if (swapRB)
                {
                    data = new byte[rgba.Length];
                    for (int i = 0; i < rgba.Length; i += 4)
                    {
                        data[i] = rgba[i + 2];
                        data[i + 1] = rgba[i + 1];
                        data[i + 2] = rgba[i];
                        data[i + 3] = 255;
                    }
                }
                else
                {
                    data = new byte[rgba.Length];
                    Buffer.BlockCopy(rgba, 0, data, 0, rgba.Length);
                    for (int i = 3; i < data.Length; i += 4) data[i] = 255;
                }
                var tex = TaleWorlds.Engine.Texture.CreateFromByteArray(data, w, h);
                if (tex != null) tex.SetTextureAsAlwaysValid();
                return tex;
            }
            catch { return null; }
        }

        private static void EnsureTerrainTexture(TacticalMapController ctrl)
        {
            if (_terrainTex != null && _texCtrl == ctrl) return;
            if (_terrainTex != null) { _terrainETex?.Release(); _terrainETex = null; _terrainTex = null; }
            if (_riskTex != null) { _riskETex?.Release(); _riskETex = null; _riskTex = null; }
            if (_agentTex != null) { _agentETex?.Release(); _agentETex = null; _agentTex = null; _agentTexVer = -1; }
            if (!UseBakedTexture) return;
            _texCtrl = ctrl;
            if (ctrl == null) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) { _texCtrl = null; return; }
            int W = cache.Width, H = cache.Height;
            byte[] td = cache.TerrainBaseRGBA;
            if (td == null || td.Length < W * H * 4) { _texCtrl = null; return; }
            var eTex = TryCreateEngineTexture(td, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(td, W, H, true);
            if (eTex == null) { _texCtrl = null; return; }
            _terrainETex = eTex;
            _terrainTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
        }

        private static void EnsureRiskTexture(TacticalMapController ctrl)
        {
            if (_riskTex != null && _texCtrl == ctrl) return;
            if (_riskTex != null) { _riskETex?.Release(); _riskETex = null; _riskTex = null; }
            if (!UseBakedTexture) return;
            if (ctrl == null) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) return;
            int W = cache.Width, H = cache.Height;
            byte[] rd = cache.RiskRGBA;
            if (rd == null || rd.Length < W * H * 4) return;
            var eTex = TryCreateEngineTexture(rd, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(rd, W, H, true);
            if (eTex == null) return;
            _riskETex = eTex;
            _riskTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
        }

        private static void EnsureAgentTexture(TacticalMapController ctrl)
        {
            if (ctrl == null) { ReleaseAgent(); return; }
            if (!UseBakedTexture) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) return;
            if (_agentTex != null && _texCtrl == ctrl && _agentTexVer == ctrl.AgentDataVersion) return;
            ReleaseAgent();
            int W = cache.Width, H = cache.Height;
            byte[] ad = ctrl.AgentRGBA;
            if (ad == null || ad.Length < W * H * 4) return;
            var eTex = TryCreateEngineTexture(ad, W, H, false);
            if (eTex == null) eTex = TryCreateEngineTexture(ad, W, H, true);
            if (eTex == null) return;
            _agentETex = eTex;
            _agentTex = new TaleWorlds.TwoDimension.Texture(new TaleWorlds.Engine.GauntletUI.EngineTexture(eTex));
            _agentTexVer = ctrl.AgentDataVersion;
            _texCtrl = ctrl;
        }

        private static void ReleaseAgent()
        {
            if (_agentTex != null) { _agentETex?.Release(); _agentETex = null; _agentTex = null; _agentTexVer = -1; }
        }

        private static void DrawTexture(TwoDimensionDrawContext ctx, TaleWorlds.TwoDimension.Texture tex, float x, float y, float w, float h)
        {
            Rectangle2D r = Rectangle2D.Create();
            SetRectPosition(ref r, new Vector2(x, y));
            SetRectMember(ref r, "LocalScale", new Vector2(w, h));
            r.CalculateMatrixFrame(default(Rectangle2D));
            var mat = new SimpleMaterial();
            mat.Texture = tex;
            mat.Color = new Color(1f, 1f, 1f, 1f);
            ImageDrawObject obj = ImageDrawObject.Create(r, Vec2.Zero, Vec2.One);
            ctx.Draw(mat, obj);
        }
    }
}
