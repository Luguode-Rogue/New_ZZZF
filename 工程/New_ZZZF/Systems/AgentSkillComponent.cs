using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static New_ZZZF.SkillFactory;

namespace New_ZZZF
{
    /// <summary>
    /// 绑定到每个Agent的技能管理器，处理技能槽、冷却、资源消耗
    /// 新增的agent属性，比如speed和复活次数也放在这里
    /// </summary>
    public class AgentSkillComponent : AgentComponent
    {
        // 添加公共属性以访问基类的Agent
        public Agent AgentInstance => base.Agent;
        public Agent BaseAgent => base.Agent;//淦，记不住上面哪个名字
        public float MaxHP { get; private set; }
        /// <summary>新增法强属性。魔法伤害 = 技能基础伤害 × 此系数，默认1。</summary>
        public float MagicPowerCoefficient { get; private set; } = 1f;
        /// <summary>来自角色属性的通用魔抗，对全部魔法属性生效。</summary>
        public float AttributeMagicResistance { get; private set; }
        /// <summary>来自装备、技能和状态等特殊来源的通用魔抗。</summary>
        public float SpecialUniversalMagicResistance { get; private set; }
        public float SpecialFireResistance { get; private set; }
        public float SpecialIceResistance { get; private set; }
        public float SpecialElectricityResistance { get; private set; }
        public float SpecialToxinResistance { get; private set; }
        private sealed class SpecialMagicResistanceSource
        {
            public float Universal;
            public float Fire;
            public float Ice;
            public float Electricity;
            public float Toxin;
        }
        private readonly Dictionary<string, SpecialMagicResistanceSource>
            _specialMagicResistanceSources =
                new Dictionary<string, SpecialMagicResistanceSource>(StringComparer.Ordinal);
        // 新增状态容器
        public AgentBuffContainer StateContainer { get; } = new AgentBuffContainer();
        //------------------------ 技能槽配置 ------------------------
        public SkillBase MainActiveSkill { get; private set; } = new NullSkill();    // 主主动技能
        public SkillBase SubActiveSkill { get; private set; } = new NullSkill();    // 副主动技能
        public SkillBase PassiveSkill { get; private set; } = new NullSkill(); // 被动栏技能
        public SkillBase[] SpellSlots { get; } = new SkillBase[4] { new NullSkill(), new NullSkill(), new NullSkill(), new NullSkill() };// 法术栏（0-3号位）
        public SkillBase CombatArtSkill { get; private set; } = new NullSkill();   // 战技
        private bool CombatArtFlag { get; set; } = false;// 是否处于战技准备状态

        //------------------------ 资源与状态 ------------------------
        private float _currentManaValue = 100f;
        private float _currentStaminaValue = 100f;
        private float _globalCooldownTimerValue;
        private float _shieldStrengthValue;
        private GameEntity _shieldStrengthVisual;
        private int _lifeResurgenceCountValue;

        /// <summary>技能结构等低频完整状态变化。</summary>
        public event Action<AgentSkillComponent> HudStateChanged;
        /// <summary>耐力、法力、护盾或复活次数变化。</summary>
        public event Action<AgentSkillComponent> HudVitalsChanged;
        /// <summary>技能冷却或公共冷却开始、结束、延长。</summary>
        public event Action<AgentSkillComponent> HudTimersChanged;
        /// <summary>玩家选择的法术槽变化。</summary>
        public event Action<AgentSkillComponent> HudSelectionChanged;

