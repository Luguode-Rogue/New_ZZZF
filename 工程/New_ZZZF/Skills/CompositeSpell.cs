using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    // =========================================================================
    // 法术锻造 —— 静态数据层（节点 / 组件 / 官方法术 / 官方组合）
    //
    // 说明：本项目以代码驱动为准（无需依赖外部 XML 即可运行），
    // 下列 SpellForgeData 是 UI（SpellForgeVM / SpellForgeScreen）唯一的数据来源。
    // 同时在本文件底部导出与 XML（Spell_Components.xml / Spell_Component_Combinations.xml /
    // Composite_Spells.xml）对等的字段，方便后续人工校订。
    // =========================================================================
    public static class SpellForgeData
    {
        private static bool _initialized = false;

        /// <summary>节点（锻造台上的可放置格子，决定可嵌入组件数量与类型）</summary>
        public static List<ForgeNodeDef> Nodes { get; private set; } = new List<ForgeNodeDef>();

        /// <summary>组件（注入到节点中的效果模块）</summary>
        public static List<ForgeComponentDef> Components { get; private set; } = new List<ForgeComponentDef>();

        /// <summary>官方法术（仍可作为"底料"参与锻造，作为子法术节点）</summary>
        public static List<ForgeOfficialSpellDef> OfficialSpells { get; private set; } = new List<ForgeOfficialSpellDef>();

        /// <summary>官方法术组合（预设的、可直接取用的配方）</summary>
        public static List<ForgeCombinationDef> Combinations { get; private set; } = new List<ForgeCombinationDef>();

        // ---------------------------------------------------------------------
        // 初始化（幂等）。在游戏加载或首开锻造台时调用一次。
        // ---------------------------------------------------------------------
        public static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            RegisterNodes();
            RegisterComponents();
            RegisterOfficialSpells();
            RegisterCombinations();
        }

        // ---------------------------------------------------------------------
        // 节点库
        // ---------------------------------------------------------------------
        private static void RegisterNodes()
        {
            Nodes.Add(new ForgeNodeDef
            {
                NodeId = "core_node",
                Name = new TextObject("{=ZZZF_SF_N1}核心节点"),
                Description = new TextObject("{=ZZZF_SF_D1}法术的根节点，承载主元素与基础伤害。"),
                MaxComponents = 3,
                AllowedComponentCategories = new List<string> { "element", "damage", "augment" },
                Icon = "forge_node_core"
            });
            Nodes.Add(new ForgeNodeDef
            {
                NodeId = "trigger_node",
                Name = new TextObject("{=ZZZF_SF_N2}触发器节点"),
                Description = new TextObject("{=ZZZF_SF_D2}决定法术的触发方式（弹道 / 范围 / 即时）。"),
                MaxComponents = 2,
                AllowedComponentCategories = new List<string> { "trigger", "augment" },
                Icon = "forge_node_trigger"
            });
            Nodes.Add(new ForgeNodeDef
            {
                NodeId = "effect_node",
                Name = new TextObject("{=ZZZF_SF_N3}效果节点"),
                Description = new TextObject("{=ZZZF_SF_D3}附加持续效果、控制与增益。"),
                MaxComponents = 3,
                AllowedComponentCategories = new List<string> { "status", "control", "buff", "augment" },
                Icon = "forge_node_effect"
            });
            Nodes.Add(new ForgeNodeDef
            {
                NodeId = "modifier_node",
                Name = new TextObject("{=ZZZF_SF_N4}修正节点"),
                Description = new TextObject("{=ZZZF_SF_D4}强化、削弱或改变法术形态（散射 / 追踪 / 穿透）。"),
                MaxComponents = 2,
                AllowedComponentCategories = new List<string> { "augment", "modifier" },
                Icon = "forge_node_modifier"
            });
        }

        // ---------------------------------------------------------------------
        // 组件库
        // ---------------------------------------------------------------------
        private static void RegisterComponents()
        {
            // —— 元素（element）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "elm_fire",
                Name = new TextObject("{=ZZZF_SF_C1}火焰元素"),
                Description = new TextObject("{=ZZZF_SF_CD1}命中附加燃烧 DOT。"),
                Categories = new List<string> { "element" },
                Icon = "forge_elm_fire",
                EffectType = "fire"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "elm_ice",
                Name = new TextObject("{=ZZZF_SF_C2}冰霜元素"),
                Description = new TextObject("{=ZZZF_SF_CD2}命中附加减速。"),
                Categories = new List<string> { "element" },
                Icon = "forge_elm_ice",
                EffectType = "ice"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "elm_elec",
                Name = new TextObject("{=ZZZF_SF_C3}雷电元素"),
                Description = new TextObject("{=ZZZF_SF_CD3}命中附加麻痹。"),
                Categories = new List<string> { "element" },
                Icon = "forge_elm_elec",
                EffectType = "electricity"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "elm_toxin",
                Name = new TextObject("{=ZZZF_SF_C4}毒素元素"),
                Description = new TextObject("{=ZZZF_SF_CD4}命中附加中毒 DOT。"),
                Categories = new List<string> { "element" },
                Icon = "forge_elm_toxin",
                EffectType = "toxin"
            });

            // —— 伤害（damage）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "dmg_burst",
                Name = new TextObject("{=ZZZF_SF_C5}爆发伤害"),
                Description = new TextObject("{=ZZZF_SF_CD5}提升基础伤害 50%。"),
                Categories = new List<string> { "damage" },
                Icon = "forge_dmg_burst",
                ParamValue = 0.5f
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "dmg_pierce",
                Name = new TextObject("{=ZZZF_SF_C6}穿透"),
                Description = new TextObject("{=ZZZF_SF_CD6}命中可贯穿至后方目标。"),
                Categories = new List<string> { "damage", "modifier" },
                Icon = "forge_dmg_pierce"
            });

            // —— 触发器（trigger）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "trig_projectile",
                Name = new TextObject("{=ZZZF_SF_C7}弹道触发"),
                Description = new TextObject("{=ZZZF_SF_CD7}以制导飞弹形式发射法术。"),
                Categories = new List<string> { "trigger" },
                Icon = "forge_trig_projectile",
                EffectType = "projectile"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "trig_aoe",
                Name = new TextObject("{=ZZZF_SF_C8}范围触发"),
                Description = new TextObject("{=ZZZF_SF_CD8}在落点生成范围效果。"),
                Categories = new List<string> { "trigger" },
                Icon = "forge_trig_aoe",
                EffectType = "aoe"
            });

            // —— 状态（status）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "st_burn",
                Name = new TextObject("{=ZZZF_SF_C9}点燃"),
                Description = new TextObject("{=ZZZF_SF_CD9}施加燃烧状态。"),
                Categories = new List<string> { "status" },
                Icon = "forge_st_burn",
                EffectType = "fire"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "st_freeze",
                Name = new TextObject("{=ZZZF_SF_C10}冰冻"),
                Description = new TextObject("{=ZZZF_SF_CD10}施加减速冻结。"),
                Categories = new List<string> { "status", "control" },
                Icon = "forge_st_freeze",
                EffectType = "ice"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "st_poison",
                Name = new TextObject("{=ZZZF_SF_C11}中毒"),
                Description = new TextObject("{=ZZZF_SF_CD11}施加中毒状态。"),
                Categories = new List<string> { "status" },
                Icon = "forge_st_poison",
                EffectType = "toxin"
            });

            // —— 控制（control）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "ctrl_stun",
                Name = new TextObject("{=ZZZF_SF_C12}眩晕"),
                Description = new TextObject("{=ZZZF_SF_CD12}短时间使目标无法行动。"),
                Categories = new List<string> { "control" },
                Icon = "forge_ctrl_stun",
                ParamValue = 1.5f
            });

            // —— 增益（buff，对友方）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "buff_shield",
                Name = new TextObject("{=ZZZF_SF_C13}护盾"),
                Description = new TextObject("{=ZZZF_SF_CD13}对友方施放时提供护盾。"),
                Categories = new List<string> { "buff" },
                Icon = "forge_buff_shield",
                EffectType = "shield"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "buff_heal",
                Name = new TextObject("{=ZZZF_SF_C14}治疗"),
                Description = new TextObject("{=ZZZF_SF_CD14}对友方施放时回复生命。"),
                Categories = new List<string> { "buff" },
                Icon = "forge_buff_heal",
                EffectType = "heal"
            });

            // —— 强化（augment）——
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "aug_multishot",
                Name = new TextObject("{=ZZZF_SF_C15}多重发射"),
                Description = new TextObject("{=ZZZF_SF_CD15}同时发射多枚弹道（散射）。"),
                Categories = new List<string> { "augment" },
                Icon = "forge_aug_multishot",
                ParamValue = 3f
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "aug_homing",
                Name = new TextObject("{=ZZZF_SF_C16}追踪"),
                Description = new TextObject("{=ZZZF_SF_CD16}弹道自动追踪最近敌人。"),
                Categories = new List<string> { "augment", "modifier" },
                Icon = "forge_aug_homing"
            });
            Components.Add(new ForgeComponentDef
            {
                ComponentId = "aug_cooldown",
                Name = new TextObject("{=ZZZF_SF_C17}急速"),
                Description = new TextObject("{=ZZZF_SF_CD17}降低冷却 30%。"),
                Categories = new List<string> { "augment" },
                Icon = "forge_aug_cooldown",
                ParamValue = 0.3f
            });
        }

        // ---------------------------------------------------------------------
        // 官方法术（作为子法术"底料"）
        // ---------------------------------------------------------------------
        private static void RegisterOfficialSpells()
        {
            // 仅登记已存在于 SkillFactory 的、适合作为组合原料的法术
            string[] ids = new[] { "Fireball", "lingmashaodi", "HuiJianYuanZhen", "LeiJi", "HongShiZiHuoYan", "HuoYanTuXi" };
            foreach (var id in ids)
            {
                var sk = SkillFactory._skillRegistry.TryGetValue(id, out var s) ? s : null;
                if (sk == null) continue;
                OfficialSpells.Add(new ForgeOfficialSpellDef
                {
                    SpellId = id,
                    Name = sk.Text ?? new TextObject(id),
                    Description = sk.Description ?? new TextObject(""),
                    SourceType = sk.Type.ToString(),
                    Icon = "lib_book_open_a"
                });
            }
        }

        // ---------------------------------------------------------------------
        // 官方法术组合（预设配方）
        // ---------------------------------------------------------------------
        private static void RegisterCombinations()
        {
            Combinations.Add(new ForgeCombinationDef
            {
                CombinationId = "combo_fireball",
                Name = new TextObject("{=ZZZF_SF_B1}烈焰火球"),
                Description = new TextObject("{=ZZZF_SF_BD1}火球 + 火焰元素 + 爆发伤害。"),
                RequiredComponents = new List<string> { "elm_fire", "dmg_burst" },
                BaseSkillId = "Fireball"
            });
            Combinations.Add(new ForgeCombinationDef
            {
                CombinationId = "combo_frostnova",
                Name = new TextObject("{=ZZZF_SF_B2}冰霜新星"),
                Description = new TextObject("{=ZZZF_SF_BD2}冰霜元素 + 范围触发 + 冰冻。"),
                RequiredComponents = new List<string> { "elm_ice", "trig_aoe", "st_freeze" },
                BaseSkillId = "lingmashaodi"
            });
            Combinations.Add(new ForgeCombinationDef
            {
                CombinationId = "combo_storm",
                Name = new TextObject("{=ZZZF_SF_B3}雷暴弹幕"),
                Description = new TextObject("{=ZZZF_SF_BD3}雷电元素 + 多重发射 + 追踪。"),
                RequiredComponents = new List<string> { "elm_elec", "aug_multishot", "aug_homing" },
                BaseSkillId = "LeiJi"
            });
        }

        // ---------------------------------------------------------------------
        // 静态查询辅助
        // ---------------------------------------------------------------------
        public static ForgeNodeDef GetNode(string nodeId) => Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        public static ForgeComponentDef GetComponent(string compId) => Components.FirstOrDefault(c => c.ComponentId == compId);
        public static ForgeCombinationDef GetCombination(string comboId) => Combinations.FirstOrDefault(c => c.CombinationId == comboId);
    }

    // 节点定义
    public class ForgeNodeDef
    {
        public string NodeId;
        public TextObject Name;
        public TextObject Description;
        public int MaxComponents;
        public List<string> AllowedComponentCategories;
        public string Icon;
    }

    // 组件定义
    public class ForgeComponentDef
    {
        public string ComponentId;
        public TextObject Name;
        public TextObject Description;
        public List<string> Categories;
        public string Icon;
        public string EffectType;  // fire/ice/electricity/toxin/projectile/aoe/shield/heal/null
        public float ParamValue;  // 伤害倍率 / 持续时间 / 数量等
    }

    // 官方法术定义
    public class ForgeOfficialSpellDef
    {
        public string SpellId;
        public TextObject Name;
        public TextObject Description;
        public string SourceType;
        public string Icon;
    }

    // 官方法术组合定义
    public class ForgeCombinationDef
    {
        public string CombinationId;
        public TextObject Name;
        public TextObject Description;
        public List<string> RequiredComponents;
        public string BaseSkillId;
    }

    // =========================================================================
    // 组合法术（CompositeSpell）
    //
    // 设计（以旧系统为准）：
    //   Activate 时，向视线方向发射制导弹道（注册到 WoW_ProjectileDB），
    //   命中时由系统回调 GameEntityDamage，在其中：
    //     1) 对敌方：把各子法术的 OnHit / OnEquip 等效行为注入（直接顺序 Activate 子法术作用于目标），
    //        并按组件决定元素/状态/伤害倍率/穿透/散射。
    //     2) 对友方：若有 buff 组件（护盾/治疗），改为施加增益而非伤害。
    //     3) 击杀特殊规则：若本次命中造成目标死亡，则触发"反弹"（对周围敌人追加一次伤害）。
    //        若未造成死亡，则按组件决定是否"穿透"到下一个目标。
    // =========================================================================
    public class CompositeSpell : SkillBase
    {
        // 合成后的子法术列表（来自节点库 + 官方法术原料）
        public List<SkillBase> componentSpells = new List<SkillBase>();

        // 组件效果汇总（由 SpellForgeVM 写入）
        public List<string> ComponentIds = new List<string>();

        // 派生参数
        public bool HasProjectile { get; private set; } = true;
        public bool HasAoe { get; private set; } = false;
        public bool Homing { get; private set; } = false;
        public bool Pierce { get; private set; } = false;
        public int Multishot { get; private set; } = 1;
        public float DamageMul { get; private set; } = 1f;
        public float CooldownMul { get; private set; } = 1f;
        public string PrimaryElement { get; private set; } = "none";
        public bool HasShield { get; private set; } = false;
        public bool HasHeal { get; private set; } = false;

        public CompositeSpell()
        {
            SkillID = "CompositeSpell";
            Type = SPSkillType.Spell;
            Cooldown = 5f;
            ResourceCost = 0f;
            Text = new TextObject("{=ZZZF_SF_COMP}组合法术");
            Description = new TextObject("{=ZZZF_SF_COMPD}由法术锻造台合成的法术。");
        }

        /// <summary>
        /// 设定本法术的唯一ID。
        /// 必须与 SkillFactory._skillRegistry 的 key 保持一致：
        /// 存档只写 SkillID，读档时拿它回注册表查，不一致就会退化成 NullSkill。
        /// </summary>
        public void SetSkillId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) SkillID = id;
        }

        /// <summary>由 SpellForgeVM 在合成后调用，解析组件并刷新派生参数。</summary>
        public void ApplyComponentConfiguration(List<string> componentIds)
        {
            ComponentIds = new List<string>(componentIds ?? new List<string>());
            RecomputeDerived();
        }

        private void RecomputeDerived()
        {
            HasProjectile = ComponentIds.Contains("trig_projectile") || !ComponentIds.Contains("trig_aoe");
            HasAoe = ComponentIds.Contains("trig_aoe");
            Homing = ComponentIds.Contains("aug_homing");
            Pierce = ComponentIds.Contains("dmg_pierce");
            Multishot = ComponentIds.Contains("aug_multishot") ? 3 : 1;
            DamageMul = 1f;
            CooldownMul = 1f;
            PrimaryElement = "none";
            HasShield = ComponentIds.Contains("buff_shield");
            HasHeal = ComponentIds.Contains("buff_heal");

            foreach (var id in ComponentIds)
            {
                var c = SpellForgeData.GetComponent(id);
                if (c == null) continue;
                switch (c.EffectType)
                {
                    case "fire": if (PrimaryElement == "none") PrimaryElement = "fire"; break;
                    case "ice": if (PrimaryElement == "none") PrimaryElement = "ice"; break;
                    case "electricity": if (PrimaryElement == "none") PrimaryElement = "electricity"; break;
                    case "toxin": if (PrimaryElement == "none") PrimaryElement = "toxin"; break;
                }
                if (id == "dmg_burst") DamageMul += c.ParamValue;
                if (id == "aug_cooldown") CooldownMul *= (1f - c.ParamValue);
            }
            if (componentSpells.Count == 0)
            {
                // 没有子法术原料时，至少保有一个基础 Fireball 行为
                var baseSkill = SkillFactory.Create("Fireball");
                if (baseSkill != null) componentSpells.Add(baseSkill);
            }
        }

        /// <summary>组合法术"有效"条件：至少存在一个子法术或组件。</summary>
        public override bool IsValid => (componentSpells != null && componentSpells.Count > 0) || (ComponentIds != null && ComponentIds.Count > 0);

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster)) return false;
            return IsValid;
        }

        public override bool Activate(Agent casterAgent)
        {
            if (!CheckCondition(casterAgent)) return false;
            SpellForgeData.EnsureInitialized();

            // 以旧系统为准（对齐 JianQi.Activate 的可用写法）：发射制导弹道。
            int shots = Math.Max(1, Multishot);
            for (int i = 0; i < shots; i++)
            {
                // 用 LookFrame 取完整三维朝向。
                // 注意：不能用 LookDirection.AsVec2.ToVec3()，那会丢掉俯仰(Z)分量，
                // 导致弹道永远水平飞出，抬头/低头瞄准全部失效。
                MatrixFrame frame = casterAgent.LookFrame;
                frame.origin = casterAgent.GetEyeGlobalPosition();

                Vec3 origin = frame.origin;
                Vec3 dir = frame.rotation.f.NormalizedCopy();
                if (shots > 1)
                {
                    // 散射：在水平面上均匀偏转
                    float spread = (i - (shots - 1) / 2f) * 0.12f;
                    dir.RotateAboutZ(spread);
                    dir = dir.NormalizedCopy();
                }

                GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                // 必须挂一个可见网格：空实体没有包围盒，既看不见也可能不参与场景更新
                try
                {
                    projectile.AddAllMeshesOfGameEntity(
                        GameEntity.Instantiate(Mission.Current.Scene, "weapon_heap_sword_a", true));
                }
                catch { }
                projectile.SetGlobalFrame(new MatrixFrame(frame.rotation, origin));
                var projData = new ProjectileData
                {
                    Name = SkillID,
                    skillBase = this,
                    CasterAgent = casterAgent,
                    TargetPos = origin + dir * 30f,
                    SpawnTime = Mission.Current.CurrentTime,
                    Lifetime = 7f,
                    IsHoming = Homing,
                    BaseColor = PrimaryElement switch
                    {
                        "fire" => new Vec3(1f, 0.4f, 0.1f),
                        "ice" => new Vec3(0.5f, 0.8f, 1f),
                        "electricity" => new Vec3(1f, 1f, 0.3f),
                        "toxin" => new Vec3(0.5f, 1f, 0.4f),
                        _ => new Vec3(1f, 1f, 1f)
                    }
                };
                SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, projData);
            }

            // AoE 形态：在落点直接生成范围效果（不依赖弹道命中）
            if (HasAoe && !HasProjectile)
            {
                MatrixFrame aoeFrame = casterAgent.LookFrame;
                Vec3 aoeDir = aoeFrame.rotation.f.NormalizedCopy();
                ApplyAoe(casterAgent, casterAgent.GetEyeGlobalPosition() + aoeDir * 8f);
            }

            return true;
        }

        /// <summary>系统每帧回调：弹道命中判定。</summary>
        public override void GameEntityDamage(GameEntity missileEntity)
        {
            if (!SkillSystemBehavior.WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                return;
            Agent caster = data.CasterAgent;
            Vec3 pos = missileEntity.GlobalPosition;

            // 命中检测：围绕弹道当前位置检索 Agents。
            // 注意 FindAgentsWithinSpellRange 的半径参数是 int：
            // 原来写 (int)1.5f 会被截断成 1 米，而弹道每帧位移远大于 1 米，
            // 实际上几乎永远检测不到目标（表现为“法术打出去没有任何效果”）。
            // 这里对齐可用的旧法术 JianQi（半径 2）。
            List<Agent> nearby = Script.FindAgentsWithinSpellRange(pos, HasAoe ? 4 : 2);
            if (nearby.Count == 0) return;

            // 排除施法者自身，避免刚出膛就打到自己
            nearby.RemoveAll(a => a == caster);
            if (nearby.Count == 0) return;

            Script.AgentListIFF(caster, nearby, out var friends, out var foes);

            // AoE：同时影响敌我
            if (HasAoe)
            {
                ApplyAoe(caster, pos);
                return;
            }

            if (foes.Count > 0)
            {
                // 对敌：转发子法术效果 + 元素/状态
                foreach (var foe in foes)
                {
                    bool killed = ApplyOnEnemy(caster, foe);
                    if (killed && ComponentIds.Contains("dmg_pierce") == false)
                    {
                        // 击杀特殊规则：反弹到周围额外敌人（一次）
                        List<Agent> around = Script.FindAgentsWithinSpellRange(foe.Position, 4);
                        Script.AgentListIFF(caster, around, out _, out var extraFoes);
                        foreach (var extra in extraFoes)
                            if (extra != foe) ApplyOnEnemy(caster, extra);
                    }
                    if (!Pierce) break; // 不穿透则命中首个后停止
                }
            }
            else if (friends.Count > 0 && (HasShield || HasHeal))
            {
                // 对友：施加增益
                foreach (var fr in friends)
                    ApplyOnAlly(caster, fr);
            }
        }

        // —— 对敌效果：转发组合内子法术，并叠加元素/状态 ——
        private bool ApplyOnEnemy(Agent caster, Agent target)
        {
            if (target == null || !target.IsActive()) return false;

            float dmg = 30f * DamageMul;
            switch (PrimaryElement)
            {
                case "fire": Script.CalculateFinalMagicDamage(caster, target, dmg, DamageType.FIRE_DAMAGE); break;
                case "ice": Script.CalculateFinalMagicDamage(caster, target, dmg, DamageType.ICE_DAMAGE); break;
                case "electricity": Script.CalculateFinalMagicDamage(caster, target, dmg, DamageType.ELECTRICITY_DAMAGE); break;
                case "toxin": Script.CalculateFinalMagicDamage(caster, target, dmg, DamageType.TOXIN_DAMAGE); break;
                default: Script.CalculateFinalMagicDamage(caster, target, dmg, DamageType.None); break;
            }

            // 转发组合内的子法术（以旧系统为准：直接作用于 target）
            foreach (var sub in componentSpells)
            {
                try { sub.Activate(target); } catch { }
            }

            // 状态组件
            var comp = target.GetComponent<AgentSkillComponent>();
            if (comp != null)
            {
                if (ComponentIds.Contains("st_burn") || PrimaryElement == "fire")
                    comp.StateContainer.AddState(new BurningState(4f, 6f, caster) { TargetAgent = target });
                if (ComponentIds.Contains("st_freeze") || PrimaryElement == "ice")
                    comp.StateContainer.AddState(new FreezeState(3f, 0.5f, caster) { TargetAgent = target });
                if (ComponentIds.Contains("st_poison") || PrimaryElement == "toxin")
                    comp.StateContainer.AddState(new WeakenState(4f, 5f, caster) { TargetAgent = target });
                if (ComponentIds.Contains("ctrl_stun"))
                {
                    target.SetActionChannel(0, ActionIndexCache.Create("act_knocked_down"));
                }
            }

            // 返回是否击杀
            return target.Health <= 0f;
        }

        // —— 对友增益 ——
        private void ApplyOnAlly(Agent caster, Agent ally)
        {
            if (ally == null || !ally.IsActive()) return;

            var comp = ally.GetComponent<AgentSkillComponent>();
            if (HasShield)
            {
                // 护盾：临时提升最大生命（以旧系统中强化逻辑为准）
                ally.Health = MathF.Clamp(ally.Health + 40f, 0f, ally.HealthLimit + 40f);
            }
            if (HasHeal)
            {
                comp?.StateContainer.AddState(new HealState(4f, caster) { TargetAgent = ally });
            }
            foreach (var sub in componentSpells)
            {
                try { sub.Activate(ally); } catch { }
            }
        }

        // —— AoE 范围效果 ——
        private void ApplyAoe(Agent caster, Vec3 center)
        {
            List<Agent> around = Script.FindAgentsWithinSpellRange(center, 4);
            Script.AgentListIFF(caster, around, out var friends, out var foes);
            foreach (var foe in foes) ApplyOnEnemy(caster, foe);
            if (HasShield || HasHeal)
                foreach (var fr in friends) ApplyOnAlly(caster, fr);
        }
    }
}
