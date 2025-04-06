using New_ZZZF;
using New_ZZZF.Skills;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using static New_ZZZF.YingXiongZhuFu;

namespace New_ZZZF
{
    /// <summary>
    /// 技能工厂类 - 将技能ID映射到代码中定义的技能实例
    /// </summary>
    public static class SkillFactory
    {
        // 技能注册表：技能ID -> 技能实例（硬编码参数）
        public static readonly Dictionary<string, SkillBase> _skillRegistry = Refresh_skillRegistry();
        public static Dictionary<string, SkillBase> Refresh_skillRegistry()
        {
           return new Dictionary<string, SkillBase>
            {
                //空技能
                {"NullSkill",new NullSkill() },
                //// 主主动技能
                { "JianQi", new JianQi() }  ,          // 剑气斩
                { "ConeOfArrows", new ConeOfArrows() }   ,         // 多重射击
                { "DaoShan", new DaoShan() }   ,         // 刀扇
                {"ShadowStep",new ShadowStep() },//暗影步
                {"ZhanYi",new ZhanYi() },
                {"JueXing",new JueXing() },
                {"TianQi",new TianQi() },
                {"GuWu",new GuWu() },
                {"ChaoFeng",new ChaoFeng() },
                {"DunWu",new DunWu() },
                {"FengBaoZhiLi",new FengBaoZhiLi() },
                {"ZhanHao",new ZhanHao() },
                {"WeiYa",new WeiYa() },
                {"JingXia",new JingXia() },
                {"YingXiongZhuFu",new YingXiongZhuFu() },
                {"KongNueCiFu",new KongNueCiFu() },
                {"NaGouCiFu",new NaGouCiFu() },
                {"JianQiCiFu",new JianQiCiFu() },
                {"HuHuanFengBao",new HuHuanFengBao() },
                {"ZhaoHuan",new ZhaoHuan半成品() },
                {"HuoYanTuXi",new HuoYanTuXi() },
                {"XieEZuZhou",new XieEZuZhou() },
                {"DaDiJianTa",new DaDiJianTa() },
                {"XuRuoZuZhou",new XuRuoZuZhou() },
                {"HuoLiZaiSheng",new HuoLiZaiSheng() },
                {"JianRenBuQu",new JianRenBuQu() },
                {"KuangNuLongXi",new KuangNuLongXi() },
                {"LingHunDanMu",new LingHunDanMu() },
                {"JianRenLuanWu",new JianRenLuanWu() },
                {"BKB",new BKB() },
                {"ZhenYinZhan",new ZhenYinZhan() },
                {"TianFaZhiJian",new TianFaZhiJian() },
            
                //// 副主动技能
                { "BaseZhanJi", new BaseZhanJi() },
                { "Rush", new Rush() },             // Rush
                {"MagicShoot",new MagicShoot() },

                {"HouYueSheJi",new HouYueSheJi() },
                {"Roll",new Roll() },
                //// 被动技能

                {"ShengZhuangWuBu",new ShengZhuangWuBu() },
                //// 法术
                { "Fireball", new FireballSkill() },               // 火球术
                { "lingmashaodi", new lingmashaodi() },               // 
                { "HuiJianYuanZhen", new HuiJianYuanZhen() },               // 辉剑圆阵
            
                {"LeiJi",new LeiJi() },
            
                //// 战技
                //{ "ShieldBash", new ShieldBashSkill() }, 
                { "HongShiZiHuoYan", new HongShiZiHuoYan() },           // 喷火

            };
        }
        /// <summary>
        /// 根据技能列表，将技能转化为物品对象，方便选择技能界面使用
        /// 调用时间：游戏开始和游戏读取
        /// </summary>
        public static void SkillToItemObject()
        {
            if (Game.Current.GameType is Campaign)
            {
                foreach (KeyValuePair<string, SkillBase> kvp in _skillRegistry)
                {
                    ItemObject io = Game.Current.ObjectManager.RegisterPresumedObject<ItemObject>(new ItemObject(kvp.Key));
                    ItemObject.InitializeTradeGood(io, kvp.Value.Text, "lib_book_open_a", DefaultItemCategories.Unassigned, 60, 1f, ItemObject.ItemTypeEnum.Book);
                    kvp.Value.Item = io;


                }
            }
        }
        /// <summary>
        /// 根据技能ID创建技能实例
        /// </summary>
        /// <param name="skillID">配置文件中定义的技能ID</param>
        /// <returns>返回对应的技能实例，若未找到则返回NullSkill</returns>
        public static SkillBase Create(string skillID)
        {
            if (true||Game.Current.GameType is Campaign)
            {
                // 处理空值或空白ID
                if (string.IsNullOrWhiteSpace(skillID))
                {
                    Debug.Print("[SkillFactory] 警告：传入空技能ID");
                    SkillBase skillBase = new NullSkill();
                    if (Game.Current.GameType is Campaign)
                    {
                        ItemObject io = Game.Current.ObjectManager.RegisterPresumedObject<ItemObject>(new ItemObject("NullSkill"));
                        ItemObject.InitializeTradeGood(io, new TextObject("NullSkill", null), "lib_book_open_a", DefaultItemCategories.Unassigned, 30, 1f, ItemObject.ItemTypeEnum.Book);
                        skillBase.Item = io;
                    }
                    return skillBase;
                }

                // 查找技能
                if (_skillRegistry.TryGetValue(skillID, out SkillBase skill))
                {
                    if (Game.Current.GameType is Campaign)
                    {
                        ItemObject io = Game.Current.ObjectManager.RegisterPresumedObject<ItemObject>(new ItemObject(skillID));
                        ItemObject.InitializeTradeGood(io, skill.Text, "lib_book_open_a", DefaultItemCategories.Unassigned, 30, 1f, ItemObject.ItemTypeEnum.Book);
                        skill.Item = io;
                    }
                    return skill;
                }

                // 处理未知技能ID
                Debug.Print($"[SkillFactory] 错误：未注册的技能ID '{skillID}'");
                SkillBase skillBase2 = new NullSkill();
                if (Game.Current.GameType is Campaign)
                {
                    ItemObject io2 = Game.Current.ObjectManager.RegisterPresumedObject<ItemObject>(new ItemObject("NullSkill"));
                    ItemObject.InitializeTradeGood(io2, new TextObject("NullSkill", null), "lib_book_open_a", DefaultItemCategories.Unassigned, 30, 1f, ItemObject.ItemTypeEnum.Book);
                    skillBase2.Item = io2;
                }
                return skillBase2;
            }
            return null;
        }



        /// <summary>
        /// 空技能占位类（防止因配置错误导致崩溃）
        /// </summary>
        public class NullSkill : SkillBase
        {
            public NullSkill()
            {
                SkillID = "NullSkill";
                Type = SkillType.None; // 设为被动避免意外触发
                Cooldown = 0;
                ResourceCost = 0;
                Text = new TaleWorlds.Localization.TextObject("{=ZZZF0000}Wu");
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
