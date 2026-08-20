using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// 完整 HTML 技能配置界面。
    ///
    /// View hierarchy:
    ///   main    -> 技能总览
    ///   catalog -> 当前槽位技能选择
    ///   forge   -> 法术锻造
    ///
    /// 页面始终使用 Captured input。C# 只负责真实业务和数据，HTML 负责页面和交互。
    /// </summary>
    public sealed class CustomSkillHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.CustomSkill";

        private const string PageName = "customskill.html";
        private const string ContentRootName = "customskill";
        private const string StateKey = "customSkill";

        private static readonly Lazy<CustomSkillHtmlUi> _instance =
            new Lazy<CustomSkillHtmlUi>(() => new CustomSkillHtmlUi());

        private static readonly MethodInfo SelectTargetMethod =
            typeof(CustomSkillScreenVM).GetMethod("SelectTarget", BindingFlags.Instance | BindingFlags.NonPublic);

        private HtmlUiConsumerScope _scope;
        private string _pageId;
        private bool _registered;
        private bool _visible;
        private bool _activeStateDisabled;

        private CustomSkillScreenVM _vm;
        private New_ZZZF.SpellForge.SpellForgeVM _forgeVm;

        private string _view = "main";
        private string _catalogSearch = string.Empty;
        private float _publishAccum;
        private string _lastSignature;

        public static CustomSkillHtmlUi Instance => _instance.Value;
        public bool IsVisible => _visible;
        public string CurrentView => _view;

        private CustomSkillHtmlUi() { }

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady)
                return;

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
            HtmlUiLogger.Info("CustomSkill HtmlUI v3 registered.");
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("setTargetType", payload => Execute(() =>
            {
                if (_vm == null) return;
                _vm.CurrentTargetTypeInt = payload?["value"]?.ToObject<int>() ?? 0;
                _view = "main";
            }));

            _scope.RegisterCommand("selectHero", payload => Execute(() =>
            {
                if (_vm?.Roster == null || SelectTargetMethod == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Roster.Count) return;
                SelectTargetMethod.Invoke(_vm, new object[] { _vm.Roster[index] });
                _view = "main";
            }));

            _scope.RegisterCommand("selectSlot", payload => Execute(() =>
            {
                if (_vm?.Skills == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Skills.Count) return;
                _vm.SelectSlotByIndex(index);
                _catalogSearch = string.Empty;
                _view = "catalog";
            }));

            _scope.RegisterCommand("clearSlot", payload => Execute(() =>
            {
                if (_vm?.Skills == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Skills.Count) return;
                _vm.ClearSkillSlot(_vm.Skills[index]);
            }));

            _scope.RegisterCommand("searchCatalog", payload => ExecuteWithoutStateRefresh(() =>
            {
                _catalogSearch = payload?["text"]?.ToObject<string>() ?? string.Empty;
            }));

            _scope.RegisterCommand("catalogBack", _ => Execute(() => BackToMain()));
            _scope.RegisterCommand("catalogSelect", payload => Execute(() =>
            {
                string skillId = payload?["skillId"]?.ToObject<string>();
                AssignCatalogSkill(skillId);
            }));

            _scope.RegisterCommand("apply", _ => Execute(() => _vm?.ExecuteApply()));
            _scope.RegisterCommand("undo", _ => Execute(() => _vm?.ExecuteUndoChanges()));
            _scope.RegisterCommand("export", _ => Execute(() => _vm?.ExecuteExport()));
            _scope.RegisterCommand("toggleDebug", _ => Execute(() => _vm?.ExecuteToggleDebug()));

            _scope.RegisterCommand("openForge", _ => Execute(OpenForge));
            _scope.RegisterCommand("forgeBack", _ => Execute(CloseForge));
            _scope.RegisterCommand("forgeAdd", payload => Execute(() =>
                _forgeVm?.ExecuteAddNode(payload?["id"]?.ToObject<string>())));
            _scope.RegisterCommand("forgeRemove", payload => Execute(() =>
                _forgeVm?.ExecuteRemoveNode(payload?["id"]?.ToObject<string>())));
            _scope.RegisterCommand("forgeClear", _ => Execute(() => _forgeVm?.ExecuteClearBuild()));
            _scope.RegisterCommand("forgeConfirm", _ => Execute(() => _forgeVm?.ExecuteConfirmSpell()));
            _scope.RegisterCommand("forgeEquip", payload => Execute(() =>
                _forgeVm?.ExecuteEquipSpell(payload?["id"]?.ToObject<string>())));
            _scope.RegisterCommand("forgeEdit", payload => Execute(() =>
                _forgeVm?.ExecuteEditSpell(payload?["id"]?.ToObject<string>())));
            _scope.RegisterCommand("forgeSetName", payload => ExecuteWithoutStateRefresh(() =>
            {
                if (_forgeVm != null)
                    _forgeVm.NewSpellName = payload?["value"]?.ToObject<string>() ?? string.Empty;
            }));

            _scope.RegisterCommand("close", _ => Execute(Close));
            _scope.RegisterRequest("getState", _ => Task.FromResult<object>(BuildState()));
        }

        private void Execute(Action action)
        {
            if (!_visible && action != Close)
                return;

            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi command failed.", ex); }
            PublishState(true);
        }

        private void ExecuteWithoutStateRefresh(Action action)
        {
            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi lightweight command failed.", ex); }
            PublishState(false);
        }

        public bool TryOpen()
        {
            if (!_registered || !HtmlUiService.IsReady || _visible)
                return _visible;

            try
            {
                _vm = new CustomSkillScreenVM();
                _vm.SetCloseAction(Close);
                _forgeVm = null;
                _view = "main";
                _catalogSearch = string.Empty;
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
                HtmlUiLogger.Info("CustomSkill HtmlUI opened: full-capture multi-level UI.");
                return true;
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI open failed.", ex);
                Close();
                return false;
            }
        }

        private void OpenForge()
        {
            if (_vm == null) return;
            if (_forgeVm == null)
                _forgeVm = new New_ZZZF.SpellForge.SpellForgeVM(_vm, CloseForge);
            _view = "forge";
        }

        private void CloseForge()
        {
            _forgeVm = null;
            _view = "main";
            _catalogSearch = string.Empty;
        }

        private void BackToMain()
        {
            if (_view == "catalog")
            {
                _vm?.ExecuteCloseCatalog();
                _catalogSearch = string.Empty;
                _view = "main";
                return;
            }

            if (_view == "forge")
                CloseForge();
        }

        private void AssignCatalogSkill(string skillId)
        {
            if (_vm?.ActiveSlot == null || string.IsNullOrWhiteSpace(skillId))
                return;

            var selected = _vm.Catalog?.GetSkillById(skillId);
            if (selected == null || selected.IsEmpty)
                return;

            if (selected.Type != _vm.ActiveSlot.SlotFilterType)
                return;

            _vm.AssignSkillToSlot(_vm.ActiveSlot, selected);
            _vm.ExecuteCloseCatalog();
            _catalogSearch = string.Empty;
            _view = "main";
        }

        public void Close()
        {
            try
            {
                _forgeVm = null;
                _view = "main";
                _catalogSearch = string.Empty;
                if (_registered && HtmlUiService.IsReady && !string.IsNullOrEmpty(_pageId))
                    HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUi page close failed.", ex);
            }

            if (_activeStateDisabled && Game.Current != null)
            {
                try { Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(this); }
                catch (Exception ex) { HtmlUiLogger.Error("CustomSkill HtmlUi active-state release failed.", ex); }
                _activeStateDisabled = false;
            }

            _visible = false;
            if (_vm != null)
            {
                try { _vm.OnFinalize(); }
                catch (Exception ex) { HtmlUiLogger.Error("CustomSkill HtmlUi VM finalize failed.", ex); }
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
            if (!_registered || !_visible || _vm == null || _scope == null)
                return;

            try
            {
                var state = BuildState();
                string signature = Newtonsoft.Json.JsonConvert.SerializeObject(state);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal))
                    return;

                _lastSignature = signature;
                // 必须写入 Owner-scoped state；旧实现写入全局 key，JS scope 永远收不到。
                _scope.SetState(StateKey, state);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkillHtmlUi state publish failed.", ex);
            }
        }

        private object BuildState()
        {
            return new
            {
                visible = _visible,
                view = _view,
                canBack = _view != "main",
                debugMode = _vm.DebugMode,
                targetType = _vm.CurrentTargetTypeInt,
                targetTypeText = _vm.TargetTypeText ?? string.Empty,
                currentHeroId = _vm.CurrentHeroId ?? string.Empty,
                currentHeroName = _vm.CurrentHero?.HeroName ?? string.Empty,
                dirty = _vm.IsDirty,
                exportStatus = _vm.ExportStatusText ?? string.Empty,
                catalogSearch = _catalogSearch,
                activeSlot = _vm.ActiveSlot?.SlotLabel ?? string.Empty,
                activeSlotIndex = GetActiveSlotIndex(),
                heroes = BuildHeroState(),
                slots = BuildSlotState(),
                proficiencies = BuildProficiencyState(),
                catalog = BuildCatalogState(),
                forge = _view == "forge" ? BuildForgeState() : null
            };
        }

        private List<object> BuildHeroState()
        {
            var result = new List<object>();
            if (_vm?.Roster == null) return result;

            for (int i = 0; i < _vm.Roster.Count; i++)
            {
                var hero = _vm.Roster[i];
                result.Add(new
                {
                    index = i,
                    id = hero.HeroId ?? string.Empty,
                    name = hero.HeroName ?? string.Empty,
                    subtitle = hero.Subtitle ?? string.Empty,
                    selected = hero.IsSelected
                });
            }

            return result;
        }

        private List<object> BuildSlotState()
        {
            var result = new List<object>();
            if (_vm?.Skills == null) return result;

            for (int i = 0; i < _vm.Skills.Count; i++)
            {
                var slot = _vm.Skills[i];
                result.Add(new
                {
                    index = i,
                    id = slot.SlotId ?? string.Empty,
                    label = slot.SlotLabel ?? string.Empty,
                    skillName = slot.SkillName ?? string.Empty,
                    skillId = slot.Skill?.SkillId ?? string.Empty,
                    icon = slot.SkillIcon ?? string.Empty,
                    type = slot.SlotFilterType.ToString(),
                    empty = slot.IsEmpty,
                    equipped = slot.IsEquipped,
                    cooldown = slot.CooldownText ?? "-",
                    cost = slot.CostText ?? "-",
                    active = ReferenceEquals(_vm.ActiveSlot, slot)
                });
            }

            return result;
        }

        private List<object> BuildProficiencyState()
        {
            var result = new List<object>();
            if (_vm?.Proficiencies == null) return result;

            for (int i = 0; i < _vm.Proficiencies.Count; i++)
            {
                var p = _vm.Proficiencies[i];
                result.Add(new { name = p.SkillName ?? string.Empty, value = p.Value, text = p.DisplayText ?? "-" });
            }

            return result;
        }

        private List<object> BuildCatalogState()
        {
            var result = new List<object>();
            if (_view != "catalog" || _vm?.ActiveSlot == null || _vm.Catalog?.AllSkills == null)
                return result;

            string filter = (_catalogSearch ?? string.Empty).Trim();
            foreach (var skill in _vm.Catalog.AllSkills)
            {
                if (skill == null || skill.IsEmpty || skill.Type != _vm.ActiveSlot.SlotFilterType)
                    continue;

                if (!string.IsNullOrWhiteSpace(filter)
                    && !ContainsIgnoreCase(skill.SkillName, filter)
                    && !ContainsIgnoreCase(skill.Description, filter)
                    && !ContainsIgnoreCase(skill.SkillId, filter))
                    continue;

                var difficulties = new List<object>();
                if (skill.Difficulties != null)
                {
                    foreach (var diff in skill.Difficulties)
                    {
                        if (diff == null) continue;
                        difficulties.Add(new { difficulty = diff.Difficulty, attribute = diff.UseAttribute ?? string.Empty });
                    }
                }

                result.Add(new
                {
                    id = skill.SkillId ?? string.Empty,
                    name = skill.SkillName ?? string.Empty,
                    description = skill.Description ?? string.Empty,
                    type = skill.Type.ToString(),
                    icon = skill.IconItemId ?? string.Empty,
                    cooldown = skill.Cooldown,
                    cost = skill.ResourceCost,
                    difficulties
                });
            }

            return result.OrderBy(x => JObject.FromObject(x)["name"]?.Value<string>(), StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private object BuildForgeState()
        {
            if (_forgeVm == null) return null;

            return new
            {
                newSpellName = _forgeVm.NewSpellName ?? string.Empty,
                buildDescription = _forgeVm.BuildDescription ?? string.Empty,
                validationMessage = _forgeVm.ValidationMessage ?? string.Empty,
                availableNodes = BuildForgeEntries(_forgeVm.AvailableNodes),
                currentBuild = BuildForgeEntries(_forgeVm.CurrentBuild),
                allSpells = BuildForgeEntries(_forgeVm.AllSpells)
            };
        }

        private static List<object> BuildForgeEntries(IEnumerable<New_ZZZF.SpellForge.ForgeEntryVM> entries)
        {
            var result = new List<object>();
            if (entries == null) return result;

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                result.Add(new
                {
                    id = entry.SkillId ?? string.Empty,
                    name = entry.SkillName ?? string.Empty,
                    description = entry.Description ?? string.Empty
                });
            }

            return result;
        }

        private int GetActiveSlotIndex()
        {
            if (_vm?.Skills == null || _vm.ActiveSlot == null) return -1;
            for (int i = 0; i < _vm.Skills.Count; i++)
                if (ReferenceEquals(_vm.Skills[i], _vm.ActiveSlot)) return i;
            return -1;
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(search)
                && value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        public void Dispose()
        {
            Close();
            try { _scope?.Dispose(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkill HtmlUi scope dispose failed.", ex); }
            _scope = null;
            _registered = false;
        }
    }
}
