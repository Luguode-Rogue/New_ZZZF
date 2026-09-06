using System;
using System.Collections.Generic;
using System.IO;
using BannerlordHtmlUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using New_ZZZF.TacticalMap.Diagnostics;

namespace New_ZZZF.BattleHud
{
    /// <summary>
    /// 战斗内 HTML HUD（耐力/法力/技能冷却等）。
    ///
    /// 这是框架上第一个真正的 Surface 消费者：
    ///  - 以 Surface（而非 Page）注册，声明 CoexistWithPage=true，因此 TacticalMap Page 打开时
    ///    不会被 PageDominant 策略抑制，反而由 HtmlUiCoexistHost 直接挂载进地图页面文档；
    ///  - Passive 输入需求，绝不参与输入聚合与屏蔽；
    ///  - 由 BattleHudMissionLogic 随 Mission 生命周期 Show/Hide，10Hz 推送 + 签名去重。
    /// </summary>
    public sealed class BattleHudHtmlUi : IDisposable
    {
        public const string OwnerId = "New_ZZZF.BattleHud";
        private const string SurfaceName = "battlehud";
        private const string ContentRootName = "ui";
        private const string StateKey = "battleHud";
        private const float PublishIntervalSeconds = 0.10f;

        private static readonly Lazy<BattleHudHtmlUi> _instance =
            new Lazy<BattleHudHtmlUi>(() => new BattleHudHtmlUi());

        private HtmlUiConsumerScope _scope;
        private string _surfaceId;
        private bool _registered;
        private bool _shown;
        private float _publishAccum;
        private string _lastSignature;

        public static BattleHudHtmlUi Instance => _instance.Value;

        private BattleHudHtmlUi() { }

        public void InitializeOnFrameworkReady()
        {
            HtmlUiService.OnReady(Register);
        }

        private void Register()
        {
            if (_registered || !HtmlUiService.IsReady) return;

            // 与 TacticalMapHtmlUi 相同的模块 UI 根推导：<模块根>\UI
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

        /// <summary>战斗开始（由 BattleHudMissionLogic 驱动）。</summary>
        public void OnMissionStarted()
        {
            if (!_registered || !HtmlUiService.IsReady || _shown) return;
            try
            {
                if (HtmlUiService.Surfaces.Show(_surfaceId))
                {
                    _shown = true;
                    _lastSignature = null;
                    TacticalMapLog.Info("[BattleHud] Surface shown for mission.");
                    PublishState(true);
                }
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Surface show failed.", ex);
            }
        }

        /// <summary>战斗结束（由 BattleHudMissionLogic 驱动）。</summary>
        public void OnMissionEnded()
        {
            if (!_registered || !HtmlUiService.IsReady || !_shown) return;
            try
            {
                HtmlUiService.Surfaces.Hide(_surfaceId);
                _shown = false;
                _lastSignature = null;
                TacticalMapLog.Info("[BattleHud] Surface hidden after mission.");
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] Surface hide failed.", ex);
            }
        }

        /// <summary>由 BattleHudMissionLogic.OnMissionTick 每帧驱动；内部 10Hz 节流。</summary>
        public void Tick(float dt)
        {
            if (!_shown || !_registered || !HtmlUiService.IsReady) return;

            _publishAccum += Math.Max(0f, dt);
            if (_publishAccum < PublishIntervalSeconds) return;
            _publishAccum = 0f;
            PublishState(false);
        }

        private void PublishState(bool force)
        {
            if (!_shown || !_registered || _scope == null) return;
            try
            {
                object state = BuildState();
                string signature = JsonConvert.SerializeObject(state, Formatting.None);
                if (!force && string.Equals(signature, _lastSignature, StringComparison.Ordinal)) return;
                _lastSignature = signature;
                _scope.SetState(StateKey, state);
            }
            catch (Exception ex)
            {
                TacticalMapLog.Error("[BattleHud] State publish failed.", ex);
            }
        }

        private static object BuildState()
        {
            var agent = TaleWorlds.MountAndBlade.Agent.Main;
            AgentSkillComponent comp = null;
            if (agent != null && agent.IsActive())
                SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out comp);

            if (comp == null)
            {
                return new
                {
                    available = false,
                    stamina = 0f,
                    mana = 0f,
                    gcd = 0f,
                    shield = 0f,
                    lives = 0,
                    selectedSpellSlot = 0,
                    skills = Array.Empty<object>()
                };
            }

            // 全量 8 槽：空槽也上报（empty=true），前端固定布局显示"空"。
            var skills = new List<object>();
            AddSlot(skills, "main", comp.MainActiveSkill, comp, selected: false);
            AddSlot(skills, "sub", comp.SubActiveSkill, comp, selected: false);
            AddSlot(skills, "passive", comp.PassiveSkill, comp, selected: false);
            AddSlot(skills, "combat", comp.CombatArtSkill, comp, selected: false);
            for (int i = 0; i < comp.SpellSlots.Length; i++)
                AddSlot(skills, "spell" + i, comp.SpellSlots[i], comp, selected: i == comp.SelectedSpellSlot);

            return new
            {
                available = true,
                stamina = comp._currentStamina,
                mana = comp._currentMana,
                gcd = comp._globalCooldownTimer,
                shield = comp._shieldStrength,
                lives = comp._lifeResurgenceCount,
                selectedSpellSlot = comp.SelectedSpellSlot,
                skills
            };
        }

        private static void AddSlot(List<object> list, string slot, SkillBase skill, AgentSkillComponent comp, bool selected)
        {
            if (skill == null || string.Equals(skill.SkillID, "NullSkill", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new { slot, empty = true, id = string.Empty, name = string.Empty, cd = 0f, cdMax = 0f, cost = 0f, mana = false, selected, passive = false });
                return;
            }

            // 与 TryActivateSkill 的资源扣除保持一致：法术系耗法力，其余耗耐力。
            bool costsMana = skill.Type == SPSkillType.Spell
                || skill.Type == SPSkillType.Passive_Spell
                || skill.Type == SPSkillType.CombatArt_Spell;

            float cdRemaining = 0f;
            if (comp._cooldownTimers.TryGetValue(skill, out float timer) && timer > 0f)
                cdRemaining = timer;

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
                passive = skill.Type == SPSkillType.Passive
            });
        }

        public void Dispose()
        {
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
            _surfaceId = null;
        }
    }
}