        public float _currentMana
        {
            get => _currentManaValue;
            set
            {
                if (ToHudWholeBucket(_currentManaValue) == ToHudWholeBucket(value))
                {
                    _currentManaValue = value;
                    return;
                }
                _currentManaValue = value;
                NotifyHudVitalsChanged();
            }
        }
        public float _currentStamina
        {
            get => _currentStaminaValue;
            set
            {
                if (ToHudWholeBucket(_currentStaminaValue) == ToHudWholeBucket(value))
                {
                    _currentStaminaValue = value;
                    return;
                }
                _currentStaminaValue = value;
                NotifyHudVitalsChanged();
            }
        }
        public float _globalCooldownTimer
        {
            get => _globalCooldownTimerValue;
            set
            {
                float oldValue = _globalCooldownTimerValue;
                float newValue = value > 0f ? value : 0f;
                _globalCooldownTimerValue = newValue;

                // GCD 倒计时由 HTML 本地显示。这里只在开始、结束或被主动延长时通知，
                // 避免倒计时期间每 0.1 秒跨 HTMLUI 桥刷新整份 HUD 状态。
                bool wasActive = oldValue > 0.0001f;
                bool isActive = newValue > 0.0001f;
                bool extended = isActive && newValue > oldValue + 0.05f;
                if (wasActive != isActive || extended)
                    NotifyHudTimersChanged();
            }
        }
        public bool _isInCombatArtState;        // 是否处于战技准备状态
        public float _shieldStrength
        {
            get => _shieldStrengthValue;
            set
            {
                bool visibilityChanged = (_shieldStrengthValue > 0f) != (value > 0f);
                bool hudChanged = ToHudWholeBucket(_shieldStrengthValue) != ToHudWholeBucket(value);
                _shieldStrengthValue = value;
                if (visibilityChanged)
                    UpdateShieldStrengthVisual();
                if (hudChanged)
                    NotifyHudVitalsChanged();
            }
        }

        private void UpdateShieldStrengthVisual()
        {
            if (_shieldStrengthValue <= 0f)
            {
                ReleaseShieldStrengthVisual();
                return;
            }
            if (Agent == null || !Agent.IsActive())
                return;
            if (_shieldStrengthVisual == null)
                _shieldStrengthVisual = Script.CreateBellShieldVisual(Agent,
                    new Color(1f, 0.79f, 0.2f, 0.34f));
            else
                Script.UpdateEggShellVisual(_shieldStrengthVisual, Agent);
        }

        internal void ReleaseShieldStrengthVisual()
        {
            if (_shieldStrengthVisual != null)
                _shieldStrengthVisual.Remove(0);
            _shieldStrengthVisual = null;
        }
        public int _lifeResurgenceCount
        {
            get => _lifeResurgenceCountValue;
            set
            {
                if (_lifeResurgenceCountValue == value)
                    return;
                _lifeResurgenceCountValue = value;
                NotifyHudVitalsChanged();
            }
        }

        public int _beHitCount = 0;//受击次数记录
        public float _beHitTime = 0f;//受击间隔记录

        private Vec3 _velocity = Vec3.Zero; // 当前速度（内部状态）用于坐骑横向行走
        public AgentSpeed Speed { get; set; }
        public class AgentSpeed
        {
            public Vec3 oldPos;
            public Vec3 newPos;
            public Agent agent;
            public Vec3 speed { get; set; }

            public AgentSpeed(Agent Nagent)
            {
                agent = Nagent;
                oldPos = Nagent?.Position ?? Vec3.Zero;
                newPos = oldPos;
                speed = Vec3.Zero;
            }
            public void Tick(float dt)
            {
                if (agent == null || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                {
                    speed = Vec3.Zero;
                    return;
                }
                this.oldPos = this.newPos;
                this.newPos = this.agent.Position;
                this.speed = (this.newPos - this.oldPos) / dt;
            }
        }


        // 冷却计时器（Key: 技能实例, Value: 剩余冷却时间）
        public readonly Dictionary<SkillBase, float> _cooldownTimers = new Dictionary<SkillBase, float>();
        private readonly List<SkillBase> _cooldownKeysScratch = new List<SkillBase>(8);
        private const float AiDecisionInterval = 0.5f;
        private float _aiDecisionTimer;

        private static int ToHudWholeBucket(float value)
        {
            return value <= 0f ? 0 : (int)Math.Floor(value + 0.5f);
        }

        private void NotifyHudStateChanged()
        {
            HudStateChanged?.Invoke(this);
        }

