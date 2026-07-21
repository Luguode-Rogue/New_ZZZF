using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.Core;
using TaleWorlds.TwoDimension;

namespace New_ZZZF.TacticalMap.UI
{
    /// <summary>
    /// 管理小地图的 GauntletLayer：加载 XML、把自定义绘制控件 MinimapWidget 接到控制器、计算点击命中区域。
    /// 与战斗 MissionScreen 解耦，方便整体抽离成独立 mod。
    /// </summary>
    public sealed class TacticalMapLayer
    {
        private readonly TacticalMapController _controller;
        private GauntletLayer _layer;
        private GauntletMovieIdentifier _movieId;
        private Widget _panel;
        private MinimapWidget _minimap;

        public TacticalMapLayer(TacticalMapController controller)
        {
            _controller = controller;
        }

        public void Create(MissionScreen ms)
        {
            // MinimapWidget 是继承自 Widget 的自定义控件，会被 GauntletUI 的 WidgetInfo 自动反射发现（
            // 凡引用了 TaleWorlds.GauntletUI 的程序集都会被扫描），无需也无法手动 RegisterWidget。
            if (_layer != null) { try { ms.RemoveLayer(_layer); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] RemoveLayer 失败: {ex.Message}")); } }
            _layer = new GauntletLayer("TacticalMap", 90);
            _layer.IsFocusLayer = false;
            try
            {
                _movieId = _layer.LoadMovie("TacticalMap", new TacticalMapVM());
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] LoadMovie 失败: {ex.GetType().Name}: {ex.Message}"));
                return;
            }

            try
            {
                if (_movieId.Movie == null) { InformationManager.DisplayMessage(new InformationMessage("[TMap] LoadMovie 返回 Movie 为空")); return; }
                var root = _movieId.Movie.RootWidget;
                if (root == null) { InformationManager.DisplayMessage(new InformationMessage("[TMap] Movie.RootWidget 为空")); return; }
            _panel = FindWidgetById(root, "MinimapPanel");
            _minimap = FindWidgetById(root, "MinimapTex") as MinimapWidget;
            InformationManager.DisplayMessage(new InformationMessage($"[TMap] 创建层: panel={_panel != null} minimap={_minimap != null}"));

            var s = TacticalSettings.Instance;
            if (_panel != null)
            {
                _panel.WidthSizePolicy = SizePolicy.Fixed;
                _panel.HeightSizePolicy = SizePolicy.Fixed;
                _panel.SuggestedWidth = s.MapSize;
                _panel.SuggestedHeight = s.MapSize;
                // 右上角定位 + 边距改由 TacticalMap.xml 的 HorizontalAlignment/MarginRight/MarginTop 处理，
                // 避免不同 BL 版本 Widget.PosOffset 类型不一致导致的 MissingMethodException。
            }

            if (_minimap != null)
            {
                _minimap.Controller = _controller;
                _minimap.WidthSizePolicy = SizePolicy.StretchToParent;
                _minimap.HeightSizePolicy = SizePolicy.StretchToParent;
            }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[TMap] 创建层后处理失败: {ex.GetType().Name}: {ex.Message}"));
            }

            try { ms.AddLayer(_layer); }
            catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] AddLayer 失败: {ex.GetType().Name}: {ex.Message}")); }
        }

        public void Destroy(MissionScreen ms)
        {
            if (_layer != null && ms != null)
            {
                try { ms.RemoveLayer(_layer); } catch (Exception ex) { InformationManager.DisplayMessage(new InformationMessage($"[TMap] Destroy RemoveLayer 失败: {ex.Message}")); }
            }
            _layer = null;
            _movieId = default;
            _panel = null;
            _minimap = null;
        }

        /// <summary>屏幕像素 -> 小地图 UV；命中返回 true。</summary>
        public bool HitTestMinimap(Vec2 mousePixel, out Vec2 uv)
        {
            uv = Vec2.Zero;
            if (_panel == null) return false;
            // 避开 Widget.GlobalPosition/Size 的版本差异（旧版本 GlobalPosition 返回 Rectangle2D，非 Vector2）。
            // 改用各版本稳定的 AreaRect -> GetBoundingBox()（返回 SimpleRectangle，含 X/Y/Width/Height）。
            Rectangle2D area = _panel.AreaRect;
            var box = area.GetBoundingBox();
            float x0 = box.X, y0 = box.Y;
            float w = box.Width;
            float h = box.Height;
            if (mousePixel.X < x0 || mousePixel.X > x0 + w) return false;
            if (mousePixel.Y < y0 || mousePixel.Y > y0 + h) return false;
            uv = new Vec2((mousePixel.X - x0) / w, (mousePixel.Y - y0) / h);
            return true;
        }

        private static Widget FindWidgetById(Widget root, string id)
        {
            if (root == null) return null;
            if (root.Id == id) return root;
            var children = root.Children;
            if (children == null) return null;
            foreach (var c in children)
            {
                var found = FindWidgetById(c, id);
                if (found != null) return found;
            }
            return null;
        }
    }
}

