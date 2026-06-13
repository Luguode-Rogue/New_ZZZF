using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 运行时物品实例记录。
    /// 把"模板物品 ItemObject"与"某一次生成的词缀结果"绑定起来。
    /// 每一件带词缀的装备都对应一条记录，存档时序列化。
    /// </summary>
    public sealed class AffixedItemRecord
    {
        /// <summary>生成时的唯一标识（Guid字符串）</summary>
        [SaveableField(1)]
        public string InstanceId = string.Empty;

        /// <summary>基础物品的 ItemObject.StringId</summary>
        [SaveableField(2)]
        public string BaseItemId = string.Empty;

        /// <summary>来源描述（掉落/商店/调试等）</summary>
        [SaveableField(3)]
        public string Source = string.Empty;

        /// <summary>堆叠数量</summary>
        [SaveableField(4)]
        public int StackCount = 1;

        /// <summary>绑定的词缀实例</summary>
        [SaveableField(5)]
        public AffixInstance Affix = new AffixInstance();

        /// <summary>是否已被装备到角色身上</summary>
        [SaveableField(6)]
        public bool IsEquipped;

        public AffixedItemRecord()
        {
            InstanceId = System.Guid.NewGuid().ToString("N");
        }
    }
}
