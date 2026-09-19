using System;
using HarmonyLib;
using Helpers;
using SandBox.Missions.MissionLogics.Hideout;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.SaveSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 管理一次遭遇内的临时战场操控者选择。
    /// 选择阶段不修改全局 PlayerTroop；仅在战斗任务创建前切换，任务结束后恢复。
    /// </summary>
    public sealed class HeroChangeCampaignBehavior : CampaignBehaviorBase
    {
        private enum SelectionState
        {
            None,
            Pending,
            AppliedToMission
        }

        public static HeroChangeCampaignBehavior Current { get; private set; }

        private SelectionState _state;
        private CharacterObject _selectedHero;
        private BasicCharacterObject _originalPlayerTroop;
        private MapEvent _expectedMapEvent;
        private Settlement _expectedSettlement;

        public HeroChangeCampaignBehavior()
        {
            Current = this;
        }

        internal bool HasHealthySelectedHero
        {
            get
            {
                return _state == SelectionState.Pending &&
                       IsEligibleHero(_selectedHero) &&
                       IsSameEncounter();
            }
        }

        internal CharacterObject SelectedHero
        {
            get { return HasHealthySelectedHero ? _selectedHero : null; }
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.BeforeMissionOpenedEvent.AddNonSerializedListener(this, OnBeforeMissionOpened);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // 战前选择是一次性会话，禁止写入存档，避免读档后 PlayerTroop 身份污染。
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            ResetSession(true);
            AddGameMenus(starter);
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            ResetSession(true);
            AddGameMenus(starter);
        }

        private void AddGameMenus(CampaignGameStarter starter)
        {
            AddSelectionOption(starter, "encounter");
            AddSelectionOption(starter, "join_encounter");
            AddSelectionOption(starter, "army_encounter");
            AddSelectionOption(starter, "hideout_place");
            AddSelectionOption(starter, "hideout_after_wait");
            AddSelectionOption(starter, "menu_siege_strategies");
            AddSelectionOption(starter, "assault_town");
        }

        private void AddSelectionOption(CampaignGameStarter starter, string menuId)
        {
            starter.AddGameMenuOption(
                menuId,
                "new_zzzf_choose_battle_hero",
                "{=new_zzzf_choose_battle_hero}选择本场操控英雄",
                SelectionOptionCondition,
                OpenHeroSelection,
                false,
                -1,
                false,
                null);
        }

        private bool SelectionOptionCondition(MenuCallbackArgs args)
        {
            if (_state == SelectionState.AppliedToMission)
                return false;

            if (_state == SelectionState.Pending && !IsSameEncounter())
                ResetSession(false);

            int eligibleCount = 0;
            TroopRoster roster = MobileParty.MainParty?.MemberRoster;
            if (roster != null)
            {
                foreach (TroopRosterElement element in roster.GetTroopRoster())
                {
                    if (IsEligibleHero(element.Character))
                        eligibleCount++;
                }
            }

            if (eligibleCount == 0)
                return false;

            if (_state == SelectionState.Pending && IsEligibleHero(_selectedHero))
            {
                TextObject text = new TextObject("{=new_zzzf_choose_battle_hero_selected}本场操控：{HERO_NAME}（点击更换）");
                text.SetTextVariable("HERO_NAME", _selectedHero.Name);
                args.Text = text;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        private void OpenHeroSelection(MenuCallbackArgs args)
        {
            TroopRoster fullRoster = MobileParty.MainParty?.MemberRoster;
            if (fullRoster == null)
                return;

            TroopRoster initialSelection = TroopRoster.CreateDummyTroopRoster();
            if (_state == SelectionState.Pending && IsEligibleHero(_selectedHero))
                initialSelection.AddToCounts(_selectedHero, 1);

            args.MenuContext.OpenTroopSelection(
                fullRoster,
                initialSelection,
                null,
                IsEligibleHero,
                OnHeroSelectionDone,
                1,
                1);
        }

        private void OnHeroSelectionDone(TroopRoster selectedRoster)
        {
            if (selectedRoster == null || selectedRoster.TotalManCount == 0)
                return;

            CharacterObject selected = selectedRoster.GetElementCopyAtIndex(0).Character;
            if (!IsEligibleHero(selected))
            {
                ResetSession(false);
                return;
            }

            _selectedHero = selected;
            _expectedMapEvent = MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle;
            _expectedSettlement = Settlement.CurrentSettlement;
            _state = SelectionState.Pending;

            Campaign.Current?.CurrentMenuContext?.Refresh();
        }

        private void OnBeforeMissionOpened()
        {
            if (_state != SelectionState.Pending)
                return;

            if (!IsEligibleHero(_selectedHero) || !IsSameEncounter())
            {
                ResetSession(false);
                return;
            }

            _originalPlayerTroop = Game.Current?.PlayerTroop;
            if (_originalPlayerTroop == null)
            {
                ResetSession(false);
                return;
            }

            Game.Current.PlayerTroop = _selectedHero;
            _state = SelectionState.AppliedToMission;
        }

        private void OnMissionEnded(IMission mission)
        {
            RestorePlayerTroop();
        }

        internal void ValidateOpenedMission(Mission mission)
        {
            if (_state != SelectionState.AppliedToMission)
                return;

            bool isBattleMission = mission != null &&
                (mission.GetMissionBehavior<IMissionAgentSpawnLogic>() != null ||
                 mission.GetMissionBehavior<HideoutAmbushMissionController>() != null);

            if (!isBattleMission)
                RestorePlayerTroop();
        }

        internal void PrepareHideoutRoster(TroopRoster hideoutTroops, bool isDirectAssault)
        {
            CharacterObject selected = SelectedHero;
            if (hideoutTroops == null || selected == null)
                return;

            int selectedCount = hideoutTroops.GetTroopCount(selected);

            // 潜入任务由 HideoutAmbushMissionController 单独 SpawnPlayer。
            // 同行名单中必须移除所选英雄，否则会同时被供应器和 SpawnPlayer 各生成一次。
            if (!isDirectAssault)
            {
                if (selectedCount > 0)
                    hideoutTroops.AddToCounts(selected, -selectedCount);
                return;
            }

            // 强攻任务完全依赖受限花名册。所选操控英雄必须实际占用一个出战名额。
            if (selectedCount > 0)
                return;

            int maximumCount = Campaign.Current.Models.BanditDensityModel
                .GetMaximumTroopCountForHideoutMission(MobileParty.MainParty, true);

            EnsureSelectedHeroInRestrictedRoster(hideoutTroops, selected, maximumCount);
        }

        internal void PrepareLordsHallRoster(TroopRoster selectedTroops)
        {
            CharacterObject selected = SelectedHero;
            if (selectedTroops == null || selected == null)
                return;

            int maximumCount = Campaign.Current.Models.SiegeLordsHallFightModel.MaxAttackerSideTroopCount;
            EnsureSelectedHeroInRestrictedRoster(selectedTroops, selected, maximumCount);
        }

        private static void EnsureSelectedHeroInRestrictedRoster(
            TroopRoster restrictedRoster,
            CharacterObject selected,
            int maximumCount)
        {
            if (restrictedRoster.GetTroopCount(selected) > 0)
                return;

            if (restrictedRoster.TotalManCount >= maximumCount)
            {
                CharacterObject replacement = FindRestrictedRosterReplacement(restrictedRoster, selected);
                if (replacement != null)
                    restrictedRoster.AddToCounts(replacement, -1);
            }

            restrictedRoster.AddToCounts(selected, 1);
        }

        internal void RestorePlayerTroop()
        {
            if (_state == SelectionState.AppliedToMission && _originalPlayerTroop != null && Game.Current != null)
                Game.Current.PlayerTroop = _originalPlayerTroop;

            ResetSession(false);
        }

        private void ResetSession(bool restorePlayerTroop)
        {
            if (restorePlayerTroop && _state == SelectionState.AppliedToMission &&
                _originalPlayerTroop != null && Game.Current != null)
            {
                Game.Current.PlayerTroop = _originalPlayerTroop;
            }

            _state = SelectionState.None;
            _selectedHero = null;
            _originalPlayerTroop = null;
            _expectedMapEvent = null;
            _expectedSettlement = null;
        }

        private bool IsSameEncounter()
        {
            MapEvent currentMapEvent = MapEvent.PlayerMapEvent ?? PlayerEncounter.Battle;
            if (_expectedMapEvent != null)
                return currentMapEvent == _expectedMapEvent;

            if (_expectedSettlement != null)
                return Settlement.CurrentSettlement == _expectedSettlement;

            return currentMapEvent != null;
        }

        private static bool IsEligibleHero(CharacterObject character)
        {
            if (character == null || !character.IsHero || character.HeroObject == null)
                return false;

            Hero hero = character.HeroObject;
            if (hero.IsDead || hero.IsPrisoner || hero.IsWounded)
                return false;

            MobileParty mainParty = MobileParty.MainParty;
            return mainParty != null && mainParty.MemberRoster.GetTroopCount(character) > 0;
        }

        private static CharacterObject FindRestrictedRosterReplacement(
            TroopRoster restrictedRoster,
            CharacterObject selected)
        {
            CharacterObject fallback = null;
            foreach (TroopRosterElement element in restrictedRoster.GetTroopRoster())
            {
                CharacterObject character = element.Character;
                if (character == null || character == selected || element.Number <= 0)
                    continue;

                // 优先替换普通士兵，尽量保留原主角和其他已选英雄作为 AI 队友。
                if (!character.IsHero)
                    return character;

                if (fallback == null && !character.IsPlayerCharacter)
                    fallback = character;
            }

            if (fallback != null)
                return fallback;

            foreach (TroopRosterElement element in restrictedRoster.GetTroopRoster())
            {
                if (element.Character != null && element.Character != selected && element.Number > 0)
                    return element.Character;
            }

            return null;
        }
    }

    /// <summary>
    /// 任务侧只负责确认此次打开的是战斗任务，并在所有退出路径上幂等恢复玩家身份。
    /// 原主角没有从花名册移除，因此健康时仍会作为 AI 英雄正常上场。
    /// </summary>
    public sealed class HeroChangeMissionBehavior : MissionLogic
    {
        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            HeroChangeCampaignBehavior.Current?.ValidateOpenedMission(Mission);
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();
            HeroChangeCampaignBehavior.Current?.RestorePlayerTroop();
        }

        public override void OnRemoveBehavior()
        {
            HeroChangeCampaignBehavior.Current?.RestorePlayerTroop();
            base.OnRemoveBehavior();
        }
    }

    /// <summary>
    /// 原版遭遇按钮硬编码检查 Hero.MainHero.IsWounded。
    /// 已选择健康英雄时，仅撤销由“主角受伤”这一条规则造成的禁用；其它禁用原因保持原样。
    /// </summary>
    [HarmonyPatch(typeof(MenuHelper), nameof(MenuHelper.EncounterAttackCondition))]
    internal static class HeroChangeEncounterAttackConditionPatch
    {
        private static readonly TextObject WoundedTooltip = new TextObject("{=UL8za0AO}You are wounded.");

        [HarmonyPostfix]
        private static void Postfix(MenuCallbackArgs args)
        {
            if (CanOverrideWoundedRestriction(args, WoundedTooltip))
                args.IsEnabled = true;
        }

        internal static bool CanOverrideWoundedRestriction(MenuCallbackArgs args, TextObject woundedTooltip)
        {
            return HeroChangeCampaignBehavior.Current?.HasHealthySelectedHero == true &&
                   Hero.MainHero != null &&
                   Hero.MainHero.IsWounded &&
                   args?.Tooltip != null &&
                   args.Tooltip.HasSameValue(woundedTooltip);
        }
    }

    [HarmonyPatch(typeof(HideoutCampaignBehavior), "game_menu_hideout_sneak_in_on_condition")]
    internal static class HeroChangeHideoutSneakInConditionPatch
    {
        private static readonly TextObject WoundedTooltip =
            new TextObject("{=pM9GOxrV}You are wounded, you can't sneak in!");

        [HarmonyPostfix]
        private static void Postfix(MenuCallbackArgs args)
        {
            if (HeroChangeEncounterAttackConditionPatch.CanOverrideWoundedRestriction(args, WoundedTooltip))
                args.IsEnabled = true;
        }
    }

    [HarmonyPatch(typeof(HideoutCampaignBehavior), "game_menu_assault_hideout_parties_on_condition")]
    internal static class HeroChangeHideoutAssaultConditionPatch
    {
        private static readonly TextObject WoundedTooltip =
            new TextObject("{=ZCKKVRjT}You are wounded, you can't assault the hideout!");

        [HarmonyPostfix]
        private static void Postfix(MenuCallbackArgs args)
        {
            if (HeroChangeEncounterAttackConditionPatch.CanOverrideWoundedRestriction(args, WoundedTooltip))
                args.IsEnabled = true;
        }
    }

    [HarmonyPatch(typeof(HideoutCampaignBehavior), "OnTroopRosterManageDone")]
    internal static class HeroChangeHideoutRosterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TroopRoster hideoutTroops, bool isDirectAssault)
        {
            HeroChangeCampaignBehavior.Current?.PrepareHideoutRoster(hideoutTroops, isDirectAssault);
        }
    }

    [HarmonyPatch(typeof(MenuHelper), "LordsHallTroopRosterManageDone")]
    internal static class HeroChangeLordsHallRosterPatch
    {
        [HarmonyPrefix]
        private static void Prefix(TroopRoster selectedTroops)
        {
            HeroChangeCampaignBehavior.Current?.PrepareLordsHallRoster(selectedTroops);
        }
    }

    [HarmonyPatch(typeof(SiegeEventCampaignBehavior), "game_menu_siege_strategies_lead_assault_on_condition")]
    internal static class HeroChangeSiegeAssaultConditionPatch
    {
        private static readonly TextObject WoundedTooltip =
            new TextObject("{=gzYuWR28}You are wounded, and in no condition to lead an assault.");

        [HarmonyPostfix]
        private static void Postfix(MenuCallbackArgs args)
        {
            if (HeroChangeEncounterAttackConditionPatch.CanOverrideWoundedRestriction(args, WoundedTooltip))
                args.IsEnabled = true;
        }
    }

    [HarmonyPatch(typeof(SiegeAmbushCampaignBehavior), "menu_siege_strategies_ambush_condition")]
    internal static class HeroChangeSiegeAmbushConditionPatch
    {
        private static readonly TextObject WoundedTooltip =
            new TextObject("{=pQaQW1As}You cannot ambush right now due to your wounds.");

        [HarmonyPostfix]
        private static void Postfix(MenuCallbackArgs args)
        {
            if (HeroChangeEncounterAttackConditionPatch.CanOverrideWoundedRestriction(args, WoundedTooltip))
                args.IsEnabled = true;
        }
    }
}
