using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Localization;
using New_ZZZF.LegacyWorld.Core;

namespace New_ZZZF.LegacyWorld.Core.Settings
{
    /// <summary>
    /// LegacyWorld 世界状态继承系统的 MCM 设置面板。
    /// 参照 ProjectileTrajectorySystem.ProjectileTrajectorySettings 模式。
    /// 属性改动时会自动通过 OnPropertyChanged 同步到 XML 后端。
    /// </summary>
    public class LegacyWorldMCMSettings : AttributeGlobalSettings<LegacyWorldMCMSettings>
    {
        // ===== MCM 元数据 =====

        public override string Id => "LegacyWorld_v1";

        // 本地化 DisplayName，通过 {=} 前缀支持翻译模块加载
        public override string DisplayName =>
            new TextObject("{=LW_DisplayName}LegacyWorld - 世界状态继承系统").ToString();

        public override string FolderName => "New_ZZZF";
        public override string FormatType => "xml";

        // ===== 基础控制 =====

        [SettingPropertyBool(
            "{=LW_001}启用世界状态继承系统",
            RequireRestart = false,
            HintText = "{=LW_H001}主开关：关闭后完全禁用导入/导出，即使存在 Legacy.json 也不会触发，也不再生成。",
            Order = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool(
            "{=LW_002}存档时自动导出",
            RequireRestart = false,
            HintText = "{=LW_H002}关闭后保存游戏不再自动生成 Legacy.json。",
            Order = 1)]
        public bool AutoExportOnSave { get; set; } = true;

        [SettingPropertyBool(
            "{=LW_003}启用调试日志",
            RequireRestart = false,
            HintText = "{=LW_H003}关闭后不再写入 affix_debug.log。",
            Order = 2)]
        public bool LogEnabled { get; set; } = true;

        // ===== 导入数据类别 =====

        [SettingPropertyGroup("{=LW_GroupImport}导入数据类别")]
        [SettingPropertyBool(
            "{=LW_010}恢复王国结构",
            RequireRestart = false,
            HintText = "{=LW_H010}导入王国的统治者、宣战/和平状态等数据。",
            Order = 10)]
        public bool RestoreKingdoms { get; set; } = true;

        [SettingPropertyGroup("{=LW_GroupImport}导入数据类别")]
        [SettingPropertyBool(
            "{=LW_011}恢复家族数据",
            RequireRestart = false,
            HintText = "{=LW_H011}导入家族的所属王国、等级、声望等。",
            Order = 11)]
        public bool RestoreClans { get; set; } = true;

        [SettingPropertyGroup("{=LW_GroupImport}导入数据类别")]
        [SettingPropertyBool(
            "{=LW_012}恢复领地所有权",
            RequireRestart = false,
            HintText = "{=LW_H012}导入城镇/城堡/村庄的归属关系。",
            Order = 12)]
        public bool RestoreSettlements { get; set; } = true;

        [SettingPropertyGroup("{=LW_GroupImport}导入数据类别")]
        [SettingPropertyBool(
            "{=LW_013}恢复家族经济",
            RequireRestart = false,
            HintText = "{=LW_H013}导入家族的金币、声望、影响力数据。",
            Order = 13)]
        public bool RestoreClanEconomy { get; set; } = true;

        [SettingPropertyGroup("{=LW_GroupImport}导入数据类别")]
        [SettingPropertyBool(
            "{=LW_014}创建缺失家族",
            RequireRestart = false,
            HintText = "{=LW_H014}在新世界中不存在目标家族时自动创建（预留功能）。",
            Order = 14)]
        public bool CreateMissingClans { get; set; } = false;

        // ===== 操作触发（布尔标志 → Tick 消费） =====
        // MCMv5 的 SettingPropertyButton 仅支持属性/索引器，
        // 故使用布尔触发标志：用户拨动到 true → OnPropertyChanged 触发操作 → 自动复位回 false。

        [SettingPropertyBool(
            "{=LW_BtnExport}手动导出（拨动即触发）",
            RequireRestart = false,
            HintText = "{=LW_HBtnExport}拨动到「是」立即将当前世界状态导出到 Legacy.json（完成后自动复位）",
            Order = 20)]
        public bool ManualExportTrigger { get; set; } = false;

        [SettingPropertyBool(
            "{=LW_BtnApply}手动应用（拨动即触发）",
            RequireRestart = false,
            HintText = "{=LW_HBtnApply}拨动到「是」立即将 Legacy.json 导入当前世界（完成后自动复位）",
            Order = 21)]
        public bool ManualApplyTrigger { get; set; } = false;

        // ===== 构造与同步 =====

        /// <summary>
        /// MCM 属性变更时，同步到 XML 后端并实时更新运行时开关。
        /// 参照 ProjectileTrajectorySettings.OnPropertyChanged 模式。
        /// 触发属性（ManualExportTrigger/ManualApplyTrigger）被拨动后立即复位并发送请求。
        /// </summary>
        public override void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);

            // 处理按钮触发：拨动到 true 时发送请求并立即复位
            if (propertyName == nameof(ManualExportTrigger) && this.ManualExportTrigger)
            {
                LegacyWorldSettingsManager.RequestManualExport();
                this.ManualExportTrigger = false; // 复位
            }
            if (propertyName == nameof(ManualApplyTrigger) && this.ManualApplyTrigger)
            {
                LegacyWorldSettingsManager.RequestManualApply();
                this.ManualApplyTrigger = false; // 复位
            }

            LegacyWorldSettingsManager.SyncFromMCM(
                enabled: this.Enabled,
                autoExportOnSave: this.AutoExportOnSave,
                logEnabled: this.LogEnabled,
                restoreKingdoms: this.RestoreKingdoms,
                restoreClans: this.RestoreClans,
                restoreSettlements: this.RestoreSettlements,
                restoreClanEconomy: this.RestoreClanEconomy,
                createMissingClans: this.CreateMissingClans);

            // 日志开关实时生效
            if (propertyName == nameof(LogEnabled))
            {
                AffixLogger.LogEnabled = this.LogEnabled;
            }
        }

        /// <summary>
        /// 构造时从 XML 数据层载入已有设置，保证 UI 显示值与实际值一致。
        /// </summary>
        public LegacyWorldMCMSettings()
        {
            var data = LegacyWorldSettingsManager.Settings;
            if (data != null)
            {
                this.Enabled = data.Enabled;
                this.AutoExportOnSave = data.AutoExportOnSave;
                this.LogEnabled = data.LogEnabled;
                this.RestoreKingdoms = data.RestoreKingdoms;
                this.RestoreClans = data.RestoreClans;
                this.RestoreSettlements = data.RestoreSettlements;
                this.RestoreClanEconomy = data.RestoreClanEconomy;
                this.CreateMissingClans = data.CreateMissingClans;
            }
        }
    }
}
