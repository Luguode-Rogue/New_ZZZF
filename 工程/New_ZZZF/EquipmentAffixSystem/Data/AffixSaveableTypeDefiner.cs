using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// Registers custom affix lifecycle objects for Bannerlord SaveSystem.
    /// Affix data is nested inside campaign save data, so these classes must be
    /// known by the serializer during save/load.
    /// </summary>
    public class AffixSaveableTypeDefiner : SaveableTypeDefiner
    {
        public AffixSaveableTypeDefiner()
            : base(5234000)
        {
        }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(AffixInstance), 1);
            AddClassDefinition(typeof(AffixedItemRecord), 2);
        }
    }
}
