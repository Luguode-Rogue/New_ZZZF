using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
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
        public Agent BaseAgent=> base.Agent;//淦，记不住上面哪个名字
        public float MaxHP { get; private set; }
        // 新增状态容器
        public AgentBuffContainer StateContainer { get; } = new AgentBuffContainer();
        //------------------------ 技能槽配置 ------------------------
        public SkillBase MainActiveSkill { get; private set; } = new NullSkill();    // 主主动技能
        public SkillBase SubActiveSkill { get; private set; } = new NullSkill();    // 副主动技能
        public SkillBase PassiveSkill { get; private set; } = new NullSkill(); // 被动栏技能
        public SkillBase[] SpellSlots { get; } = new SkillBase[4] {  new NullSkill() ,  new NullSkill() ,  new NullSkill() ,  new NullSkill()  };// 法术栏（0-3号位）
        public SkillBase CombatArtSkill { get; private set; } = new NullSkill();   // 战技
        private bool CombatArtFlag { get; set; } = false;// 是否处于战技准备状态

        //------------------------ 资源与状态 ------------------------
        public float _currentMana = 100f;       // 当前法力值
        public float _currentStamina = 100f;    // 当前耐力值
        public float _globalCooldownTimer = 0f; // 公共CD计时器（仅法术）
        public bool _isInCombatArtState;        // 是否处于战技准备状态
        public float _shieldStrength = 0f; //护盾值
        public int _lifeResurgenceCount = 0;//剩余复活次数

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
            }
            public void Tick(float dt)
            {
                this.oldPos = this.newPos;
                this.newPos = this.agent.Position;
                this.speed = (this.newPos - this.oldPos) / dt;
            }
        }


        // 冷却计时器（Key: 技能实例, Value: 剩余冷却时间）
        public readonly Dictionary<SkillBase, float> _cooldownTimers = new Dictionary<SkillBase, float>();

        public AgentSkillComponent(Agent agent) : base(agent)
        {
            MaxHP = agent.Health;
            Speed =new AgentSpeed(agent);
        }

        public bool HasSkill(string skill)
        {
            //if (MainActiveSkill==null ) return false;
            //if (MainActiveSkill.SkillID==skill||SubActiveSkill.SkillID==skill||PassiveSkill.SkillID==skill||CombatArtSkill.SkillID==skill
            //    || SpellSlots[0].SkillID == skill || SpellSlots[1].SkillID == skill || SpellSlots[2].SkillID == skill || SpellSlots[3].SkillID == skill)
            //{
            //    return true;
            //}
            return false;
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
                Debug.Print($"[被动] {PassiveSkill.SkillID} 已生效");
            }
        }

        /// <summary>
        /// 手动调用的每帧更新方法（由MissionBehavior驱动）
        /// </summary>
        public void Tick(float dt)
        {
            if (!Agent.IsActive()) return;

            // 玩家控制时处理输入
            if (Agent.IsPlayerControlled)
                HandlePlayerInput(dt);
            else
                HandleAIBehaviorOfTick(dt);
        }
        /// <summary>
        /// 手动调用的每帧更新方法（由MissionBehavior驱动）
        /// </summary>
        public void CoolDownTick(float dt)
        {
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
                    _selectedSpellSlot = (_selectedSpellSlot + 1) % 4;
                    if (SpellSlots[_selectedSpellSlot].SkillID != "NullSkill")
                        Script.SysOut(SpellSlots[_selectedSpellSlot].SkillID, Agent);
                }
                else if (scrollDelta < 0)
                {
                    _selectedSpellSlot = (_selectedSpellSlot - 1 + 4) % 4;
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
            }
        }

        //====================== 法术栏输入处理 ======================
        private int _selectedSpellSlot = 0; // 当前选中的法术栏位（0-3）


        /// <summary>
        /// 尝试激活技能（核心逻辑）
        /// </summary>
        private void TryActivateSkill(SkillBase skill)
        {
            if (skill == null || !CanActivateSkill(skill)) {
                Script.SysOut("条件不满足", Agent);
                return; }

            // 扣除资源// 触发技能效果
            if (skill.Activate(Agent))
            {
                if (skill.Type == SkillType.Spell|| skill.Type == SkillType.Spell_CombatArt)
                {
                    Agent.SetActionChannel(1, ActionIndexCache.Create("act_horse_command_follow"), false, 172UL, 0, 1.5f, -0.2f, 0.4f, 0.5f);
                    _currentMana = Math.Max(0, _currentMana - skill.ResourceCost);
                }
                else
                {
                    _currentStamina = Math.Max(0, _currentStamina - skill.ResourceCost);
                }
                _cooldownTimers[skill] = skill.Cooldown;


                // 触发公共CD（仅法术）
                if (skill.Type == SkillType.Spell|| skill.Type == SkillType.Spell_CombatArt)
                    _globalCooldownTimer += 1.0f; // 公共CD设为1秒

                Console.WriteLine($"[技能触发] {skill.SkillID} 剩余法力: {_currentMana}, 耐力: {_currentStamina}");
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
        private bool CanActivateSkill(SkillBase skill)
        {
            // 基础检查
            if (skill == null) return false;
            if (Agent.IsPerformingAction()) return false; // 角色正忙

            // 资源检查
            bool hasResource = (skill.Type == SkillType.Spell || skill.Type == SkillType.Spell_CombatArt) ?
                _currentMana >= skill.ResourceCost :
                _currentStamina >= skill.ResourceCost;

            // 冷却检查
            bool isOnCooldown = _cooldownTimers.TryGetValue(skill, out float remaining) && remaining > 0;
            bool isGCDBlocked = (skill.Type == SkillType.Spell) && _globalCooldownTimer > 0;
            if (!hasResource) { Script.SysOut("耐力或魔法不足",Agent); }
            if (isOnCooldown) { Script.SysOut("技能未冷却", Agent); }
            if (isGCDBlocked) { Script.SysOut("法术公共冷却未结束", Agent); }
            return hasResource && !isOnCooldown && !isGCDBlocked;
        }

        //====================== 状态更新 ======================
        /// <summary>
        /// 如果参数二为none类型，则更新全部技能cd，否则只更新对应类型技能的cd
        /// </summary>
        /// <param name="dt"></param>
        /// <param name="skillType"></param>
        public void UpdateCooldowns(float dt, SkillType skillType= SkillType.None)
        {
            List<SkillBase> expiredSkills = new List<SkillBase>();
            // 使用临时变量来存储剩余时间，避免直接修改字典
            var timersToUpdate = new Dictionary<SkillBase, float>(_cooldownTimers);

            foreach (var entry in timersToUpdate)
            {
                bool matchedType = ((entry.Key.Type == SkillType.Spell || entry.Key.Type == SkillType.Spell_CombatArt)&& skillType== SkillType.Spell) ||
                    (entry.Key.Type == SkillType.MainActive && skillType == SkillType.MainActive) ||
                    (entry.Key.Type == SkillType.SubActive && skillType == SkillType.SubActive) ||
                    ((entry.Key.Type == SkillType.Passive|| entry.Key.Type == SkillType.Passive_Spell) && skillType == SkillType.Passive);
                if (skillType== SkillType.None|| matchedType)
                {
                    float remainingTime = entry.Value - dt;
                    if (remainingTime <= 0)
                    {
                        expiredSkills.Add(entry.Key);
                        _cooldownTimers.Remove(entry.Key); // 直接在这里移除，因为我们使用的是副本
                    }
                    else
                    {
                        _cooldownTimers[entry.Key] = remainingTime; // 更新剩余时间
                    }
                }

            }
        }

        private void UpdateGlobalCooldown(float dt)
        {
            if (_globalCooldownTimer > 0)
            {
                //Script.SysOut(_globalCooldownTimer.ToString(),this.AgentInstance);
                _globalCooldownTimer -= dt;
            }
        }
        /// <summary>
        /// （可选）可视化当前选择法术槽
        /// </summary>
        public  void OnFocusTick(float dt)
        {
            if (Agent.IsPlayerControlled)
            {

            }
        }


        //====================== AI逻辑 ======================
        //依次调用所有装备的技能的ai施法检查
        private void HandleAIBehaviorOfTick(float dt)
        {
            Random random = new Random();
            if (MainActiveSkill.CheckCondition(Agent)&& random.NextFloat()>0.5f)
            {
                TryActivateSkill(MainActiveSkill);
            }
            else if (SubActiveSkill.CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(SubActiveSkill);
            }
            else if (CombatArtSkill.CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(CombatArtSkill);
            }
            else if (SpellSlots[0].CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(SpellSlots[0]);
            }
            else if (SpellSlots[1].CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(SpellSlots[1]);
            }
            else if (SpellSlots[2].CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(SpellSlots[2]);
            }
            else if (SpellSlots[3].CheckCondition(Agent) && random.NextFloat() > 0.5f)
            {
                TryActivateSkill(SpellSlots[3]);
            }
            
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