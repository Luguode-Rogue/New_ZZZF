using System;
using System.Collections.Generic;
using System.IO;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.BattleHud
{
    /// <summary>
    /// 战斗内 HTML HUD（耐力/法力/技能冷却等）。
    ///
    /// 刷新策略：
    ///  - AgentSkillComponent 在“HUD 可见值”发生变化时发 HudStateChanged；
    ///  - 本类只把事件合并为 dirty 标记，并在 MissionTick 最多发布一次；
    ///  - 不再固定 10Hz 轮询/序列化完整状态；
    ///  - CD/GCD 以 0.1 秒、资源/护盾以整数为显示粒度，避免每帧浮点变化跨 HTMLUI 桥。
    /// </summary>
    public sealed class BattleHudHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.BattleHud";
        private const string SurfaceName = "battlehud";
        private const string ContentRootName = "ui";
        private const string StateKey = "battleHud";

        private static readonly Lazy<BattleHudHtmlUi> _instance =
            new Lazy<BattleHudHtmlUi>(() => new BattleHudHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _surfaceId;
        private bool _registered;
        private bool _shown;
        private bool _captureSuspended;
        private bool _dirty = true;
        private string _lastSignature;
        private AgentSkillComponent _boundComponent;

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
            if (_captureSuspended || !_registered || !HtmlUiService.IsReady || _shown) return;
            try
            {
                if (HtmlUiService.Surfaces.Show(_surfaceId))
                {
                    _shown = true;
                    _lastSignature = null;
                    _dirty = true;
                    EnsureBoundComponent();
                    TacticalMapLog.Info("[BattleHud] Surface shown for mission.");
                    PublishState(true);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Surface show failed.", ex);
            }
        }

        public void OnMissionEnded()
        {
            UnbindComponent();

            if (!_registered || !HtmlUiService.IsReady || !_shown)
            {
                _shown = false;
                _dirty = true;
                _lastSignature = null;
                return;
            }

            try
            {
                HtmlUiService.Surfaces.Hide(_surfaceId);
                _shown = false;
                _dirty = true;
                _lastSignature = null;
                TacticalMapLog.Info("[BattleHud] Surface hidden after mission.");
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Surface hide failed.", ex);
            }
        }

        public void SetCaptureSuspended(bool suspended)
        {
            if (_captureSuspended == suspended) return;
            _captureSuspended = suspended;

            if (suspended)
            {
                UnbindComponent();
                if (!_registered || !HtmlUiService.IsReady || !_shown) return;
                try
                {
                    HtmlUiService.Surfaces.Hide(_surfaceId);
                    _shown = false;
                    _dirty = true;
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("[BattleHud] Capture hide failed.", ex);
                }
            }
            else
            {
                OnMissionStarted();
            }
        }

        /// <summary>
        /// MissionTick 只负责主角组件绑定检查与 dirty 合并发布，不做定时轮询。
        /// </summary>
        public void Tick(float dt)
        {
            _ = dt;
            if (_captureSuspended || !_shown || !_registered || !HtmlUiService.IsReady) return;

            EnsureBoundComponent();
            if (_dirty)
                PublishState(false);
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
                _boundComponent.HudStateChanged += OnHudStateChanged;

            _dirty = true;
        }

        private void UnbindComponent()
        {
            if (_boundComponent != null)
                _boundComponent.HudStateChanged -= OnHudStateChanged;
            _boundComponent = null;
        }

        private void OnHudStateChanged(AgentSkillComponent component)
        {
            if (ReferenceEquals(component, _boundComponent))
                _dirty = true;
        }

        private void PublishState(bool force)
        {
            if (!_shown || !_registered || _scope == null) return;
            if (!force && !_dirty) return;

            try
            {
                object state = BuildState(_boundComponent);
                string signature = JsonConvert.SerializeObject(state, Formatting.None);
                _dirty = false;

                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal))
                    return;

                _lastSignature = signature;
                _scope.SetState(StateKey, state);
            }
            catch (Exception ex)
            {
                _dirty = true;
                TacticalMapLog.Error("[BattleHud] State publish failed.", ex);
            }
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

            float cdRemaining = 0f;
            if (comp._cooldownTimers.TryGetValue(skill, out float timer) && timer > 0f)
                cdRemaining = QuantizeTenths(timer);

            bool isPassive = skill.Type == SPSkillType.Passive || skill.Type == SPSkillType.Passive_Spell;

            list.Add(new
            {
                slot,
                empty = false,
                id = skill.SkillID ?? string.Empty,
                name = skill.Text != null ? skill.Text.ToString() : (skill.SkillID ?? string.Empty),
                cd = cdRemaining,
                cdMax = skill.Cooldown,
                cost = skill.ResourceCost,
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
            _dirty = true;
            _surfaceId = null;
            _lastSignature = null;
        }
    }
}
