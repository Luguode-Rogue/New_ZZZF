using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BannerlordHtmlUI;
using TaleWorlds.Core;
using Newtonsoft.Json.Linq;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// HTML-first 技能配置界面。
    /// 不创建/依赖 CustomSkillScreen；直接持有 CustomSkillScreenVM 作为业务控制器。
    /// Gauntlet Screen 只作为历史实现保留，不再参与运行时入口。
    /// </summary>
    public sealed class CustomSkillHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.CustomSkill";
        private const string PageName = "customskill.html";
        private const string ContentRootName = "customskill";

        private static readonly Lazy<CustomSkillHtmlUi> _instance =
            new Lazy<CustomSkillHtmlUi>(() => new CustomSkillHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private bool _registered;
        private bool _visible;
        private CustomSkillScreenVM _vm;
        private bool _activeStateDisabled;
        private float _publishAccum;
        private string _lastSignature;

        private static readonly MethodInfo SelectTargetMethod =
            typeof(CustomSkillScreenVM).GetMethod("SelectTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CatalogSelectedIndexField =
            typeof(CustomSkillScreenVM).GetField("_catalogSelectedIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RefreshCatalogHighlightMethod =
            typeof(CustomSkillScreenVM).GetMethod("RefreshCatalogHighlight", BindingFlags.Instance | BindingFlags.NonPublic);

        public static CustomSkillHtmlUi Instance => _instance.Value;
        public bool IsVisible => _visible;

        private CustomSkillHtmlUi() { }

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            string assemblyDir = Path.GetDirectoryName(typeof(CustomSkillHtmlUi).Assembly.Location) ?? ".";
            string uiRoot = Path.Combine(assemblyDir, "CustomSkillUI");
            if (!Directory.Exists(uiRoot))
                throw new DirectoryNotFoundException("CustomSkill HtmlUI content root not found: " + uiRoot);

            _scope = HtmlUiService.CreateScope(OwnerId);
            _scope.RegisterContentRoot(ContentRootName, uiRoot);
            _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "index.html")
            {
                ContentRootId = ContentRootName,
                HotReload = true,
                DefaultInputMode = HtmlUiInputMode.Captured
            });

            RegisterCommands();
            _registered = true;
            HtmlUiLogger.Info("CustomSkill HtmlUI registered as HTML-first page.");
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("cycleTargetType", _ => Execute(() => _vm?.ExecuteCycleTargetType()));
            _scope.RegisterCommand("setTargetType", payload => Execute(() =>
            {
                if (_vm != null) _vm.CurrentTargetTypeInt = payload?["value"]?.ToObject<int>() ?? 0;
            }));
            _scope.RegisterCommand("selectHero", payload => Execute(() =>
            {
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (_vm?.Roster == null || index < 0 || index >= _vm.Roster.Count) return;
                SelectTargetMethod?.Invoke(_vm, new object[] { _vm.Roster[index] });
            }));
            _scope.RegisterCommand("selectSlot", payload => Execute(() =>
            {
                _vm?.SelectSlotByIndex(payload?["index"]?.ToObject<int>() ?? -1);
            }));
            _scope.RegisterCommand("search", payload => Execute(() =>
            {
                if (_vm != null) _vm.SearchText = payload?["text"]?.ToObject<string>() ?? string.Empty;
            }));
            _scope.RegisterCommand("catalogNext", _ => Execute(() => _vm?.SelectNextCatalogItem()));
            _scope.RegisterCommand("catalogPrev", _ => Execute(() => _vm?.SelectPrevCatalogItem()));
            _scope.RegisterCommand("catalogLeft", _ => Execute(() => _vm?.SelectPrevCatalogRow()));
            _scope.RegisterCommand("catalogRight", _ => Execute(() => _vm?.SelectNextCatalogRow()));
            _scope.RegisterCommand("catalogSelect", payload => Execute(() =>
            {
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (_vm?.CatalogItems == null || index < 0 || index >= _vm.CatalogItems.Count) return;
                CatalogSelectedIndexField?.SetValue(_vm, index);
                RefreshCatalogHighlightMethod?.Invoke(_vm, null);
                _vm.ExecuteSelectFromCatalog();
            }));
            _scope.RegisterCommand("catalogConfirm", _ => Execute(() => _vm?.ExecuteSelectFromCatalog()));
            _scope.RegisterCommand("catalogBack", _ => Execute(() => _vm?.ExecuteCloseCatalog()));
            _scope.RegisterCommand("apply", _ => Execute(() => _vm?.ExecuteApply()));
            _scope.RegisterCommand("undo", _ => Execute(() => _vm?.ExecuteUndoChanges()));
            _scope.RegisterCommand("export", _ => Execute(() => _vm?.ExecuteExport()));
            _scope.RegisterCommand("toggleDebug", _ => Execute(() => _vm?.ExecuteToggleDebug()));
            _scope.RegisterCommand("close", _ => Execute(Close));
            _scope.RegisterRequest("getState", _ => System.Threading.Tasks.Task.FromResult<object>(BuildState()));
        }

        private void Execute(Action action)
        {
            if (_vm == null) return;
            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi command failed.", ex); }
            PublishState(true);
        }

        public bool TryOpen()
        {
            if (!_registered || !HtmlUiService.IsReady || _visible) return _visible;
            try
            {
                _vm = new CustomSkillScreenVM();
                _vm.SetCloseAction(Close);
                _publishAccum = 0f;
                _lastSignature = null;

                if (Game.Current != null && !_activeStateDisabled)
                {
                    Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
                    _activeStateDisabled = true;
                }

                _visible = true;
                if (!HtmlUiService.Pages.Open(_pageId))
                {
                    Close();
                    return false;
                }
                PublishState(true);
                HtmlUiLogger.Info("CustomSkill HtmlUI opened (HTML-first, no CustomSkillScreen).");
                return true;
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI open failed.", ex);
                Close();
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (_registered && HtmlUiService.IsReady && !string.IsNullOrEmpty(_pageId))
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI page close failed.", ex);
            }

            if (_activeStateDisabled && Game.Current != null)
            {
                try { Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this); }
                catch (Exception ex) { HtmlUiLogger.Error("CustomSkill HtmlUI active-state release failed.", ex); }
                _activeStateDisabled = false;
            }

            _visible = false;
            if (_vm != null)
            {
                try { _vm.OnFinalize(); }
                catch (Exception ex) { HtmlUiLogger.Error("CustomSkill HtmlUI VM finalize failed.", ex); }
            }
            _vm = null;
            _lastSignature = null;
            _publishAccum = 0f;
        }

        public void Tick(float dt)
        {
            if (!_visible || _vm == null) return;
            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.10f) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        private void PublishState(bool force)
        {
            if (!_registered || !_visible || _vm == null) return;
            try
            {
                var state = BuildState();
                string signature = BuildSignature(state);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal)) return;
                _lastSignature = signature;
                HtmlUiService.State.Set("customSkill", state);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkillHtmlUi state publish failed.", ex);
            }
        }

        private object BuildState()
        {
            var heroes = new List<object>();
            for (int i = 0; _vm.Roster != null && i < _vm.Roster.Count; i++)
            {
                var h = _vm.Roster[i];
                heroes.Add(new { index = i, id = h.HeroId ?? "", name = h.HeroName ?? "", subtitle = h.Subtitle ?? "", selected = h.IsSelected });
            }

            var slots = new List<object>();
            for (int i = 0; _vm.Skills != null && i < _vm.Skills.Count; i++)
            {
                var slot = _vm.Skills[i];
                slots.Add(new
                {
                    index = i,
                    id = slot.SlotId ?? "",
                    label = slot.SlotLabel ?? "",
                    skillName = slot.SkillName ?? "",
                    skillId = slot.Skill?.SkillId ?? "",
                    icon = slot.SkillIcon ?? "",
                    empty = slot.IsEmpty,
                    equipped = slot.IsEquipped,
                    cooldown = slot.CooldownText ?? "-",
                    cost = slot.CostText ?? "-",
                    active = ReferenceEquals(_vm.ActiveSlot, slot)
                });
            }

            var catalog = new List<object>();
            for (int i = 0; _vm.CatalogItems != null && i < _vm.CatalogItems.Count; i++)
            {
                var item = _vm.CatalogItems[i];
                catalog.Add(new
                {
                    index = i,
                    id = item.SkillId ?? "",
                    name = item.SkillName ?? "",
                    description = item.Description ?? "",
                    type = item.TypeText ?? "",
                    cooldown = item.CooldownLabel ?? "-",
                    cost = item.CostLabel ?? "-",
                    highlighted = item.IsHighlighted,
                    selectable = item.IsSelectable
                });
            }

            var proficiencies = new List<object>();
            for (int i = 0; _vm.Proficiencies != null && i < _vm.Proficiencies.Count; i++)
            {
                var p = _vm.Proficiencies[i];
                proficiencies.Add(new { name = p.SkillName ?? "", value = p.Value, text = p.DisplayText ?? "-" });
            }

            return new
            {
                visible = _visible,
                debugMode = _vm.DebugMode,
                targetType = _vm.CurrentTargetTypeInt,
                targetTypeText = _vm.TargetTypeText ?? "",
                currentHeroId = _vm.CurrentHeroId ?? "",
                currentHeroName = _vm.CurrentHero?.HeroName ?? "",
                dirty = _vm.IsDirty,
                searchText = _vm.SearchText ?? "",
                inCatalog = _vm.IsInCatalogView,
                activeSlot = _vm.ActiveSlot?.SlotLabel ?? "",
                exportStatus = _vm.ExportStatusText ?? "",
                heroes,
                slots,
                catalog,
                proficiencies
            };
        }

        private static string BuildSignature(object state) => Newtonsoft.Json.JsonConvert.SerializeObject(state);

        public void Dispose()
        {
            Close();
            _scope = null;
            _registered = false;
        }
    }
}
