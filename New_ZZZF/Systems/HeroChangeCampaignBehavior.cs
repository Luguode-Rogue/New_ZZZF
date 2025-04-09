
using System;
using global::TaleWorlds.CampaignSystem.GameMenus;
using global::TaleWorlds.CampaignSystem.Party;
using global::TaleWorlds.CampaignSystem.Roster;
using global::TaleWorlds.CampaignSystem;
using global::TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{

    public class HeroChangeCampaignBehavior : CampaignBehaviorBase
    {
        [SaveableProperty(2)]
        public BasicCharacterObject PlayerCharacter { get; set; }
        [SaveableProperty(3)]
        public CharacterObject ChooseHero { get; set; }
        public static HeroChangeCampaignBehavior currecct;
        public HeroChangeCampaignBehavior() { currecct = this; }
        public void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
        {
            this.AddGameMenus(campaignGameStarter);
        }

        // Token: 0x0600374D RID: 14157 RVA: 0x000F9E81 File Offset: 0x000F8081
        public void OnGameLoaded(CampaignGameStarter campaignGameStarter)
        {
            this.AddGameMenus(campaignGameStarter);
        }
        protected void AddGameMenus(CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddGameMenuOption("hideout_place", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
            campaignGameStarter.AddGameMenuOption("continue_siege_after_attack", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
            campaignGameStarter.AddGameMenuOption("encounter_interrupted", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
            campaignGameStarter.AddGameMenuOption("army_encounter", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
            campaignGameStarter.AddGameMenuOption("encounter", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
            campaignGameStarter.AddGameMenuOption("join_encounter", "ChooseHero", "{=xx}ChooseHero",
                new GameMenuOption.OnConditionDelegate(this.retTrue),
                new GameMenuOption.OnConsequenceDelegate(this.ChooseUseAgent),
                false, -1, false, null);
        }
        private void ChooseUseAgent(MenuCallbackArgs args)
        {
            // 1. 获取玩家在藏身处任务中允许的最大部队人数（由 BanditDensityModel 动态计算）
            int playerMaximumTroopCountForHideoutMission = 1;

            // 2. 创建一个临时的空部队花名册，用于存储玩家的初始选择
            TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();

            // 6. 打开部队选择界面，允许玩家调整出战部队
            args.MenuContext.OpenTroopSelection(
                MobileParty.MainParty.MemberRoster, // 玩家当前的完整部队花名册
                troopRoster, // 初始选中的部队（最强且优先级高的士兵）
                new Func<CharacterObject, bool>(this.CanChangeStatusOfTroop), // 判断角色是否可选的委托
                new Action<TroopRoster>(this.OnTroopRosterManageDone), // 确认选择后的回调
                1, // 最大可选人数
                1 // 最小可选人数（至少1人）
            );
        }
        private void OnTroopRosterManageDone(TroopRoster hideoutTroops)
        {
            //待实现
            PlayerCharacter = Game.Current.PlayerTroop;
            ChooseHero = hideoutTroops.GetManAtIndexFromFlattenedRosterWithFilter(0, true, false);
            Game.Current.PlayerTroop = ChooseHero;

            //if (PlayerEncounter.IsActive)
            //{
            //    PlayerEncounter.LeaveEncounter = false;
            //}
            //else
            //{
            //    PlayerEncounter.Start();
            //    PlayerEncounter.Current.SetupFields(PartyBase.MainParty, Settlement.CurrentSettlement.Party);
            //}
            //if (PlayerEncounter.Battle == null)
            //{
            //    PlayerEncounter.StartBattle();
            //    PlayerEncounter.Update();
            //}
            //CampaignMission.OpenHideoutBattleMission(Settlement.CurrentSettlement.Hideout.SceneName, (hideoutTroops != null) ? hideoutTroops.ToFlattenedRoster() : null);
        }
        private bool CanChangeStatusOfTroop(CharacterObject character)
        {
            return character.IsHero;// !character.IsPlayerCharacter && !character.IsNotTransferableInHideouts;
        }
        private bool retTrue(MenuCallbackArgs args)
        {
            return true;
        }
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
        }

        public override void SyncData(IDataStore dataStore)
        {

        }
    }
    public class HeroChangeMissionBehavior : MissionLogic
    {
        protected override void OnEndMission()
        {
            base.OnEndMission();
            if (HeroChangeCampaignBehavior.currecct!=null&&HeroChangeCampaignBehavior.currecct.PlayerCharacter != null && HeroChangeCampaignBehavior.currecct.PlayerCharacter != HeroChangeCampaignBehavior.currecct.ChooseHero)
            {
                Game.Current.PlayerTroop = HeroChangeCampaignBehavior.currecct.PlayerCharacter;
                HeroChangeCampaignBehavior.currecct.PlayerCharacter = null;
                HeroChangeCampaignBehavior.currecct.ChooseHero = null;
            }
        }
    }
    public class GameMenuManagerPatch
    {
        [HarmonyPatch(typeof(GameMenuManager), "GetLeaveMenuOption", new[] { typeof(MenuContext) })]
        public static class GetLeaveMenuOptionPatch
        {
            // 使用前缀补丁（Prefix）可以完全覆盖原方法逻辑
            // 若需要修改返回值，可以改用后缀补丁（Postfix）并操作 __result
            public static bool Prefix(
                GameMenuManager __instance,   // 原方法所属的实例
                MenuContext menuContext,      // 原方法参数
                ref GameMenuOption __result)  // 用于存储/修改返回值的引用
            {
                if (HeroChangeCampaignBehavior.currecct.PlayerCharacter != null && HeroChangeCampaignBehavior.currecct.PlayerCharacter != HeroChangeCampaignBehavior.currecct.ChooseHero)
                {
                    Game.Current.PlayerTroop = HeroChangeCampaignBehavior.currecct.PlayerCharacter;
                    HeroChangeCampaignBehavior.currecct.PlayerCharacter = null;
                    HeroChangeCampaignBehavior.currecct.ChooseHero = null;
                }


                // 这里可以添加自定义逻辑
                // 示例：强制返回一个自定义的离开选项
                // __result = new GameMenuOption(...);
                // return false; // 跳过原方法执行

                // 若需要保持原方法逻辑并修改结果，改用 Postfix：
                // 保持原方法执行
                return true;
            }

            // 或者使用后缀补丁获取/修改返回值
            public static void Postfix(
                GameMenuManager __instance,
                MenuContext menuContext,
                ref GameMenuOption __result)
            {
                // 在此处可以访问和修改 __result
                if (__result != null)
                {
                    // 示例：修改离开菜单选项的文本
                    // __result.Text = "Custom Leave Text";
                }
            }
        }
    }
}
