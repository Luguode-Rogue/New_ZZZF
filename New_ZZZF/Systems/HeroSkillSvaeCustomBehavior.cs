using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;
using Bannerlord.ButterLib.SaveSystem.Extensions;
namespace New_ZZZF
{
    //public class HeroSkillSaveCustomBehavior : CampaignBehaviorBase
    //{

    //    private Dictionary<string, SkillSet> troopSkillMap = SkillConfigManager.Instance._troopSkillMap;

    //    public override void SyncData(IDataStore dataStore)
    //    {
    //        if (dataStore.IsSaving)
    //        {
    //            troopSkillMap = SkillConfigManager.Instance._troopSkillMap;
    //            var jsonString = JsonConvert.SerializeObject(troopSkillMap);
    //            dataStore.SyncData("troopSkill", ref jsonString);

    //        }
    //        if (dataStore.IsLoading)
    //        {
    //            var jsonString = "";
    //            if (dataStore.SyncData("troopSkill", ref jsonString) && !string.IsNullOrEmpty(jsonString))
    //            {
    //                troopSkillMap = JsonConvert.DeserializeObject<Dictionary<string, SkillSet>>(jsonString);
    //                SkillConfigManager.Instance._troopSkillMap=troopSkillMap;
    //            }
    //            else
    //            {
    //                troopSkillMap = new Dictionary<string, SkillSet>(); 
    //                if (SkillConfigManager.Instance._troopSkillMap == null || SkillConfigManager.Instance._troopSkillMap.Count == 0)
    //                {
    //                    try
    //                    {
    //                        // 初始化技能配置管理器并加载XML
    //                        string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
    //                        SkillConfigManager.Instance.LoadFromXml(xmlPath);

    //                        // 调试日志
    //                        Debug.Print("[New_ZZZF] 技能配置加载完成！");
    //                    }
    //                    catch (Exception ex)
    //                    {
    //                        Debug.Print($"[New_ZZZF] 配置加载失败: {ex.Message}");
    //                    }
    //                }
    //            }

    //        }
    //    }

    //    public override void RegisterEvents() { }
    //}
    internal class HeroSkillSaveCustomBehavior : CampaignBehaviorBase
    {
        public Dictionary<string, SkillSet> troopSkill = SkillConfigManager.Instance._troopSkillMap;
        public Dictionary<string, List<string>> _troopSkillMap = new Dictionary<string, List<string>>();
        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                foreach (var item in SkillConfigManager.Instance._troopSkillMap)
                {
                    _troopSkillMap[item.Key] = SkillConfigManager.ToStringList(item.Value);
                }
                dataStore.SyncDataAsJson("troopSkillMap", ref _troopSkillMap);
                SkillConfigManager.Instance.SaveToXml("../../Modules/New_ZZZF/svae_troop_skills.xml");
            }
            if (dataStore.IsLoading)
            {
                dataStore.SyncDataAsJson("troopSkillMap", ref _troopSkillMap);
                foreach (var item in _troopSkillMap)
                {
                    SkillConfigManager.Instance._troopSkillMap[item.Key] = SkillConfigManager.ListToSkillSet(item.Value);
                }

            }
        }

        public override void RegisterEvents()
        {
            //_troopSkillMap = SkillConfigManager.Instance._troopSkillMap;
            InformationManager.DisplayMessage(new InformationMessage("Save_SkillConfigManager.Instance._troopSkillMap", Colors.Red));
            if (SkillConfigManager.Instance._troopSkillMap == null || SkillConfigManager.Instance._troopSkillMap.Count == 0)
            {
                try
                {
                    // 初始化技能配置管理器并加载XML
                    string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                    SkillConfigManager.Instance.LoadFromXml(xmlPath);

                    // 调试日志
                    Debug.Print("[New_ZZZF] 技能配置加载完成！");
                }
                catch (Exception ex)
                {
                    Debug.Print($"[New_ZZZF] 配置加载失败: {ex.Message}");
                }
            }
        }
    }
    //public class CustomSaveDefiner : SaveableTypeDefiner
    //{
    //    public CustomSaveDefiner() : base(0x36af1c) { }

    //    protected override void DefineClassTypes()
    //    {
    //        AddClassDefinition(typeof(SkillSet), 1);
    //        AddClassDefinition(typeof(SkillBase), 2);
    //        AddClassDefinition(typeof(SkillDifficulty), 3);
    //    }

    //    protected override void DefineContainerDefinitions()
    //    {
    //        ConstructContainerDefinition(typeof(SkillSet));
    //        ConstructContainerDefinition(typeof(SkillBase));
    //        ConstructContainerDefinition(typeof(List<SkillDifficulty>));
    //        ConstructContainerDefinition(typeof(SkillDifficulty));
    //        ConstructContainerDefinition(typeof(Dictionary<string, SkillSet>));
    //    }
    //}
}
