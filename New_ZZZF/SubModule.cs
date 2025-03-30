using System;
using System.IO;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
using SandBox.Issues;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.MountAndBlade.View.Screens;
using New_ZZZF.Systems;
using MountedSlashCamera;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using StoryMode.GameComponents.CampaignBehaviors;
using TaleWorlds.Localization;
using SandBox.GauntletUI.Missions;


namespace New_ZZZF
{
    public class SubModule : MBSubModuleBase
    {

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

        }
        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            SkillFactory.SkillToItemObject();
            SkillConfigManager.Instance._troopSkillMap.Clear();
            if (!(SkillConfigManager.Instance._troopSkillMap != null && SkillConfigManager.Instance._troopSkillMap.Count > 1))
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
        protected override  void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

        
        }
        public override void OnGameLoaded(Game game, object gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                
            }
            SkillFactory.SkillToItemObject();

            SkillConfigManager.Instance._troopSkillMap.Clear();
        }
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // 添加自定义的 MissionBehavior 到当前任务
            mission.AddMissionBehavior(new SkillSystemBehavior());
            mission.AddMissionBehavior(new MountedSlashCameraMissionLogic());
            // 调试日志
            Debug.Print("[New_ZZZF] 技能系统已激活！");

        }
        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Debug.Print("[New_ZZZF] Mod已启动！");
        }
        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {

            //ExtendedData extendedData = new ExtendedData();
            //默认使用的代码    

            //extendedData.CreateOrRetrieveDataForGun();
            gameStarterObject.AddModel(new WOW_DefaultStrikeMagnitudeModel());
            gameStarterObject.AddModel(new WOW_CustomBattleAgentStatCalculateModel());
            gameStarterObject.AddModel(new WOW_CustomAgentApplyDamageModel());
            //gameStarterObject.AddModel(new WOW_DefaultRidingModel());
            //gameStarterObject.AddModel(new WOW_DefaultPartySpeedCalculatingModel());

            new Harmony("New_ZZZF").PatchAll(Assembly.GetExecutingAssembly());
            if (game.GameType is Campaign)
            {
                //战役里使用的代码
                //gameStarterObject.AddModel(new WoW_DefaultClanTierModel());
                gameStarterObject.AddModel(new WOW_SandboxAgentApplyDamageModel());
                gameStarterObject.AddModel(new WOW_SandboxStrikeMagnitudeModel());
                gameStarterObject.AddModel(new ZZZF_SandboxAgentStatCalculateModel());

                CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                campaignGameStarter.AddBehavior(new HeroSkillSaveCustomBehavior());

            }
        }
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            if (Game.Current != null)
            {
                if (Input.IsKeyPressed(InputKey.K) && !Game.Current.GameStateManager.ActiveState.IsMenuState)
                {

                    if (Hero.MainHero != null)
                    {
                        Hero mainHero = Hero.MainHero;
                        if (mainHero != null && !mainHero.IsDead)
                        {
                            SkillInventoryManager.OpenScreenAsInventory(null);
                        }
                    }



                }
                if (Input.IsKeyPressed(InputKey.L))
                {
                    MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;



                }
            }
        }
    }
}
//代码说明
//1. 关键功能
//配置加载：在 OnSubModuleLoad 阶段从 troop_skills.xml 加载技能配置。

//错误处理：捕获XML解析异常并通过游戏内消息和日志输出。

//行为注册：在 OnMissionBehaviorInitialize 中将 SkillSystemBehavior 添加到任务中。

//2. 路径说明
//BasePath.Name：自动获取游戏根目录（如 ...\Steam\steamapps\common\Mount & Blade II Bannerlord）。

//模块路径：Modules/YourMod/ModuleData/troop_skills.xml 需按实际 Mod 名称调整。

//3. 扩展性
//战役模式支持：通过 OnGameStart 可扩展战役模式逻辑（如英雄技能存档）。