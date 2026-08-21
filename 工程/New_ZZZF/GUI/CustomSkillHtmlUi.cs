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
    public sealed class CustomSkillHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.CustomSkill";
        private const string PageName = "customskill.html";
        private const string ContentRootName = "customskill";
        private const string StateKey = "customSkill";
        private static readonly Lazy<CustomSkillHtmlUi> _instance = new Lazy<CustomSkillHtmlUi>(() => new CustomSkillHtmlUi());
        private static readonly MethodInfo SelectTargetMethod = typeof(CustomSkillScreenVM).GetMethod("SelectTarget", BindingFlags.Instance | BindingFlags.NonPublic);

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
        private string _lastModelStamp;
        private string _catalogCacheKey;
        private List<object> _catalogCache;
        private string _forgeCacheKey;
        private object _forgeCache;

        public static CustomSkillHtmlUi Instance => _instance.Value;
        public bool IsVisible => _visible;
        public string CurrentView => _view;
        private CustomSkillHtmlUi() { }

        public void InitializeOnFrameworkReady() => HtmlUiService.OnReady(Register);

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;
            string assemblyDir = Path.GetDirectoryName(typeof(CustomSkillHtmlUi).Assembly.Location) ?? ".";
            string uiRoot = Path.Combine(assemblyDir, "CustomSkillUI");
            if (!Directory.Exists(uiRoot)) throw new DirectoryNotFoundException("CustomSkill HtmlUI content root not found: " + uiRoot);

            _scope = HtmlUiService.CreateScope(OwnerId);
            _scope.RegisterContentRoot(ContentRootName, uiRoot);
            _pageId = _scope.RegisterPage(new HtmlUiPage(PageName, "index.html")
            {
                ContentRootId = ContentRootName,
                HotReload = true,
                DefaultInputMode = HtmlUiInputMode.Captured,
                CloseOnEscape = true,
                Opened = OnPageOpened,
                Closed = OnPageClosed
            });
            RegisterCommands();
            _registered = true;
            HtmlUiLogger.Info("CustomSkill HtmlUI v4 registered with authoritative page lifecycle callbacks.");
        }

        private void OnPageOpened()
        {
            HtmlUiLogger.Info("CustomSkill page Opened callback.");
        }

        private void OnPageClosed()
        {
            HtmlUiLogger.Info("CustomSkill page Closed callback. Releasing consumer state.");
            ReleaseLocalState();
        }

        private void RegisterCommands()
        {
            _scope.RegisterCommand("setTargetType", payload => Execute(() =>
            {
                if (_vm == null) return;
                _vm.CurrentTargetTypeInt = payload?["value"]?.ToObject<int>() ?? 0;
                _view = "main";
                InvalidateStateCaches();
            }));
            _scope.RegisterCommand("selectHero", payload => Execute(() =>
            {
                if (_vm?.Roster == null || SelectTargetMethod == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Roster.Count) return;
                SelectTargetMethod.Invoke(_vm, new object[] { _vm.Roster[index] });
                _view = "main";
                InvalidateStateCaches();
            }));
            _scope.RegisterCommand("selectSlot", payload => Execute(() =>
            {
                if (_vm?.Skills == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Skills.Count) return;
                _vm.SelectSlotByIndex(index);
                _catalogSearch = string.Empty;
                _view = "catalog";
                InvalidateStateCaches();
            }));
            _scope.RegisterCommand("clearSlot", payload => Execute(() =>
            {
                if (_vm?.Skills == null) return;
                int index = payload?["index"]?.ToObject<int>() ?? -1;
                if (index < 0 || index >= _vm.Skills.Count) return;
                _vm.ClearSkillSlot(_vm.Skills[index]);
                InvalidateStateCaches();
            }));
            _scope.RegisterCommand("searchCatalog", payload => ExecuteWithoutStateRefresh(() =>
            {
                var next = payload?["text"]?.ToObject<string>() ?? string.Empty;
                if (!string.Equals(_catalogSearch, next, StringComparison.Ordinal))
                {
                    _catalogSearch = next;
                    InvalidateCatalogCache();
                }
            }));
            _scope.RegisterCommand("catalogBack", _ => Execute(() => BackToMain()));
            _scope.RegisterCommand("catalogSelect", payload => Execute(() => AssignCatalogSkill(payload?["skillId"]?.ToObject<string>())));
            _scope.RegisterCommand("apply", _ => Execute(() => _vm?.ExecuteApply()));
            _scope.RegisterCommand("undo", _ => Execute(() => _vm?.ExecuteUndoChanges()));
            _scope.RegisterCommand("export", _ => Execute(() => _vm?.ExecuteExport()));
            _scope.RegisterCommand("toggleDebug", _ => Execute(() => _vm?.ExecuteToggleDebug()));
            _scope.RegisterCommand("openForge", _ => Execute(OpenForge));
            _scope.RegisterCommand("forgeBack", _ => Execute(CloseForge));
            _scope.RegisterCommand("forgeAdd", payload => Execute(() => { _forgeVm?.ExecuteAddNode(payload?["id"]?.ToObject<string>()); InvalidateForgeCache(); }));
            _scope.RegisterCommand("forgeRemove", payload => Execute(() => { _forgeVm?.ExecuteRemoveNode(payload?["id"]?.ToObject<string>()); InvalidateForgeCache(); }));
            _scope.RegisterCommand("forgeClear", _ => Execute(() => { _forgeVm?.ExecuteClearBuild(); InvalidateForgeCache(); }));
            _scope.RegisterCommand("forgeConfirm", _ => Execute(() => { _forgeVm?.ExecuteConfirmSpell(); InvalidateForgeCache(); InvalidateStateCaches(); }));
            _scope.RegisterCommand("forgeEquip", payload => Execute(() => _forgeVm?.ExecuteEquipSpell(payload?["id"]?.ToObject<string>())));
            _scope.RegisterCommand("forgeEdit", payload => Execute(() => { _forgeVm?.ExecuteEditSpell(payload?["id"]?.ToObject<string>()); InvalidateForgeCache(); }));
            _scope.RegisterCommand("forgeSetName", payload => ExecuteWithoutStateRefresh(() =>
            {
                if (_forgeVm != null)
                {
                    _forgeVm.NewSpellName = payload?["value"]?.ToObject<string>() ?? string.Empty;
                    InvalidateForgeCache();
                }
            }));
            _scope.RegisterCommand("close", _ => Execute(Close));
            _scope.RegisterRequest("getState", _ => Task.FromResult<object>(BuildState()));
        }

        private void Execute(Action action)
        {
            if (!_visible && action != Close) return;
            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi command failed.", ex); }
            PublishState(true);
        }

        private void ExecuteWithoutStateRefresh(Action action)
        {
            if (!_visible) return;
            try { action?.Invoke(); }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi lightweight command failed.", ex); }
            PublishState(false);
        }

        public bool TryOpen()
        {
            if (!_registered || !HtmlUiService.IsReady) return false;
            if (_visible) return true;

            try
            {
                _vm = new CustomSkillScreenVM();
                _vm.SetCloseAction(Close);
                _forgeVm = null;
                _view = "main";
                _catalogSearch = string.Empty;
                _publishAccum = 0f;
                _lastSignature = null;
                InvalidateStateCaches();

                if (Game.Current != null && !_activeStateDisabled)
                {
                    Game.Current.GameStateManager.RegisterActiveStateDisableRequest(this);
                    _activeStateDisabled = true;
                }

                _visible = true;
                if (!HtmlUiService.Pages.Open(_pageId))
                {
                    ReleaseLocalState();
                    return false;
                }

                PublishState(true);
                HtmlUiLogger.Info("CustomSkill HtmlUI opened: full-capture multi-level UI.");
                return true;
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUI open failed.", ex);
                try { HtmlUiService.Pages.Close(_pageId); } catch { }
                ReleaseLocalState();
                return false;
            }
        }

        private void OpenForge()
        {
            if (_vm == null) return;
            if (_forgeVm == null) _forgeVm = new New_ZZZF.SpellForge.SpellForgeVM(_vm, CloseForge);
            _view = "forge";
            InvalidateForgeCache();
        }

        private void CloseForge()
        {
            _forgeVm = null;
            _view = "main";
            _catalogSearch = string.Empty;
            InvalidateStateCaches();
        }

        private void BackToMain()
        {
            if (_view == "catalog")
            {
                _vm?.ExecuteCloseCatalog();
                _catalogSearch = string.Empty;
                _view = "main";
                InvalidateStateCaches();
                return;
            }
            if (_view == "forge") CloseForge();
        }

        private void AssignCatalogSkill(string skillId)
        {
            if (_vm?.ActiveSlot == null || string.IsNullOrWhiteSpace(skillId)) return;
            var selected = _vm.Catalog?.GetSkillById(skillId);
            if (selected == null || selected.IsEmpty || selected.Type != _vm.ActiveSlot.SlotFilterType) return;
            _vm.AssignSkillToSlot(_vm.ActiveSlot, selected);
            _vm.ExecuteCloseCatalog();
            _catalogSearch = string.Empty;
            _view = "main";
            InvalidateStateCaches();
        }

        public void Close()
        {
            if (!_visible && !_activeStateDisabled && _vm == null && _forgeVm == null) return;
            try
            {
                bool wasRegistered = _registered && HtmlUiService.IsReady && !string.IsNullOrEmpty(_pageId);
                ReleaseLocalState();
                if (wasRegistered) HtmlUiService.Pages.Close(_pageId);
            }
            catch (Exception ex)
            {
                HtmlUiLogger.Error("CustomSkill HtmlUi page close failed.", ex);
                ReleaseLocalState();
            }
        }

        private void ReleaseLocalState()
        {
            _forgeVm = null;
            _view = "main";
            _catalogSearch = string.Empty;

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
            InvalidateStateCaches();
        }

        public void Tick(float dt)
        {
            if (!_visible || _vm == null) return;
            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < 0.20f) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        private void PublishState(bool force)
        {
            if (!_registered || !_visible || _vm == null || _scope == null) return;
            try
            {
                var modelStamp = BuildCheapModelStamp();
                if (!force && string.Equals(modelStamp, _lastModelStamp, StringComparison.Ordinal)) return;
                _lastModelStamp = modelStamp;

                var state = BuildState();
                string signature = Newtonsoft.Json.JsonConvert.SerializeObject(state);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal)) return;
                _lastSignature = signature;
                _scope.SetState(StateKey, state);
            }
            catch (Exception ex) { HtmlUiLogger.Error("CustomSkillHtmlUi state publish failed.", ex); }
        }

        private string BuildCheapModelStamp()
        {
            var parts = new List<string>(32)
            {
                _view ?? string.Empty,
                _catalogSearch ?? string.Empty,
                _vm?.CurrentTargetTypeInt.ToString() ?? "-1",
                _vm?.CurrentHeroId ?? string.Empty,
                _vm?.IsDirty == true ? "1" : "0",
                _vm?.ExportStatusText ?? string.Empty,
                GetActiveSlotIndex().ToString()
            };

            if (_vm?.Skills != null)
            {
                for (int i = 0; i < _vm.Skills.Count; i++)
                {
                    var slot = _vm.Skills[i];
                    parts.Add(slot?.Skill?.SkillId ?? string.Empty);
                    parts.Add(slot?.IsEquipped == true ? "1" : "0");
                }
            }

            if (_vm?.Proficiencies != null)
                for (int i = 0; i < _vm.Proficiencies.Count; i++)
                {
                    var p = _vm.Proficiencies[i];
                    parts.Add(p?.Value.ToString() ?? "0");
                }

            if (_forgeVm != null)
            {
                parts.Add(_forgeVm.NewSpellName ?? string.Empty);
                parts.Add(_forgeVm.BuildDescription ?? string.Empty);
                parts.Add(_forgeVm.ValidationMessage ?? string.Empty);
                parts.Add(_forgeVm.CurrentBuild?.Count.ToString() ?? "0");
                parts.Add(_forgeVm.AllSpells?.Count.ToString() ?? "0");
            }

            return string.Join("|", parts);
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
                catalog = GetCatalogStateCached(),
                forge = _view == "forge" ? GetForgeStateCached() : null
            };
        }

        private List<object> BuildHeroState()
        {
            var result = new List<object>();
            if (_vm?.Roster == null) return result;
            for (int i = 0; i < _vm.Roster.Count; i++)
            {
                var hero = _vm.Roster[i];
                result.Add(new { index = i, id = hero.HeroId ?? string.Empty, name = hero.HeroName ?? string.Empty, subtitle = hero.Subtitle ?? string.Empty, selected = hero.IsSelected });
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

        private List<object> GetCatalogStateCached()
        {
            if (_view != "catalog" || _vm?.ActiveSlot == null || _vm.Catalog?.AllSkills == null)
                return new List<object>();

            var key = (_vm.ActiveSlot.SlotFilterType.ToString()) + "|" + (_catalogSearch ?? string.Empty);
            if (_catalogCache != null && string.Equals(_catalogCacheKey, key, StringComparison.Ordinal)) return _catalogCache;
            _catalogCacheKey = key;
            _catalogCache = BuildCatalogState();
            return _catalogCache;
        }

        private List<object> BuildCatalogState()
        {
            var result = new List<object>();
            string filter = (_catalogSearch ?? string.Empty).Trim();
            foreach (var skill in _vm.Catalog.AllSkills)
            {
                if (skill == null || skill.IsEmpty || skill.Type != _vm.ActiveSlot.SlotFilterType) continue;
                if (!string.IsNullOrWhiteSpace(filter) && !ContainsIgnoreCase(skill.SkillName, filter) && !ContainsIgnoreCase(skill.Description, filter) && !ContainsIgnoreCase(skill.SkillId, filter)) continue;
                var difficulties = new List<object>();
                if (skill.Difficulties != null)
                    foreach (var diff in skill.Difficulties)
                        if (diff != null) difficulties.Add(new { difficulty = diff.Difficulty, attribute = diff.UseAttribute ?? string.Empty });
                result.Add(new { id = skill.SkillId ?? string.Empty, name = skill.SkillName ?? string.Empty, description = skill.Description ?? string.Empty, type = skill.Type.ToString(), icon = skill.IconItemId ?? string.Empty, cooldown = skill.Cooldown, cost = skill.ResourceCost, difficulties });
            }
            return result.OrderBy(x => JObject.FromObject(x)["name"]?.Value<string>(), StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private object GetForgeStateCached()
        {
            if (_forgeVm == null) return null;
            var key = (_forgeVm.NewSpellName ?? string.Empty) + "|" + (_forgeVm.BuildDescription ?? string.Empty) + "|" + (_forgeVm.ValidationMessage ?? string.Empty) + "|" + (_forgeVm.CurrentBuild?.Count ?? 0) + "|" + (_forgeVm.AllSpells?.Count ?? 0);
            if (_forgeCache != null && string.Equals(_forgeCacheKey, key, StringComparison.Ordinal)) return _forgeCache;
            _forgeCacheKey = key;
            _forgeCache = new
            {
                newSpellName = _forgeVm.NewSpellName ?? string.Empty,
                buildDescription = _forgeVm.BuildDescription ?? string.Empty,
                validationMessage = _forgeVm.ValidationMessage ?? string.Empty,
                availableNodes = BuildForgeEntries(_forgeVm.AvailableNodes),
                currentBuild = BuildForgeEntries(_forgeVm.CurrentBuild),
                allSpells = BuildForgeEntries(_forgeVm.AllSpells)
            };
            return _forgeCache;
        }

        private static List<object> BuildForgeEntries(IEnumerable<New_ZZZF.SpellForge.ForgeEntryVM> entries)
        {
            var result = new List<object>();
            if (entries == null) return result;
            foreach (var entry in entries)
                if (entry != null) result.Add(new { id = entry.SkillId ?? string.Empty, name = entry.SkillName ?? string.Empty, description = entry.Description ?? string.Empty });
            return result;
        }

        private void InvalidateStateCaches()
        {
            _lastModelStamp = null;
            _lastSignature = null;
            InvalidateCatalogCache();
            InvalidateForgeCache();
        }

        private void InvalidateCatalogCache()
        {
            _catalogCacheKey = null;
            _catalogCache = null;
        }

        private void InvalidateForgeCache()
        {
            _forgeCacheKey = null;
            _forgeCache = null;
        }

        private int GetActiveSlotIndex()
        {
            if (_vm?.Skills == null || _vm.ActiveSlot == null) return -1;
            for (int i = 0; i < _vm.Skills.Count; i++) if (ReferenceEquals(_vm.Skills[i], _vm.ActiveSlot)) return i;
            return -1;
        }

        private static bool ContainsIgnoreCase(string value, string search)
        {
            return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(search) && value.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
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