        private void NotifyHudVitalsChanged()
        {
            HudVitalsChanged?.Invoke(this);
        }

        private void NotifyHudTimersChanged()
        {
            HudTimersChanged?.Invoke(this);
        }

        /// <summary>阶段性技能开始或结束可重复施法窗口时刷新 HUD。</summary>
        public void NotifySkillAvailabilityChanged()
        {
            NotifyHudStateChanged();
            NotifyHudTimersChanged();
        }

        public float GetSkillCooldownForDisplay(SkillBase skill)
        {
            if (skill == null || skill.GetActivationPolicy(Agent).IgnoreSkillCooldown)
                return 0f;
            return _cooldownTimers.TryGetValue(skill, out float remaining) && remaining > 0f
                ? remaining : 0f;
        }

        public float GetSkillResourceCostForDisplay(SkillBase skill)
        {
            return skill == null ? 0f : skill.GetActivationPolicy(Agent).ResourceCost;
        }

        private void NotifyHudSelectionChanged()
        {
            HudSelectionChanged?.Invoke(this);
        }

        public AgentSkillComponent(Agent agent) : base(agent)
        {
            MaxHP = agent.Health;
            Speed = new AgentSpeed(agent);
            StateContainer.TimersChanged += NotifyHudTimersChanged;
            // 将 AI 检查均匀分散到 0.5 秒窗口，避免整支部队在同一帧做战术判断。
            _aiDecisionTimer = (agent.Index % 10) * (AiDecisionInterval / 10f);
        }

        public bool HasSkill(string skill)
        {
            if (MainActiveSkill == null) return false;
            if (MainActiveSkill.SkillID == skill || SubActiveSkill.SkillID == skill || PassiveSkill.SkillID == skill || CombatArtSkill.SkillID == skill
                || SpellSlots[0].SkillID == skill || SpellSlots[1].SkillID == skill || SpellSlots[2].SkillID == skill || SpellSlots[3].SkillID == skill)
            {
                return true;
            }
            return false;
        }

        public void SetMagicPowerCoefficient(float value)
        {
            MagicPowerCoefficient = MathF.Max(0f, value);
        }

        public void SetAttributeMagicResistance(float value)
        {
            AttributeMagicResistance = value;
        }

        public void SetSpecialMagicResistances(
            float universal,
            float fire,
            float ice,
            float electricity,
            float toxin)
        {
            SetSpecialMagicResistanceSource(
                "direct", universal, fire, ice, electricity, toxin);
        }

        public void SetSpecialMagicResistanceSource(
            string sourceId,
            float universal,
            float fire,
            float ice,
            float electricity,
            float toxin)
        {
            if (string.IsNullOrEmpty(sourceId))
                return;
            _specialMagicResistanceSources[sourceId] = new SpecialMagicResistanceSource
            {
                Universal = universal,
                Fire = fire,
                Ice = ice,
                Electricity = electricity,
                Toxin = toxin
            };
            RecalculateSpecialMagicResistances();
        }

        public void RemoveSpecialMagicResistanceSource(string sourceId)
        {
            if (!string.IsNullOrEmpty(sourceId) &&
                _specialMagicResistanceSources.Remove(sourceId))
                RecalculateSpecialMagicResistances();
        }

        private void RecalculateSpecialMagicResistances()
        {
            float universal = 0f;
            float fire = 0f;
            float ice = 0f;
            float electricity = 0f;
            float toxin = 0f;
            foreach (SpecialMagicResistanceSource source in _specialMagicResistanceSources.Values)
            {
                universal += source.Universal;
                fire += source.Fire;
                ice += source.Ice;
                electricity += source.Electricity;
                toxin += source.Toxin;
            }
            SpecialUniversalMagicResistance = universal;
            SpecialFireResistance = fire;
            SpecialIceResistance = ice;
            SpecialElectricityResistance = electricity;
            SpecialToxinResistance = toxin;
        }

