using System;
using System.IO;
using System.Xml.Linq;

namespace New_ZZZF
{
    /// <summary>
    /// 闪退排查用子系统开关（ModuleData/NewZZZF_Diag.xml）。
    ///
    /// 用法：改 XML 里对应开关为 false → 重启游戏 → 复现/排除。定位到罪魁后修好该子系统或保持关闭。
    ///
    /// 排查矩阵建议（每轮改 XML 重启即可，无需编译）：
    ///   ① 全 false         → 若仍崩：嫌疑在引擎层/其他 mod/环境
    ///   ② 仅 HarmonyPatchAll=true → 若崩：嫌疑在 Harmonys/ 与各 [HarmonyPatch] 补丁（再对补丁二分）
    ///   ③ 逐组开 Behavior   → 崩溃出现的那个组合即锁定子系统
    /// </summary>
    internal static class NewZZZFDiag
    {
        // ---- 各子系统开关（默认全开 = 与原行为一致） ----

        /// <summary>TacticalMap：Bootstrap/HtmlUi 初始化、mission 挂载、N 键切换、Tick</summary>
        public static bool TacticalMap = true;

        /// <summary>技能 HTML 界面：CustomSkillHtmlUi 初始化/Tick/M 键界面/Shift+M 旧界面</summary>
        public static bool CustomSkillHtmlUi = true;

        /// <summary>SkillSystemBehavior（战斗内技能系统）</summary>
        public static bool SkillSystemBehavior = true;

        /// <summary>MountedSlashCameraMissionLogic（斩击镜头）</summary>
        public static bool MountedSlashCamera = true;

        /// <summary>HeroChangeMissionBehavior + HeroChangeCampaignBehavior + HeroSkillSaveCustomBehavior</summary>
        public static bool HeroChange = true;

        /// <summary>Affix 词缀系统：AffixMissionBehavior + AffixCampaignBehavior + Ctrl+F5~F9 调试热键</summary>
        public static bool Affix = true;

        /// <summary>NewZZZF_MissionAgentStatusView（兵种状态浮窗）</summary>
        public static bool AgentStatusView = true;

        /// <summary>Harmony PatchAll（Harmonys/ 目录与各 [HarmonyPatch] 特性补丁，含 NewDamageModel 等）</summary>
        public static bool HarmonyPatchAll = true;

        /// <summary>技能注册：CompositeSpellRegistry / SkillFactory / SkillConfigManager 加载与 L 键热重载</summary>
        public static bool SkillRegistry = true;

        /// <summary>伤害模型替换：InitializeGameStarter 中的 WOW_* / ZZZF_* 模型</summary>
        public static bool DamageModels = true;

        private static bool _loaded;

        /// <summary>从本程序集位置推导模块根（不依赖进程 cwd —— 实测相对路径在部分启动方式下不可靠）。</summary>
        private static string ResolveXmlPath()
        {
            // <模块根>\bin\Win64_Shipping_Client\New_ZZZF.dll → 上三级 = 模块根
            string binDir = Path.GetDirectoryName(typeof(NewZZZFDiag).Assembly.Location);
            string moduleRoot = Path.GetFullPath(Path.Combine(binDir ?? ".", "..", ".."));
            return Path.Combine(moduleRoot, "ModuleData", "NewZZZF_Diag.xml");
        }

        public static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            string xmlPath = ResolveXmlPath();
            try
            {
                if (File.Exists(xmlPath))
                {
                    ReadIntoFields(xmlPath);
                    Announce("诊断开关已加载: " + xmlPath);
                }
                else
                {
                    Save(xmlPath);
                    Announce("诊断开关模板已生成: " + xmlPath);
                }
            }
            catch (Exception ex)
            {
                Announce("诊断开关加载失败（全默认开启）: " + ex.Message);
            }
        }

