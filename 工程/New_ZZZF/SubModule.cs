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
using New_ZZZF.TacticalMap.Config;
using New_ZZZF.TacticalMap.UI;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using StoryMode.GameComponents.CampaignBehaviors;
using TaleWorlds.Localization;
using SandBox.GauntletUI.Missions;
using System.Collections.Generic;
using New_ZZZF.ActionExplorer;
using TaleWorlds.Engine.GauntletUI;
using New_ZZZF.GUI;

namespace New_ZZZF
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private bool _harmonyPatched;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            TacticalMapBootstrap.OnSubModuleLoad();
            TacticalMapHtmlUi.Instance.InitializeOnFrameworkReady();
            CustomSkillHtmlUi.Instance.InitializeOnFrameworkReady();
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            ApplyZZZFSkillDisplayNames();
            InformationManager.DisplayMessage(new InformationMessage("Save_SkillConfigManager.Instance._troopSkillMap", Colors.Red));
            CompositeSpellRegistry.LoadAndRegisterAll();
            SkillFactory.SkillToItemObject();
            SkillConfigManager.Instance._troopSkillMap.Clear();
            if (!(SkillConfigManager.Instance._troopSkillMap != null && SkillConfigManager.Instance._troopSkillMap.Count > 1))
            {
                try
                {
                    string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                    SkillConfigManager.Instance.LoadFromXml(xmlPath);
                    InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] 技能配置加载完成！"));
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[New_ZZZF] 配置加载失败: {ex.Message}"));
                }
            }

            if (!_harmonyPatched)
            {
                _harmonyPatched = true;
                _harmony = new Harmony("New_ZZZF");
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Harmony 补丁已就绪（新游戏）。"));
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
        }

        public override void OnGameLoaded(Game game, object gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            ApplyZZZFSkillDisplayNames();
            CompositeSpellRegistry.LoadAndRegisterAll();
            SkillFactory.SkillToItemObject();
            SkillConfigManager.Instance._troopSkillMap.Clear();

            if (!_harmonyPatched)
            {
                _harmonyPatched = true;
                _harmony = new Harmony("New_ZZZF");
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Harmony 补丁已就绪（读档后）。"));
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new SkillSystemBehavior());
            mission.AddMissionBehavior(new MountedSlashCameraMissionLogic());
            mission.AddMissionBehavior(new HeroChangeMissionBehavior());
            TacticalMapBootstrap.OnMissionStart(mission);
            mission.AddMissionBehavior(new AffixMissionBehavior());
            mission.AddMissionBehavior(new NewZZZF_MissionAgentStatusView());
        }

        protected override void OnSubModuleUnloaded()
        {
            TacticalMapHtmlUi.Instance.Dispose();
            CustomSkillHtmlUi.Instance.Dispose();
            base.OnSubModuleUnloaded();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Mod已启动！"));
        }

        private static void ApplyZZZFSkillDisplayNames()
        {
            RenameSkill(DefaultSkills.OneHanded, "{=ZZZF_SwordMastery}Sword Mastery", "{=ZZZF_SwordMastery_Desc}Sword Mastery");
            RenameSkill(DefaultSkills.TwoHanded, "{=ZZZF_AxeMastery}Axe Mastery", "{=ZZZF_AxeMastery_Desc}Axe Mastery");
            RenameSkill(DefaultSkills.Polearm, "{=ZZZF_HammerMastery}Hammer Mastery", "{=ZZZF_HammerMastery_Desc}Hammer Mastery");
            RenameSkill(DefaultSkills.Throwing, "{=ZZZF_SpearMastery}Spear Mastery", "{=ZZZF_SpearMastery_Desc}Spear Mastery");
        }

        private static void RenameSkill(SkillObject skill, string localizedName, string localizedDescription)
        {
            skill.Initialize(new TextObject(localizedName), new TextObject(localizedDescription));
        }

        protected override void InitializeGameStarter(Game game, IGameStarter gameStarterObject)
        {
            gameStarterObject.AddModel(new WOW_DefaultStrikeMagnitudeModel());
            gameStarterObject.AddModel(new WOW_CustomBattleAgentStatCalculateModel());
            gameStarterObject.AddModel(new WOW_CustomAgentApplyDamageModel());
            if (game.GameType is Campaign)
            {
                gameStarterObject.AddModel(new WOW_SandboxAgentApplyDamageModel());
                gameStarterObject.AddModel(new WOW_SandboxStrikeMagnitudeModel());
                gameStarterObject.AddModel(new ZZZF_SandboxAgentStatCalculateModel());
                gameStarterObject.AddModel(new WOW_DefaultPartySpeedCalculatingModel());
                CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                campaignGameStarter.AddBehavior(new HeroSkillSaveCustomBehavior());
                campaignGameStarter.AddBehavior(new HeroChangeCampaignBehavior());
                campaignGameStarter.AddBehavior(new AffixCampaignBehavior());
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            CustomSkillHtmlUi.Instance.Tick(dt);

            if (Game.Current == null) return;

            bool shiftDown = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
            if (shiftDown && Input.IsKeyPressed(InputKey.M)
                && Campaign.Current != null
                && Mission.Current == null
                && !Game.Current.GameStateManager.ActiveState.IsMenuState)
            {
                if (CustomSkillHtmlUi.Instance.IsVisible)
                    CustomSkillHtmlUi.Instance.Close();
                else
                    CustomSkillHtmlUi.Instance.TryOpen();
            }

            if (Input.IsKeyPressed(InputKey.F11) && Campaign.Current != null
                && Mission.Current == null
                && !Game.Current.GameStateManager.ActiveState.IsMenuState
                && !(ScreenManager.TopScreen is New_ZZZF.ActionExplorer.ActionExplorerScreen))
            {
                New_ZZZF.ActionExplorer.ActionExplorerLauncher.TryOpen();
            }

            if (Input.IsKeyDown(InputKey.L))
            {
                SkillFactory.Refresh_skillRegistry();
                CompositeSpellRegistry.LoadAndRegisterAll();
                SkillFactory.SkillToItemObject();
                SkillConfigManager.Instance._troopSkillMap.Clear();
                if (!(SkillConfigManager.Instance._troopSkillMap != null && SkillConfigManager.Instance._troopSkillMap.Count > 1) && Mission.Current == null)
                {
                    try
                    {
                        string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                        SkillConfigManager.Instance.LoadFromXml(xmlPath);
                        InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] 技能配置加载完成！"));
                    }
                    catch (Exception ex)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[New_ZZZF] 配置加载失败: {ex.Message}"));
                    }
                }
                Dictionary<string, List<string>> troopSkillMap = new Dictionary<string, List<string>>();
                foreach (var item in SkillConfigManager.Instance._troopSkillMap)
                    troopSkillMap[item.Key] = SkillConfigManager.ToStringList(item.Value);
                foreach (var item in troopSkillMap)
                    SkillConfigManager.Instance._troopSkillMap[item.Key] = SkillConfigManager.ListToSkillSet(item.Value);
            }

            if (Campaign.Current != null && Mission.Current == null)
            {
                bool ctrlDown = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                if (ctrlDown && Input.IsKeyPressed(InputKey.F5)) AffixDebugHelper.GiveRandomAffixWeapon();
                if (ctrlDown && Input.IsKeyPressed(InputKey.F6)) AffixDebugHelper.GiveRandomAffixArmor();
                if (ctrlDown && Input.IsKeyPressed(InputKey.F7)) AffixDebugHelper.ListPlayerAffixItems();
                if (ctrlDown && Input.IsKeyPressed(InputKey.F8)) AffixDebugHelper.PrintSystemStatus();
                if (ctrlDown && Input.IsKeyPressed(InputKey.F9)) AffixDebugHelper.RerollRandomItemAffix();
            }
        }
    }
}
