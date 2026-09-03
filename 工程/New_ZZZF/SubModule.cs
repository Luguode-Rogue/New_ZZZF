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
using New_ZZZF.TacticalMap.Diagnostics;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using StoryMode.GameComponents.CampaignBehaviors;
using TaleWorlds.Localization;
using SandBox.GauntletUI.Missions;
using System.Collections.Generic;
using TaleWorlds.Engine.GauntletUI;
using New_ZZZF.GUI;
using BannerlordHtmlUI;

namespace New_ZZZF
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;
        private bool _harmonyPatched;
        private bool _tacticalMapToggleKeyWasDown;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            NewZZZFDiag.Load();   // 闪退排查：先读子系统开关（ModuleData/NewZZZF_Diag.xml）
            TacticalMapLog.Initialize();
            TacticalMapLog.Section("SUBMODULE LOAD");
            TacticalMapLog.Info("Assembly=" + typeof(SubModule).Assembly.Location);
            TacticalMapLog.Info("ModLogPath=" + TacticalMapLog.LogPath);

            if (NewZZZFDiag.TacticalMap)
            {
                try
                {
                    TacticalMapBootstrap.OnSubModuleLoad();
                    TacticalMapLog.Info("TacticalMapBootstrap.OnSubModuleLoad completed.");
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("TacticalMapBootstrap.OnSubModuleLoad failed.", ex);
                    throw;
                }

                try
                {
                    TacticalMapHtmlUi.Instance.InitializeOnFrameworkReady();
                    TacticalMapLog.Info("TacticalMapHtmlUi.InitializeOnFrameworkReady registered.");
                }
                catch (Exception ex)
                {
                    TacticalMapLog.Error("TacticalMapHtmlUi.InitializeOnFrameworkReady failed.", ex);
                    throw;
                }
            }

            if (NewZZZFDiag.CustomSkillHtmlUi)
            {
                CustomSkillHtmlUi.Instance.InitializeOnFrameworkReady();
                TacticalMapLog.Info("CustomSkill HtmlUi InitializeOnFrameworkReady registered.");
            }
            HtmlUiInputTraceLogger.Event("NEW_ZZZF_SUBMODULE_LOAD");
        }

        public override void OnNewGameCreated(Game game, object initializerObject)
        {
            base.OnNewGameCreated(game, initializerObject);
            if (NewZZZFDiag.SkillRegistry)
            {
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
            }

            if (NewZZZFDiag.HarmonyPatchAll && !_harmonyPatched)
            {
                _harmonyPatched = true;
                _harmony = new Harmony("New_ZZZF");
                try
                {
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                    InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Harmony 补丁已就绪（新游戏）。"));
                }
                catch (Exception ex)
                {
                    // 一条补丁签名不符会中断 PatchAll 并使其余补丁（含缴械延迟补丁）全部失效
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[New_ZZZF] Harmony 补丁应用失败: " + ex.Message, Colors.Red));
                }
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
        }

        public override void OnGameLoaded(Game game, object gameStarterObject)
        {
            base.OnGameLoaded(game, gameStarterObject);
            if (NewZZZFDiag.SkillRegistry)
            {
                ApplyZZZFSkillDisplayNames();
                CompositeSpellRegistry.LoadAndRegisterAll();
                SkillFactory.SkillToItemObject();
                SkillConfigManager.Instance._troopSkillMap.Clear();
            }

            if (NewZZZFDiag.HarmonyPatchAll && !_harmonyPatched)
            {
                _harmonyPatched = true;
                _harmony = new Harmony("New_ZZZF");
                try
                {
                    _harmony.PatchAll(Assembly.GetExecutingAssembly());
                    InformationManager.DisplayMessage(new InformationMessage("[New_ZZZF] Harmony 补丁已就绪（读档后）。"));
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[New_ZZZF] Harmony 补丁应用失败: " + ex.Message, Colors.Red));
                }
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            TacticalMapLog.Section("MISSION BEHAVIOR INITIALIZE");
            TacticalMapLog.Info("Mission=" + (mission == null ? "null" : mission.GetType().FullName));
            if (NewZZZFDiag.SkillSystemBehavior)
                mission.AddMissionBehavior(new SkillSystemBehavior());
            if (NewZZZFDiag.MountedSlashCamera)
                mission.AddMissionBehavior(new MountedSlashCameraMissionLogic());
            if (NewZZZFDiag.HeroChange)
                mission.AddMissionBehavior(new HeroChangeMissionBehavior());
            if (NewZZZFDiag.TacticalMap)
            {
                TacticalMapBootstrap.OnMissionStart(mission);
                TacticalMapLog.Info("TacticalMapBootstrap.OnMissionStart completed.");
            }
            if (NewZZZFDiag.Affix)
                mission.AddMissionBehavior(new AffixMissionBehavior());
            if (NewZZZFDiag.AgentStatusView)
                mission.AddMissionBehavior(new NewZZZF_MissionAgentStatusView());
        }

        protected override void OnSubModuleUnloaded()
        {
            HtmlUiInputTraceLogger.Event("NEW_ZZZF_SUBMODULE_UNLOAD_BEGIN");
            TacticalMapLog.Section("SUBMODULE UNLOAD");
            if (NewZZZFDiag.TacticalMap)
            {
                try { TacticalMapHtmlUi.Instance.Dispose(); }
                catch (Exception ex) { TacticalMapLog.Error("TacticalMapHtmlUi.Dispose failed.", ex); }
            }
            if (NewZZZFDiag.CustomSkillHtmlUi)
            {
                try { CustomSkillHtmlUi.Instance.Dispose(); }
                catch (Exception ex) { TacticalMapLog.Error("CustomSkill HtmlUI Dispose failed.", ex); }
            }
            HtmlUiInputTraceLogger.Event("NEW_ZZZF_SUBMODULE_UNLOAD_END");
            base.OnSubModuleUnloaded();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            TacticalMapLog.Info("OnBeforeInitialModuleScreenSetAsRoot.");
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
            if (NewZZZFDiag.DamageModels)
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
                }
            }
            if (game.GameType is Campaign)
            {
                CampaignGameStarter campaignGameStarter = gameStarterObject as CampaignGameStarter;
                if (NewZZZFDiag.HeroChange)
                {
                    campaignGameStarter.AddBehavior(new HeroSkillSaveCustomBehavior());
                    campaignGameStarter.AddBehavior(new HeroChangeCampaignBehavior());
                }
                if (NewZZZFDiag.Affix)
                    campaignGameStarter.AddBehavior(new AffixCampaignBehavior());
            }
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            // 延迟缴械的统一执行点：每帧必跑、主线程、不依赖任何 Behavior 挂载或 Harmony 补丁。
            // （Mark 发生在上一帧 Mission.Tick 的碰撞判定内，此处在其后的安全阶段执行 DropItem）
            DeferredDisarmExecutor.Execute(Mission.Current);
            if (NewZZZFDiag.CustomSkillHtmlUi)
                CustomSkillHtmlUi.Instance.Tick(dt);

            var game = Game.Current;
            var stateManager = game?.GameStateManager;
            var activeState = stateManager?.ActiveState;
            var customVisible = CustomSkillHtmlUi.Instance.IsVisible;
            var mPressed = Input.IsKeyPressed(InputKey.M);
            var shiftDown = Input.IsKeyDown(InputKey.LeftShift) || Input.IsKeyDown(InputKey.RightShift);
            var campaignAvailable = Campaign.Current != null;
            var missionActive = Mission.Current != null;
            var isMenuState = activeState?.IsMenuState ?? true;

            InputKey tacticalMapToggleKey = TacticalSettings.Instance.ToggleKey;
            bool tacticalMapToggleKeyDown = NewZZZFDiag.TacticalMap && missionActive && Input.IsKeyDown(tacticalMapToggleKey);
            bool tacticalMapTogglePressed = tacticalMapToggleKeyDown && !_tacticalMapToggleKeyWasDown;
            _tacticalMapToggleKeyWasDown = tacticalMapToggleKeyDown;

            // TacticalMap 的 N 键是 New_ZZZF 正式游戏热键。
            // 使用 KeyDown 上升沿，避免 IsKeyPressed 被其他输入读取点消费导致切换失效。
            if (tacticalMapTogglePressed && missionActive && !customVisible)
            {
                TacticalMapLog.Info("TacticalMap toggle key pressed: " + tacticalMapToggleKey);
                TacticalMapHtmlUi.Instance.ToggleInteractive();
                TacticalMapLog.Info("TacticalMap mode after toggle: " + TacticalMapHtmlUi.Instance.Mode);
            }

            if (mPressed || (Input.IsKeyDown(InputKey.M) && shiftDown))
            {
                HtmlUiInputTraceLogger.Event(
                    "NEW_ZZZF_M_GATE "
                    + "mPressed=" + mPressed
                    + " shiftDown=" + shiftDown
                    + " customVisible=" + customVisible
                    + " game=" + (game != null)
                    + " campaign=" + campaignAvailable
                    + " mission=" + missionActive
                    + " activeState=" + (activeState == null ? "<null>" : activeState.GetType().FullName)
                    + " isMenuState=" + isMenuState);
            }

            // 新 HTML 技能界面拥有全输入时，不处理 New_ZZZF 的其它全局热键。
            if (customVisible)
            {
                if (mPressed || shiftDown)
                    HtmlUiInputTraceLogger.Event("NEW_ZZZF_M_BLOCKED_BY_CUSTOM_SKILL_VISIBLE");
                return;
            }

            if (game == null) return;
            if (stateManager == null || activeState == null) return;

            if (Campaign.Current != null
                && Mission.Current == null
                && !stateManager.ActiveState.IsMenuState)
            {
                bool shiftMPressed = shiftDown && mPressed;
                bool normalMPressed = !shiftDown && mPressed;

                // M：新的 HTML 技能界面
                if (normalMPressed && NewZZZFDiag.CustomSkillHtmlUi)
                {
                    HtmlUiInputTraceLogger.Event("NEW_ZZZF_M_ACCEPTED_OPEN_HTML");
                    if (ScreenManager.TopScreen is CustomSkillScreen)
                        ScreenManager.PopScreen();
                    CustomSkillHtmlUi.Instance.TryOpen();
                    return;
                }

                // Shift+M：旧的 Gauntlet 技能界面
                if (shiftMPressed && NewZZZFDiag.CustomSkillHtmlUi)
                {
                    HtmlUiInputTraceLogger.Event("NEW_ZZZF_SHIFT_M_ACCEPTED_OPEN_GAUNTLET");
                    if (CustomSkillHtmlUi.Instance.IsVisible)
                        CustomSkillHtmlUi.Instance.Close();
                    if (!(ScreenManager.TopScreen is CustomSkillScreen))
                        ScreenManager.PushScreen(new CustomSkillScreen());
                    return;
                }

                if (Input.IsKeyDown(InputKey.L) && NewZZZFDiag.SkillRegistry)
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

                if (Campaign.Current != null && Mission.Current == null && NewZZZFDiag.Affix)
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
}
