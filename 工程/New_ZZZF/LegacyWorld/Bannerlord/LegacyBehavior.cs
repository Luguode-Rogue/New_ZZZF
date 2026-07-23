using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Settings;

namespace New_ZZZF.LegacyWorld.Bannerlord
{
    /// <summary>
    /// 世界状态继承系统的 CampaignBehavior。
    /// 负责挂钩 Save / 新游戏生命周期事件：
    /// - OnBeforeSaveEvent → 自动导出世界状态
    /// - OnNewGameCreatedEvent → 自动导入世界遗产（仅新游戏）
    /// - OnCampaignTickEvent → 消费 MCM 按钮触发的导出/导入请求
    /// 使用 applied 标志防止重复导入（随存档序列化）。
    /// 所有开关通过 MCM 面板（LegacyWorldMCMSettings）控制。
    /// </summary>
    public class LegacyBehavior : CampaignBehaviorBase
    {
        private bool _applied;
        private string _appliedWorldId;

        public override void RegisterEvents()
        {
            CampaignEvents.OnBeforeSaveEvent.AddNonSerializedListener(this, OnSave);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnTick);

            // 从 MCM 数据层同步日志开关
            AffixLogger.LogEnabled = LegacyWorldSettingsManager.Settings.LogEnabled;
        }

        /// <summary>
        /// 游戏保存时自动导出世界状态。
        /// 受「主开关 + 自动导出开关」双重控制。
        /// </summary>
        private void OnSave()
        {
            // 主开关检测
            if (!LegacyWorldSettingsManager.Settings.Enabled)
            {
                AffixLogger.Info("BEHAVIOR", "OnSave 跳过：主开关已关闭");
                return;
            }

            // 自动导出开关检测
            if (!LegacyWorldSettingsManager.Settings.AutoExportOnSave)
            {
                AffixLogger.Info("BEHAVIOR", "OnSave 跳过：自动导出已关闭");
                return;
            }

            LegacyService.Export();
        }

        /// <summary>
        /// 新游戏创建时尝试导入世界遗产。
        /// 仅在第一次新游戏时执行，且受主开关控制。
        /// 已包裹全局 try-catch，防止第三方 Mod 冲突引发崩溃。
        /// </summary>
        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            AffixLogger.Info("BEHAVIOR", "OnNewGameCreated 触发");

            // 主开关检测
            if (!LegacyWorldSettingsManager.Settings.Enabled)
            {
                AffixLogger.Info("BEHAVIOR", "OnNewGameCreated 跳过：主开关已关闭");
                return;
            }

            try
            {
                TryApply();
            }
            catch (Exception ex)
            {
                AffixLogger.Error("BEHAVIOR", "OnNewGameCreated 全局 catch 捕获异常", ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[LegacyWorld] 导入失败（全局保护）: {ex.Message}",
                    TaleWorlds.Library.Colors.Red));
            }

            AffixLogger.Info("BEHAVIOR", "OnNewGameCreated 完成");
        }

        /// <summary>
        /// 每小时 Tick：消费 MCM 按钮触发的导出/导入请求。
        /// 使用 HourlyTickEvent 避免不必要的高频轮询。
        /// </summary>
        private void OnTick()
        {
            if (LegacyWorldSettingsManager.TryConsumeManualExport())
            {
                try
                {
                    AffixLogger.Info("BEHAVIOR", "MCM 按钮：手动导出触发");
                    LegacyService.Export();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[LegacyWorld] 手动导出完成", Colors.Green));
                }
                catch (Exception ex)
                {
                    AffixLogger.Error("BEHAVIOR", "手动导出异常", ex);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[LegacyWorld] 手动导出失败: {ex.Message}", Colors.Red));
                }
            }

            if (LegacyWorldSettingsManager.TryConsumeManualApply())
            {
                try
                {
                    AffixLogger.Info("BEHAVIOR", "MCM 按钮：手动应用触发");
                    string worldId = Campaign.Current?.UniqueGameId ?? "0";
                    bool result = LegacyService.ForceImport(worldId);
                    _applied = result;
                    if (result) _appliedWorldId = worldId;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[LegacyWorld] 手动应用完成", Colors.Green));
                }
                catch (Exception ex)
                {
                    AffixLogger.Error("BEHAVIOR", "手动应用异常", ex);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[LegacyWorld] 手动应用失败: {ex.Message}", Colors.Red));
                }
            }
        }

        /// <summary>
        /// 尝试应用世界遗产（仅一次）。
        /// </summary>
        private void TryApply()
        {
            AffixLogger.Info("BEHAVIOR", $"TryApply 开始, _applied={_applied}");

            if (_applied)
            {
                AffixLogger.Info("BEHAVIOR", "已应用过，跳过");
                return;
            }

            string worldId = Campaign.Current?.UniqueGameId ?? "0";
            AffixLogger.Info("BEHAVIOR", $"当前世界ID={worldId}");

            bool result = LegacyService.Import(worldId);
            AffixLogger.Info("BEHAVIOR", $"Import 返回结果={result}");

            if (result)
            {
                _applied = true;
                _appliedWorldId = worldId;
                AffixLogger.Info("BEHAVIOR", $"应用完成, worldId={worldId}");
            }
            else
            {
                AffixLogger.Info("BEHAVIOR", "Import 返回 false，未应用");
            }
        }

        /// <summary>
        /// 序列化/反序列化导入状态，保证跨存档不重复导入。
        /// </summary>
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("LegacyWorld_Applied", ref _applied);
            dataStore.SyncData("LegacyWorld_AppliedWorldId", ref _appliedWorldId);
        }
    }
}