        public float GetSpecialElementResistance(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.FIRE_DAMAGE:
                case DamageType.FIRE_ENHANCEMENT_BLASTING:
                    return SpecialFireResistance;
                case DamageType.ICE_DAMAGE:
                case DamageType.ICE_ENHANCEMENT_FREEZING:
                    return SpecialIceResistance;
                case DamageType.ELECTRICITY_DAMAGE:
                case DamageType.ELECTRICITY_ENHANCEMENT_PARALYZING:
                    return SpecialElectricityResistance;
                case DamageType.TOXIN_DAMAGE:
                case DamageType.TOXIN_ENHANCEMENT_CORRUPTING:
                    return SpecialToxinResistance;
                default:
                    return 0f;
            }
        }
        /// <summary>
        /// 根据兵种配置初始化技能槽
        /// </summary>
        public void InitializeFromTroop(string troopId)
        {
            var skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(troopId);
            if (skillSet == null)
            { return; }
            MainActiveSkill = skillSet.MainActive;
            SubActiveSkill = skillSet.SubActive;
            PassiveSkill = skillSet.Passive;
            CombatArtSkill = skillSet.CombatArt;
            Array.Copy(skillSet.Spells, SpellSlots, 4);
            for (int i = 0; i < 4; i++)
            {

                if (skillSet.Spells[i] == null)
                    skillSet.Spells[i] = new NullSkill();
            }
            // 初始化被动技能
            if (PassiveSkill != null)
            {
                PassiveSkill.OnEquip(Agent);
            }
            NotifyHudStateChanged();
        }

        /// <summary>
        /// 手动调用的每帧更新方法（由MissionBehavior驱动）
        /// </summary>
        public void Tick(float dt)
        {
            if (!Agent.IsActive()) return;
            if (_shieldStrengthValue > 0f)
                UpdateShieldStrengthVisual();

            // 主角在冲刺斩期间会暂时切换为 AI Controller，让原生导航负责移动。
            // 身份仍然是 MainAgent，不能因此进入普通士兵的自动施法逻辑。
            if (Agent.IsMainAgent)
            {
                if (Agent.IsPlayerControlled)
                    HandlePlayerInput(dt);
                return;
            }

            if (Agent.IsPlayerControlled)
                HandlePlayerInput(dt);
            else
            {
                _aiDecisionTimer -= dt;
                if (_aiDecisionTimer <= 0f)
                {
                    // 加而不是直接赋值，避免帧长波动造成长期同步。
                    _aiDecisionTimer += AiDecisionInterval;
                    if (_aiDecisionTimer <= 0f)
                        _aiDecisionTimer = AiDecisionInterval;
                    HandleAIBehaviorOfTick();
                }
            }
        }
        /// <summary>
        /// 手动调用的每帧更新方法（由MissionBehavior驱动）
        /// </summary>
        public void CoolDownTick(float dt)
        {
            if (_currentStamina < 100f)
                _currentStamina = TaleWorlds.Library.MathF.Clamp(_currentStamina + dt, 0f, 100f);
            if (_currentMana < 100f)
                _currentMana = TaleWorlds.Library.MathF.Clamp(_currentMana + dt, 0f, 100f);
            _beHitTime -= dt;
            if (_beHitTime <= 0)
            {
                _beHitCount = 0;
            }
            UpdateCooldowns(dt);
            UpdateGlobalCooldown(dt);

            // 更新所有状态
            StateContainer.UpdateStates(Agent, dt);
        }

