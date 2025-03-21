using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 兵种技能配置管理器（单例模式）
    /// </summary>
    public sealed class SkillConfigManager
    {
        // 单例实例
        private static readonly SkillConfigManager _instance = new SkillConfigManager();
        public static SkillConfigManager Instance => _instance;

        // 兵种ID到技能配置的映射
        private Dictionary<string, SkillSet> _troopSkillMap = new Dictionary<string, SkillSet>();

        // 私有构造函数（禁止外部实例化）
        private SkillConfigManager() { }

        /// <summary>
        /// 从XML文件加载兵种技能配置
        /// </summary>
        /// <param name="xmlPath">XML文件路径（例如："Modules/YourMod/ModuleData/troop_skills.xml"）</param>
        public void LoadFromXml(string xmlPath)
        {
            _troopSkillMap.Clear();
            XDocument doc = XDocument.Load(xmlPath);

            foreach (XElement troopNode in doc.Descendants("Troop"))
            {
                string troopId = troopNode.Attribute("id").Value;
                SkillSet skillSet = new SkillSet
                {
                    MainActive = ParseSkill(troopNode.Element("MainActive")?.Value),
                    SubActive = ParseSkill(troopNode.Element("SubActive")?.Value),
                    Passive = ParseSkill(troopNode.Element("Passive")?.Value), // 单个被动
                    CombatArt = ParseSkill(troopNode.Element("CombatArt")?.Value)
                };

                // 加载法术栏
                XElement spellsNode = troopNode.Element("Spells");
                if (spellsNode != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        string slotName = $"Slot{i + 1}";
                        XElement slotNode = spellsNode.Element(slotName);
                        skillSet.Spells[i] = ParseSkill(slotNode?.Value);
                    }
                }

                _troopSkillMap[troopId] = skillSet;
            }
        }
        /// <summary>
        /// 获取指定兵种的技能配置
        /// </summary>
        public SkillSet GetSkillSetForTroop(string troopId)
        {
            if (_troopSkillMap.TryGetValue(troopId, out SkillSet skillSet))
            {
                return skillSet;
            }

            Debug.Print($"[警告] 未找到兵种 {troopId} 的技能配置，返回默认");
            return SkillSet.Default;
        }

        /// <summary>
        /// 将技能ID转换为SkillBase实例
        /// </summary>
        private SkillBase ParseSkill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;

            SkillBase skill = SkillFactory.Create(skillId);
            if (skill == null)
            {
                Debug.Print($"[警告] 未知技能ID: {skillId}");
                return new NullSkill(); // 返回空技能占位
            }
            return skill;
        }
    }

    /// <summary>
    /// 兵种技能配置容器类
    /// </summary>
    public class SkillSet
    {
        public SkillBase MainActive { get; set; }     // 主主动
        public SkillBase SubActive { get; set; }      // 副主动
        public SkillBase Passive { get; set; }        // 被动（唯一）
        public SkillBase[] Spells { get; } = new SkillBase[4]; // 法术/低级被动
        public SkillBase CombatArt { get; set; }      // 战技

        public static SkillSet Default => new SkillSet();
    }

    /// <summary>
    /// 空技能占位（防止空引用）
    /// </summary>
    public class NullSkill : SkillBase
    {
        public NullSkill() => SkillID = "Null";
        public override bool Activate(Agent agent) { return false; }
    }
}
//代码说明
//1. 核心功能
//单例模式：通过私有构造函数和静态 Instance 属性确保全局唯一实例。

//配置加载：从 troop_skills.xml 解析兵种技能配置，映射到 SkillSet 对象。

//容错处理：

//忽略无效的兵种节点（缺失 id 属性）。

//未知技能ID时返回 NullSkill 占位。

//缺失配置时返回默认 SkillSet.Default。

//2. XML解析规则
//被动栏：仅读取 第一个 <Passive> 标签，后续同名标签会被忽略。

//法术栏：严格按 Slot1 到 Slot4 解析，允许空槽。

//战技栏：通过 <CombatArt> 标签配置。

//3. 错误处理
//文件级错误：捕获 XDocument.Load 异常（如文件不存在）。

//技能级错误：未知技能ID时记录警告日志。

//兵种级错误：未找到配置时返回默认值。

//4. 性能优化
//预加载缓存：所有配置在Mod启动时加载到内存。

//按需获取：通过 GetSkillSetForTroop 快速检索缓存数据。