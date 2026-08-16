using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BannerlordHtmlUI;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// HTML 版技能配置/选择界面。
    /// 底层仍使用现有 CustomSkillScreenVM；HTML 只负责显示与输入。
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
        private New_ZZZF.CustomSkillScreen _screen;
        private New_ZZZF.CustomSkillScreenVM _vm;
        private float _publishAccum;
        private string _lastSignature;

        private static readonly FieldInfo VmField =
            typeof(New_ZZZF.CustomSkillScreen).GetField("_dataSource", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SelectTargetMethod =
            typeof(New_ZZZF.CustomSkillScreenVM).GetMethod("SelectTarget", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CatalogSelectedIndexField =
            typeof(New_ZZZF.CustomSkillScreenVM).GetField("_catalogSelectedIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RefreshCatalogHighlightMethod =
            typeof(New_ZZZF.CustomSkillScreenVM).GetMethod("RefreshCatalogHighlight", BindingFlags.Instance | BindingFlags.NonPublic);

        public static CustomSkillHtmlUi Instance => _instance.Value;
        public bool IsVisible => _visible;

        private CustomSkillHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            string assemblyDir = Path.GetDirectoryName(typeof(CustomSkillHtmlUi).Assembly.Location) ?? ".";
            string uiRoot = Path.Combine(assemblyDir, "CustomSkillUI");
            if (!Directory.Exists(uiRoot))
            {
                throw new DirectoryNotFoundException(
                    "CustomSkill HtmlUI content root not found: " + uiRoot);
            }

            _scope = HtmlUiService.CreateScope(OwnerId);
            _scope.RegisterContentRoot(ContentRootName, uiRoot);
            _pageId = _scope.RegisterPage(
                new HtmlUiPage(PageName, "index.html")
                {
                    ContentRootId = ContentRootName,
                    HotReload = true,
                    DefaultInputMode = HtmlUiInputMode.Captured
                });

            RegisterCommands();
            _registered = true;

            if (_screen != null && _vm != null)
                SetVisible(true);
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("cycleTargetType", _ => Execute(() => _vm?.ExecuteCycleTargetType()));
            _scope.RegisterCommand("setTargetType", payload => Execute(() =>
            {
                int value = payload?["value"]?.ToObject<int>() ?? 0;
                _vm.CurrentTargetTypeInt = value;
            }));
            _scope.RegisterCommand("selectHero", payload => Execute(() =>
            {
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (_vm?.Roster == null || index < 0 || index >= _vm.Roster.Count) return;
                SelectTargetMethod?.Invoke(_vm, new object[] { _vm.Roster[index] });
            }));
            _scope.RegisterCommand("selectSlot", payload => Execute(() =>
            {
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                _vm?.SelectSlotByIndex(index);
            }));
            _scope.RegisterCommand("search", payload => Execute(() =>
            {
                _vm.SearchText = payload?["text"]?.ToObject<string>() ?? string.Empty;
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
            _scope.RegisterCommand("openSpellForge", _ => Execute(() => _vm?.ExecuteOpenSpellForge()));
            _scope.RegisterCommand("close", _ => Execute(() => _vm?.ExecuteClose()));
            _scope.RegisterRequest("getState", _ => System.Threading.Tasks.Task.FromResult<object>(BuildState()));
        }

        private void Execute(Action action)
        {
            if (_vm == null) return;
            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi command failed.", ex); }
            PublishState(true);
        }

        public void Attach(New_ZZZF.CustomSkillScreen screen, New_ZZZF.CustomSkillScreenVM vm)
        {
            _screen = screen;
            _vm = vm ?? (VmField?.GetValue(screen) as New_ZZZF.CustomSkillScreenVM);
            _lastSignature = null;
            _publishAccum = 0f;
            if (_registered)
                SetVisible(true);
        }

        public void TryAttachFromScreen(New_ZZZF.CustomSkillScreen screen)
        {
            if (screen == null) return;
            var vm = VmField?.GetValue(screen) as New_ZZZF.CustomSkillScreenVM;
            Attach(screen, vm);
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

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!_registered || !HtmlUiService.IsReady || string.IsNullOrEmpty(_pageId)) return;
            try
            {
                if (visible)
                {
                    HtmlUiService.Pages.Open(_pageId);
                    PublishState(true);
                }
                else
                {
                    HtmlUiService.Pages.Close(_pageId);
                }
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkillHtmlUi visibility transition failed.", ex);
            }
        }

        public void Detach(New_ZZZF.CustomSkillScreen screen)
        {
            if (_screen != null && screen != null && !ReferenceEquals(_screen, screen)) return;
            SetVisible(false);
            _screen = null;
            _vm = null;
            _lastSignature = null;
        }

        private object BuildState()
        {
            var heroes = new List<object>();
            if (_vm.Roster != null)
            {
                for (int i = 0; i < _vm.Roster.Count; i++)
                {
                    var h = _vm.Roster[i];
                    heroes.Add(new
                    {
                        index = i,
                        id = h.HeroId ?? string.Empty,
                        name = h.HeroName ?? string.Empty,
                        subtitle = h.Subtitle ?? string.Empty,
                        selected = h.IsSelected
                    });
                }
            }

            var slots = new List<object>();
            if (_vm.Skills != null)
            {
                for (int i = 0; i < _vm.Skills.Count; i++)
                {
                    var slot = _vm.Skills[i];
                    slots.Add(new
                    {
                        index = i,
                        id = slot.SlotId ?? string.Empty,
                        label = slot.SlotLabel ?? string.Empty,
                        skillName = slot.SkillName ?? string.Empty,
                        skillId = slot.Skill?.SkillId ?? string.Empty,
                        icon = slot.SkillIcon ?? string.Empty,
                        empty = slot.IsEmpty,
                        equipped = slot.IsEquipped,
                        cooldown = slot.CooldownText ?? "-",
                        cost = slot.CostText ?? "-",
                        active = ReferenceEquals(_vm.ActiveSlot, slot)
                    });
                }
            }

            var catalog = new List<object>();
            if (_vm.CatalogItems != null)
            {
                for (int i = 0; i < _vm.CatalogItems.Count; i++)
                {
                    var item = _vm.CatalogItems[i];
                    catalog.Add(new
                    {
                        index = i,
                        id = item.SkillId ?? string.Empty,
                        name = item.SkillName ?? string.Empty,
                        description = item.Description ?? string.Empty,
                        type = item.TypeText ?? string.Empty,
                        cooldown = item.CooldownLabel ?? "-",
                        cost = item.CostLabel ?? "-",
                        highlighted = item.IsHighlighted,
                        selectable = item.IsSelectable
                    });
                }
            }

            var proficiencies = new List<object>();
            if (_vm.Proficiencies != null)
            {
                for (int i = 0; i < _vm.Proficiencies.Count; i++)
                {
                    var p = _vm.Proficiencies[i];
                    proficiencies.Add(new { name = p.SkillName ?? string.Empty, value = p.Value, text = p.DisplayText ?? "-" });
                }
            }

            return new
            {
                visible = _visible,
                debugMode = _vm.DebugMode,
                targetType = _vm.CurrentTargetTypeInt,
                targetTypeText = _vm.TargetTypeText ?? string.Empty,
                currentHeroId = _vm.CurrentHeroId ?? string.Empty,
                currentHeroName = _vm.CurrentHero?.HeroName ?? string.Empty,
                dirty = _vm.IsDirty,
                searchText = _vm.SearchText ?? string.Empty,
                inCatalog = _vm.IsInCatalogView,
                activeSlot = _vm.ActiveSlot?.SlotLabel ?? string.Empty,
                exportStatus = _vm.ExportStatusText ?? string.Empty,
                heroes,
                slots,
                catalog,
                proficiencies
            };
        }

        private static string BuildSignature(object state)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(state);
        }

        public void Dispose()
        {
            SetVisible(false);
            _scope = null;
            _screen = null;
            _vm = null;
            _registered = false;
        }
    }
}