        /// <summary>
        /// 玩家输入处理
        /// </summary>
        private void HandlePlayerInput(float dt)
        {
            // 主主动技能（E键）
            if (Input.IsKeyPressed(InputKey.E))
            {
                TryActivateSkill(MainActiveSkill);
            }

            // 副主动技能（左Alt）
            if (Input.IsKeyPressed(InputKey.LeftAlt))
            {
                TryActivateSkill(SubActiveSkill);
            }

            // 法术选择（鼠标滚轮）
            float scrollDelta = Input.DeltaMouseScroll;
            if (SpellSlots[_selectedSpellSlot] != null)
            {
                if (scrollDelta > 0)
                {
                    SetSelectedSpellSlot((_selectedSpellSlot + 1) % 4);
                    if (SpellSlots[_selectedSpellSlot].SkillID != "NullSkill")
                        Script.SysOut(SpellSlots[_selectedSpellSlot].SkillID, Agent);
                }
                else if (scrollDelta < 0)
                {
                    SetSelectedSpellSlot((_selectedSpellSlot - 1 + 4) % 4);
                    if (SpellSlots[_selectedSpellSlot].SkillID != "NullSkill")
                        Script.SysOut(SpellSlots[_selectedSpellSlot].SkillID, Agent);
                }
            }


            // 法术施放（右键）
            if (Input.IsKeyPressed(InputKey.RightMouseButton))
            {
                SkillBase selectedSpell = SpellSlots[_selectedSpellSlot];
                TryActivateSkill(selectedSpell);
            }

            // 战技（长按攻击键后松开）

            if (Input.IsKeyReleased(InputKey.LeftMouseButton) && Agent.GetCurrentActionProgress(1) < 0.5f && Agent.GetCurrentActionType(1) == Agent.ActionCodeType.ReleaseMelee)
            {
                CombatArtFlag = true;
            }
            if (CombatArtFlag && Agent.GetCurrentActionProgress(1) > 0.5f)
            {
                CombatArtFlag = false;
                TryActivateSkill(CombatArtSkill);
                AgentSkillComponent agentSkill = Script.GetActiveComponents(Agent);
                if (agentSkill != null && agentSkill.StateContainer.HasState("ZhenYinZhanBuff"))
                {
                    (agentSkill.MainActiveSkill as ZhenYinZhan).CanUse(Agent);
                }
            }
        }

        //====================== 法术栏输入处理 ======================
        private int _selectedSpellSlot = 0; // 当前选中的法术栏位（0-3）

        /// <summary>当前选中的法术栏位（0-3），供 HUD 等只读展示使用。</summary>
        public int SelectedSpellSlot => _selectedSpellSlot;

        private void SetSelectedSpellSlot(int slot)
        {
            slot = Math.Max(0, Math.Min(SpellSlots.Length - 1, slot));
            if (_selectedSpellSlot == slot)
                return;
            _selectedSpellSlot = slot;
            NotifyHudSelectionChanged();
        }


        /// <summary>
        /// 尝试激活技能（核心逻辑）
        /// </summary>
        private void TryActivateSkill(SkillBase skill)
        {
            bool reportFailure = Agent != null && Agent.IsPlayerControlled;
            if (skill == null)
            {
                if (reportFailure)
                    Script.SysOut("技能发动失败：技能槽为空。", Agent);
                return;
            }

            SkillActivationPolicy policy = skill.GetActivationPolicy(Agent);
            string failureReason;
            if (!CanActivateSkill(skill, policy, out failureReason))
            {
                if (reportFailure)
                    Script.SysOut("[" + skill.SkillID + "] 发动失败：" + failureReason, Agent);
                return;
            }

            skill.ResetActivationFailureReason();
            bool activated;
            try
            {
                activated = skill.Activate(Agent);
            }
            catch (Exception ex)
            {
                if (reportFailure)
                    Script.SysOut("[" + skill.SkillID + "] 发动异常：" + ex.Message, Agent);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return;
            }

            // 扣除资源// 触发技能效果
            if (activated)
            {
                if (skill.Type == SPSkillType.Spell || skill.Type == SPSkillType.Spell_CombatArt)
                {
                    Agent.SetActionChannel(1, ActionIndexCache.Create("act_horse_command_follow"), false, (AnimFlags)172UL, 0, 1.5f, -0.2f, 0.4f, 0.5f);
                    _currentMana = Math.Max(0, _currentMana - policy.ResourceCost);
                }
                else
                {
                    _currentStamina = Math.Max(0, _currentStamina - policy.ResourceCost);
                }
                if (policy.CooldownOnSuccess > 0f)
                    _cooldownTimers[skill] = policy.CooldownOnSuccess;


                // 触发公共CD（仅法术）
                if (skill.Type == SPSkillType.Spell || skill.Type == SPSkillType.Spell_CombatArt)
                    _globalCooldownTimer += 1.0f; // 公共CD设为1秒

                NotifyHudTimersChanged();

                // 统一施法音效入口：只在真正发动成功后播放，技能可自行指定事件。
                if (!string.IsNullOrEmpty(skill.CastSoundEvent) && Agent != null && Agent.IsActive())
                {
                    try { SoundManager.StartOneShotEvent(skill.CastSoundEvent, Agent.Position); }
                    catch (Exception) { /* 音效故障不得影响已成功结算的技能。 */ }
                }

            }
            else
            {
                string reason = string.IsNullOrEmpty(skill.LastActivationFailureReason)
                    ? "技能效果未成功创建。"
                    : skill.LastActivationFailureReason;
                if (reportFailure)
                    Script.SysOut("[" + skill.SkillID + "] 发动失败：" + reason, Agent);
            }

        }

