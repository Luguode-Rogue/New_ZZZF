﻿using New_ZZZF;
using New_ZZZF.Skills;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 技能工厂类 - 将技能ID映射到代码中定义的技能实例
    /// </summary>
    public static class SkillFactory
    {
        // 技能注册表：技能ID -> 技能实例（硬编码参数）
        public static readonly Dictionary<string, SkillBase> _skillRegistry = new Dictionary<string, SkillBase>
        {
            //// 主主动技能
            //{ "ShieldCharge", new ShieldChargeSkill() },       // 盾牌冲锋
            //{ "BladeStorm", new BladeStormSkill() },           // 剑刃风暴
            
            //// 副主动技能
            //{ "QuickDash", new QuickDashSkill() },             // 快速闪避
            //{ "AdrenalineRush", new AdrenalineRushSkill() },   // 肾上腺素爆发
            
            //// 被动技能
            //{ "IronWill", new IronWillPassive() },             // 钢铁意志（减伤）
            //{ "CriticalStrike", new CriticalStrikePassive() }, // 暴击被动
            
            //// 法术
            { "{=12345678}Fireball", new FireballSkill() },               // 火球术
            //{ "Heal", new HealSkill() },                       // 治疗术
            { "{=12345677}HuiJianYuanZhen", new HuiJianYuanZhen() },               // 辉剑圆阵

            
            //// 战技
            //{ "ShieldBash", new ShieldBashSkill() },           // 盾牌猛击
            { "{=12345676}JianQi", new JianQi() }            // 剑气斩

        };
        public static void SkillToItemObject()
        {
            
            foreach (KeyValuePair<string, SkillBase> kvp in _skillRegistry)
            {
                ItemObject io=  Game.Current.ObjectManager.RegisterPresumedObject<ItemObject>(new ItemObject(kvp.Key));
                ItemObject.InitializeTradeGood( io, new TextObject(kvp.Key, null), "lib_book_open_a", DefaultItemCategories.Unassigned,60,1f, ItemObject.ItemTypeEnum.Book);
                //// 使用反射设置 Difficulty 属性（核心部分）
                //PropertyInfo difficultyProp = typeof(ItemObject).GetProperty(
                //    "Difficulty",
                //    BindingFlags.Public | BindingFlags.Instance
                //);

                //// 设置新值（这里示例设置为100，可替换为实际需要的值）
                //difficultyProp.SetValue(io, 20);
            }
        }
        /// <summary>
        /// 根据技能ID创建技能实例
        /// </summary>
        /// <param name="skillID">配置文件中定义的技能ID</param>
        /// <returns>返回对应的技能实例，若未找到则返回NullSkill</returns>
        public static SkillBase Create(string skillID)
        {
            // 处理空值或空白ID
            if (string.IsNullOrWhiteSpace(skillID))
            {
                Debug.Print("[SkillFactory] 警告：传入空技能ID");
                return new NullSkill();
            }

            // 查找技能
            if (_skillRegistry.TryGetValue(skillID, out SkillBase skill))
            {
                return skill;
            }

            // 处理未知技能ID
            Debug.Print($"[SkillFactory] 错误：未注册的技能ID '{skillID}'");
            return new NullSkill();
        }

        /// <summary>
        /// 空技能占位类（防止因配置错误导致崩溃）
        /// </summary>
        private class NullSkill : SkillBase
        {
            public NullSkill()
            {
                SkillID = "NullSkill";
                Type = SkillType.Passive; // 设为被动避免意外触发
                Cooldown = 0;
                ResourceCost = 0;
            }

            public override bool Activate(Agent agent)
            {
                return false;
                // 空实现
            }
        }
    }
}
//代码说明
//1. 核心机制
//静态注册表：通过 _skillRegistry 字典预注册所有技能实例，键为技能ID。

//大小写不敏感：使用 StringComparer.OrdinalIgnoreCase 忽略ID大小写差异。

//错误兜底：未知ID返回 NullSkill 防止崩溃，同时输出醒目错误提示。

//2. 扩展性设计
//动态注册：通过 RegisterSkill 方法支持开发期动态添加技能（如热重载）。

//模块化实现：每个技能独立成类，通过构造函数硬编码参数（符合你的需求）。

//3. 配套技能实现
//被动技能：通过 OnEquip 方法在装备时永久生效（如 IronWillPassive 增加血量）。

//主动技能：在 Activate 中实现瞬时或持续效果（如 ShieldChargeSkill 加速）。






//最佳实践建议
//技能分类存放：
//在 Skills/ 文件夹下按类型组织：

//复制
//Skills/
//├── Actives/       // 主动技能
//│   ├── ShieldChargeSkill.cs
//│   └── ...
//├── Passives/      // 被动技能
//│   ├── IronWillPassive.cs
//│   └── ...
//└── Spells/        // 法术技能
//    ├── FireballSkill.cs
//    └── ...
//技能参数模板：
//为每类技能创建基类（如 SpellBase : SkillBase），统一处理公共逻辑（如法力消耗）。

//版本兼容标记：
//在技能类中添加版本属性：

//csharp
//复制
//[AttributeUsage(AttributeTargets.Class)]
//public class SkillVersionAttribute : Attribute
//{
//    public string Version { get; }
//    public SkillVersionAttribute(string version) => Version = version;
//}

//// 使用示例
//[SkillVersion("1.1")]
//public class FireballSkill : SkillBase { ... }