        private static void Announce(string message)
        {
            try
            {
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage("[NewZZZF-Diag] " + message,
                        TaleWorlds.Library.Colors.Yellow));
            }
            catch
            {
                // 主菜单早期阶段可能无法弹消息 —— 静默
            }
            Console.WriteLine("[NewZZZF-Diag] " + message);
        }

        public static void Save()
        {
            Save(ResolveXmlPath());
        }

        private static void Save(string xmlPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(xmlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                XDocument doc = new XDocument(
                    new XComment(" New_ZZZF 闪退排查 —— 子系统开关（false=禁用该子系统，重启游戏生效）"),
                    new XComment(" 排查矩阵：① 全 false 测基线；② 仅开 HarmonyPatchAll；③ 逐组开 Behavior"),
                    new XElement("NewZZZFDiag",
                        new XComment(" TacticalMap 战术地图（初始化/挂载/N键/Tick）"),
                        new XElement("TacticalMap", TacticalMap.ToString().ToLowerInvariant()),
                        new XComment(" 技能 HTML 界面（CustomSkillHtmlUi 初始化/Tick/M键）"),
                        new XElement("CustomSkillHtmlUi", CustomSkillHtmlUi.ToString().ToLowerInvariant()),
                        new XComment(" 战斗内技能系统 Behavior"),
                        new XElement("SkillSystemBehavior", SkillSystemBehavior.ToString().ToLowerInvariant()),
                        new XComment(" 斩击镜头 Behavior"),
                        new XElement("MountedSlashCamera", MountedSlashCamera.ToString().ToLowerInvariant()),
                        new XComment(" 英雄切换系统（MissionBehavior + CampaignBehavior）"),
                        new XElement("HeroChange", HeroChange.ToString().ToLowerInvariant()),
                        new XComment(" 词缀系统（MissionBehavior + CampaignBehavior + Ctrl+F5~F9）"),
                        new XElement("Affix", Affix.ToString().ToLowerInvariant()),
                        new XComment(" 兵种状态浮窗 Behavior"),
                        new XElement("AgentStatusView", AgentStatusView.ToString().ToLowerInvariant()),
                        new XComment(" Harmony PatchAll（全部 [HarmonyPatch] 补丁，含 NewDamageModel）"),
                        new XElement("HarmonyPatchAll", HarmonyPatchAll.ToString().ToLowerInvariant()),
                        new XComment(" 技能注册加载（CompositeSpellRegistry/SkillFactory/SkillConfig + L 键热重载）"),
                        new XElement("SkillRegistry", SkillRegistry.ToString().ToLowerInvariant()),
                        new XComment(" 伤害模型替换（InitializeGameStarter 的 WOW_*/ZZZF_* 模型）"),
                        new XElement("DamageModels", DamageModels.ToString().ToLowerInvariant())));
                doc.Save(xmlPath);
            }
            catch (Exception ex)
            {
                Announce("诊断开关写入失败: " + ex.Message);
            }
        }

        private static void ReadIntoFields(string xmlPath)
        {
            XElement root = XDocument.Load(xmlPath).Root;
            if (root == null)
                return;

            if (TryParseBool(root.Element("TacticalMap")?.Value, out bool v)) TacticalMap = v;
            if (TryParseBool(root.Element("CustomSkillHtmlUi")?.Value, out v)) CustomSkillHtmlUi = v;
            if (TryParseBool(root.Element("SkillSystemBehavior")?.Value, out v)) SkillSystemBehavior = v;
            if (TryParseBool(root.Element("MountedSlashCamera")?.Value, out v)) MountedSlashCamera = v;
            if (TryParseBool(root.Element("HeroChange")?.Value, out v)) HeroChange = v;
            if (TryParseBool(root.Element("Affix")?.Value, out v)) Affix = v;
            if (TryParseBool(root.Element("AgentStatusView")?.Value, out v)) AgentStatusView = v;
            if (TryParseBool(root.Element("HarmonyPatchAll")?.Value, out v)) HarmonyPatchAll = v;
            if (TryParseBool(root.Element("SkillRegistry")?.Value, out v)) SkillRegistry = v;
            if (TryParseBool(root.Element("DamageModels")?.Value, out v)) DamageModels = v;
        }

        private static bool TryParseBool(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
                return true;
            if (value == "1") { result = true; return true; }
            if (value == "0") { result = false; return true; }
            result = default;
            return false;
        }
    }
}