        public void ChangeStamina(float value)
        {
            this._currentStamina += value;
            _currentStamina = TaleWorlds.Library.MathF.Clamp(_currentStamina, 0, 100);
        }
        public void ChangeMana(float value)
        {
            this._currentMana += value;
            _currentMana = TaleWorlds.Library.MathF.Clamp(_currentMana, 0, 100);
        }
        /// <summary>
        /// 检查技能是否可激活
        /// </summary>
        private bool CanActivateSkill(SkillBase skill, SkillActivationPolicy policy, out string failureReason)
        {
            failureReason = null;
            // 基础检查
            if (skill == null)
            {
                failureReason = "技能实例为空。";
                return false;
            }
            if (Agent == null || !Agent.IsActive())
            {
                failureReason = "施法者当前不可用。";
                return false;
            }
            // 不以当前攻防动作拦截技能；各技能自行判断能否在该时机发动。
            // if (Agent.IsPerformingAction() && !skill.CanActivateWhilePerformingAction)
            // {
            //     failureReason = "角色正在执行其他动作。";
            //     return false;
            // }

            // 资源检查
            bool hasResource = (skill.Type == SPSkillType.Spell || skill.Type == SPSkillType.Spell_CombatArt) ?
                _currentMana >= policy.ResourceCost :
                _currentStamina >= policy.ResourceCost;

            // 冷却检查
            float remaining = 0f;
            bool isOnCooldown = !policy.IgnoreSkillCooldown &&
                _cooldownTimers.TryGetValue(skill, out remaining) && remaining > 0;
            bool isGCDBlocked = (skill.Type == SPSkillType.Spell) && _globalCooldownTimer > 0;
            if (!hasResource)
            {
                bool usesMana = skill.Type == SPSkillType.Spell || skill.Type == SPSkillType.Spell_CombatArt;
                float current = usesMana ? _currentMana : _currentStamina;
                failureReason = string.Format("{0}不足（当前 {1:0.0}，需要 {2:0.0}）。",
                    usesMana ? "法力" : "耐力", current, policy.ResourceCost);
                return false;
            }
            if (isOnCooldown)
            {
                failureReason = string.Format("技能尚未冷却（剩余 {0:0.0} 秒）。", remaining);
                return false;
            }
            if (isGCDBlocked)
            {
                failureReason = string.Format("法术公共冷却尚未结束（剩余 {0:0.0} 秒）。", _globalCooldownTimer);
                return false;
            }
            return true;
        }

