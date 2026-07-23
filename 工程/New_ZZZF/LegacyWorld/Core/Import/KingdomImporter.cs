using System;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.Core;
using New_ZZZF.LegacyWorld.Core.Models;

namespace New_ZZZF.LegacyWorld.Core.Import
{
    /// <summary>
    /// 王国状态导入器。
    /// 遍历 Legacy 中的 KingdomState，在新世界中查找对应 Kingdom 并设置统治者家族。
    /// 不存在的 Kingdom 将被跳过。
    /// </summary>
    public class KingdomImporter
    {
        private readonly IGameAdapter _adapter;

        public KingdomImporter(IGameAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// 执行王国状态恢复。
        /// </summary>
        /// <param name="state">来自 Legacy 的王国状态数据</param>
        /// <returns>成功恢复的王国数量</returns>
        public int Apply(KingdomState state)
        {
            AffixLogger.Info("KINGDOMIMP", $"Apply: id={state?.Id}, name={state?.Name}, rulerClanId={state?.RulerClanId}");

            var kingdom = _adapter.FindKingdom(state.Id);
            if (kingdom == null)
            {
                AffixLogger.Info("KINGDOMIMP", $"王国 {state.Id} 在新世界不存在，跳过");
                return 0;
            }

            AffixLogger.Info("KINGDOMIMP", $"找到王国 {kingdom.Name}({kingdom.Id})");

            if (!string.IsNullOrEmpty(state.RulerClanId))
            {
                var rulerClan = _adapter.FindClan(state.RulerClanId);
                if (rulerClan != null)
                {
                    AffixLogger.Info("KINGDOMIMP", $"设置王国统治者: {rulerClan.Name}");
                    _adapter.SetKingdomRuler(kingdom, rulerClan);
                }
                else
                {
                    AffixLogger.Info("KINGDOMIMP", $"统治者家族 {state.RulerClanId} 不存在，跳过");
                }
            }

            return 1;
        }
    }
}
