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
    /// <summary>
    /// 小地图绘制控件：在 OnRender 里绘制地形栅格、编队标记、镜头目标。
    /// 地形/风险改用烘焙纹理（运行时字节数组经 TaleWorlds.Engine.Texture.CreateFromByteArray
    /// 创建，整体拉伸绘制，双线性平滑），draw call 从约万级降到个位数；
    /// 密度热力图仍为动态图元叠加。
    /// </summary>
    public sealed class MinimapWidget : Widget
    {
        public TacticalMapController Controller { get; set; }

        public MinimapWidget(UIContext context) : base(context) { }

        private static bool _warnedNoCtrl, _warnedNotBaked, _warnedArea, _warnedDrawn, _warnedRenderErr;
        private static bool _warnedNoWhite, _warnedNoTerrain, _warnedNoRisk, _warnedNoForm, _warnedNoPlayer;
        private static int _renderErrDiagCount;
        // 烘焙纹理总开关：BL 1.4.6 下 TaleWorlds.Engine.Texture.CreateFromByteArray 生成的纹理为全白/空
        // （与源 RGBA 无关，实测源像素有色但纹理整片白），故禁用纹理路径，改走 OnRender 内逐像素图元回退。
        // 若未来某版本该 API 恢复正常，只需把此常量改为 true 即可一键切回高质量单 draw call 纹理路径。
        // 注：即使 UseBakedTexture=false，EnsureTerrainTexture 仍会尝试创建纹理并验证结果；
        // 若 BGRA 字节序在某版本有效，会自动启用纹理路径而无需修改此常量。
        private static readonly bool UseBakedTexture = false;
        private static Texture _whiteTex;
        // 烘焙纹理（地形/风险各一张，双线性平滑，单 draw call）
        private static TaleWorlds.TwoDimension.Texture _terrainTex;
        private static TaleWorlds.TwoDimension.Texture _riskTex;
        // 底层 Engine.Texture：TwoDimension.Texture 只是壳、没有 Release，
        // 真正的 GPU 资源释放必须靠它，故单独持有以便复用/销毁时调用 Engine.Texture.Release()。
        private static TaleWorlds.Engine.Texture _terrainETex;
        private static TaleWorlds.Engine.Texture _riskETex;
        // 动态单位层纹理（每个 agent 一个点）
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

        // 从异常堆栈提取“抛出点”的第一帧，便于在游戏里直接看出是哪段代码出错。
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

        // Widget 的 Width/Height 在不同 BL 版本里可能是属性/字段，甚至命名不同（Width/width），
        // 直接用 this.Width 会编译失败。改为反射读取（字段优先，其次属性），失败返回 "?"。
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

        // 获取一个纯白纹理，用于 SimpleMaterial 纯色填充。
        // 优先用字节数组在引擎侧创建 1x1 纯白纹理（跨版本 100% 稳定，不依赖任何 sprite 查询）；
        // 失败再回退到反射查找已知白色 sprite 名（兼容旧版本）。这样 _whiteTex 永远不会是 null。
        private static void EnsureWhiteTexture(UIContext uiContext)
        {
            if (_whiteTex != null) return;
            // ① 字节数组创建（最可靠，与地形/单位层纹理同一套 API）
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

            // ② 回退：反射查找白色 sprite
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

                // ① 已知白色 sprite 名
                foreach (var name in new[] { "blank", "Blank", "white", "White", "ui/blank", "BlankWhite", "blank_white" })
                {
                    object sprite = null;
                    try { sprite = getSprite.Invoke(spriteData, new object[] { name }); } catch { sprite = null; }
                    if (sprite == null) continue;
                    var texProp = sprite.GetType().GetProperty("Texture", cbf);
                    var tex = texProp?.GetValue(sprite) as Texture;
                    if (tex != null && tex.IsValid) { _whiteTex = tex; Diag($"WHT got white via '{name}'"); return; }
                }

                // ② 枚举所有 sprite，优先白色名，否则第一个有效纹理
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
                            if (key.IndexOf("blank", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                key.IndexOf("white", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            { _whiteTex = tex; Diag($"WHT got white via enum '{key}'"); return; }
                            if (fallback == null) fallback = tex;
                            validNames.Add(key);
                        }
                    }
                    if (fallback != null)
                    {
                        _whiteTex = fallback;
                        Diag("WHT fallback non-white texture. validSprites=" + string.Join(",", validNames));
                        return;
                    }
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

            // 兼容不同 BL 版本的 GlobalPosition/Size 类型差异：
            // 旧版本 Widget.GlobalPosition 返回 Rectangle2D（非 System.Numerics.Vector2），
            // 直接 this.GlobalPosition 会被 JIT 解析为 Vector2 版 get_GlobalPosition 而抛 MissingMethodException。
            // 改用各版本均稳定存在的 AreaRect -> GetBoundingBox()（返回 SimpleRectangle，含 X/Y/Width/Height）。
            Rectangle2D area = this.AreaRect;
            var box = area.GetBoundingBox();
            float ox = box.X;
            float oy = box.Y;
            float w = box.Width;
            float h = box.Height;
            if (w <= 0f || h <= 0f) { WarnOnce(ref _warnedArea, "[TMap] OnRender: Size 无效"); return; }

            var cache = ctrl.Cache;
            var s = TacticalSettings.Instance;

            // 背景
            EnsureWhiteTexture(this.Context);
            if (_whiteTex == null) WarnOnce(ref _warnedNoWhite, "[TMap] 白色纹理为空：矩形/标记可能无法显示或报错");
            DrawRect(drawContext, ox, oy, w, h, new Color(0.04f, 0.06f, 0.09f, 0.85f));
            WarnOnce(ref _warnedDrawn, "[TMap] OnRender: 正在绘制小地图");

            // 地形 + 风险叠加：优先用烘焙纹理（双线性平滑 + 单 draw call）；
            // bake 未完成时降级为逐像素矩形（仅首帧）。
            EnsureTerrainTexture(ctrl);
            if (_terrainTex == null) WarnOnce(ref _warnedNoTerrain, "[TMap] 地形纹理创建失败：已降级为逐像素绘制（可能卡/不显示）");
            if (_terrainTex != null)
            {
                DrawTexture(drawContext, _terrainTex, ox, oy, w, h);
                if (s.EnableRiskOverlay)
                {
                    EnsureRiskTexture(ctrl);
                    if (_riskTex == null) WarnOnce(ref _warnedNoRisk, "[TMap] 风险纹理创建失败：不显示风险叠加层");
                    if (_riskTex != null)
                        DrawTexture(drawContext, _riskTex, ox, oy, w, h);
                }
            }
            else
            {
                // 合并通道：单循环完成地形+风险+密度+单位层绘制
                // 将四趟独立遍历合并为一趟，大幅减少DrawRect调用次数（从~15000降至~2500）
                int cols = Math.Min(48, cache.Width);  // 96→48，4倍减少
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

                    // 风险叠加（内置到主循环，免去第二趟遍历）
                    if (showRisk)
                    {
                        cache.GetPixel(cache.RiskRGBA, x, y, out byte rr, out byte rg, out byte rb, out byte ra);
                        if (ra > 0)
                        {
                            float ka = ra / 255f;
                            rf = rf * (1f - ka) + (rr / 255f) * ka;
                            gf = gf * (1f - ka) + (rg / 255f) * ka;
                            bf = bf * (1f - ka) + (rb / 255f) * ka;
                        }
                    }

                    // 密度热力叠加（内置到主循环，免去第三趟遍历）
                    if (showDensity)
                    {
                        int dens = cache.Cells[x, y].DensityAgentCount;
                        if (dens > 0)
                        {
                            float ka = Math.Min(0.3f, dens * 0.02f);
                            rf = rf * (1f - ka) + 1f * ka;
                            gf = gf * (1f - ka) + 0.85f * ka;
                            bf = bf * (1f - ka) + 0.2f * ka;
                        }
                    }

                    // 单位层叠加（内置到主循环，免去第四趟遍历）
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

            // 编队标记：每个编队质心一个描边方块（队伍色填充 + 关系色描边），与密集单位点云区分；并画出朝向。
            // 描边颜色按敌我区分：玩家=白框（不变），敌方=红框，友军=绿框（受 EnableUnitMarkers 控制）。
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
                        // 框色：玩家=白(不变) / 敌方=红 / 友军=绿
                        Color frame;
                        if (f.IsPlayer) frame = new Color(1f, 1f, 1f, 0.95f);
                        else if (f.IsEnemy) frame = new Color(1f, 0.15f, 0.15f, 0.95f);
                        else frame = new Color(0.2f, 1f, 0.2f, 0.95f);
                        Color c = Color.FromUint(f.Color);
                        // 2-call 边框：外框(边框色) + 内框(填充色)，替代原5次调用
                        DrawRect(drawContext, px - fs / 2f - ft, py - fs / 2f - ft, fs + 2f * ft, fs + 2f * ft, frame);
                        DrawRect(drawContext, px - fs / 2f, py - fs / 2f, fs, fs, c);
                        if (f.Facing.LengthSquared > 1E-4f)
                        {
                            DrawLine(drawContext, px, py, px + f.Facing.X * fs * 1.6f, py + f.Facing.Y * fs * 1.6f, frame);
                        }
                    }
                }
            }

            // 玩家（MainAgent）标记：青色描边圆环（框）+ 亮黄中心点，形状/颜色都明显区别于单位点云与编队方块，便于一眼定位自身位置
            if (!ctrl.PlayerPos.HasValue) WarnOnce(ref _warnedNoPlayer, "[TMap] 玩家位置为空：不显示玩家标记");
            if (ctrl.PlayerPos.HasValue)
            {
                Vec2 uv = cache.WorldToUV(ctrl.PlayerPos.Value);
                if (uv.X >= 0f && uv.X <= 1f && uv.Y >= 0f && uv.Y <= 1f)
                {
                    float px = ox + uv.X * w;
                    float py = oy + uv.Y * h;
                    float pr = Math.Max(10f, w * 0.05f);   // 圆环外径
                    DrawRectFrame(drawContext, px - pr / 2f, py - pr / 2f, pr, pr, Math.Max(2.5f, w * 0.012f), new Color(0f, 1f, 1f, 1f)); // 青色描边
                    DrawRect(drawContext, px - 3f, py - 3f, 6f, 6f, new Color(1f, 1f, 0.2f, 1f)); // 亮黄中心
                }
            }

            // 镜头目标指示（菱形，对应图例“相机”符号）
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

        // 兼容不同 BL 版本的 Rectangle2D 成员（LocalPosition / LocalScale / LocalRotation）：
        // 编译用的引用程序集可能把它们当作字段（IL 走 stfld），而运行时（如 1.4.6）实际是属性，
        // 直接 r.LocalScale = ... 会在 JIT 解析字段时抛 MissingFieldException。
        // 统一改用反射（字段优先，其次同名属性）写入，并缓存解析结果。
        // 关键：Rectangle2D 是 struct，必须通过 box -> 设置 -> unbox 回写，才能保证修改作用在调用方的副本上。
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

        // 运行时可能存在“两份 System.Numerics.Vector2”（游戏自带一份、本 mod 引用另一份）：
        // 二者全名相同但程序集身份不同，反射 FieldInfo/PropertyInfo.SetValue 会因类型不匹配抛
        // ArgumentException: “Object of type 'System.Numerics.Vector2' cannot be converted to type 'System.Numerics.Vector2'”。
        // 解决：当目标字段/属性类型来自与当前代码不同的程序集时，用目标类型在其程序集内自行构造 Vector2，保证类型身份一致。
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

        // 以 (cx,cy) 为中心画一个菱形（用 4 条线连成），d 为半径，width 为线宽。
        private static void DrawDiamond(TwoDimensionDrawContext ctx, float cx, float cy, float d, Color color, float width = 3f)
        {
            float tx = cx, ty = cy - d;   // 上
            float rx = cx + d, ry = cy;   // 右
            float bx = cx, by = cy + d;   // 下
            float lx = cx - d, ly = cy;   // 左
            DrawLine(ctx, tx, ty, rx, ry, color, width);
            DrawLine(ctx, rx, ry, bx, by, color, width);
            DrawLine(ctx, bx, by, lx, ly, color, width);
            DrawLine(ctx, lx, ly, tx, ty, color, width);
        }

        // 画矩形描边框（4 条细边），用于标记轮廓，提升在密集单位点云上的辨识度。
        private static void DrawRectFrame(TwoDimensionDrawContext ctx, float x, float y, float w, float h, float t, Color color)
        {
            DrawRect(ctx, x, y, w, t, color);              // 上
            DrawRect(ctx, x, y + h - t, w, t, color);      // 下
            DrawRect(ctx, x, y, t, h, color);              // 左
            DrawRect(ctx, x + w - t, y, t, h, color);      // 右
        }

        // 尝试用给定的 RGBA 数据创建引擎纹理，失败返回 null。
        // 部分 BL 版本只认 BGRA 字节序，因此先试 RGBA，若纹理创建成功但全白则试 BGRA。
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
                        data[i] = rgba[i + 2];     // B←R
                        data[i + 1] = rgba[i + 1]; // G←G
                        data[i + 2] = rgba[i];     // R←B
                        data[i + 3] = 255;         // A=255
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

        // 把地形/风险 RGBA 烘焙成 GPU 纹理并缓存，绘制时整体拉伸（双线性平滑）。
        // 仅在缓存切换或重新 bake 时重建，避免每帧创建纹理。
        // 自动尝试 RGBA 和 BGRA 两种字节序，以兼容不同 BL 版本。
        private static void EnsureTerrainTexture(TacticalMapController ctrl)
        {
            if (_terrainTex != null && _texCtrl == ctrl) return;
            if (_terrainTex != null) { _terrainETex?.Release(); _terrainETex = null; _terrainTex = null; }
            if (_riskTex != null) { _riskETex?.Release(); _riskETex = null; _riskTex = null; }
            // 切换战斗：单位层纹理一并释放（其数据属于旧 ctrl）
            if (_agentTex != null) { _agentETex?.Release(); _agentETex = null; _agentTex = null; _agentTexVer = -1; }
            // 当 UseBakedTexture=false 时跳过创建（走逐像素回退）
            if (!UseBakedTexture) return;
            _texCtrl = ctrl;
            if (ctrl == null) return;
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) { _texCtrl = null; return; }
            int W = cache.Width, H = cache.Height;
            byte[] td = cache.TerrainBaseRGBA;
            if (td == null || td.Length < W * H * 4) { _texCtrl = null; return; }
            // 先试 RGBA，失败则试 BGRA
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
            if (!UseBakedTexture) return; // 同地形：禁用纹理，走图元回退
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

        // 把动态单位层（AgentRGBA）烘焙成 GPU 纹理；仅当数据版本变化（节流刷新）时重建，
        // 用单 draw call 整体拉伸绘制——清晰呈现成千上万单位的真实分布，且敌我分明。
        private static void EnsureAgentTexture(TacticalMapController ctrl)
        {
            if (ctrl == null) { ReleaseAgent(); return; }
            if (!UseBakedTexture) return; // 同地形：禁用纹理，改走下方图元回退
            var cache = ctrl.Cache;
            if (cache == null || !cache.IsBaked) return;
            // 未切换战斗且数据未刷新 -> 复用旧纹理
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
