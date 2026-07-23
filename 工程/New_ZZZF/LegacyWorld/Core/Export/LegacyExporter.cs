using System;
using New_ZZZF.LegacyWorld.Adapter;
using New_ZZZF.LegacyWorld.Core.Models;
using New_ZZZF.LegacyWorld.Core.Serialization;
using New_ZZZF.LegacyWorld.Core.Storage;

namespace New_ZZZF.LegacyWorld.Core.Export
{
    /// <summary>
    /// 世界状态导出引擎。
    /// 通过 IGameAdapter 从当前 Campaign 读取所有 Kingdom / Clan / Settlement，
    /// 转换为状态模型并持久化为 Legacy.json。
    /// </summary>
    public class LegacyExporter
    {
        private readonly IGameAdapter _adapter;

        public LegacyExporter(IGameAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// 执行导出操作，将当前世界状态保存到 Legacy.json。
        /// </summary>
        public void Export()
        {
            var data = new LegacyData
            {
                Version = 1,
                WorldId = _adapter.GetWorldId(),
                CreatedAt = _adapter.GetCurrentGameTime(),
                Culture = _adapter.GetDominantCulture(),
                GameVersion = _adapter.GetGameVersion(),
            };

            // 导出所有王国
            foreach (var kingdom in _adapter.GetAllKingdoms())
            {
                data.Kingdoms.Add(new KingdomState
                {
                    Id = kingdom.Id,
                    Name = kingdom.Name,
                    RulerClanId = kingdom.RulerClanId,
                    Culture = kingdom.Culture,
                });
            }

            // 导出所有家族
            foreach (var clan in _adapter.GetAllClans())
            {
                data.Clans.Add(new ClanState
                {
                    Id = clan.Id,
                    Name = clan.Name,
                    KingdomId = clan.KingdomId,
                    Tier = clan.Tier,
                    Gold = clan.Gold,
                    Renown = clan.Renown,
                    Influence = clan.Influence,
                    IsDestroyed = clan.IsDestroyed,
                });
            }

            // 导出所有定居点（城镇/城堡/村庄）
            foreach (var settlement in _adapter.GetAllSettlements())
            {
                data.Settlements.Add(new SettlementState
                {
                    Id = settlement.Id,
                    Name = settlement.Name,
                    Type = settlement.Type,
                    OwnerClanId = settlement.OwnerClanId,
                    OwnerKingdomId = settlement.OwnerKingdomId,
                    Culture = settlement.Culture,
                    Prosperity = settlement.Prosperity,
                });
            }

            // 确保目录存在并写入文件
            LegacyStorage.EnsureDirectoryExists();
            LegacySerializer.Save(data, LegacyStorage.LegacyFile);
        }
    }
}