        //====================== 状态更新 ======================
        /// <summary>
        /// 如果参数二为none类型，则更新全部技能cd，否则只更新对应类型技能的cd
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="skillType"></param>
        public void UpdateCooldowns(float dt, SPSkillType skillType = SPSkillType.None)
        {
            if (_cooldownTimers.Count == 0)
                return;

            // 觉醒仅加速正常逐帧流逝的副主动和战技冷却；其他技能主动调用的
            // UpdateCooldowns(dt, skillType) 保持其原有的固定减时语义。
            int awakeningTiers = skillType == SPSkillType.None && StateContainer.HasState("JueXingBuff")
                ? (int)(_currentStamina / 30f) : 0;

            // 复用键缓存，避免每个角色每次更新都创建 List 和 Dictionary 副本。
            _cooldownKeysScratch.Clear();
            foreach (SkillBase key in _cooldownTimers.Keys)
                _cooldownKeysScratch.Add(key);

            bool anyCooldownFinished = false;
            for (int i = 0; i < _cooldownKeysScratch.Count; i++)
            {
                SkillBase key = _cooldownKeysScratch[i];
                if (!_cooldownTimers.TryGetValue(key, out float value))
                    continue;

                bool matchedType = ((key.Type == SPSkillType.Spell || key.Type == SPSkillType.Spell_CombatArt) && skillType == SPSkillType.Spell) ||
                    (key.Type == SPSkillType.MainActive && skillType == SPSkillType.MainActive) ||
                    (key.Type == SPSkillType.SubActive && skillType == SPSkillType.SubActive) ||
                    ((key.Type == SPSkillType.Passive || key.Type == SPSkillType.Passive_Spell) && skillType == SPSkillType.Passive);

                if (skillType != SPSkillType.None && !matchedType)
                    continue;

                bool accelerated = awakeningTiers > 0 &&
                    (ReferenceEquals(key, SubActiveSkill) || ReferenceEquals(key, CombatArtSkill));
                float remainingTime = value - dt * (accelerated ? 1 + awakeningTiers : 1);
                if (remainingTime <= 0f)
                {
                    _cooldownTimers.Remove(key);
                    anyCooldownFinished = true;
                }
                else
                {
                    _cooldownTimers[key] = remainingTime;
                }
            }

            // 冷却中的视觉倒计时由 HTML 本地完成；C# 只在冷却真正结束时再同步一次。
            if (anyCooldownFinished)
                NotifyHudTimersChanged();
        }

        private void UpdateGlobalCooldown(float dt)
        {
            if (_globalCooldownTimer > 0)
            {
                //Script.SysOut(_globalCooldownTimer.ToString(),this.AgentInstance);
                _globalCooldownTimer = Math.Max(0f, _globalCooldownTimer - dt);
            }
        }
        /// <summary>
        /// （可选）可视化当前选择法术槽
        /// </summary>
        public void OnFocusTick(float dt)
        {
            if (Agent.IsPlayerControlled)
            {

            }
        }


        //====================== AI逻辑 ======================
        //依次调用所有装备的技能的ai施法检查
        private void HandleAIBehaviorOfTick()
        {
            AggressiveAi.AiDefenseThreatAdjustment.RefreshForCurrentTarget(Agent);
            // 强制移动期间不再运行普通 AI 技能轮询。否则冲刺斩临时接管移动时，
            // 同一 Agent 仍可能通过其他技能槽连续发动技能并打断冲锋。
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement != null && movement.IsRushing(Agent))
                return;

