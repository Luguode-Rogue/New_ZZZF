using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BannerlordHtmlUI;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.BattleHud
{
    /// <summary>
    /// 战斗内 HTML HUD（耐力/法力/技能冷却等）。
    ///
    /// 刷新策略：
    ///  - 完整结构、属性、冷却和选槽使用独立 retained state；
    ///  - 同类变化在 MissionTick 合并，页面晚加载或重载也能从状态快照恢复；
    ///  - 属性和选槽变化不再构造、序列化完整技能状态；
    ///  - 技能 CD/GCD 在变化时同步；状态持续时间开始、结束时同步，生效期间每秒校准一次；
    ///  - 资源/护盾按整数变化通知，避免每帧浮点变化跨 HTMLUI 桥。
    /// </summary>
    public sealed class BattleHudHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.BattleHud";
        private const string SurfaceName = "battlehud";
        private const string ContentRootName = "ui";
        private const string StateKey = "battleHud";
        private const string VitalsStateKey = "battleHud.vitals";
        private const string TimersStateKey = "battleHud.timers";
        private const string SelectionStateKey = "battleHud.selection";

        private static readonly Lazy<BattleHudHtmlUi> _instance =
            new Lazy<BattleHudHtmlUi>(() => new BattleHudHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _surfaceId;
        private bool _registered;
        private bool _shown;
        private bool _missionActive;
        private readonly HashSet<string> _captureSuspensionOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _fullDirty = true;
        private bool _vitalsDirty = true;
        private bool _timersDirty = true;
        private bool _selectionDirty = true;
        private AgentSkillComponent _boundComponent;
        private long _nextShowAttemptTimestamp;
        private long _nextPublishAttemptTimestamp;
        private long _nextDurationRefreshTimestamp;
        private static readonly long RetryDelayTicks = Stopwatch.Frequency * 2L;

        public static BattleHudHtmlUi Instance => _instance.Value;

        private BattleHudHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            string assemblyDir = Path.GetDirectoryName(typeof(BattleHudHtmlUi).Assembly.Location) ?? ".";
            DirectoryInfo binDir = Directory.GetParent(assemblyDir);
            DirectoryInfo moduleDir = binDir == null ? null : Directory.GetParent(binDir.FullName);
            string uiRoot = moduleDir == null
                ? Path.Combine(assemblyDir, "UI")
                : Path.Combine(moduleDir.FullName, "UI");
            if (!Directory.Exists(uiRoot))
                throw new DirectoryNotFoundException("BattleHud content root not found: " + uiRoot);

            _scope = HtmlUiService.CreateScope(OwnerId);
            _scope.RegisterContentRoot(ContentRootName, uiRoot);
            _scope.RegisterRequest("getState", _ =>
            {
                EnsureBoundComponent();
                return Task.FromResult<object>(BuildState(_boundComponent));
            });
            _surfaceId = _scope.RegisterSurface(new HtmlUiSurface(SurfaceName, "BattleHud/index.html")
            {
                ContentRootId = ContentRootName,
                ZIndex = 50,
                InputDemand = HtmlUiInputMode.Passive,
                CoexistWithPage = true
            });

            _registered = true;
            TacticalMapLog.Info("[BattleHud] Surface registered. Root=" + uiRoot);
        }

        public void OnMissionStarted()
        {
            _missionActive = true;
            if (_captureSuspensionOwners.Count > 0 || !_registered || !HtmlUiService.IsReady || _shown) return;
            long now = Stopwatch.GetTimestamp();
            if (now < _nextShowAttemptTimestamp) return;
            try
            {
                if (HtmlUiService.Surfaces.Show(_surfaceId))
                {
                    _nextShowAttemptTimestamp = 0L;
                    _shown = true;
                    MarkAllDirty();
                    EnsureBoundComponent();
                    TacticalMapLog.Info("[BattleHud] Surface shown for mission.");
                    PublishPendingState();
                }
                else
                {
                    _nextShowAttemptTimestamp = now + RetryDelayTicks;
                }
            }
            catch (Exception ex)
            {
                _nextShowAttemptTimestamp = now + RetryDelayTicks;
                TacticalMapLog.Error("[BattleHud] Surface show failed.", ex);
            }
        }

        public void OnMissionEnded()
        {
            _missionActive = false;
            _captureSuspensionOwners.Clear();
            _nextShowAttemptTimestamp = 0L;
            _nextPublishAttemptTimestamp = 0L;
            _nextDurationRefreshTimestamp = 0L;
            UnbindComponent();

            if (!_registered || !HtmlUiService.IsReady || !_shown)
            {
                _shown = false;
                MarkAllDirty();
                return;
            }

            try
            {
                HtmlUiService.Surfaces.Hide(_surfaceId);
                _shown = false;
                MarkAllDirty();
                TacticalMapLog.Info("[BattleHud] Surface hidden after mission.");
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Surface hide failed.", ex);
            }
        }

        public void SetCaptureSuspended(bool suspended)
        {
            SetCaptureSuspended(suspended, OwnerId + ".legacy");
        }

        public void SetCaptureSuspended(bool suspended, string ownerId)
        {
            string source = string.IsNullOrWhiteSpace(ownerId) ? OwnerId + ".legacy" : ownerId.Trim();
            bool wasSuspended = _captureSuspensionOwners.Count > 0;
            if (suspended) _captureSuspensionOwners.Add(source);
            else _captureSuspensionOwners.Remove(source);
            bool isSuspended = _captureSuspensionOwners.Count > 0;
            if (wasSuspended == isSuspended) return;

            if (isSuspended)
            {
                if (!_registered || !HtmlUiService.IsReady || !_shown) return;
                try
                {
                    HtmlUiService.Surfaces.Hide(_surfaceId);
                    _shown = false;
                    MarkAllDirty();
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[BattleHud] Capture hide failed.", ex);
                }
            }
            else
            {
                // A paused mission may have no next tick to retry the HUD show.
                if (_missionActive)
                {
                    _nextShowAttemptTimestamp = 0L;
                    OnMissionStarted();
                }
            }
        }

        /// <summary>
        /// MissionTick 合并主角 HUD 通知，持续状态每秒校准一次。
        /// </summary>
        public void Tick(float dt)
        {
            _ = dt;
            if (_captureSuspensionOwners.Count > 0 || !_shown || !_registered || !HtmlUiService.IsReady) return;

            EnsureBoundComponent();
            long now = Stopwatch.GetTimestamp();
            if (now >= _nextDurationRefreshTimestamp)
            {
                _nextDurationRefreshTimestamp = now + Stopwatch.Frequency;
                if (HasActiveHudDuration(_boundComponent)) _timersDirty = true;
            }
            if (now >= _nextPublishAttemptTimestamp &&
                (_fullDirty || _vitalsDirty || _timersDirty || _selectionDirty))
                PublishPendingState();
        }

        private void EnsureBoundComponent()
        {
            AgentSkillComponent next = null;
            var agent = TaleWorlds.MountAndBlade.Agent.Main;
            if (agent != null && agent.IsActive())
                SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out next);

            if (ReferenceEquals(next, _boundComponent))
                return;

            UnbindComponent();
            _boundComponent = next;
            if (_boundComponent != null)
            {
                _boundComponent.HudStateChanged += OnHudStateChanged;
                _boundComponent.HudVitalsChanged += OnHudVitalsChanged;
                _boundComponent.HudTimersChanged += OnHudTimersChanged;
                _boundComponent.HudSelectionChanged += OnHudSelectionChanged;
            }

            MarkAllDirty();
        }

        private void UnbindComponent()
        {
            if (_boundComponent != null)
            {
                _boundComponent.HudStateChanged -= OnHudStateChanged;
                _boundComponent.HudVitalsChanged -= OnHudVitalsChanged;
                _boundComponent.HudTimersChanged -= OnHudTimersChanged;
                _boundComponent.HudSelectionChanged -= OnHudSelectionChanged;
            }
            _boundComponent = null;
        }

        private void OnHudStateChanged(AgentSkillComponent component)
        {
            if (ReferenceEquals(component, _boundComponent))
                _fullDirty = true;
        }

        private void OnHudVitalsChanged(AgentSkillComponent component)
        {
            if (ReferenceEquals(component, _boundComponent))
                _vitalsDirty = true;
        }

        private void OnHudTimersChanged(AgentSkillComponent component)
        {
            if (ReferenceEquals(component, _boundComponent))
                _timersDirty = true;
        }

        private void OnHudSelectionChanged(AgentSkillComponent component)
        {
            if (ReferenceEquals(component, _boundComponent))
                _selectionDirty = true;
        }

        private void MarkAllDirty()
        {
            _fullDirty = true;
            _vitalsDirty = true;
            _timersDirty = true;
            _selectionDirty = true;
        }

        private void PublishPendingState()
        {
            if (!_shown || !_registered || _scope == null) return;

            try
            {
                if (_fullDirty)
                {
                    _scope.SetState(StateKey, BuildState(_boundComponent));
                    _fullDirty = false;
                }
                if (_vitalsDirty)
                {
                    _scope.SetState(VitalsStateKey, BuildVitalsState(_boundComponent));
                    _vitalsDirty = false;
                }
                if (_timersDirty)
                {
                    _scope.SetState(TimersStateKey, BuildTimersState(_boundComponent));
                    _timersDirty = false;
                }
                if (_selectionDirty)
                {
                    _scope.SetState(SelectionStateKey, _boundComponent == null ? 0 : _boundComponent.SelectedSpellSlot);
                    _selectionDirty = false;
                }
            }
            catch (Exception ex)
            {
                MarkAllDirty();
                _nextPublishAttemptTimestamp = Stopwatch.GetTimestamp() + RetryDelayTicks;
                TacticalMapLog.Error("[BattleHud] State publish failed.", ex);
            }
        }

        private static int[] BuildVitalsState(AgentSkillComponent comp)
        {
            return new[]
            {
                comp == null ? 0 : QuantizeWhole(comp._currentStamina),
                comp == null ? 0 : QuantizeWhole(comp._currentMana),
                comp == null ? 0 : QuantizeWhole(comp._shieldStrength),
                comp == null ? 0 : comp._lifeResurgenceCount
            };
        }

        private static float[] BuildTimersState(AgentSkillComponent comp)
        {
            // [0] GCD，[1..8] 冷却，[9..16] 持续剩余，[17..24] 持续最大值。
            var values = new float[25];
            if (comp == null) return values;

            values[0] = QuantizeTenths(comp._globalCooldownTimer);
            for (int i = 0; i < comp.SpellSlots.Length; i++)
                values[i + 1] = GetCooldown(comp.SpellSlots[i], comp);
            values[5] = GetCooldown(comp.MainActiveSkill, comp);
            values[6] = GetCooldown(comp.SubActiveSkill, comp);
            values[7] = GetCooldown(comp.PassiveSkill, comp);
            values[8] = GetCooldown(comp.CombatArtSkill, comp);
            SkillBase[] skills = {
                comp.SpellSlots[0], comp.SpellSlots[1], comp.SpellSlots[2], comp.SpellSlots[3],
                comp.MainActiveSkill, comp.SubActiveSkill, comp.PassiveSkill, comp.CombatArtSkill
            };
            for (int i = 0; i < skills.Length; i++)
            {
                GetDuration(skills[i], comp, out float remaining, out float maximum);
                values[9 + i] = remaining;
                values[17 + i] = maximum;
            }
            return values;
        }

        private static float GetCooldown(SkillBase skill, AgentSkillComponent comp)
        {
            return skill == null || string.Equals(skill.SkillID, "NullSkill", StringComparison.OrdinalIgnoreCase)
                ? 0f : QuantizeTenths(comp.GetSkillCooldownForDisplay(skill));
        }

        private static bool HasActiveHudDuration(AgentSkillComponent comp)
        {
            if (comp == null) return false;
            foreach (SkillBase skill in comp.SpellSlots)
                if (IsActiveHudDuration(skill, comp)) return true;
            return IsActiveHudDuration(comp.MainActiveSkill, comp) ||
                   IsActiveHudDuration(comp.SubActiveSkill, comp) ||
                   IsActiveHudDuration(comp.CombatArtSkill, comp);
        }

        private static bool IsActiveHudDuration(SkillBase skill, AgentSkillComponent comp)
        {
            return skill != null && skill.Type != SPSkillType.Passive &&
                   skill.Type != SPSkillType.Passive_Spell &&
                   comp.StateContainer.TryGetSkillDuration(skill, out _, out _);
        }

        private static void GetDuration(SkillBase skill, AgentSkillComponent comp,
            out float remaining, out float maximum)
        {
            remaining = 0f;
            maximum = 0f;
            if (skill == null || skill.Type == SPSkillType.Passive ||
                skill.Type == SPSkillType.Passive_Spell ||
                !comp.StateContainer.TryGetSkillDuration(skill, out float active, out float activeMaximum))
                return;
            remaining = QuantizeTenths(active);
            maximum = Math.Max(remaining, activeMaximum);
        }

        private static object BuildState(AgentSkillComponent comp)
        {
            if (comp == null)
            {
                return new
                {
                    available = false,
                    stamina = 0,
                    mana = 0,
                    gcd = 0f,
                    shield = 0,
                    lives = 0,
                    selectedSpellSlot = 0,
                    skills = Array.Empty<object>()
                };
            }

            var skills = new List<object>(8);
            for (int i = 0; i < comp.SpellSlots.Length; i++)
                AddSlot(skills, "spell" + i, comp.SpellSlots[i], comp, selected: i == comp.SelectedSpellSlot);
            AddSlot(skills, "main", comp.MainActiveSkill, comp, selected: false);
            AddSlot(skills, "sub", comp.SubActiveSkill, comp, selected: false);
            AddSlot(skills, "passive", comp.PassiveSkill, comp, selected: false);
            AddSlot(skills, "combat", comp.CombatArtSkill, comp, selected: false);

            return new
            {
                available = true,
                stamina = QuantizeWhole(comp._currentStamina),
                mana = QuantizeWhole(comp._currentMana),
                gcd = QuantizeTenths(comp._globalCooldownTimer),
                shield = QuantizeWhole(comp._shieldStrength),
                lives = comp._lifeResurgenceCount,
                selectedSpellSlot = comp.SelectedSpellSlot,
                skills
            };
        }

        private static void AddSlot(List<object> list, string slot, SkillBase skill, AgentSkillComponent comp, bool selected)
        {
            if (skill == null || string.Equals(skill.SkillID, "NullSkill", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new
                {
                    slot,
                    empty = true,
                    id = string.Empty,
                    name = string.Empty,
                    cd = 0f,
                    cdMax = 0f,
                    dur = 0f,
                    durMax = 0f,
                    cost = 0f,
                    mana = false,
                    selected,
                    passive = false
                });
                return;
            }

            bool costsMana = skill.Type == SPSkillType.Spell
                || skill.Type == SPSkillType.Passive_Spell
                || skill.Type == SPSkillType.CombatArt_Spell
                || skill.Type == SPSkillType.Spell_CombatArt;

            float cdRemaining = QuantizeTenths(comp.GetSkillCooldownForDisplay(skill));
            GetDuration(skill, comp, out float durationRemaining, out float durationMaximum);

            bool isPassive = skill.Type == SPSkillType.Passive || skill.Type == SPSkillType.Passive_Spell;

            list.Add(new
            {
                slot,
                empty = false,
                id = skill.SkillID ?? string.Empty,
                name = skill.Text != null ? skill.Text.ToString() : (skill.SkillID ?? string.Empty),
                cd = cdRemaining,
                cdMax = skill.Cooldown,
                dur = durationRemaining,
                durMax = durationMaximum,
                cost = comp.GetSkillResourceCostForDisplay(skill),
                mana = costsMana,
                selected,
                passive = isPassive
            });
        }

        private static int QuantizeWhole(float value)
        {
            if (value <= 0f) return 0;
            return (int)Math.Floor(value + 0.5f);
        }

        private static float QuantizeTenths(float value)
        {
            if (value <= 0f) return 0f;
            return (float)(Math.Ceiling((value - 0.0001f) * 10f) / 10.0);
        }

        public void Dispose()
        {
            _captureSuspensionOwners.Clear();
            UnbindComponent();
            try
            {
                if (_shown && _registered && HtmlUiService.IsReady)
                    HtmlUiService.Surfaces.Hide(_surfaceId);
            }
            catch { }

            try { _scope?.Dispose(); } catch { }
            _scope = null;
            _registered = false;
            _shown = false;
            MarkAllDirty();
            _surfaceId = null;
            _nextShowAttemptTimestamp = 0L;
            _nextPublishAttemptTimestamp = 0L;
        }
    }
}
