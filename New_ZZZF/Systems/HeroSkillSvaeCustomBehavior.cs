using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    //internal class HeroSkillSvaeCustomBehavior : CampaignBehaviorBase
    //{    // The data in this field will persist across saving
    //public Dictionary<string,SkillSet> troopSkillMap ;

    //public override void SyncData(IDataStore dataStore)
    //{
    //    // First argument is an identifier, only needs to be unique to this behavior
    //    dataStore.SyncData("Data", ref troopSkillMap);
    //}

    //public override void RegisterEvents() {
    //        troopSkillMap = SkillConfigManager.Instance._troopSkillMap;
    //        InformationManager.DisplayMessage(new InformationMessage("Save_SkillConfigManager.Instance._troopSkillMap", Colors.Red));
    //    }
    //}
    public class CustomSaveDefiner : SaveableTypeDefiner
    {
        // use a big number and ensure that no other mod is using a close range
        public CustomSaveDefiner() : base(0x36af1c) { }

        protected override void DefineClassTypes()
        {
            // The Id's here are local and will be related to the Id passed to the constructor
            AddClassDefinition(typeof(SkillConfigManager), 1);
        }

        protected override void DefineContainerDefinitions()
        {
            // Both of these are necessary: order isn't important
            ConstructContainerDefinition(typeof(SkillConfigManager));
            ConstructContainerDefinition(typeof(Dictionary<string, SkillConfigManager>));
        }
    }
}