            if (IsSkillReadyForAi(MainActiveSkill) &&
                (MainActiveSkill is Skills.JianQi || MainActiveSkill is Skills.ConeOfArrows ||
                 MainActiveSkill is ZhanYi || MainActiveSkill is JueXing ||
                 MainActiveSkill is TianQi ||
                 MBRandom.RandomFloat > 0.5f) &&
                MainActiveSkill.CheckCondition(Agent))
            {
                TryActivateSkill(MainActiveSkill);
            }
            else if (IsSkillReadyForAi(SubActiveSkill) &&
                     (SubActiveSkill is JiFengLianZhan && StateContainer.HasState("JueXingBuff") ||
                      MBRandom.RandomFloat > 0.5f) &&
                     SubActiveSkill.CheckCondition(Agent))
            {
                TryActivateSkill(SubActiveSkill);
            }
            else if (IsSkillReadyForAi(CombatArtSkill) &&
                     MBRandom.RandomFloat > 0.5f && CombatArtSkill.CheckCondition(Agent))
            {
                TryActivateSkill(CombatArtSkill);
            }
            else if (IsSkillReadyForAi(SpellSlots[0]) &&
                     MBRandom.RandomFloat > 0.5f && SpellSlots[0].CheckCondition(Agent))
            {
                TryActivateSkill(SpellSlots[0]);
            }
            else if (IsSkillReadyForAi(SpellSlots[1]) &&
                     MBRandom.RandomFloat > 0.5f && SpellSlots[1].CheckCondition(Agent))
            {
                TryActivateSkill(SpellSlots[1]);
            }
            else if (IsSkillReadyForAi(SpellSlots[2]) &&
                     MBRandom.RandomFloat > 0.5f && SpellSlots[2].CheckCondition(Agent))
            {
                TryActivateSkill(SpellSlots[2]);
            }
            else if (IsSkillReadyForAi(SpellSlots[3]) &&
                     MBRandom.RandomFloat > 0.5f && SpellSlots[3].CheckCondition(Agent))
            {
                TryActivateSkill(SpellSlots[3]);
            }

        }

        /// <summary>
        /// AI 专用的无日志机械条件预筛选。把冷却、资源和动作判断放在战术条件之前，
        /// 避免技能不可用时仍执行距离、视线或单位搜索。
        /// </summary>
        private bool IsSkillReadyForAi(SkillBase skill)
        {
            if (skill == null || !skill.IsValid || skill.Type == SPSkillType.None ||
                Agent == null || !Agent.IsActive())
                return false;
            // 不以当前攻防动作拦截 NPC 技能候选。
            // if (Agent.IsPerformingAction() && !skill.CanActivateWhilePerformingAction)
            //     return false;

            bool usesMana = skill.Type == SPSkillType.Spell || skill.Type == SPSkillType.Spell_CombatArt;
            SkillActivationPolicy policy = skill.GetActivationPolicy(Agent);
            if ((usesMana ? _currentMana : _currentStamina) < policy.ResourceCost)
                return false;
            if (!policy.IgnoreSkillCooldown &&
                _cooldownTimers.TryGetValue(skill, out float remaining) && remaining > 0f)
                return false;
            return skill.Type != SPSkillType.Spell || _globalCooldownTimer <= 0f;
        }

        /// <summary>
        /// 消费冲刺斩抵达后排队的一次原生右砍请求。该回调进入和普通 AI 攻击相同的
        /// 引擎输入链，因此仍由原生武器扫掠、格挡和伤害系统完成命中判定。
        /// </summary>
        public override void OnAIInputSet(
            ref Agent.EventControlFlag eventFlag,
            ref Agent.MovementControlFlag movementFlag,
            ref Vec2 inputVector)
        {
            RushMovementMissionLogic.Current?.ApplyPendingAiAttack(Agent, ref movementFlag);
            JiFengLianZhanMissionLogic.Current?.ApplyPendingAiAttack(Agent, ref movementFlag);
            AggressiveAi.AiDefenseThreatAdjustment.SuppressDefenseInput(Agent, ref movementFlag);
        }
    }
}
//代码说明
//1. 核心功能
//技能槽管理：严格区分主主动、副主动、被动、法术、战技栏位。

//输入响应：

//E 键触发主主动技能

//左Alt 触发副主动技能

//鼠标滚轮 切换法术槽，右键 施放当前法术

//长按攻击键松开 触发战技

//AI逻辑：简单概率触发主主动技能（可扩展）。

//2. 资源与冷却
//双资源系统：法力（法术/战技）和耐力（主动技能）独立扣除。

//冷却分层：

//单个技能独立冷却

//法术共享全局冷却（GCD）

//3. 错误处理
//空技能检查：SpellSlots 允许空槽（null 值）。

//资源不足保护：使用 Math.Max 确保资源不低于0。

//4. 调试支持
//控制台日志：关键操作（技能触发、被动生效）输出调试信息。

//法术槽提示：玩家聚焦时显示当前选中法术槽。
