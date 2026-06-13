using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀物品拥有者类型。
    /// </summary>
    public enum AffixOwnerType
    {
        Inventory,
        Equipment,
        Loot,
        Shop,
        Debug
    }

    /// <summary>
    /// 词缀物品运行时绑定记录。
    /// 记录"哪件词缀物品（InstanceId）现在在谁手里、在哪个槽位"。
    /// BindingMap 的 Key 格式：OwnerId:SlotIndex
    /// </summary>
    public sealed class AffixBinding
    {
        /// <summary>关联的 AffixedItemRecord.InstanceId</summary>
        [SaveableField(1)]
        public string InstanceId = string.Empty;

        /// <summary>拥有者类型</summary>
        [SaveableField(2)]
        public AffixOwnerType OwnerType;

        /// <summary>拥有者ID（HeroId / PartyId / ShopId）</summary>
        [SaveableField(3)]
        public string OwnerId = string.Empty;

        /// <summary>装备槽位索引（EquipmentIndex 转 int），背包时为背包索引</summary>
        [SaveableField(4)]
        public int SlotIndex;
    }
}
