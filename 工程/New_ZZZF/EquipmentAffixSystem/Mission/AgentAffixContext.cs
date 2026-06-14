using System.Collections.Generic;
using TaleWorlds.Core;

namespace New_ZZZF
{
    /// <summary>
    /// Mission 层 Agent 装备槽 → InstanceId 缓存。
    /// 在 Agent 创建时从 Campaign 层复制词缀绑定，战斗期间供 NewDamageModel 查询。
    ///
    /// 每条记录的语义：
    ///   SlotToInstanceId[slot] = InstanceId 表示此 Agent 的某个装备槽位对应的词缀物品实例。
    ///   未绑定的槽位不存在于字典中，查询返回 null。
    /// </summary>
    public sealed class AgentAffixContext
    {
        /// <summary>
        /// 装备槽位 → InstanceId 映射。
        /// Key: EquipmentIndex (Weapon0=0..Weapon3=3 等)
        /// Value: AffixedItemRecord.InstanceId
        /// </summary>
        public readonly Dictionary<EquipmentIndex, string> SlotToInstanceId
            = new Dictionary<EquipmentIndex, string>();

        /// <summary>
        /// 查询指定槽位的 InstanceId，未绑定时返回 null。
        /// </summary>
        public string? GetInstanceId(EquipmentIndex slot)
        {
            return SlotToInstanceId.TryGetValue(slot, out var id) ? id : null;
        }
    }
}
