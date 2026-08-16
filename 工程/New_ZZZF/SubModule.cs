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
        // Harmony 实例延后到读档/新游戏加载完成后再创建并 PatchAll
        private Harmony _harmony;
        private bool _harmonyPatched;


        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            TacticalMapBootstrap.OnSubModuleLoad();
            CustomSkillHtmlUi.Instance.InitializeOnFrameworkReady();

            // 动作浏览器（ActionExplorer）使用 SceneLayer 渲染 3D 预览，无需注册自定义 Widget
        }
        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            ApplyZZZFSkillDisplayNames();
            InformationManager.DisplayMessage(new InformationMessage("Save_SkillConfigManager.Instance._troopSkillMap", Colors.Red));
            // 先把自创组合法术重建回注册表，再生成 ItemObject，
            // 否则自创法术没有 Item，且存档里的 SkillID 会查不到而退化成 NullSkill
            CompositeSpellRegistry.LoadAndRegisterAll();
            SkillFactory.SkillToItemObject();
            SkillConfigManager.Instance._troopSkillMap.Clear();
            if (!(SkillConfigManager.Instance._troopSkillMap != null && SkillConfigManager.Instance._troopSkillMap.Count > 1))
            {
                try
                {
                    string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                    SkillConfigManager.Instance.LoadFromXml(xmlPath);

                    InformationManager.DisplayMessage(new InformationMessage(
                        "[New_ZZZF] 技能配置加载完成！"));
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"[New_ZZZF] 配置加载失败: {ex.Message}"));
                }
            }

            // 新游戏同样延后到此时机执行 Harmony 织入（与读档路径共用守卫，仅执行一次）
            if (!_harmonyPatched)
            {
                _harmonyPatched = true;
                _harmony = new Harmony("New_ZZZF");
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Harmony 补丁已就绪（新游戏）。"));
            }
        }
        protected override  void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

        }
        public override void OnGameLoaded(Game game, object gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            ApplyZZZFSkillDisplayNames();
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                
            }
            CompositeSpellRegistry.LoadAndRegisterAll();
            SkillFactory.SkillToItemObject();

            SkillConfigManager.Instance._troopSkillMap.Clear();

            // 把 Harmony 织入（含 ActionExplorer 的全部 patch）推迟到“读档/新游戏真正加载完成”之后，
            // 避免启动/读档初始化阶段就触碰未就绪的资源与场景。OnGameLoaded 本身即在存档加载完成后触发。
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

            // 添加自定义的 MissionBehavior 到当前任务
            mission.AddMissionBehavior(new SkillSystemBehavior());
            mission.AddMissionBehavior(new MountedSlashCameraMissionLogic());
            mission.AddMissionBehavior(new HeroChangeMissionBehavior());
            TacticalMapBootstrap.OnMissionStart(mission);
            mission.AddMissionBehavior(new AffixMissionBehavior());
            mission.AddMissionBehavior(new NewZZZF_MissionAgentStatusView());
            // 调试日志
            InformationManager.DisplayMessage(new InformationMessage(
                "[New_ZZZF] 技能系统已激活！"));

        }
        protected override void OnSubModuleUnloaded()
        {
            CustomSkillHtmlUi.Instance.Dispose();
            base.OnSubModuleUnloaded();

        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(new InformationMessage(
                "[New_ZZZF] Mod已启动！"));
        }

        /// <summary>
        /// 仅替换四个战斗专精 SkillObject 的显示名称（剑/斧/锤/矛精通），
        /// 不改 SkillObject 本身（StringId/数值/Perk/经验/存档均不受影响）。
        /// </summary>
        private static void ApplyZZZFSkillDisplayNames()
        {
            RenameSkill(DefaultSkills.OneHanded, "{=ZZZF_SwordMastery}Sword Mastery",   "{=ZZZF_SwordMastery_Desc}Sword Mastery");
            RenameSkill(DefaultSkills.TwoHanded, "{=ZZZF_AxeMastery}Axe Mastery",        "{=ZZZF_AxeMastery_Desc}Axe Mastery");
            RenameSkill(DefaultSkills.Polearm,   "{=ZZZF_HammerMastery}Hammer Mastery",  "{=ZZZF_HammerMastery_Desc}Hammer Mastery");
            RenameSkill(DefaultSkills.Throwing,  "{=ZZZF_SpearMastery}Spear Mastery",    "{=ZZZF_SpearMastery_Desc}Spear Mastery");
        }

        private static void RenameSkill(SkillObject skill, string localizedName, string localizedDescription)
        {
            // 当前 Bannerlord 版本 SkillObject.Initialize 仅有
            //   Initialize(TextObject name, TextObject description)
            //   Initialize(TextObject name, TextObject description, CharacterAttribute[] attributes)
            //   Initialize()
            // 这里用两参重载，同时替换 Name 与 Description，不触碰 StringId 等其它字段。
            skill.Initialize(new TextObject(localizedName), new TextObject(localizedDescription));
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

            // 注：Harmony.PatchAll 已延后到“读档/新游戏真正加载完成”后执行（见 OnGameLoaded / OnAllLoaded），
            // 目的是把本模组（含 ActionExplorer）的织入时机推迟到资源/场景就绪之后，缩短启动期卡顿与崩溃风险。
            if (game.GameType is Campaign)
            {
                //战役里使用的代码
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
            if (Game.Current != null)
            {
                // ---- 旧版技能界面（M键，基于物品系统）【已暂停】 ----
                //if (Input.IsKeyPressed(InputKey.M) && !Game.Current.GameStateManager.ActiveState.IsMenuState)
                //{
                //    if (Hero.MainHero != null)
                //    {
                //        Hero mainHero = Hero.MainHero;
                //        if (mainHero != null && !mainHero.IsDead)
                //        {
                //            SkillInventoryScreenHelper.OpenScreenAsInventory(null);
                //        }
                //    }
                //}

                // ---- 新版技能界面（M键，纯MVVM，不依赖物品系统） ----
                if (Input.IsKeyPressed(InputKey.M) && Campaign.Current != null
                    && Mission.Current == null
                    && !Game.Current.GameStateManager.ActiveState.IsMenuState
                    && !(ScreenManager.TopScreen is CustomSkillScreen))
                {
                    ScreenManager.PushScreen(new CustomSkillScreen());
                }

                // ---- M1：正式 4×5 ActionExplorer UI（F11 打开） ----
                // 不创建 Scene / Agent / 模型 / 动作，仅 UI 骨架验证。
                if (Input.IsKeyPressed(InputKey.F11) && Campaign.Current != null
                    && Mission.Current == null
                    && !Game.Current.GameStateManager.ActiveState.IsMenuState
                    && !(ScreenManager.TopScreen is New_ZZZF.ActionExplorer.ActionExplorerScreen))
                {
                    New_ZZZF.ActionExplorer.ActionExplorerLauncher.TryOpen();
                }

                if (Input.IsKeyDown(InputKey.L))
                {
                    MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                    SkillFactory.Refresh_skillRegistry();
                    CompositeSpellRegistry.LoadAndRegisterAll();
                    SkillFactory.SkillToItemObject();
                    SkillConfigManager.Instance._troopSkillMap.Clear();
                    if (!(SkillConfigManager.Instance._troopSkillMap != null && SkillConfigManager.Instance._troopSkillMap.Count > 1)&&Mission.Current==null)
                    {
                        try
                        {
                            string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                            SkillConfigManager.Instance.LoadFromXml(xmlPath);

                            InformationManager.DisplayMessage(new InformationMessage(
                                "[New_ZZZF] 技能配置加载完成！"));
                        }
                        catch (Exception ex)
                        {
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"[New_ZZZF] 配置加载失败: {ex.Message}"));
                        }
                    }
                    Dictionary<string, List<string>> _troopSkillMap = new Dictionary<string, List<string>>();
                    foreach (var item in SkillConfigManager.Instance._troopSkillMap)
                    {
                        _troopSkillMap[item.Key] = SkillConfigManager.ToStringList(item.Value);
                    }
                    foreach (var item in _troopSkillMap)
                    {
                        SkillConfigManager.Instance._troopSkillMap[item.Key] = SkillConfigManager.ListToSkillSet(item.Value);
                    }

                }

                // ---- 词缀系统测试快捷键（Ctrl+F5~F8） ----
                if (Campaign.Current != null && Mission.Current == null)
                {
                    bool ctrlDown = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);

                    if (ctrlDown && Input.IsKeyPressed(InputKey.F5))
                    {
                        AffixDebugHelper.GiveRandomAffixWeapon();
                    }
                    if (ctrlDown && Input.IsKeyPressed(InputKey.F6))
                    {
                        AffixDebugHelper.GiveRandomAffixArmor();
                    }
                    if (ctrlDown && Input.IsKeyPressed(InputKey.F7))
                    {
                        AffixDebugHelper.ListPlayerAffixItems();
                    }
                    if (ctrlDown && Input.IsKeyPressed(InputKey.F8))
                    {
                        AffixDebugHelper.PrintSystemStatus();
                    }
                    if (ctrlDown && Input.IsKeyPressed(InputKey.F9))
                    {
                        AffixDebugHelper.RerollRandomItemAffix();
                    }

                    // ---- LegacyWorld MCM 按钮已替代原 Ctrl+F10/F11 快捷键 ----
                    // 手动导出/应用改为 MCM 菜单按钮触发（OnTick 消费）
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
//战役模式支持：通过 OnGameStart 可扩展战役模式逻辑（如队伍成员管理）。