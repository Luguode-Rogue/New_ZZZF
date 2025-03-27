using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Core.ViewModelCollection.Tutorial;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static SandBox.ViewModelCollection.GameOver.StatItem;

namespace New_ZZZF
{
    public class SPSkillVM : ViewModel, IInventoryStateHandler
    {

        public SPSkillVM(InventoryLogic inventoryLogic, bool isInCivilianModeByDefault, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags, string fiveStackShortcutkeyText, string entireStackShortcutkeyText)
        {
            this._usageType = SkillInventoryManager.Instance.CurrentMode;
            this._inventoryLogic = inventoryLogic;
            this._viewDataTracker = Campaign.Current.GetCampaignBehavior<IViewDataTracker>();
            this._getItemUsageSetFlags = getItemUsageSetFlags;
            this._fiveStackShortcutkeyText = fiveStackShortcutkeyText;
            this._entireStackShortcutkeyText = entireStackShortcutkeyText;
            this._filters = new Dictionary<SPInventoryVM.Filters, List<int>>();
            this._filters.Add(SPInventoryVM.Filters.All, this._everyItemType);
            this._filters.Add(SPInventoryVM.Filters.Weapons, this._weaponItemTypes);
            this._filters.Add(SPInventoryVM.Filters.Armors, this._armorItemTypes);
            this._filters.Add(SPInventoryVM.Filters.Mounts, this._mountItemTypes);
            this._filters.Add(SPInventoryVM.Filters.ShieldsAndRanged, this._shieldAndRangedItemTypes);
            this._filters.Add(SPInventoryVM.Filters.Miscellaneous, this._miscellaneousItemTypes);
            this._equipAfterTransferStack = new Stack<SPSkillItemVM>();
            this._comparedItemList = new List<ItemVM>();
            this._donationMaxShareableXp = MobilePartyHelper.GetMaximumXpAmountPartyCanGet(MobileParty.MainParty);
            MBTextManager.SetTextVariable("XP_DONATION_LIMIT", this._donationMaxShareableXp);
            if (this._inventoryLogic != null)
            {
                this._currentCharacter = this._inventoryLogic.InitialEquipmentCharacter;
                this._isTrading = inventoryLogic.IsTrading;
                this._inventoryLogic.AfterReset += this.AfterReset;
                InventoryLogic inventoryLogic2 = this._inventoryLogic;
                inventoryLogic2.TotalAmountChange = (Action<int>)Delegate.Combine(inventoryLogic2.TotalAmountChange, new Action<int>(this.OnTotalAmountChange));
                InventoryLogic inventoryLogic3 = this._inventoryLogic;
                inventoryLogic3.DonationXpChange = (Action)Delegate.Combine(inventoryLogic3.DonationXpChange, new Action(this.OnDonationXpChange));
                this._inventoryLogic.AfterTransfer += this.AfterTransfer;
                this._rightTroopRoster = inventoryLogic.RightMemberRoster;
                this._leftTroopRoster = inventoryLogic.LeftMemberRoster;
                this._currentInventoryCharacterIndex = this._rightTroopRoster.FindIndexOfTroop(this._currentCharacter);
                this.OnDonationXpChange();
                this.CompanionExists = this.DoesCompanionExist();
            }
            this.MainCharacter = new HeroViewModel(CharacterViewModel.StanceTypes.None);
            this.MainCharacter.FillFrom(this._currentCharacter.HeroObject, -1, false, false);
            this.ItemMenu = new ItemMenuVM(new Action<ItemVM, int>(this.ResetComparedItems), this._inventoryLogic, this._getItemUsageSetFlags, new Func<EquipmentIndex, SPSkillItemVM>(this.GetItemFromIndex));
            this.IsRefreshed = false;
            this.RightItemListVM = new MBBindingList<SPSkillItemVM>();
            this.LeftItemListVM = new MBBindingList<SPSkillItemVM>();
            this.CharacterHelmSlot = new SPSkillItemVM();
            this.CharacterCloakSlot = new SPSkillItemVM();
            this.CharacterTorsoSlot = new SPSkillItemVM();
            this.CharacterGloveSlot = new SPSkillItemVM();
            this.CharacterBootSlot = new SPSkillItemVM();
            this.CharacterMountSlot = new SPSkillItemVM();
            this.CharacterMountArmorSlot = new SPSkillItemVM();
            this.CharacterWeapon1Slot = new SPSkillItemVM();
            this.CharacterWeapon2Slot = new SPSkillItemVM();
            this.CharacterWeapon3Slot = new SPSkillItemVM();
            this.CharacterWeapon4Slot = new SPSkillItemVM();
            this.CharacterBannerSlot = new SPSkillItemVM();
            this.ProductionTooltip = new BasicTooltipViewModel();
            this.CurrentCharacterSkillsTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetInventoryCharacterTooltip(this._currentCharacter.HeroObject));
            this.RefreshCallbacks();
            this._selectedEquipmentIndex = 0;
            this.EquipmentMaxCountHint = new BasicTooltipViewModel();
            this.IsInWarSet = !isInCivilianModeByDefault;
            if (this._inventoryLogic != null)
            {
                this.UpdateRightCharacter();
                this.UpdateLeftCharacter();
                this.InitializeInventory();
            }
            this.RightInventoryOwnerGold = Hero.MainHero.Gold;
            if (this._inventoryLogic.OtherSideCapacityData != null)
            {
                this.OtherSideHasCapacity = (this._inventoryLogic.OtherSideCapacityData.GetCapacity() != -1);
            }
            this.IsOtherInventoryGoldRelevant = (this._usageType != InventoryMode.Loot);
            MBBindingList<SPItemVM> listToControl = ConvertToParentList(_rightItemListVM);
            this.PlayerInventorySortController = new SPInventorySortControllerVM(ref listToControl);
            this._rightItemListVM = ConvertToSkillItemList(listToControl);
            listToControl = ConvertToParentList(_leftItemListVM);
            this.OtherInventorySortController = new SPInventorySortControllerVM(ref listToControl);
            this._leftItemListVM = ConvertToSkillItemList(listToControl);
            this.PlayerInventorySortController.SortByDefaultState();
            if (this._usageType == InventoryMode.Loot)
            {
                this.OtherInventorySortController.CostState = 1;
                this.OtherInventorySortController.ExecuteSortByCost();
            }
            else
            {
                this.OtherInventorySortController.SortByDefaultState();
            }
            Tuple<int, int> tuple = this._viewDataTracker.InventoryGetSortPreference((int)this._usageType);
            if (tuple != null)
            {
                this.PlayerInventorySortController.SortByOption((SPInventorySortControllerVM.InventoryItemSortOption)tuple.Item1, (SPInventorySortControllerVM.InventoryItemSortState)tuple.Item2);
            }
            this.ItemPreview = new ItemPreviewVM(new Action(this.OnPreviewClosed));
            this._characterList = new SelectorVM<InventoryCharacterSelectorItemVM>(0, new Action<SelectorVM<InventoryCharacterSelectorItemVM>>(this.OnCharacterSelected));
            this.AddApplicableCharactersToListFromRoster(this._rightTroopRoster.GetTroopRoster());
            if (this._inventoryLogic.IsOtherPartyFromPlayerClan && this._leftTroopRoster != null)
            {
                this.AddApplicableCharactersToListFromRoster(this._leftTroopRoster.GetTroopRoster());
            }
            if (this._characterList.SelectedIndex == -1 && this._characterList.ItemList.Count > 0)
            {
                this._characterList.SelectedIndex = 0;
            }
            this.BannerTypeCode = 24;
            InventoryTradeVM.RemoveZeroCounts += this.ExecuteRemoveZeroCounts;
            Game.Current.EventManager.RegisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
            this.RefreshValues();
            if (this._inventoryLogic != null)
            {
                this.InitializeInventory();
            }
        }


        private void AddApplicableCharactersToListFromRoster(MBList<TroopRosterElement> roster)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                CharacterObject character = roster[i].Character;
                if (character.IsHero && this.CanSelectHero(character.HeroObject))
                {
                    this._characterList.AddItem(new InventoryCharacterSelectorItemVM(character.HeroObject.StringId, character.HeroObject, character.HeroObject.Name));
                    if (character == this._currentCharacter)
                    {
                        this._characterList.SelectedIndex = this._characterList.ItemList.Count - 1;
                    }
                }
            }
        }


        public override void RefreshValues()
        {
            base.RefreshValues();
            this.RightInventoryOwnerName =PartyBase.MainParty.Name.ToString();
            this.DoneLbl = GameTexts.FindText("str_done", null).ToString();
            this.CancelLbl = GameTexts.FindText("str_cancel", null).ToString();
            this.ResetLbl = GameTexts.FindText("str_reset", null).ToString();
            this.TypeText = GameTexts.FindText("str_sort_by_type_label", null).ToString();
            this.NameText = GameTexts.FindText("str_sort_by_name_label", null).ToString();
            this.QuantityText = GameTexts.FindText("str_quantity_sign", null).ToString();
            this.CostText = GameTexts.FindText("str_value", null).ToString();
            this.SearchPlaceholderText = new TextObject("{=tQOPRBFg}Search...", null).ToString();
            this.FilterAllHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_all", null), null);
            this.FilterWeaponHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_weapons", null), null);
            this.FilterArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_armors", null), null);
            this.FilterShieldAndRangedHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_shields_ranged", null), null);
            this.FilterMountAndHarnessHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_mounts", null), null);
            this.FilterMiscHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_other", null), null);
            this.CivilianOutfitHint = new HintViewModel(GameTexts.FindText("str_inventory_civilian_outfit", null), null);
            this.BattleOutfitHint = new HintViewModel(GameTexts.FindText("str_inventory_battle_outfit", null), null);
            this.EquipmentHelmSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_helm_slot", null), null);
            this.EquipmentArmorSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_armor_slot", null), null);
            this.EquipmentBootSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_boot_slot", null), null);
            this.EquipmentCloakSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_cloak_slot", null), null);
            this.EquipmentGloveSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_glove_slot", null), null);
            this.EquipmentHarnessSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_mount_armor_slot", null), null);
            this.EquipmentMountSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_mount_slot", null), null);
            this.EquipmentWeaponSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_filter_weapons", null), null);
            this.EquipmentBannerSlotHint = new HintViewModel(GameTexts.FindText("str_inventory_banner_slot", null), null);
            this.WeightHint = new HintViewModel(GameTexts.FindText("str_inventory_weight_desc", null), null);
            this.ArmArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_arm_armor", null), null);
            this.BodyArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_body_armor", null), null);
            this.HeadArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_head_armor", null), null);
            this.LegArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_leg_armor", null), null);
            this.HorseArmorHint = new HintViewModel(GameTexts.FindText("str_inventory_horse_armor", null), null);
            this.DonationLblHint = new HintViewModel(GameTexts.FindText("str_inventory_donation_label_hint", null), null);
            this.SetPreviousCharacterHint();
            this.SetNextCharacterHint();
            this.PreviewHint = new HintViewModel(GameTexts.FindText("str_inventory_preview", null), null);
            this.EquipHint = new HintViewModel(GameTexts.FindText("str_inventory_equip", null), null);
            this.UnequipHint = new HintViewModel(GameTexts.FindText("str_inventory_unequip", null), null);
            this.ResetHint = new HintViewModel(GameTexts.FindText("str_reset", null), null);
            this.PlayerSideCapacityExceededText = GameTexts.FindText("str_capacity_exceeded", null).ToString();
            this.PlayerSideCapacityExceededHint = new HintViewModel(GameTexts.FindText("str_capacity_exceeded_hint", null), null);
            if (this._inventoryLogic.OtherSideCapacityData != null)
            {
                TextObject capacityExceededWarningText = this._inventoryLogic.OtherSideCapacityData.GetCapacityExceededWarningText();
                this.OtherSideCapacityExceededText = ((capacityExceededWarningText != null) ? capacityExceededWarningText.ToString() : null);
                this.OtherSideCapacityExceededHint = new HintViewModel(this._inventoryLogic.OtherSideCapacityData.GetCapacityExceededHintText(), null);
            }
            this.SetBuyAllHint();
            this.SetSellAllHint();
            if (this._usageType == InventoryMode.Loot || this._usageType == InventoryMode.Stash)
            {
                this.SellHint = new HintViewModel(GameTexts.FindText("str_inventory_give", null), null);
            }
            else if (this._usageType == InventoryMode.Default)
            {
                this.SellHint = new HintViewModel(GameTexts.FindText("str_inventory_discard", null), null);
            }
            else
            {
                this.SellHint = new HintViewModel(GameTexts.FindText("str_inventory_sell", null), null);
            }
            this.CharacterHelmSlot.RefreshValues();
            this.CharacterCloakSlot.RefreshValues();
            this.CharacterTorsoSlot.RefreshValues();
            this.CharacterGloveSlot.RefreshValues();
            this.CharacterBootSlot.RefreshValues();
            this.CharacterMountSlot.RefreshValues();
            this.CharacterMountArmorSlot.RefreshValues();
            this.CharacterWeapon1Slot.RefreshValues();
            this.CharacterWeapon2Slot.RefreshValues();
            this.CharacterWeapon3Slot.RefreshValues();
            this.CharacterWeapon4Slot.RefreshValues();
            this.CharacterBannerSlot.RefreshValues();
            SPInventorySortControllerVM playerInventorySortController = this.PlayerInventorySortController;
            if (playerInventorySortController != null)
            {
                playerInventorySortController.RefreshValues();
            }
            SPInventorySortControllerVM otherInventorySortController = this.OtherInventorySortController;
            if (otherInventorySortController == null)
            {
                return;
            }
            otherInventorySortController.RefreshValues();
        }


        public override void OnFinalize()
        {
            //退出时，按照当前的物品/技能设置，映射到对应的char身上
            //想了一下，还是直接写道交换物品时吧
            ItemVM.ProcessEquipItem = null;
            ItemVM.ProcessUnequipItem = null;
            ItemVM.ProcessPreviewItem = null;
            ItemVM.ProcessBuyItem = null;
            SPSkillItemVM.ProcessSellItem = null;
            ItemVM.ProcessItemSelect = null;
            ItemVM.ProcessItemTooltip = null;
            SPSkillItemVM.ProcessItemSlaughter = null;
            SPSkillItemVM.ProcessItemDonate = null;
            SPSkillItemVM.OnFocus = null;
            InventoryTradeVM.RemoveZeroCounts -= this.ExecuteRemoveZeroCounts;
            Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(new Action<TutorialNotificationElementChangeEvent>(this.OnTutorialNotificationElementIDChange));
            this.ItemPreview.OnFinalize();
            this.ItemPreview = null;
            this.CancelInputKey.OnFinalize();
            this.DoneInputKey.OnFinalize();
            this.ResetInputKey.OnFinalize();
            this.PreviousCharacterInputKey.OnFinalize();
            this.NextCharacterInputKey.OnFinalize();
            this.BuyAllInputKey.OnFinalize();
            this.SellAllInputKey.OnFinalize();
            ItemVM.ProcessEquipItem = null;
            ItemVM.ProcessUnequipItem = null;
            ItemVM.ProcessPreviewItem = null;
            ItemVM.ProcessBuyItem = null;
            SPSkillItemVM.ProcessLockItem = null;
            SPSkillItemVM.ProcessSellItem = null;
            ItemVM.ProcessItemSelect = null;
            ItemVM.ProcessItemTooltip = null;
            SPSkillItemVM.ProcessItemSlaughter = null;
            SPSkillItemVM.ProcessItemDonate = null;
            SPSkillItemVM.OnFocus = null;
            this.MainCharacter.OnFinalize();
            this._isFinalized = true;
            this._inventoryLogic = null;
            base.OnFinalize();
        }


        public void RefreshCallbacks()
        {
            ItemVM.ProcessEquipItem = new Action<ItemVM>(this.ProcessEquipItem);
            ItemVM.ProcessUnequipItem = new Action<ItemVM>(this.ProcessUnequipItem);
            ItemVM.ProcessPreviewItem = new Action<ItemVM>(this.ProcessPreviewItem);
            ItemVM.ProcessBuyItem = new Action<ItemVM, bool>(this.ProcessBuyItem);
            SPSkillItemVM.ProcessLockItem = new Action<SPSkillItemVM, bool>(this.ProcessLockItem);
            SPSkillItemVM.ProcessSellItem = new Action<SPSkillItemVM, bool>(this.ProcessSellItem);
            ItemVM.ProcessItemSelect = new Action<ItemVM>(this.ProcessItemSelect);
            ItemVM.ProcessItemTooltip = new Action<ItemVM>(this.ProcessItemTooltip);
            SPSkillItemVM.ProcessItemSlaughter = new Action<SPSkillItemVM>(this.ProcessItemSlaughter);
            SPSkillItemVM.ProcessItemDonate = new Action<SPSkillItemVM>(this.ProcessItemDonate);
            SPSkillItemVM.OnFocus = new Action<SPSkillItemVM>(this.OnItemFocus);
        }


        private bool CanSelectHero(Hero hero)
        {
            return hero.IsAlive && hero.CanHeroEquipmentBeChanged() && hero.Clan == Clan.PlayerClan && hero.HeroState != Hero.CharacterStates.Disabled && !hero.IsChild;
        }


        private void SetPreviousCharacterHint()
        {
            this.PreviousCharacterHint = new BasicTooltipViewModel(delegate ()
            {
                GameTexts.SetVariable("HOTKEY", this.GetPreviousCharacterKeyText());
                GameTexts.SetVariable("TEXT", GameTexts.FindText("str_inventory_prev_char", null));
                return GameTexts.FindText("str_hotkey_with_hint", null).ToString();
            });
        }


        private void SetNextCharacterHint()
        {
            this.NextCharacterHint = new BasicTooltipViewModel(delegate ()
            {
                GameTexts.SetVariable("HOTKEY", this.GetNextCharacterKeyText());
                GameTexts.SetVariable("TEXT", GameTexts.FindText("str_inventory_next_char", null));
                return GameTexts.FindText("str_hotkey_with_hint", null).ToString();
            });
        }


        private void SetBuyAllHint()
        {
            TextObject buyAllHintText;
            if (this._usageType == InventoryMode.Trade)
            {
                buyAllHintText = GameTexts.FindText("str_inventory_buy_all", null);
            }
            else
            {
                buyAllHintText = GameTexts.FindText("str_inventory_take_all", null);
            }
            this.BuyAllHint = new BasicTooltipViewModel(delegate ()
            {
                GameTexts.SetVariable("HOTKEY", this.GetBuyAllKeyText());
                GameTexts.SetVariable("TEXT", buyAllHintText);
                return GameTexts.FindText("str_hotkey_with_hint", null).ToString();
            });
        }


        private void SetSellAllHint()
        {
            TextObject sellAllHintText;
            if (this._usageType == InventoryMode.Loot || this._usageType == InventoryMode.Stash)
            {
                sellAllHintText = GameTexts.FindText("str_inventory_give_all", null);
            }
            else if (this._usageType == InventoryMode.Default)
            {
                sellAllHintText = GameTexts.FindText("str_inventory_discard_all", null);
            }
            else
            {
                sellAllHintText = GameTexts.FindText("str_inventory_sell_all", null);
            }
            this.SellAllHint = new BasicTooltipViewModel(delegate ()
            {
                GameTexts.SetVariable("HOTKEY", this.GetSellAllKeyText());
                GameTexts.SetVariable("TEXT", sellAllHintText);
                return GameTexts.FindText("str_hotkey_with_hint", null).ToString();
            });
        }


        private void OnCharacterSelected(SelectorVM<InventoryCharacterSelectorItemVM> selector)
        {
            if (this._inventoryLogic == null || selector.SelectedItem == null)
            {
                return;
            }
            for (int i = 0; i < this._rightTroopRoster.Count; i++)
            {
                if (this._rightTroopRoster.GetCharacterAtIndex(i).StringId == selector.SelectedItem.CharacterID)
                {
                    this.UpdateCurrentCharacterIfPossible(i, true);
                    return;
                }
            }
            if (this._leftTroopRoster != null)
            {
                for (int j = 0; j < this._leftTroopRoster.Count; j++)
                {
                    if (this._leftTroopRoster.GetCharacterAtIndex(j).StringId == selector.SelectedItem.CharacterID)
                    {
                        this.UpdateCurrentCharacterIfPossible(j, false);
                        return;
                    }
                }
            }
        }



        private Equipment ActiveEquipment
        {
            get
            {
                if (!this.IsInWarSet)
                {
                    return this._currentCharacter.FirstCivilianEquipment;
                }
                return this._currentCharacter.FirstBattleEquipment;
            }
        }


        public void ExecuteShowRecap()
        {
            InformationManager.ShowTooltip(typeof(InventoryLogic), new object[]
            {
                this._inventoryLogic
            });
        }


        public void ExecuteCancelRecap()
        {
            MBInformationManager.HideInformations();
        }


        public void ExecuteRemoveZeroCounts()
        {
            List<SPSkillItemVM> list = this.LeftItemListVM.ToList<SPSkillItemVM>();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].ItemCount == 0 && i >= 0 && i < this.LeftItemListVM.Count)
                {
                    this.LeftItemListVM.RemoveAt(i);
                }
            }
            List<SPSkillItemVM> list2 = this.RightItemListVM.ToList<SPSkillItemVM>();
            for (int j = list2.Count - 1; j >= 0; j--)
            {
                if (list2[j].ItemCount == 0 && j >= 0 && j < this.RightItemListVM.Count)
                {
                    this.RightItemListVM.RemoveAt(j);
                }
            }
        }


        private void ProcessPreviewItem(ItemVM item)
        {
            this._inventoryLogic.IsPreviewingItem = true;
            this.ItemPreview.Open(item.ItemRosterElement.EquipmentElement);
        }


        public void ClosePreview()
        {
            this.ItemPreview.Close();
        }


        private void OnPreviewClosed()
        {
            this._inventoryLogic.IsPreviewingItem = false;
        }


        private void ProcessEquipItem(ItemVM draggedItem)
        {
            SPSkillItemVM SPSkillItemVM = draggedItem as SPSkillItemVM;
            if (!SPSkillItemVM.IsCivilianItem && !this.IsInWarSet)
            {
                return;
            }
            this.IsRefreshed = false;
            this.EquipEquipment(SPSkillItemVM);
            this.RefreshInformationValues();
            this.ExecuteRemoveZeroCounts();
            this.IsRefreshed = true;
        }


        private void ProcessUnequipItem(ItemVM draggedItem)
        {
            this.IsRefreshed = false;
            this.UnequipEquipment(draggedItem as SPSkillItemVM);
            this.RefreshInformationValues();
            this.IsRefreshed = true;
        }


        private void ProcessBuyItem(ItemVM itemBase, bool cameFromTradeData)
        {
            IsRefreshed = false;
            MBTextManager.SetTextVariable("ITEM_DESCRIPTION", itemBase.ItemDescription);
            MBTextManager.SetTextVariable("ITEM_COST", itemBase.ItemCost);
            SPSkillItemVM SPSkillItemVM = itemBase as SPSkillItemVM;
            if (IsEntireStackModifierActive && !cameFromTradeData)
            {
                TransactionCount = _inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.OtherInventory, SPSkillItemVM.ItemRosterElement.EquipmentElement)?.Amount ?? 0;
            }
            else if (IsFiveStackModifierActive && !cameFromTradeData)
            {
                TransactionCount = 5;
            }
            else
            {
                TransactionCount = SPSkillItemVM?.TransactionCount ?? 0;
            }

            BuyItem(SPSkillItemVM);
            if (!cameFromTradeData)
            {
                ExecuteRemoveZeroCounts();
            }

            RefreshInformationValues();
            IsRefreshed = true;
        }



        private void ProcessSellItem(SPSkillItemVM item, bool cameFromTradeData)
        {
            return;
            this.IsRefreshed = false;
            MBTextManager.SetTextVariable("ITEM_DESCRIPTION", item.ItemDescription, false);
            MBTextManager.SetTextVariable("ITEM_COST", item.ItemCost);
            if (this.IsEntireStackModifierActive && !cameFromTradeData)
            {
                ItemRosterElement? itemRosterElement = this._inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, item.ItemRosterElement.EquipmentElement);
                this.TransactionCount = ((itemRosterElement != null) ? itemRosterElement.GetValueOrDefault().Amount : 0);
            }
            else if (this.IsFiveStackModifierActive && !cameFromTradeData)
            {
                this.TransactionCount = 5;
            }
            else
            {
                this.TransactionCount = item.TransactionCount;
            }
            this.SellItem(item);
            if (!cameFromTradeData)
            {
                this.ExecuteRemoveZeroCounts();
            }
            this.RefreshInformationValues();
            this.IsRefreshed = true;
        }


        private void ProcessLockItem(SPSkillItemVM item, bool isLocked)
        {
            if (isLocked && item.InventorySide == InventoryLogic.InventorySide.PlayerInventory && !this._lockedItemIDs.Contains(item.StringId))
            {
                this._lockedItemIDs.Add(item.StringId);
                return;
            }
            if (!isLocked && item.InventorySide == InventoryLogic.InventorySide.PlayerInventory && this._lockedItemIDs.Contains(item.StringId))
            {
                this._lockedItemIDs.Remove(item.StringId);
            }
        }


        private ItemVM ProcessCompareItem(ItemVM item, int alternativeUsageIndex = 0)
        {
            this._selectedEquipmentIndex = 0;
            this._comparedItemList.Clear();
            ItemVM itemVM = null;
            bool flag = false;
            EquipmentIndex equipmentIndex = EquipmentIndex.None;
            SPSkillItemVM SPSkillItemVM = null;
            bool flag2 = item.ItemType >= EquipmentIndex.WeaponItemBeginSlot && item.ItemType < EquipmentIndex.ExtraWeaponSlot;
            if (((SPSkillItemVM)item).InventorySide != InventoryLogic.InventorySide.Equipment)
            {
                if (flag2)
                {
                    for (EquipmentIndex equipmentIndex2 = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex2 < EquipmentIndex.ExtraWeaponSlot; equipmentIndex2++)
                    {
                        EquipmentIndex itemType = equipmentIndex2;
                        SPSkillItemVM itemFromIndex = this.GetItemFromIndex(itemType);
                        if (itemFromIndex != null && itemFromIndex.ItemRosterElement.EquipmentElement.Item != null && ItemHelper.CheckComparability(item.ItemRosterElement.EquipmentElement.Item, itemFromIndex.ItemRosterElement.EquipmentElement.Item, alternativeUsageIndex))
                        {
                            this._comparedItemList.Add(itemFromIndex);
                        }
                    }
                    if (!this._comparedItemList.IsEmpty<ItemVM>())
                    {
                        this.SortComparedItems(item);
                        itemVM = this._comparedItemList[0];
                        this._lastComparedItemIndex = 0;
                    }
                    if (itemVM != null)
                    {
                        equipmentIndex = itemVM.ItemType;
                    }
                }
                else
                {
                    equipmentIndex = item.ItemType;
                }
            }
            if (item.ItemType >= EquipmentIndex.WeaponItemBeginSlot && item.ItemType < EquipmentIndex.NumEquipmentSetSlots)
            {
                SPSkillItemVM = ((equipmentIndex != EquipmentIndex.None) ? this.GetItemFromIndex(equipmentIndex) : null);
                flag = (SPSkillItemVM != null && !string.IsNullOrEmpty(SPSkillItemVM.StringId) && item.StringId != SPSkillItemVM.StringId);
            }
            if (!this._selectedTooltipItemStringID.Equals(item.StringId) || (flag && !this._comparedTooltipItemStringID.Equals(SPSkillItemVM.StringId)))
            {
                this._selectedTooltipItemStringID = item.StringId;
                if (flag)
                {
                    this._comparedTooltipItemStringID = SPSkillItemVM.StringId;
                }
            }
            this._selectedEquipmentIndex = (int)equipmentIndex;
            if (SPSkillItemVM == null || SPSkillItemVM.ItemRosterElement.IsEmpty)
            {
                return null;
            }
            return SPSkillItemVM;
        }


        private void ResetComparedItems(ItemVM item, int alternativeUsageIndex)
        {
            ItemVM comparedItem = this.ProcessCompareItem(item, alternativeUsageIndex);
            this.ItemMenu.SetItem(this._selectedItem, comparedItem, this._currentCharacter, alternativeUsageIndex);
        }


        private void SortComparedItems(ItemVM selectedItem)
        {
            List<ItemVM> list = new List<ItemVM>();
            for (int i = 0; i < this._comparedItemList.Count; i++)
            {
                if (selectedItem.StringId == this._comparedItemList[i].StringId && !list.Contains(this._comparedItemList[i]))
                {
                    list.Add(this._comparedItemList[i]);
                }
            }
            for (int j = 0; j < this._comparedItemList.Count; j++)
            {
                if (this._comparedItemList[j].ItemRosterElement.EquipmentElement.Item.Type == selectedItem.ItemRosterElement.EquipmentElement.Item.Type && !list.Contains(this._comparedItemList[j]))
                {
                    list.Add(this._comparedItemList[j]);
                }
            }
            for (int k = 0; k < this._comparedItemList.Count; k++)
            {
                WeaponComponent weaponComponent = this._comparedItemList[k].ItemRosterElement.EquipmentElement.Item.WeaponComponent;
                WeaponComponent weaponComponent2 = selectedItem.ItemRosterElement.EquipmentElement.Item.WeaponComponent;
                if (((weaponComponent2.Weapons.Count > 1 && weaponComponent2.Weapons[1].WeaponClass == weaponComponent.Weapons[0].WeaponClass) || (weaponComponent.Weapons.Count > 1 && weaponComponent.Weapons[1].WeaponClass == weaponComponent2.Weapons[0].WeaponClass) || (weaponComponent2.Weapons.Count > 1 && weaponComponent.Weapons.Count > 1 && weaponComponent2.Weapons[1].WeaponClass == weaponComponent.Weapons[1].WeaponClass)) && !list.Contains(this._comparedItemList[k]))
                {
                    list.Add(this._comparedItemList[k]);
                }
            }
            if (this._comparedItemList.Count != list.Count)
            {
                foreach (ItemVM item in this._comparedItemList)
                {
                    if (!list.Contains(item))
                    {
                        list.Add(item);
                    }
                }
            }
            this._comparedItemList = list;
        }


        public void ProcessItemTooltip(ItemVM item)
        {
            if (item == null || string.IsNullOrEmpty(item.StringId))
            {
                return;
            }
            this._selectedItem = (item as SPSkillItemVM);
            ItemVM comparedItem = this.ProcessCompareItem(item, 0);
            this.ItemMenu.SetItem(this._selectedItem, comparedItem, this._currentCharacter, 0);
            this.RefreshTransactionCost(1);
            this._selectedItem.UpdateCanBeSlaughtered();
        }


        public void ResetSelectedItem()
        {
            this._selectedItem = null;
        }


        private void ProcessItemSlaughter(SPSkillItemVM item)
        {
            this.IsRefreshed = false;
            if (string.IsNullOrEmpty(item.StringId) || !item.CanBeSlaughtered)
            {
                return;
            }
            this.SlaughterItem(item);
            this.RefreshInformationValues();
            if (item.ItemCount == 0)
            {
                this.ExecuteRemoveZeroCounts();
            }
            this.IsRefreshed = true;
        }


        private void ProcessItemDonate(SPSkillItemVM item)
        {
            this.IsRefreshed = false;
            if (string.IsNullOrEmpty(item.StringId) || !item.CanBeDonated)
            {
                return;
            }
            this.DonateItem(item);
            this.RefreshInformationValues();
            if (item.ItemCount == 0)
            {
                this.ExecuteRemoveZeroCounts();
            }
            this.IsRefreshed = true;
        }


        private void OnItemFocus(SPSkillItemVM item)
        {
            this.CurrentFocusedItem = item;
        }


        private void ProcessItemSelect(ItemVM item)
        {
            this.ExecuteRemoveZeroCounts();
        }


        private void RefreshTransactionCost(int transactionCount = 1)
        {
            if (this._selectedItem != null && this.IsTrading)
            {
                int maxIndividualPrice;
                int itemTotalPrice = this._inventoryLogic.GetItemTotalPrice(this._selectedItem.ItemRosterElement, transactionCount, out maxIndividualPrice, this._selectedItem.InventorySide == InventoryLogic.InventorySide.OtherInventory);
                this.ItemMenu.SetTransactionCost(itemTotalPrice, maxIndividualPrice);
            }
        }


        public void RefreshComparedItem()
        {
            this._lastComparedItemIndex++;
            if (this._lastComparedItemIndex > this._comparedItemList.Count - 1)
            {
                this._lastComparedItemIndex = 0;
            }
            if (!this._comparedItemList.IsEmpty<ItemVM>() && this._selectedItem != null && this._comparedItemList[this._lastComparedItemIndex] != null)
            {
                this.ItemMenu.SetItem(this._selectedItem, this._comparedItemList[this._lastComparedItemIndex], this._currentCharacter, 0);
            }
        }


        private void AfterReset(InventoryLogic itemRoster, bool fromCancel)
        {
            this._inventoryLogic = itemRoster;
            if (!fromCancel)
            {
                switch (this.ActiveFilterIndex)
                {
                    case 1:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.Weapon;
                        break;
                    case 2:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.Shield;
                        break;
                    case 3:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.Armors;
                        break;
                    case 4:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.HorseCategory;
                        break;
                    case 5:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.Goods;
                        break;
                    default:
                        this._inventoryLogic.MerchantItemType = InventoryManager.InventoryCategoryType.All;
                        break;
                }
                this.InitializeInventory();

                MBBindingList<SPItemVM> listToControl = new MBBindingList<SPItemVM>();
                this.PlayerInventorySortController = new SPInventorySortControllerVM(ref listToControl);
                this._rightItemListVM = ConvertToSkillItemList(listToControl);
                this.OtherInventorySortController = new SPInventorySortControllerVM(ref listToControl);
                this._leftItemListVM = ConvertToSkillItemList(listToControl);
                this.PlayerInventorySortController.SortByDefaultState();
                this.OtherInventorySortController.SortByDefaultState();
                Tuple<int, int> tuple = this._viewDataTracker.InventoryGetSortPreference((int)this._usageType);
                if (tuple != null)
                {
                    this.PlayerInventorySortController.SortByOption((SPInventorySortControllerVM.InventoryItemSortOption)tuple.Item1, (SPInventorySortControllerVM.InventoryItemSortState)tuple.Item2);
                }
                this.UpdateRightCharacter();
                this.UpdateLeftCharacter();
                this.RightInventoryOwnerName = PartyBase.MainParty.Name.ToString();
                this.RightInventoryOwnerGold = Hero.MainHero.Gold;
            }
        }


        private void OnTotalAmountChange(int newTotalAmount)
        {
            MBTextManager.SetTextVariable("PAY_OR_GET", (this._inventoryLogic.TotalAmount < 0) ? 1 : 0);
            int f = TaleWorlds.Library.MathF.Min(-this._inventoryLogic.TotalAmount, this._inventoryLogic.InventoryListener.GetGold());
            MBTextManager.SetTextVariable("TRADE_AMOUNT", TaleWorlds.Library.MathF.Abs(f));
            this.TradeLbl = ((this._inventoryLogic.TotalAmount == 0) ? "" : GameTexts.FindText("str_inventory_trade_label", null).ToString());
            this.RightInventoryOwnerGold = Hero.MainHero.Gold - this._inventoryLogic.TotalAmount;
            InventoryListener inventoryListener = this._inventoryLogic.InventoryListener;
            this.LeftInventoryOwnerGold = ((((inventoryListener != null) ? new int?(inventoryListener.GetGold()) : null) + this._inventoryLogic.TotalAmount) ?? 0);
        }


        private void OnDonationXpChange()
        {
            int num = (int)this._inventoryLogic.XpGainFromDonations;
            bool isDonationXpGainExceedsMax = false;
            if (num > this._donationMaxShareableXp)
            {
                num = this._donationMaxShareableXp;
                isDonationXpGainExceedsMax = true;
            }
            this.IsDonationXpGainExceedsMax = isDonationXpGainExceedsMax;
            this.HasGainedExperience = (num > 0);
            MBTextManager.SetTextVariable("XP_AMOUNT", num);
            this.ExperienceLbl = ((num == 0) ? "" : GameTexts.FindText("str_inventory_donation_label", null).ToString());
        }


        private void AfterTransfer(InventoryLogic inventoryLogic, List<TransferCommandResult> results)
        {
            this._isCharacterEquipmentDirty = false;
            List<SPSkillItemVM> list = new List<SPSkillItemVM>();
            HashSet<ItemCategory> hashSet = new HashSet<ItemCategory>();
            for (int num = 0; num != results.Count; num++)
            {
                TransferCommandResult transferCommandResult = results[num];
                if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory || transferCommandResult.ResultSide == InventoryLogic.InventorySide.PlayerInventory)
                {
                    if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.PlayerInventory && transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item != null && !transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item.IsMountable && !transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item.IsAnimal)
                    {
                        this._equipmentCount += (float)transferCommandResult.EffectedNumber * transferCommandResult.EffectedItemRosterElement.EquipmentElement.GetEquipmentElementWeight();
                    }
                    bool flag = false;
                    MBBindingList<SPSkillItemVM> mbbindingList = (transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory) ? this.LeftItemListVM : this.RightItemListVM;
                    for (int i = 0; i < mbbindingList.Count; i++)
                    {
                        SPSkillItemVM SPSkillItemVM = mbbindingList[i];
                        if (SPSkillItemVM != null && SPSkillItemVM.ItemRosterElement.EquipmentElement.IsEqualTo(transferCommandResult.EffectedItemRosterElement.EquipmentElement))
                        {
                            SPSkillItemVM.ItemRosterElement.Amount = transferCommandResult.FinalNumber;
                            SPSkillItemVM.ItemCount = transferCommandResult.FinalNumber;
                            SPSkillItemVM.ItemCost = this._inventoryLogic.GetItemPrice(SPSkillItemVM.ItemRosterElement.EquipmentElement, transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory);
                            list.Add(SPSkillItemVM);
                            if (!hashSet.Contains(SPSkillItemVM.ItemRosterElement.EquipmentElement.Item.GetItemCategory()))
                            {
                                hashSet.Add(SPSkillItemVM.ItemRosterElement.EquipmentElement.Item.GetItemCategory());
                            }
                            flag = true;
                            break;
                        }
                    }
                    if (!flag && transferCommandResult.EffectedNumber > 0 && this._inventoryLogic != null)
                    {
                        SPSkillItemVM SPSkillItemVM2;
                        if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.OtherInventory)
                        {
                            SPSkillItemVM2 = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item.StringId), this._usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.OtherInventory, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
                        }
                        else
                        {
                            SPSkillItemVM2 = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item.StringId), this._usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.PlayerInventory, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), null);
                        }
                        this.UpdateFilteredStatusOfItem(SPSkillItemVM2);
                        SPSkillItemVM2.ItemCount = transferCommandResult.FinalNumber;
                        SPSkillItemVM2.IsLocked = (SPSkillItemVM2.InventorySide == InventoryLogic.InventorySide.PlayerInventory && this._lockedItemIDs.Contains(SPSkillItemVM2.StringId));
                        SPSkillItemVM2.IsNew = true;
                        mbbindingList.Add(SPSkillItemVM2);
                    }
                }
                else if (transferCommandResult.ResultSide == InventoryLogic.InventorySide.Equipment)
                {
                    SPSkillItemVM SPSkillItemVM3 = null;
                    if (transferCommandResult.FinalNumber > 0)
                    {
                        SPSkillItemVM3 = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(transferCommandResult.EffectedItemRosterElement.EquipmentElement.Item.StringId), this._usageType, transferCommandResult.EffectedItemRosterElement, InventoryLogic.InventorySide.Equipment, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(transferCommandResult.EffectedItemRosterElement, transferCommandResult.ResultSide), new EquipmentIndex?(transferCommandResult.EffectedEquipmentIndex));
                        SPSkillItemVM3.IsNew = true;
                    }
                    this.UpdateEquipment(transferCommandResult.TransferEquipment, SPSkillItemVM3, transferCommandResult.EffectedEquipmentIndex);
                    this._isCharacterEquipmentDirty = true;
                }
            }
            SPSkillItemVM selectedItem = this._selectedItem;
            if (selectedItem != null && selectedItem.ItemCount > 1)
            {
                this.ProcessItemTooltip(this._selectedItem);
                this._selectedItem.UpdateCanBeSlaughtered();
            }
            this.CheckEquipAfterTransferStack();
            if (!this.ActiveEquipment[EquipmentIndex.HorseHarness].IsEmpty && this.ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].IsEmpty)
            {
                this.UnequipEquipment(this.CharacterMountArmorSlot);
            }
            if (!this.ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].IsEmpty && !this.ActiveEquipment[EquipmentIndex.HorseHarness].IsEmpty && this.ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].Item.HorseComponent.Monster.FamilyType != this.ActiveEquipment[EquipmentIndex.HorseHarness].Item.ArmorComponent.FamilyType)
            {
                this.UnequipEquipment(this.CharacterMountArmorSlot);
            }
            foreach (SPSkillItemVM SPSkillItemVM4 in list)
            {
                SPSkillItemVM4.UpdateTradeData(true);
                SPSkillItemVM4.UpdateCanBeSlaughtered();
            }
            this.UpdateCostOfItemsInCategory(hashSet);
            if (PartyBase.MainParty.IsMobile)
            {
                PartyBase.MainParty.MobileParty.MemberRoster.UpdateVersion();
                PartyBase.MainParty.MobileParty.PrisonRoster.UpdateVersion();
            }
        }


        private void UpdateCostOfItemsInCategory(HashSet<ItemCategory> categories)
        {
            foreach (SPSkillItemVM SPSkillItemVM in this.LeftItemListVM)
            {
                if (categories.Contains(SPSkillItemVM.ItemRosterElement.EquipmentElement.Item.GetItemCategory()))
                {
                    SPSkillItemVM.ItemCost = this._inventoryLogic.GetCostOfItemRosterElement(SPSkillItemVM.ItemRosterElement, InventoryLogic.InventorySide.OtherInventory);
                }
            }
            foreach (SPSkillItemVM SPSkillItemVM2 in this.RightItemListVM)
            {
                if (categories.Contains(SPSkillItemVM2.ItemRosterElement.EquipmentElement.Item.GetItemCategory()))
                {
                    SPSkillItemVM2.ItemCost = this._inventoryLogic.GetCostOfItemRosterElement(SPSkillItemVM2.ItemRosterElement, InventoryLogic.InventorySide.PlayerInventory);
                }
            }
        }


        private void CheckEquipAfterTransferStack()
        {
            while (this._equipAfterTransferStack.Count > 0)
            {
                SPSkillItemVM SPSkillItemVM = new SPSkillItemVM();
                SPSkillItemVM.RefreshWith(this._equipAfterTransferStack.Pop(), InventoryLogic.InventorySide.PlayerInventory);
                this.EquipEquipment(SPSkillItemVM);
            }
        }


        private void RefreshInformationValues()
        {
            // 从本地化文本中获取格式模板（如 "Left/Right" 显示格式）
            TextObject textObject = GameTexts.FindText("str_LEFT_over_RIGHT", null);

            // 获取玩家队伍的总库存容量
            int inventoryCapacity = PartyBase.MainParty.InventoryCapacity;

            // 计算当前装备的总重量（向上取整）
            int num = TaleWorlds.Library.MathF.Ceiling(this._equipmentCount);

            // 设置玩家装备数量文本（如 "5/10"）
            textObject.SetTextVariable("LEFT", num.ToString());
            textObject.SetTextVariable("RIGHT", inventoryCapacity.ToString());
            this.PlayerEquipmentCountText = textObject.ToString();

            // 检查是否超重（装备重量超过容量）
            this.PlayerEquipmentCountWarned = (num > inventoryCapacity);

            // 如果对方库存有容量限制（如商队或仓库）
            if (this.OtherSideHasCapacity)
            {
                // 计算对方库存物品的总重量（通过遍历 LeftItemListVM 中所有物品）
                int num2 = TaleWorlds.Library.MathF.Ceiling(
                    this.LeftItemListVM.Sum((SPSkillItemVM x) => x.ItemRosterElement.GetRosterElementWeight())
                );

                // 获取对方库存的容量上限
                int capacity = this._inventoryLogic.OtherSideCapacityData.GetCapacity();

                // 设置对方库存的显示文本（如 "8/15"）
                textObject.SetTextVariable("LEFT", num2.ToString());
                textObject.SetTextVariable("RIGHT", capacity.ToString());
                this.OtherEquipmentCountText = textObject.ToString();

                // 检查对方库存是否超重
                this.OtherEquipmentCountWarned = (num2 > capacity);
            }

            // 设置无马鞍警告的文本内容
            this.NoSaddleText = new TextObject("{=QSPrSsHv}No Saddle!", null).ToString();

            // 设置无马鞍警告的提示信息（包含具体效果说明）
            this.NoSaddleHint = new HintViewModel(
                new TextObject("{=VzCoqt8D}No saddle equipped. -10% penalty to mounted speed and maneuver.", null),
                null
            );

            // 检查是否需要显示无马鞍警告：
            // 1. 坐骑槽位有物品（如马匹）
            // 2. 马鞍槽位为空（未装备马鞍）
            SPSkillItemVM characterMountSlot = this.CharacterMountSlot;
            bool noSaddleWarned;
            if (characterMountSlot != null && !characterMountSlot.ItemRosterElement.IsEmpty)
            {
                SPSkillItemVM characterMountArmorSlot = this.CharacterMountArmorSlot;
                noSaddleWarned = (characterMountArmorSlot != null && characterMountArmorSlot.ItemRosterElement.IsEmpty);
            }
            else
            {
                noSaddleWarned = false;
            }
            this.NoSaddleWarned = noSaddleWarned;

            // 在战役模式下，设置装备容量提示（显示详细说明）
            if (Campaign.Current.GameMode == CampaignGameMode.Campaign)
            {
                this.EquipmentMaxCountHint = new BasicTooltipViewModel(
                    () => CampaignUIHelper.GetPartyInventoryCapacityTooltip(MobileParty.MainParty)
                );
            }

            // 如果角色装备数据已变更（_isCharacterEquipmentDirty 为 true）
            if (this._isCharacterEquipmentDirty)
            {
                // 更新主角色的装备显示
                this.MainCharacter.SetEquipment(this.ActiveEquipment);

                // 更新角色护甲值（如头部、身体护甲等）
                this.UpdateCharacterArmorValues();

                // 刷新角色总重量显示
                this.RefreshCharacterTotalWeight();
            }

            // 重置装备变更标志
            this._isCharacterEquipmentDirty = false;

            // 更新界面按钮状态（如 "完成" 按钮是否禁用）
            this.UpdateIsDoneDisabled();
        }

        /// <summary>
        /// 判定是否可以装备
        /// 调整为技能判定可以装备到对应栏位
        /// 先写一个简单的逻辑。1判定该位置是否可用该技能，2判定该角色是否可用该技能。以后可以扩展拖到人身上时，自动判定可以装备在哪个位置。
        /// </summary>
        /// <param name="itemVM"></param>
        /// <returns></returns>
        public bool IsItemEquipmentPossible(SPSkillItemVM itemVM)
        {
            if (itemVM == null)
            {
                return false;
            }
            SkillFactory._skillRegistry.TryGetValue(itemVM.ItemRosterElement.EquipmentElement.Item.StringId, out var skill);
            if (skill == null)
            {
                return true;
            }
            bool CanUseFlag1 = false;
            bool CanUseFlag2 = false;
            if (skill.Difficulty == null)
            {
                CanUseFlag1 = true;
            }
            else
            {
                foreach (var difficulty in skill.Difficulty)
                {
                    if (!CanUseFlag1)
                    {
                        //difficulty.UseAttribute确保存在
                        CanUseFlag1 = difficulty.Difficulty < this._currentCharacter.GetSkillValue(new SkillObject(difficulty.UseAttribute));
                    }
                }
            }
            switch (this.TargetEquipmentType)
            {
                case EquipmentIndex.Head:
                    if (skill.Type == SkillType.MainActive)
                    { CanUseFlag2 = true; }
                    break;
                case EquipmentIndex.Cape:
                    if (skill.Type == SkillType.SubActive)
                    { CanUseFlag2 = true; }
                    break;
                case EquipmentIndex.Body:
                    if (skill.Type == SkillType.Passive || skill.Type == SkillType.Passive_Spell)
                    { CanUseFlag2 = true; }
                    break;
                case EquipmentIndex.Weapon0:
                case EquipmentIndex.Weapon2:
                case EquipmentIndex.Weapon1:
                case EquipmentIndex.Weapon3:
                    if (skill.Type == SkillType.CombatArt_Spell || skill.Type == SkillType.Spell || skill.Type == SkillType.Passive_Spell)
                    { CanUseFlag2 = true; }
                    break;
                case EquipmentIndex.Gloves:
                    if (skill.Type == SkillType.CombatArt || skill.Type == SkillType.Spell_CombatArt)
                    { CanUseFlag2 = true; }
                    break;
                default: break;
            }
            return CanUseFlag1 && CanUseFlag2;
            //if (this.TargetEquipmentType == EquipmentIndex.None)
            //{
            //    this.TargetEquipmentType = itemVM.GetItemTypeWithItemObject();
            //    if (this.TargetEquipmentType == EquipmentIndex.None)
            //    {
            //        return false;
            //    }
            //    if (this.TargetEquipmentType == EquipmentIndex.WeaponItemBeginSlot)
            //    {
            //        EquipmentIndex targetEquipmentType = EquipmentIndex.WeaponItemBeginSlot;
            //        bool flag = false;
            //        bool flag2 = false;
            //        SPSkillItemVM[] array = new SPSkillItemVM[]
            //        {
            //            this.CharacterWeapon1Slot,
            //            this.CharacterWeapon2Slot,
            //            this.CharacterWeapon3Slot,
            //            this.CharacterWeapon4Slot
            //        };
            //        for (int i = 0; i < array.Length; i++)
            //        {
            //            if (string.IsNullOrEmpty(array[i].StringId))
            //            {
            //                flag = true;
            //                targetEquipmentType = EquipmentIndex.WeaponItemBeginSlot + i;
            //                break;
            //            }
            //            if (array[i].ItemRosterElement.EquipmentElement.Item.Type == itemVM.ItemRosterElement.EquipmentElement.Item.Type)
            //            {
            //                flag2 = true;
            //                targetEquipmentType = EquipmentIndex.WeaponItemBeginSlot + i;
            //                break;
            //            }
            //        }
            //        if (flag || flag2)
            //        {
            //            this.TargetEquipmentType = targetEquipmentType;
            //        }
            //        else
            //        {
            //            this.TargetEquipmentType = EquipmentIndex.WeaponItemBeginSlot;
            //        }
            //    }
            //}
            //else if (itemVM.ItemType != this.TargetEquipmentType && (this.TargetEquipmentType < EquipmentIndex.WeaponItemBeginSlot || this.TargetEquipmentType > EquipmentIndex.Weapon3 || itemVM.ItemType < EquipmentIndex.WeaponItemBeginSlot || itemVM.ItemType > EquipmentIndex.Weapon3))
            //{
            //    return false;
            //}
            //if (!this.CanCharacterUseItemBasedOnSkills(itemVM.ItemRosterElement.EquipmentElement.Item.Name.Value))
            //{
            //    TextObject textObject = new TextObject("{=rgqA29b8}You don't have enough {SKILL_NAME} skill to equip this item", null);
            //    //textObject.SetTextVariable("SKILL_NAME", itemVM.ItemRosterElement.EquipmentElement.Item.RelevantSkill.Name);
            //    MBInformationManager.AddQuickInformation(textObject, 0, null, "");
            //    return false;
            //}
            //if (!this.CanCharacterUserItemBasedOnUsability(itemVM.ItemRosterElement))
            //{
            //    TextObject textObject2 = new TextObject("{=ITKb4cKv}{ITEM_NAME} is not equippable.", null);
            //    textObject2.SetTextVariable("ITEM_NAME", itemVM.ItemRosterElement.EquipmentElement.GetModifiedItemName());
            //    MBInformationManager.AddQuickInformation(textObject2, 0, null, "");
            //    return false;
            //}
            //if (!Equipment.IsItemFitsToSlot((EquipmentIndex)this.TargetEquipmentIndex, itemVM.ItemRosterElement.EquipmentElement.Item))
            //{
            //    TextObject textObject3 = new TextObject("{=Omjlnsk3}{ITEM_NAME} cannot be equipped on this slot.", null);
            //    textObject3.SetTextVariable("ITEM_NAME", itemVM.ItemRosterElement.EquipmentElement.GetModifiedItemName());
            //    MBInformationManager.AddQuickInformation(textObject3, 0, null, "");
            //    return false;
            //}
            //if (this.TargetEquipmentType == EquipmentIndex.HorseHarness)
            //{
            //    if (string.IsNullOrEmpty(this.CharacterMountSlot.StringId))
            //    {
            //        return false;
            //    }
            //    if (!this.ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].IsEmpty && this.ActiveEquipment[EquipmentIndex.ArmorItemEndSlot].Item.HorseComponent.Monster.FamilyType != itemVM.ItemRosterElement.EquipmentElement.Item.ArmorComponent.FamilyType)
            //    {
            //        return false;
            //    }
            //}
            return true;
        }


        private bool CanCharacterUserItemBasedOnUsability(ItemRosterElement itemRosterElement)
        {
            return !itemRosterElement.EquipmentElement.Item.HasHorseComponent || itemRosterElement.EquipmentElement.Item.HorseComponent.IsRideable;
        }


        private bool CanCharacterUseItemBasedOnSkills(String item)
        {
            SkillFactory._skillRegistry.TryGetValue(item, out var skill);
            if (skill == null)
                return true;
            bool CanUse = false;
            if (skill.Difficulty == null)
                return true;
            foreach (var difficulty in skill.Difficulty)
            {
                if (!CanUse)
                {
                    //difficulty.UseAttribute确保存在
                    CanUse = difficulty.Difficulty < this._currentCharacter.GetSkillValue(new SkillObject(difficulty.UseAttribute));
                }
            }
            return CanUse;
        }

        //private void EquipEquipment(SPSkillItemVM itemVM)
        //{
        //    // 防御性检查：如果物品视图模型为空或物品ID无效，则直接返回
        //    if (itemVM == null || string.IsNullOrEmpty(itemVM.StringId))
        //    {
        //        Debug.Print("[ERROR] EquipEquipment: Invalid itemVM or StringId");
        //        return;
        //    }

        //    // 获取目标装备槽位索引（假设通过UI操作设置）
        //    EquipmentIndex targetIndex = this.TargetEquipmentType;
        //    if (targetIndex < EquipmentIndex.WeaponItemBeginSlot || targetIndex >= EquipmentIndex.NumAllWeaponSlots)
        //    {
        //        Debug.Print($"[ERROR] EquipEquipment: Invalid target slot index {targetIndex}");
        //        return;
        //    }

        //    // 获取玩家库存引用（假设通过角色对象获取）
        //    ItemRoster playerInventory = Hero.MainHero.PartyBelongedTo.Party.ItemRoster;

        //    // 临时插入虚构物品到玩家库存（绕过DoesTransferItemExist校验）
        //    ItemRosterElement tempElement = itemVM.ItemRosterElement;
        //    playerInventory.AddToCounts(tempElement.EquipmentElement, 1);
        //    Debug.Print($"临时插入物品 {tempElement.EquipmentElement.Item?.Name} 到玩家库存");

        //    // 检查目标装备槽是否被占用
        //    bool isSlotOccupied = this._currentCharacter.Equipment[targetIndex].Item != null;
        //    List<TransferCommand> commands = new List<TransferCommand>();

        //    // 如果槽位被占用，生成卸下原装备的命令
        //    if (isSlotOccupied)
        //    {
        //        EquipmentElement oldEquipment = this._currentCharacter.Equipment[targetIndex];
        //        TransferCommand unequipCommand = TransferCommand.Transfer(
        //            amount: 1,
        //            fromSide: InventoryLogic.InventorySide.Equipment,
        //            toSide: InventoryLogic.InventorySide.PlayerInventory,
        //            elementToTransfer: new ItemRosterElement(oldEquipment, 1),
        //            fromEquipmentIndex: targetIndex,
        //            toEquipmentIndex: EquipmentIndex.None,
        //            character: this._currentCharacter,
        //            civilianEquipment: !this.IsInWarSet
        //        );
        //        commands.Add(unequipCommand);
        //        Debug.Print($"生成卸下命令：{oldEquipment.Item?.Name}");
        //    }

        //    // 生成装备新物品的命令
        //    TransferCommand equipCommand = TransferCommand.Transfer(
        //        amount: 1,
        //        fromSide: InventoryLogic.InventorySide.PlayerInventory, // 明确来源为玩家库存
        //        toSide: InventoryLogic.InventorySide.Equipment,
        //        elementToTransfer: new ItemRosterElement(tempElement.EquipmentElement, 1),
        //        fromEquipmentIndex: EquipmentIndex.None, // 来自库存，无装备槽索引
        //        toEquipmentIndex: targetIndex,
        //        character: this._currentCharacter,
        //        civilianEquipment: !this.IsInWarSet
        //    );
        //    commands.Add(equipCommand);
        //    Debug.Print($"生成装备命令：{tempElement.EquipmentElement.Item?.Name} -> Slot {targetIndex}");

        //    // 执行所有转移命令
        //    this._inventoryLogic.AddTransferCommands(commands);
        //    Debug.Print("已提交转移命令列表");

        //    // 恢复玩家库存（移除临时插入的虚构物品）
        //    playerInventory.AddToCounts(tempElement.EquipmentElement, -1);
        //    Debug.Print($"已从库存移除临时物品 {tempElement.EquipmentElement.Item?.Name}");
        //}

        private void EquipEquipment(SPSkillItemVM itemVM)
        {
            if (itemVM == null || string.IsNullOrEmpty(itemVM.StringId))
            {
                return;
            }
            // 创建一个新的 SPSkillItemVM 实例，并刷新其数据（标记为装备栏物品）
            SPSkillItemVM SPSkillItemVM = new SPSkillItemVM();
            SPSkillItemVM.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);

            // 检查当前是否允许装备该物品（如角色是否满足装备条件）
            if (!this.IsItemEquipmentPossible(SPSkillItemVM))
            {
                return; // 不允许装备则退出
            }
            SkillSet skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(this._currentCharacter.StringId);
            if (skillSet == null)
            {
                SkillConfigManager.Instance.SetSkillSetForTroop(_currentCharacter.StringId, new SkillSet());
                skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(this._currentCharacter.StringId);
            }
            SkillFactory._skillRegistry.TryGetValue("NullSkill", out SkillBase skillBase);
            ItemRosterElement itemRosterElement = new ItemRosterElement(new ItemObject(itemVM.ItemRosterElement.EquipmentElement.Item));
            SkillFactory._skillRegistry.TryGetValue(itemVM.ItemRosterElement.EquipmentElement.Item.StringId, out SkillBase value);
            switch (TargetEquipmentType)
            {
                case EquipmentIndex.None:
                    break;
                case EquipmentIndex.WeaponItemBeginSlot:
                    this.CharacterWeapon1Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.WeaponItemBeginSlot);
                    skillSet.Spells[0] = value;
                    break;
                case EquipmentIndex.Weapon1:
                    this.CharacterWeapon2Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon1);
                    skillSet.Spells[1] = value;
                    break;
                case EquipmentIndex.Weapon2:
                    this.CharacterWeapon3Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon2);
                    skillSet.Spells[2] = value;
                    break;
                case EquipmentIndex.Weapon3:
                    this.CharacterWeapon4Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon3);
                    skillSet.Spells[3] = value;
                    break;
                case EquipmentIndex.ExtraWeaponSlot:

                    break;
                case EquipmentIndex.NumAllWeaponSlots:
                    this.CharacterHelmSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.NumAllWeaponSlots);
                    skillSet.MainActive = value;
                    break;
                case EquipmentIndex.Body:
                    this.CharacterTorsoSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Body);
                    skillSet.Passive = value;
                    break;
                case EquipmentIndex.Leg:
                    break;
                case EquipmentIndex.Gloves:
                    this.CharacterGloveSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Gloves);
                    skillSet.CombatArt = value;
                    break;
                case EquipmentIndex.Cape:
                    this.CharacterCloakSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Cape);
                    skillSet.SubActive = value;
                    break;
                case EquipmentIndex.ArmorItemEndSlot:
                    break;
                case EquipmentIndex.HorseHarness:
                    break;
                default:
                    break;
            }
            SkillConfigManager.Instance.SetSkillSetForTroop(_currentCharacter.StringId, skillSet);
            return;
        }


        private void UnequipEquipment(SPSkillItemVM itemVM)
        {
            if (itemVM == null || string.IsNullOrEmpty(itemVM.StringId)) return;
            // 创建一个新的 SPSkillItemVM 实例，并刷新其数据（标记为装备栏物品）
            SPSkillItemVM SPSkillItemVM = new SPSkillItemVM();
            SPSkillItemVM.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
            SkillSet skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(this._currentCharacter.StringId);
            if (skillSet == null)
            {
                SkillConfigManager.Instance.SetSkillSetForTroop(_currentCharacter.StringId, new SkillSet());
            }
            SkillFactory._skillRegistry.TryGetValue("NullSkill", out SkillBase skillBase);
            ItemRosterElement itemRosterElement = new ItemRosterElement(new ItemObject(skillBase.Item));
            SkillFactory._skillRegistry.TryGetValue(itemVM.ItemRosterElement.EquipmentElement.Item.StringId, out SkillBase value);
            switch (itemVM.ItemType)
            {
                case EquipmentIndex.None:
                    break;
                case EquipmentIndex.WeaponItemBeginSlot:
                    this.CharacterWeapon1Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.WeaponItemBeginSlot);
                    skillSet.Spells[0] = skillBase;
                    break;
                case EquipmentIndex.Weapon1:
                    this.CharacterWeapon2Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon1);
                    skillSet.Spells[1] = skillBase;
                    break;
                case EquipmentIndex.Weapon2:
                    this.CharacterWeapon3Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon2);
                    skillSet.Spells[2] = skillBase;
                    break;
                case EquipmentIndex.Weapon3:
                    this.CharacterWeapon4Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon3);
                    skillSet.Spells[3] = skillBase;
                    break;
                case EquipmentIndex.ExtraWeaponSlot:

                    break;
                case EquipmentIndex.NumAllWeaponSlots:
                    this.CharacterHelmSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.NumAllWeaponSlots);
                    skillSet.MainActive = skillBase;
                    break;
                case EquipmentIndex.Body:
                    this.CharacterTorsoSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Body);
                    skillSet.Passive = skillBase;
                    break;
                case EquipmentIndex.Leg:
                    break;
                case EquipmentIndex.Gloves:
                    this.CharacterGloveSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Gloves);
                    skillSet.CombatArt = skillBase;
                    break;
                case EquipmentIndex.Cape:
                    this.CharacterCloakSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Cape);
                    skillSet.SubActive = skillBase;
                    break;
                case EquipmentIndex.ArmorItemEndSlot:
                    break;
                case EquipmentIndex.HorseHarness:
                    break;
                default:
                    break;
            }
            SkillConfigManager.Instance.SetSkillSetForTroop(_currentCharacter.StringId, skillSet);
            return;



            // 1. 获取目标装备槽索引
            EquipmentIndex equipmentIndex = (EquipmentIndex)itemVM.ItemType;

            // 2. 保存原始物品
            EquipmentElement originalEquipment = this._currentCharacter.Equipment[equipmentIndex];

            // 3. 临时将虚构物品插入装备槽
            this._currentCharacter.Equipment[equipmentIndex] = itemVM.ItemRosterElement.EquipmentElement;

            // 4. 创建 TransferCommand（此时校验通过）
            TransferCommand command = TransferCommand.Transfer(
                amount: 1,
                fromSide: InventoryLogic.InventorySide.Equipment,
                toSide: InventoryLogic.InventorySide.PlayerInventory,
                elementToTransfer: new ItemRosterElement(
                    this._currentCharacter.Equipment[equipmentIndex], // 使用当前装备槽物品
                    1
                ),
                fromEquipmentIndex: equipmentIndex,
                toEquipmentIndex: EquipmentIndex.None,
                character: this._currentCharacter,
                civilianEquipment: !this.IsInWarSet
            );

            // 5. 提交转移命令
            this._inventoryLogic.AddTransferCommand(command);

            // 6. 恢复装备槽原始物品（可选）
            this._currentCharacter.Equipment[equipmentIndex] = originalEquipment;
        }


        private void UpdateEquipment(Equipment equipment, SPSkillItemVM itemVM, EquipmentIndex itemType)
        {
            if (this.ActiveEquipment == equipment)
            {
                this.RefreshEquipment(itemVM, itemType);
            }
            equipment[itemType] = ((itemVM == null) ? default(EquipmentElement) : itemVM.ItemRosterElement.EquipmentElement);
        }


        private void UnequipEquipmentWithEquipmentIndex(EquipmentIndex slotType)
        {
            switch (slotType)
            {
                case EquipmentIndex.None:
                    return;
                case EquipmentIndex.WeaponItemBeginSlot:
                    this.UnequipEquipment(this.CharacterWeapon1Slot);
                    return;
                case EquipmentIndex.Weapon1:
                    this.UnequipEquipment(this.CharacterWeapon2Slot);
                    return;
                case EquipmentIndex.Weapon2:
                    this.UnequipEquipment(this.CharacterWeapon3Slot);
                    return;
                case EquipmentIndex.Weapon3:
                    this.UnequipEquipment(this.CharacterWeapon4Slot);
                    return;
                case EquipmentIndex.ExtraWeaponSlot:
                    this.UnequipEquipment(this.CharacterBannerSlot);
                    return;
                case EquipmentIndex.NumAllWeaponSlots:
                    this.UnequipEquipment(this.CharacterHelmSlot);
                    return;
                case EquipmentIndex.Body:
                    this.UnequipEquipment(this.CharacterTorsoSlot);
                    return;
                case EquipmentIndex.Leg:
                    this.UnequipEquipment(this.CharacterBootSlot);
                    return;
                case EquipmentIndex.Gloves:
                    this.UnequipEquipment(this.CharacterGloveSlot);
                    return;
                case EquipmentIndex.Cape:
                    this.UnequipEquipment(this.CharacterCloakSlot);
                    return;
                case EquipmentIndex.ArmorItemEndSlot:
                    this.UnequipEquipment(this.CharacterMountSlot);
                    if (!string.IsNullOrEmpty(this.CharacterMountArmorSlot.StringId))
                    {
                        this.UnequipEquipment(this.CharacterMountArmorSlot);
                    }
                    return;
                case EquipmentIndex.HorseHarness:
                    this.UnequipEquipment(this.CharacterMountArmorSlot);
                    return;
                default:
                    return;
            }
        }


        protected void RefreshEquipment(SPSkillItemVM itemVM, EquipmentIndex itemType)
        {
            switch (itemType)
            {
                case EquipmentIndex.None:
                    return;
                case EquipmentIndex.WeaponItemBeginSlot:
                    this.CharacterWeapon1Slot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Weapon1:
                    this.CharacterWeapon2Slot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Weapon2:
                    this.CharacterWeapon3Slot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Weapon3:
                    this.CharacterWeapon4Slot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.ExtraWeaponSlot:
                    this.CharacterBannerSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.NumAllWeaponSlots:
                    this.CharacterHelmSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Body:
                    this.CharacterTorsoSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Leg:
                    this.CharacterBootSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Gloves:
                    this.CharacterGloveSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.Cape:
                    this.CharacterCloakSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.ArmorItemEndSlot:
                    this.CharacterMountSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                case EquipmentIndex.HorseHarness:
                    this.CharacterMountArmorSlot.RefreshWith(itemVM, InventoryLogic.InventorySide.Equipment);
                    return;
                default:
                    return;
            }
        }


        private bool UpdateCurrentCharacterIfPossible(int characterIndex, bool isFromRightSide)
        {
            CharacterObject character = (isFromRightSide ? this._rightTroopRoster : this._leftTroopRoster).GetElementCopyAtIndex(characterIndex).Character;
            if (character.IsHero)
            {
                if (!character.HeroObject.CanHeroEquipmentBeChanged())
                {
                    Hero mainHero = Hero.MainHero;
                    bool flag;
                    if (mainHero == null)
                    {
                        flag = false;
                    }
                    else
                    {
                        Clan clan = mainHero.Clan;
                        bool? flag2 = (clan != null) ? new bool?(clan.Lords.Contains(character.HeroObject)) : null;
                        bool flag3 = true;
                        flag = (flag2.GetValueOrDefault() == flag3 & flag2 != null);
                    }
                    if (!flag)
                    {
                        return false;
                    }
                }
                this._currentInventoryCharacterIndex = characterIndex;
                this._currentCharacter = character;
                this.MainCharacter.FillFrom(this._currentCharacter.HeroObject, -1, false, false);
                if (this._currentCharacter.IsHero)
                {
                    CharacterViewModel mainCharacter = this.MainCharacter;
                    IFaction mapFaction = this._currentCharacter.HeroObject.MapFaction;
                    mainCharacter.ArmorColor1 = ((mapFaction != null) ? mapFaction.Color : 0U);
                    CharacterViewModel mainCharacter2 = this.MainCharacter;
                    IFaction mapFaction2 = this._currentCharacter.HeroObject.MapFaction;
                    mainCharacter2.ArmorColor2 = ((mapFaction2 != null) ? mapFaction2.Color2 : 0U);
                }
                this.UpdateRightCharacter();
                this.RefreshInformationValues();
                return true;
            }
            return false;
        }


        private bool DoesCompanionExist()
        {
            for (int i = 1; i < this._rightTroopRoster.Count; i++)
            {
                CharacterObject character = this._rightTroopRoster.GetElementCopyAtIndex(i).Character;
                if (character.IsHero && !character.HeroObject.CanHeroEquipmentBeChanged() && character.HeroObject != Hero.MainHero)
                {
                    return true;
                }
            }
            return false;
        }


        private void UpdateLeftCharacter()
        {
            this.IsTradingWithSettlement = false;
            if (this._inventoryLogic.LeftRosterName != null)
            {
                this.LeftInventoryOwnerName = this._inventoryLogic.LeftRosterName.ToString();
                Settlement settlement = this._currentCharacter.HeroObject.CurrentSettlement;
                if (settlement != null && InventoryManager.Instance.CurrentMode == InventoryMode.Warehouse)
                {
                    this.IsTradingWithSettlement = true;
                    this.ProductionTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetSettlementProductionTooltip(settlement));
                    return;
                }
            }
            else
            {
                Settlement settlement = this._currentCharacter.HeroObject.CurrentSettlement;
                if (settlement != null)
                {
                    this.LeftInventoryOwnerName = settlement.Name.ToString();
                    this.ProductionTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetSettlementProductionTooltip(settlement));
                    this.IsTradingWithSettlement = !settlement.IsHideout;
                    if (this._inventoryLogic.InventoryListener != null)
                    {
                        this.LeftInventoryOwnerGold = this._inventoryLogic.InventoryListener.GetGold();
                        return;
                    }
                }
                else
                {
                    PartyBase oppositePartyFromListener = this._inventoryLogic.OppositePartyFromListener;
                    MobileParty mobileParty = (oppositePartyFromListener != null) ? oppositePartyFromListener.MobileParty : null;
                    if (mobileParty != null && (mobileParty.IsCaravan || mobileParty.IsVillager))
                    {
                        this.LeftInventoryOwnerName = mobileParty.Name.ToString();
                        InventoryListener inventoryListener = this._inventoryLogic.InventoryListener;
                        this.LeftInventoryOwnerGold = ((inventoryListener != null) ? inventoryListener.GetGold() : 0);
                        return;
                    }
                    this.LeftInventoryOwnerName = GameTexts.FindText("str_loot", null).ToString();
                }
            }
        }


        private void UpdateRightCharacter()
        {
            this.UpdateCharacterEquipment();
            this.UpdateCharacterArmorValues();
            this.RefreshCharacterTotalWeight();
            this.RefreshCharacterCanUseItem();
            this.CurrentCharacterName = this._currentCharacter.Name.ToString();
            this.RightInventoryOwnerGold = Hero.MainHero.Gold - this._inventoryLogic.TotalAmount;
        }


        private SPSkillItemVM InitializeCharacterEquipmentSlot(ItemRosterElement itemRosterElement, EquipmentIndex equipmentIndex)
        {
            SPSkillItemVM SPSkillItemVM;
            if (!itemRosterElement.IsEmpty)
            {
                SPSkillItemVM = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(itemRosterElement.EquipmentElement.Item.StringId), this._usageType, itemRosterElement, InventoryLogic.InventorySide.Equipment, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(itemRosterElement, InventoryLogic.InventorySide.Equipment), new EquipmentIndex?(equipmentIndex));
            }
            else
            {
                SPSkillItemVM = new SPSkillItemVM();
                SPSkillItemVM.RefreshWith(null, InventoryLogic.InventorySide.Equipment);
            }
            return SPSkillItemVM;
        }


        private void UpdateCharacterEquipment()
        {
            SkillSet skillSet = SkillConfigManager.Instance.GetSkillSetForTroop(this._currentCharacter.StringId);
            SkillBase skillBase;
            if (skillSet == null)
            {
                this.CharacterHelmSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.NumAllWeaponSlots), 1), EquipmentIndex.NumAllWeaponSlots);
                this.CharacterCloakSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Cape), 1), EquipmentIndex.Cape);
                this.CharacterTorsoSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Body), 1), EquipmentIndex.Body);
                this.CharacterGloveSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Gloves), 1), EquipmentIndex.Gloves);
                this.CharacterBootSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Leg), 1), EquipmentIndex.Leg);
                this.CharacterMountSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.ArmorItemEndSlot), 1), EquipmentIndex.ArmorItemEndSlot);
                this.CharacterMountArmorSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.HorseHarness), 1), EquipmentIndex.HorseHarness);
                this.CharacterWeapon1Slot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.WeaponItemBeginSlot), 1), EquipmentIndex.WeaponItemBeginSlot);
                this.CharacterWeapon2Slot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Weapon1), 1), EquipmentIndex.Weapon1);
                this.CharacterWeapon3Slot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Weapon2), 1), EquipmentIndex.Weapon2);
                this.CharacterWeapon4Slot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.Weapon3), 1), EquipmentIndex.Weapon3);
                this.CharacterBannerSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(this.ActiveEquipment.GetEquipmentFromSlot(EquipmentIndex.ExtraWeaponSlot), 1), EquipmentIndex.ExtraWeaponSlot);
            }
            else
            {
                SkillFactory._skillRegistry.TryGetValue("NullSkill", out skillBase);
                ItemRosterElement itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.MainActive != null ? skillSet.MainActive.Item : skillBase.Item));
                this.CharacterHelmSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.NumAllWeaponSlots);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.SubActive != null ? skillSet.SubActive.Item : skillBase.Item));
                this.CharacterCloakSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Cape);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.Passive != null ? skillSet.Passive.Item : skillBase.Item));
                this.CharacterTorsoSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Body);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.CombatArt != null ? skillSet.CombatArt.Item : skillBase.Item));
                this.CharacterGloveSlot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Gloves);
                //药水位，暂时为空
                this.CharacterBootSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(new ItemObject(skillBase.Item)), EquipmentIndex.Leg);
                //马匹位暂时无用，为空
                this.CharacterMountSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(new ItemObject(skillBase.Item)), EquipmentIndex.ArmorItemEndSlot);
                //马匹位暂时无用，为空
                this.CharacterMountArmorSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(new ItemObject(skillBase.Item)), EquipmentIndex.HorseHarness);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.Spells[0] != null ? skillSet.Spells[0].Item : skillBase.Item));
                this.CharacterWeapon1Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.WeaponItemBeginSlot);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.Spells[1] != null ? skillSet.Spells[1].Item : skillBase.Item));
                this.CharacterWeapon2Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon1);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.Spells[2] != null ? skillSet.Spells[2].Item : skillBase.Item));
                this.CharacterWeapon3Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon2);
                itemRosterElement = new ItemRosterElement(new ItemObject(skillSet.Spells[3] != null ? skillSet.Spells[3].Item : skillBase.Item));
                this.CharacterWeapon4Slot = this.InitializeCharacterEquipmentSlot(itemRosterElement, EquipmentIndex.Weapon3);

                //旗帜位暂时无用，为空
                this.CharacterBannerSlot = this.InitializeCharacterEquipmentSlot(new ItemRosterElement(new ItemObject(skillBase.Item)), EquipmentIndex.ExtraWeaponSlot);



            }

            //this.MainCharacter.SetEquipment(this.ActiveEquipment);
        }


        private void UpdateCharacterArmorValues()
        {
            this.CurrentCharacterArmArmor = 0;// this._currentCharacter.GetArmArmorSum(!this.IsInWarSet);
            this.CurrentCharacterBodyArmor = 0;//this._currentCharacter.GetBodyArmorSum(!this.IsInWarSet);
            this.CurrentCharacterHeadArmor = 0;//this._currentCharacter.GetHeadArmorSum(!this.IsInWarSet);
            this.CurrentCharacterLegArmor = 0;//this._currentCharacter.GetLegArmorSum(!this.IsInWarSet);
            this.CurrentCharacterHorseArmor = 0;//this._currentCharacter.GetHorseArmorSum(!this.IsInWarSet);
        }


        private void RefreshCharacterTotalWeight()
        {
            CharacterObject currentCharacter = this._currentCharacter;
            float num = 0;// (currentCharacter != null && currentCharacter.GetPerkValue(DefaultPerks.Athletics.FormFittingArmor)) ? (1f + DefaultPerks.Athletics.FormFittingArmor.PrimaryBonus) : 1f;
            this.CurrentCharacterTotalEncumbrance = TaleWorlds.Library.MathF.Round(this.ActiveEquipment.GetTotalWeightOfWeapons() + this.ActiveEquipment.GetTotalWeightOfArmor(true) * num, 1).ToString("0.0");
        }


        private void RefreshCharacterCanUseItem()
        {
            for (int i = 0; i < this.RightItemListVM.Count; i++)
            {
                this.RightItemListVM[i].CanCharacterUseItem = this.CanCharacterUseItemBasedOnSkills(this.RightItemListVM[i].StringId);
            }
            for (int j = 0; j < this.LeftItemListVM.Count; j++)
            {
                this.LeftItemListVM[j].CanCharacterUseItem = this.CanCharacterUseItemBasedOnSkills(this.LeftItemListVM[j].StringId);
            }
        }


        private void InitializeInventory()
        {
            this.IsRefreshed = false;
            switch (this._inventoryLogic.MerchantItemType)
            {
                case InventoryManager.InventoryCategoryType.Armors:
                    this.ActiveFilterIndex = 3;
                    break;
                case InventoryManager.InventoryCategoryType.Weapon:
                    this.ActiveFilterIndex = 1;
                    break;
                case InventoryManager.InventoryCategoryType.Shield:
                    this.ActiveFilterIndex = 2;
                    break;
                case InventoryManager.InventoryCategoryType.HorseCategory:
                    this.ActiveFilterIndex = 4;
                    break;
                case InventoryManager.InventoryCategoryType.Goods:
                    this.ActiveFilterIndex = 5;
                    break;
                default:
                    this.ActiveFilterIndex = 0;
                    break;
            }
            this._equipmentCount = 0f;
            this.RightItemListVM.Clear();
            this.LeftItemListVM.Clear();
            int num = TaleWorlds.Library.MathF.Max(this._inventoryLogic.GetElementCountOnSide(InventoryLogic.InventorySide.PlayerInventory), this._inventoryLogic.GetElementCountOnSide(InventoryLogic.InventorySide.OtherInventory));
            ItemRosterElement[] array = (from i in this._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.PlayerInventory)
                                         orderby i.EquipmentElement.GetModifiedItemName().ToString()
                                         select i).ToArray<ItemRosterElement>();
            ItemRosterElement[] array2 = (from i in this._inventoryLogic.GetElementsInRoster(InventoryLogic.InventorySide.OtherInventory)
                                          orderby i.EquipmentElement.GetModifiedItemName().ToString()
                                          select i).ToArray<ItemRosterElement>();
            this._lockedItemIDs = this._viewDataTracker.GetInventoryLocks().ToList<string>();
            for (int j = 0; j < num; j++)
            {
                if (j < array.Length)
                {
                    ItemRosterElement itemRosterElement = array[j];
                    SPSkillItemVM SPSkillItemVM = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(itemRosterElement.EquipmentElement.Item.StringId), this._usageType, itemRosterElement, InventoryLogic.InventorySide.PlayerInventory, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(itemRosterElement, InventoryLogic.InventorySide.PlayerInventory), null);
                    this.UpdateFilteredStatusOfItem(SPSkillItemVM);
                    SPSkillItemVM.IsLocked = (SPSkillItemVM.InventorySide == InventoryLogic.InventorySide.PlayerInventory && this.IsItemLocked(itemRosterElement));
                    this.RightItemListVM.Add(SPSkillItemVM);
                    if (!itemRosterElement.EquipmentElement.Item.IsMountable && !itemRosterElement.EquipmentElement.Item.IsAnimal)
                    {
                        this._equipmentCount += itemRosterElement.GetRosterElementWeight();
                    }
                }
                if (j < array2.Length)
                {
                    ItemRosterElement itemRosterElement2 = array2[j];
                    SPSkillItemVM SPSkillItemVM2 = new SPSkillItemVM(this._inventoryLogic, this.MainCharacter.IsFemale, this.CanCharacterUseItemBasedOnSkills(itemRosterElement2.EquipmentElement.Item.StringId), this._usageType, itemRosterElement2, InventoryLogic.InventorySide.OtherInventory, this._fiveStackShortcutkeyText, this._entireStackShortcutkeyText, this._inventoryLogic.GetCostOfItemRosterElement(itemRosterElement2, InventoryLogic.InventorySide.OtherInventory), null);
                    this.UpdateFilteredStatusOfItem(SPSkillItemVM2);
                    SPSkillItemVM2.IsLocked = (SPSkillItemVM2.InventorySide == InventoryLogic.InventorySide.PlayerInventory && this.IsItemLocked(itemRosterElement2));
                    this.LeftItemListVM.Add(SPSkillItemVM2);
                }
            }
            this.RefreshInformationValues();
            this.IsRefreshed = true;
        }


        private bool IsItemLocked(ItemRosterElement item)
        {
            string text = item.EquipmentElement.Item.StringId;
            if (item.EquipmentElement.ItemModifier != null)
            {
                text += item.EquipmentElement.ItemModifier.StringId;
            }
            return this._lockedItemIDs.Contains(text);
        }


        public void CompareNextItem()
        {
            this.CycleBetweenWeaponSlots();
            this.RefreshComparedItem();
        }


        private void BuyItem(SPSkillItemVM item)
        {
            if (this.TargetEquipmentType != EquipmentIndex.None && item.ItemType != this.TargetEquipmentType && (this.TargetEquipmentType < EquipmentIndex.WeaponItemBeginSlot || this.TargetEquipmentType > EquipmentIndex.ExtraWeaponSlot || item.ItemType < EquipmentIndex.WeaponItemBeginSlot || item.ItemType > EquipmentIndex.ExtraWeaponSlot))
            {
                return;
            }
            if (this.TargetEquipmentType == EquipmentIndex.None)
            {
                this.TargetEquipmentType = item.ItemType;
                if (item.ItemType >= EquipmentIndex.WeaponItemBeginSlot && item.ItemType <= EquipmentIndex.ExtraWeaponSlot)
                {
                    this.TargetEquipmentType = this.ActiveEquipment.GetWeaponPickUpSlotIndex(item.ItemRosterElement.EquipmentElement, false);
                }
            }
            int b = item.ItemCount;
            if (item.InventorySide == InventoryLogic.InventorySide.PlayerInventory)
            {
                ItemRosterElement? itemRosterElement = this._inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.OtherInventory, item.ItemRosterElement.EquipmentElement);
                if (itemRosterElement != null)
                {
                    b = itemRosterElement.Value.Amount;
                }
            }
            TransferCommand command = TransferCommand.Transfer(TaleWorlds.Library.MathF.Min(this.TransactionCount, b), InventoryLogic.InventorySide.OtherInventory, InventoryLogic.InventorySide.PlayerInventory, item.ItemRosterElement, item.ItemType, this.TargetEquipmentType, this._currentCharacter, !this.IsInWarSet);
            this._inventoryLogic.AddTransferCommand(command);
            if (this.EquipAfterBuy)
            {
                this._equipAfterTransferStack.Push(item);
            }
        }


        private void SellItem(SPSkillItemVM item)
        {
            InventoryLogic.InventorySide inventorySide = item.InventorySide;
            int b = item.ItemCount;
            if (inventorySide == InventoryLogic.InventorySide.OtherInventory)
            {
                inventorySide = InventoryLogic.InventorySide.PlayerInventory;
                ItemRosterElement? itemRosterElement = this._inventoryLogic.FindItemFromSide(InventoryLogic.InventorySide.PlayerInventory, item.ItemRosterElement.EquipmentElement);
                if (itemRosterElement != null)
                {
                    b = itemRosterElement.Value.Amount;
                }
            }
            TransferCommand command = TransferCommand.Transfer(TaleWorlds.Library.MathF.Min(this.TransactionCount, b), inventorySide, InventoryLogic.InventorySide.OtherInventory, item.ItemRosterElement, item.ItemType, this.TargetEquipmentType, this._currentCharacter, !this.IsInWarSet);
            this._inventoryLogic.AddTransferCommand(command);
        }


        private void SlaughterItem(SPSkillItemVM item)
        {
            int num = 1;
            if (this.IsFiveStackModifierActive)
            {
                num = TaleWorlds.Library.MathF.Min(5, item.ItemCount);
            }
            else if (this.IsEntireStackModifierActive)
            {
                num = item.ItemCount;
            }
            for (int i = 0; i < num; i++)
            {
                this._inventoryLogic.SlaughterItem(item.ItemRosterElement);
            }
        }


        private void DonateItem(SPSkillItemVM item)
        {
            if (this.IsFiveStackModifierActive)
            {
                int itemCount = item.ItemCount;
                for (int i = 0; i < TaleWorlds.Library.MathF.Min(5, itemCount); i++)
                {
                    this._inventoryLogic.DonateItem(item.ItemRosterElement);
                }
                return;
            }
            this._inventoryLogic.DonateItem(item.ItemRosterElement);
        }


        private float GetCapacityBudget(MobileParty party, bool isBuy)
        {
            if (isBuy)
            {
                int? num = (party != null) ? new int?(party.InventoryCapacity) : null;
                float? num2 = ((num != null) ? new float?((float)num.GetValueOrDefault()) : null) - this._equipmentCount;
                if (num2 == null)
                {
                    return 0f;
                }
                return num2.GetValueOrDefault();
            }
            else
            {
                if (this._inventoryLogic.OtherSideCapacityData != null)
                {
                    return (float)this._inventoryLogic.OtherSideCapacityData.GetCapacity() - this.LeftItemListVM.Sum((SPSkillItemVM x) => x.ItemRosterElement.GetRosterElementWeight());
                }
                return 0f;
            }
        }


        private void TransferAll(bool isBuy)
        {
            this.IsRefreshed = false;
            List<TransferCommand> list = new List<TransferCommand>(this.LeftItemListVM.Count);
            MBBindingList<SPSkillItemVM> mbbindingList = isBuy ? this.LeftItemListVM : this.RightItemListVM;
            MobileParty mobileParty;
            if (!isBuy)
            {
                PartyBase oppositePartyFromListener = this._inventoryLogic.OppositePartyFromListener;
                mobileParty = ((oppositePartyFromListener != null) ? oppositePartyFromListener.MobileParty : null);
            }
            else
            {
                mobileParty = MobileParty.MainParty;
            }
            MobileParty party = mobileParty;
            float num = 0f;
            float capacityBudget = this.GetCapacityBudget(party, isBuy);
            SPSkillItemVM SPSkillItemVM = mbbindingList.FirstOrDefault((SPSkillItemVM x) => !x.IsFiltered && !x.IsLocked);
            float num2 = (SPSkillItemVM != null) ? SPSkillItemVM.ItemRosterElement.EquipmentElement.GetEquipmentElementWeight() : 0f;
            bool flag = capacityBudget <= num2;
            InventoryLogic.InventorySide fromSide = isBuy ? InventoryLogic.InventorySide.OtherInventory : InventoryLogic.InventorySide.PlayerInventory;
            InventoryLogic.InventorySide inventorySide = isBuy ? InventoryLogic.InventorySide.PlayerInventory : InventoryLogic.InventorySide.OtherInventory;
            List<SPSkillItemVM> list2 = new List<SPSkillItemVM>();
            bool flag2 = this._inventoryLogic.CanInventoryCapacityIncrease(inventorySide);
            for (int i = 0; i < mbbindingList.Count; i++)
            {
                SPSkillItemVM SPSkillItemVM2 = mbbindingList[i];
                if (SPSkillItemVM2 != null && !SPSkillItemVM2.IsFiltered && SPSkillItemVM2 != null && !SPSkillItemVM2.IsLocked && SPSkillItemVM2 != null && SPSkillItemVM2.IsTransferable)
                {
                    int num3 = SPSkillItemVM2.ItemRosterElement.Amount;
                    if (!flag)
                    {
                        float equipmentElementWeight = SPSkillItemVM2.ItemRosterElement.EquipmentElement.GetEquipmentElementWeight();
                        float num4 = num + equipmentElementWeight * (float)num3;
                        if (flag2)
                        {
                            if (this._inventoryLogic.GetCanItemIncreaseInventoryCapacity(mbbindingList[i].ItemRosterElement.EquipmentElement.Item))
                            {
                                list2.Add(mbbindingList[i]);
                                goto IL_29E;
                            }
                            if (num4 >= capacityBudget && list2.Count > 0)
                            {
                                List<TransferCommand> list3 = new List<TransferCommand>(list2.Count);
                                for (int j = 0; j < list2.Count; j++)
                                {
                                    SPSkillItemVM SPSkillItemVM3 = list2[j];
                                    TransferCommand item = TransferCommand.Transfer(SPSkillItemVM3.ItemRosterElement.Amount, fromSide, inventorySide, SPSkillItemVM3.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, this._currentCharacter, !this.IsInWarSet);
                                    list3.Add(item);
                                }
                                this._inventoryLogic.AddTransferCommands(list3);
                                list3.Clear();
                                list2.Clear();
                                capacityBudget = this.GetCapacityBudget(party, isBuy);
                            }
                        }
                        if (num3 > 0 && num4 > capacityBudget)
                        {
                            num3 = MBMath.ClampInt(num3, 0, TaleWorlds.Library.MathF.Floor((capacityBudget - num) / equipmentElementWeight));
                            i = mbbindingList.Count;
                        }
                        num += (float)num3 * equipmentElementWeight;
                    }
                    if (num3 > 0)
                    {
                        TransferCommand item2 = TransferCommand.Transfer(num3, fromSide, inventorySide, SPSkillItemVM2.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, this._currentCharacter, !this.IsInWarSet);
                        list.Add(item2);
                    }
                }
            IL_29E:;
            }
            if (num <= capacityBudget)
            {
                foreach (SPSkillItemVM SPSkillItemVM4 in list2)
                {
                    TransferCommand item3 = TransferCommand.Transfer(SPSkillItemVM4.ItemRosterElement.Amount, fromSide, inventorySide, SPSkillItemVM4.ItemRosterElement, EquipmentIndex.None, EquipmentIndex.None, this._currentCharacter, !this.IsInWarSet);
                    list.Add(item3);
                }
            }
            this._inventoryLogic.AddTransferCommands(list);
            this.RefreshInformationValues();
            this.ExecuteRemoveZeroCounts();
            this.IsRefreshed = true;
        }


        public void ExecuteBuyAllItems()
        {
            this.TransferAll(true);
        }


        public void ExecuteSellAllItems()
        {
            return;
            this.TransferAll(false);
        }


        public void ExecuteBuyItemTest()
        {
            this.TransactionCount = 1;
            this.EquipAfterBuy = false;
            int totalGold = Hero.MainHero.Gold;
            foreach (SPSkillItemVM SPSkillItemVM in this.LeftItemListVM.Where(delegate (SPSkillItemVM i)
            {
                ItemObject item = i.ItemRosterElement.EquipmentElement.Item;
                return item != null && item.IsFood && i.ItemCost <= totalGold;
            }))
            {
                if (SPSkillItemVM.ItemCost <= totalGold)
                {
                    this.ProcessBuyItem(SPSkillItemVM, false);
                    totalGold -= SPSkillItemVM.ItemCost;
                }
            }
        }


        public void ExecuteResetTranstactions()
        {
            this._inventoryLogic.Reset(false);
            InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_inventory_reset_message", null).ToString()));
            this.CurrentFocusedItem = null;
        }


        public void ExecuteResetAndCompleteTranstactions()
        {
            if (InventoryManager.Instance.CurrentMode == InventoryMode.Loot)
            {
                InformationManager.ShowInquiry(new InquiryData("", GameTexts.FindText("str_leaving_loot_behind", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate ()
                {
                    if (!this._isFinalized)
                    {
                        this._inventoryLogic.Reset(true);
                        SkillInventoryManager.Instance.CloseInventoryPresentation(true);
                    }
                }, null, "", 0f, null, null, null), false, false);
                return;
            }
            this._inventoryLogic.Reset(true);
            SkillInventoryManager.Instance.CloseInventoryPresentation(true);
        }


        public void ExecuteCompleteTranstactions()
        {
            if (InventoryManager.Instance.CurrentMode == InventoryMode.Loot && !this._inventoryLogic.IsThereAnyChanges() && this._inventoryLogic.GetElementsInInitialRoster(InventoryLogic.InventorySide.OtherInventory).Any<ItemRosterElement>())
            {
                InformationManager.ShowInquiry(new InquiryData("", GameTexts.FindText("str_leaving_loot_behind", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), new Action(this.HandleDone), null, "", 0f, null, null, null), false, false);
                return;
            }
            this.HandleDone();
        }


        private void HandleDone()
        {
            if (!this._isFinalized)
            {
                MBInformationManager.HideInformations();
                bool flag = this._inventoryLogic.TotalAmount < 0;
                InventoryListener inventoryListener = this._inventoryLogic.InventoryListener;
                bool flag2 = ((inventoryListener != null) ? inventoryListener.GetGold() : 0) >= TaleWorlds.Library.MathF.Abs(this._inventoryLogic.TotalAmount);
                int num = (int)this._inventoryLogic.XpGainFromDonations;
                int num2 = (this._usageType == InventoryMode.Default && num == 0 && !Game.Current.CheatMode) ? this._inventoryLogic.GetElementCountOnSide(InventoryLogic.InventorySide.OtherInventory) : 0;
                if (flag && !flag2)
                {
                    InformationManager.ShowInquiry(new InquiryData("", GameTexts.FindText("str_trader_doesnt_have_enough_money", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate ()
                    {
                        SkillInventoryManager.Instance.CloseInventoryPresentation(false);
                    }, null, "", 0f, null, null, null), false, false);
                }
                else if (num2 > 0)
                {
                    InformationManager.ShowInquiry(new InquiryData("", GameTexts.FindText("str_discarding_items", null).ToString(), true, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate ()
                    {
                        SkillInventoryManager.Instance.CloseInventoryPresentation(false);
                    }, null, "", 0f, null, null, null), false, false);
                }
                else
                {
                    SkillInventoryManager.Instance.CloseInventoryPresentation(false);
                }
                this.SaveItemLockStates();
                this.SaveItemSortStates();
            }
        }


        private void SaveItemLockStates()
        {
            this._viewDataTracker.SetInventoryLocks(this._lockedItemIDs);
        }


        private void SaveItemSortStates()
        {
            this._viewDataTracker.InventorySetSortPreference((int)this._usageType, (int)this.PlayerInventorySortController.CurrentSortOption.Value, (int)this.PlayerInventorySortController.CurrentSortState.Value);
        }


        public void ExecuteTransferWithParameters(SPSkillItemVM item, int index, string targetTag)
        {
            // 根据目标标签（targetTag）执行不同的物品转移逻辑。
            if (targetTag == "OverCharacter")
            {
                // 如果目标是角色身上（OverCharacter），重置目标装备索引为 -1。
                this.TargetEquipmentIndex = -1;

                // 如果物品在对方库存中（OtherInventory）：
                if (item.InventorySide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // 设置交易数量为 1（TransactionCount 表示交易数量）。
                    item.TransactionCount = 1;
                    this.TransactionCount = 1;

                    // 调用 ProcessEquipItem 方法处理装备逻辑（将物品装备到角色身上）。
                    this.ProcessEquipItem(item);
                    return; // 结束方法。
                }

                // 如果物品在玩家库存中（PlayerInventory）：
                if (item.InventorySide == InventoryLogic.InventorySide.PlayerInventory)
                {
                    // 直接调用 ProcessEquipItem 方法处理装备逻辑。
                    this.ProcessEquipItem(item);
                    return; // 结束方法。
                }
            }
            else if (targetTag == "PlayerInventory")
            {
                // 如果目标是玩家库存（PlayerInventory），同样重置目标装备索引为 -1。
                this.TargetEquipmentIndex = -1;

                // 如果物品在装备栏中（Equipment）：
                if (item.InventorySide == InventoryLogic.InventorySide.Equipment)
                {
                    // 调用 ProcessUnequipItem 方法处理卸下装备逻辑（从角色身上卸下装备）。
                    this.ProcessUnequipItem(item);
                    return; // 结束方法。
                }

                // 如果物品在对方库存中（OtherInventory）：
                if (item.InventorySide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // 设置交易数量为物品的总数（ItemCount）。
                    item.TransactionCount = item.ItemCount;
                    this.TransactionCount = item.ItemCount;

                    // 调用 ProcessBuyItem 方法处理购买逻辑（从对方库存购买物品）。
                    this.ProcessBuyItem(item, false);
                    return; // 结束方法。
                }
            }
            else if (targetTag == "OtherInventory")
            {
                // 如果目标是玩家库存（PlayerInventory），同样重置目标装备索引为 -1。
                this.TargetEquipmentIndex = -1;

                // 如果物品在装备栏中（Equipment）：
                if (item.InventorySide == InventoryLogic.InventorySide.Equipment)
                {
                    // 调用 ProcessUnequipItem 方法处理卸下装备逻辑（从角色身上卸下装备）。
                    this.ProcessUnequipItem(item);
                    return; // 结束方法。
                }

                // 如果物品在对方库存中（OtherInventory）：
                if (item.InventorySide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // 设置交易数量为物品的总数（ItemCount）。
                    item.TransactionCount = item.ItemCount;
                    this.TransactionCount = item.ItemCount;

                    // 调用 ProcessBuyItem 方法处理购买逻辑（从对方库存购买物品）。
                    this.ProcessBuyItem(item, false);
                    return; // 结束方法。
                }
                return;
                // 如果目标是对方库存（OtherInventory）：
                if (item.InventorySide != InventoryLogic.InventorySide.OtherInventory)
                {
                    // 如果物品不在对方库存中，设置交易数量为物品的总数（ItemCount）。
                    item.TransactionCount = item.ItemCount;
                    this.TransactionCount = item.ItemCount;

                    // 调用 ProcessSellItem 方法处理出售逻辑（将物品出售给对方库存）。
                    this.ProcessSellItem(item, false);
                    return; // 结束方法。
                }
            }
            else if (targetTag.StartsWith("Equipment"))
            {
                // 如果目标是装备栏中的某个槽位（例如 Equipment_0, Equipment_1 等）：
                // 提取目标装备槽位索引（去掉 "Equipment_" 前缀后解析为整数）。
                this.TargetEquipmentIndex = int.Parse(targetTag.Substring("Equipment".Length + 1));

                // 如果物品在对方库存中（OtherInventory）：
                if (item.InventorySide == InventoryLogic.InventorySide.OtherInventory)
                {
                    // 设置交易数量为 1。
                    item.TransactionCount = 1;
                    this.TransactionCount = 1;

                    // 调用 ProcessEquipItem 方法处理装备逻辑。
                    this.ProcessEquipItem(item);
                    return; // 结束方法。
                }

                // 如果物品在玩家库存中（PlayerInventory）或装备栏中（Equipment）：
                if (item.InventorySide == InventoryLogic.InventorySide.PlayerInventory || item.InventorySide == InventoryLogic.InventorySide.Equipment)
                {
                    // 调用 ProcessEquipItem 方法处理装备逻辑。
                    this.ProcessEquipItem(item);
                }
            }
        }


        private void UpdateIsDoneDisabled()
        {
            this.IsDoneDisabled = !this._inventoryLogic.CanPlayerCompleteTransaction();
        }


        private void ProcessFilter(SPInventoryVM.Filters filterIndex)
        {

            this.ActiveFilterIndex = (int)filterIndex;
            this.IsRefreshed = false;


            foreach (SPSkillItemVM SPSkillItemVM in this.LeftItemListVM)
            {
                if (SPSkillItemVM != null)
                {
                    this.UpdateFilteredStatusOfItem(SPSkillItemVM);
                }
            }
            foreach (SPSkillItemVM SPSkillItemVM2 in this.RightItemListVM)
            {
                if (SPSkillItemVM2 != null)
                {
                    this.UpdateFilteredStatusOfItem(SPSkillItemVM2);
                }
            }
            this.IsRefreshed = true;
        }


        private void UpdateFilteredStatusOfItem(SPSkillItemVM item)
        {
            bool flag = true;
            SkillType searchTarget = SkillType.MainActive;
            switch (this.ActiveFilterIndex)
            {
                case 1:
                    searchTarget = SkillType.MainActive;
                    _activeFilterIndex = SPInventoryVM.Filters.Weapons;
                    break;
                case 2:
                    searchTarget = SkillType.SubActive;
                    _activeFilterIndex = SPInventoryVM.Filters.ShieldsAndRanged;
                    break;
                case 3:
                    searchTarget = SkillType.Spell;
                    _activeFilterIndex = SPInventoryVM.Filters.Armors;
                    break;
                case 4:
                    searchTarget = SkillType.Passive;
                    _activeFilterIndex = SPInventoryVM.Filters.Mounts;
                    break;
                case 5:
                    searchTarget = SkillType.CombatArt;
                    _activeFilterIndex = SPInventoryVM.Filters.Miscellaneous;
                    break;
                default:
                    // 默认显示所有类型
                    flag = false;
                    item.IsFiltered = (flag);
                    return;
            }
            /**/
            //bool flag = !this._filters[this._activeFilterIndex].Contains(item.TypeId);
            SkillFactory._skillRegistry.TryGetValue(item.StringId, out SkillBase val);
            flag = !(val.Type == searchTarget) ||
            (searchTarget == SkillType.Passive && val.Type == SkillType.Passive_Spell) ||
            (searchTarget == SkillType.CombatArt && val.Type == SkillType.CombatArt_Spell) ||
            (searchTarget == SkillType.Spell && val.Type == SkillType.Spell_CombatArt);

            item.IsFiltered = (flag);
        }


        private void OnSearchTextChanged(bool isLeft)
        {
            if (this.IsSearchAvailable)
            {
                (isLeft ? this.LeftItemListVM : this.RightItemListVM).ApplyActionOnAllItems(delegate (SPSkillItemVM x)
                {
                    this.UpdateFilteredStatusOfItem(x);
                });
            }
        }


        public void ExecuteFilterNone()
        {
            this.ProcessFilter(SPInventoryVM.Filters.All);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.All));
        }


        public void ExecuteFilterWeapons()
        {
            this.ProcessFilter(SPInventoryVM.Filters.Weapons);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.Weapons));
        }


        public void ExecuteFilterArmors()
        {
            this.ProcessFilter(SPInventoryVM.Filters.Armors);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.Armors));
        }


        public void ExecuteFilterShieldsAndRanged()
        {
            this.ProcessFilter(SPInventoryVM.Filters.ShieldsAndRanged);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.ShieldsAndRanged));
        }


        public void ExecuteFilterMounts()
        {
            this.ProcessFilter(SPInventoryVM.Filters.Mounts);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.Mounts));
        }


        public void ExecuteFilterMisc()
        {
            this.ProcessFilter(SPInventoryVM.Filters.Miscellaneous);
            Game.Current.EventManager.TriggerEvent<InventoryFilterChangedEvent>(new InventoryFilterChangedEvent(SPInventoryVM.Filters.Miscellaneous));
        }


        public void CycleBetweenWeaponSlots()
        {
            EquipmentIndex selectedEquipmentIndex = (EquipmentIndex)this._selectedEquipmentIndex;
            if (selectedEquipmentIndex >= EquipmentIndex.WeaponItemBeginSlot && selectedEquipmentIndex < EquipmentIndex.NumAllWeaponSlots)
            {
                int selectedEquipmentIndex2 = this._selectedEquipmentIndex;
                do
                {
                    if (this._selectedEquipmentIndex < 3)
                    {
                        this._selectedEquipmentIndex++;
                    }
                    else
                    {
                        this._selectedEquipmentIndex = 0;
                    }
                }
                while (this._selectedEquipmentIndex != selectedEquipmentIndex2 && this.GetItemFromIndex((EquipmentIndex)this._selectedEquipmentIndex).ItemRosterElement.EquipmentElement.Item == null);
            }
        }


        private SPSkillItemVM GetItemFromIndex(EquipmentIndex itemType)
        {
            switch (itemType)
            {
                case EquipmentIndex.WeaponItemBeginSlot:
                    return this.CharacterWeapon1Slot;
                case EquipmentIndex.Weapon1:
                    return this.CharacterWeapon2Slot;
                case EquipmentIndex.Weapon2:
                    return this.CharacterWeapon3Slot;
                case EquipmentIndex.Weapon3:
                    return this.CharacterWeapon4Slot;
                case EquipmentIndex.ExtraWeaponSlot:
                    return this.CharacterBannerSlot;
                case EquipmentIndex.NumAllWeaponSlots:
                    return this.CharacterHelmSlot;
                case EquipmentIndex.Body:
                    return this.CharacterTorsoSlot;
                case EquipmentIndex.Leg:
                    return this.CharacterBootSlot;
                case EquipmentIndex.Gloves:
                    return this.CharacterGloveSlot;
                case EquipmentIndex.Cape:
                    return this.CharacterCloakSlot;
                case EquipmentIndex.ArmorItemEndSlot:
                    return this.CharacterMountSlot;
                case EquipmentIndex.HorseHarness:
                    return this.CharacterMountArmorSlot;
                default:
                    return null;
            }
        }


        private void OnTutorialNotificationElementIDChange(TutorialNotificationElementChangeEvent obj)
        {
            if (obj.NewNotificationElementID != this._latestTutorialElementID)
            {
                if (this._latestTutorialElementID != null)
                {
                    if (obj.NewNotificationElementID != "TransferButtonOnlyFood" && this._isFoodTransferButtonHighlightApplied)
                    {
                        this.SetFoodTransferButtonHighlightState(false);
                        this._isFoodTransferButtonHighlightApplied = false;
                    }
                    if (obj.NewNotificationElementID != "InventoryMicsFilter" && this.IsMicsFilterHighlightEnabled)
                    {
                        this.IsMicsFilterHighlightEnabled = false;
                    }
                    if (obj.NewNotificationElementID != "CivilianFilter" && this.IsCivilianFilterHighlightEnabled)
                    {
                        this.IsCivilianFilterHighlightEnabled = false;
                    }
                    if (obj.NewNotificationElementID != "InventoryOtherBannerItems" && this.IsBannerItemsHighlightApplied)
                    {
                        this.SetBannerItemsHighlightState(false);
                        this.IsCivilianFilterHighlightEnabled = false;
                    }
                }
                this._latestTutorialElementID = obj.NewNotificationElementID;
                if (!string.IsNullOrEmpty(this._latestTutorialElementID))
                {
                    if (!this._isFoodTransferButtonHighlightApplied && this._latestTutorialElementID == "TransferButtonOnlyFood")
                    {
                        this.SetFoodTransferButtonHighlightState(true);
                        this._isFoodTransferButtonHighlightApplied = true;
                    }
                    if (!this.IsMicsFilterHighlightEnabled && this._latestTutorialElementID == "InventoryMicsFilter")
                    {
                        this.IsMicsFilterHighlightEnabled = true;
                    }
                    if (!this.IsCivilianFilterHighlightEnabled && this._latestTutorialElementID == "CivilianFilter")
                    {
                        this.IsCivilianFilterHighlightEnabled = true;
                    }
                    if (!this.IsBannerItemsHighlightApplied && this._latestTutorialElementID == "InventoryOtherBannerItems")
                    {
                        this.IsBannerItemsHighlightApplied = true;
                        this.ExecuteFilterMisc();
                        this.SetBannerItemsHighlightState(true);
                        return;
                    }
                }
                else
                {
                    if (this._isFoodTransferButtonHighlightApplied)
                    {
                        this.SetFoodTransferButtonHighlightState(false);
                        this._isFoodTransferButtonHighlightApplied = false;
                    }
                    if (this.IsMicsFilterHighlightEnabled)
                    {
                        this.IsMicsFilterHighlightEnabled = false;
                    }
                    if (this.IsCivilianFilterHighlightEnabled)
                    {
                        this.IsCivilianFilterHighlightEnabled = false;
                    }
                    if (this.IsBannerItemsHighlightApplied)
                    {
                        this.SetBannerItemsHighlightState(false);
                        this.IsBannerItemsHighlightApplied = false;
                    }
                }
            }
        }


        private void SetFoodTransferButtonHighlightState(bool state)
        {
            for (int i = 0; i < this.LeftItemListVM.Count; i++)
            {
                SPSkillItemVM SPSkillItemVM = (SPSkillItemVM)this.LeftItemListVM[i];
                if (SPSkillItemVM.ItemRosterElement.EquipmentElement.Item.IsFood)
                {
                    SPSkillItemVM.IsTransferButtonHighlighted = state;
                }
            }
        }


        private void SetBannerItemsHighlightState(bool state)
        {
            for (int i = 0; i < this.LeftItemListVM.Count; i++)
            {
                SPSkillItemVM SPSkillItemVM = (SPSkillItemVM)this.LeftItemListVM[i];
                if (SPSkillItemVM.ItemRosterElement.EquipmentElement.Item.IsBannerItem)
                {
                    SPSkillItemVM.IsItemHighlightEnabled = state;
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel ResetHint
        {
            get
            {
                return this._resetHint;
            }
            set
            {
                if (value != this._resetHint)
                {
                    this._resetHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "ResetHint");
                }
            }
        }




        [DataSourceProperty]
        public string LeftInventoryLabel
        {
            get
            {
                return this._leftInventoryLabel;
            }
            set
            {
                if (value != this._leftInventoryLabel)
                {
                    this._leftInventoryLabel = value;
                    base.OnPropertyChangedWithValue<string>(value, "LeftInventoryLabel");
                }
            }
        }




        [DataSourceProperty]
        public string RightInventoryLabel
        {
            get
            {
                return this._rightInventoryLabel;
            }
            set
            {
                if (value != this._rightInventoryLabel)
                {
                    this._rightInventoryLabel = value;
                    base.OnPropertyChangedWithValue<string>(value, "RightInventoryLabel");
                }
            }
        }




        [DataSourceProperty]
        public string DoneLbl
        {
            get
            {
                return this._doneLbl;
            }
            set
            {
                if (value != this._doneLbl)
                {
                    this._doneLbl = value;
                    base.OnPropertyChangedWithValue<string>(value, "DoneLbl");
                }
            }
        }




        [DataSourceProperty]
        public bool IsDoneDisabled
        {
            get
            {
                return this._isDoneDisabled;
            }
            set
            {
                if (value != this._isDoneDisabled)
                {
                    this._isDoneDisabled = value;
                    base.OnPropertyChangedWithValue(value, "IsDoneDisabled");
                }
            }
        }




        [DataSourceProperty]
        public bool OtherSideHasCapacity
        {
            get
            {
                return this._otherSideHasCapacity;
            }
            set
            {
                if (value != this._otherSideHasCapacity)
                {
                    this._otherSideHasCapacity = value;
                    base.OnPropertyChangedWithValue(value, "OtherSideHasCapacity");
                }
            }
        }




        [DataSourceProperty]
        public bool IsSearchAvailable
        {
            get
            {
                return this._isSearchAvailable;
            }
            set
            {
                if (value != this._isSearchAvailable)
                {
                    if (!value)
                    {
                        this.LeftSearchText = string.Empty;
                        this.RightSearchText = string.Empty;
                    }
                    this._isSearchAvailable = value;
                    base.OnPropertyChangedWithValue(value, "IsSearchAvailable");
                }
            }
        }




        [DataSourceProperty]
        public bool IsOtherInventoryGoldRelevant
        {
            get
            {
                return this._isOtherInventoryGoldRelevant;
            }
            set
            {
                if (value != this._isOtherInventoryGoldRelevant)
                {
                    this._isOtherInventoryGoldRelevant = value;
                    base.OnPropertyChangedWithValue(value, "IsOtherInventoryGoldRelevant");
                }
            }
        }




        [DataSourceProperty]
        public string CancelLbl
        {
            get
            {
                return this._cancelLbl;
            }
            set
            {
                if (value != this._cancelLbl)
                {
                    this._cancelLbl = value;
                    base.OnPropertyChangedWithValue<string>(value, "CancelLbl");
                }
            }
        }




        [DataSourceProperty]
        public string ResetLbl
        {
            get
            {
                return this._resetLbl;
            }
            set
            {
                if (value != this._resetLbl)
                {
                    this._resetLbl = value;
                    base.OnPropertyChangedWithValue<string>(value, "ResetLbl");
                }
            }
        }




        [DataSourceProperty]
        public string TypeText
        {
            get
            {
                return this._typeText;
            }
            set
            {
                if (value != this._typeText)
                {
                    this._typeText = value;
                    base.OnPropertyChangedWithValue<string>(value, "TypeText");
                }
            }
        }




        [DataSourceProperty]
        public string NameText
        {
            get
            {
                return this._nameText;
            }
            set
            {
                if (value != this._nameText)
                {
                    this._nameText = value;
                    base.OnPropertyChangedWithValue<string>(value, "NameText");
                }
            }
        }




        [DataSourceProperty]
        public string QuantityText
        {
            get
            {
                return this._quantityText;
            }
            set
            {
                if (value != this._quantityText)
                {
                    this._quantityText = value;
                    base.OnPropertyChangedWithValue<string>(value, "QuantityText");
                }
            }
        }




        [DataSourceProperty]
        public string CostText
        {
            get
            {
                return this._costText;
            }
            set
            {
                if (value != this._costText)
                {
                    this._costText = value;
                    base.OnPropertyChangedWithValue<string>(value, "CostText");
                }
            }
        }




        [DataSourceProperty]
        public string SearchPlaceholderText
        {
            get
            {
                return this._searchPlaceholderText;
            }
            set
            {
                if (value != this._searchPlaceholderText)
                {
                    this._searchPlaceholderText = value;
                    base.OnPropertyChangedWithValue<string>(value, "SearchPlaceholderText");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel ProductionTooltip
        {
            get
            {
                return this._productionTooltip;
            }
            set
            {
                if (value != this._productionTooltip)
                {
                    this._productionTooltip = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "ProductionTooltip");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel EquipmentMaxCountHint
        {
            get
            {
                return this._equipmentMaxCountHint;
            }
            set
            {
                if (value != this._equipmentMaxCountHint)
                {
                    this._equipmentMaxCountHint = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "EquipmentMaxCountHint");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel CurrentCharacterSkillsTooltip
        {
            get
            {
                return this._currentCharacterSkillsTooltip;
            }
            set
            {
                if (value != this._currentCharacterSkillsTooltip)
                {
                    this._currentCharacterSkillsTooltip = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "CurrentCharacterSkillsTooltip");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel NoSaddleHint
        {
            get
            {
                return this._noSaddleHint;
            }
            set
            {
                if (value != this._noSaddleHint)
                {
                    this._noSaddleHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "NoSaddleHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel DonationLblHint
        {
            get
            {
                return this._donationLblHint;
            }
            set
            {
                if (value != this._donationLblHint)
                {
                    this._donationLblHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "DonationLblHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel ArmArmorHint
        {
            get
            {
                return this._armArmorHint;
            }
            set
            {
                if (value != this._armArmorHint)
                {
                    this._armArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "ArmArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel BodyArmorHint
        {
            get
            {
                return this._bodyArmorHint;
            }
            set
            {
                if (value != this._bodyArmorHint)
                {
                    this._bodyArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "BodyArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel HeadArmorHint
        {
            get
            {
                return this._headArmorHint;
            }
            set
            {
                if (value != this._headArmorHint)
                {
                    this._headArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "HeadArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel LegArmorHint
        {
            get
            {
                return this._legArmorHint;
            }
            set
            {
                if (value != this._legArmorHint)
                {
                    this._legArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "LegArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel HorseArmorHint
        {
            get
            {
                return this._horseArmorHint;
            }
            set
            {
                if (value != this._horseArmorHint)
                {
                    this._horseArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "HorseArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterAllHint
        {
            get
            {
                return this._filterAllHint;
            }
            set
            {
                if (value != this._filterAllHint)
                {
                    this._filterAllHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterAllHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterWeaponHint
        {
            get
            {
                return this._filterWeaponHint;
            }
            set
            {
                if (value != this._filterWeaponHint)
                {
                    this._filterWeaponHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterWeaponHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterArmorHint
        {
            get
            {
                return this._filterArmorHint;
            }
            set
            {
                if (value != this._filterArmorHint)
                {
                    this._filterArmorHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterArmorHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterShieldAndRangedHint
        {
            get
            {
                return this._filterShieldAndRangedHint;
            }
            set
            {
                if (value != this._filterShieldAndRangedHint)
                {
                    this._filterShieldAndRangedHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterShieldAndRangedHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterMountAndHarnessHint
        {
            get
            {
                return this._filterMountAndHarnessHint;
            }
            set
            {
                if (value != this._filterMountAndHarnessHint)
                {
                    this._filterMountAndHarnessHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterMountAndHarnessHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel FilterMiscHint
        {
            get
            {
                return this._filterMiscHint;
            }
            set
            {
                if (value != this._filterMiscHint)
                {
                    this._filterMiscHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "FilterMiscHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel CivilianOutfitHint
        {
            get
            {
                return this._civilianOutfitHint;
            }
            set
            {
                if (value != this._civilianOutfitHint)
                {
                    this._civilianOutfitHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "CivilianOutfitHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel BattleOutfitHint
        {
            get
            {
                return this._battleOutfitHint;
            }
            set
            {
                if (value != this._battleOutfitHint)
                {
                    this._battleOutfitHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "BattleOutfitHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentHelmSlotHint
        {
            get
            {
                return this._equipmentHelmSlotHint;
            }
            set
            {
                if (value != this._equipmentHelmSlotHint)
                {
                    this._equipmentHelmSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentHelmSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentArmorSlotHint
        {
            get
            {
                return this._equipmentArmorSlotHint;
            }
            set
            {
                if (value != this._equipmentArmorSlotHint)
                {
                    this._equipmentArmorSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentArmorSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentBootSlotHint
        {
            get
            {
                return this._equipmentBootSlotHint;
            }
            set
            {
                if (value != this._equipmentBootSlotHint)
                {
                    this._equipmentBootSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentBootSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentCloakSlotHint
        {
            get
            {
                return this._equipmentCloakSlotHint;
            }
            set
            {
                if (value != this._equipmentCloakSlotHint)
                {
                    this._equipmentCloakSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentCloakSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentGloveSlotHint
        {
            get
            {
                return this._equipmentGloveSlotHint;
            }
            set
            {
                if (value != this._equipmentGloveSlotHint)
                {
                    this._equipmentGloveSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentGloveSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentHarnessSlotHint
        {
            get
            {
                return this._equipmentHarnessSlotHint;
            }
            set
            {
                if (value != this._equipmentHarnessSlotHint)
                {
                    this._equipmentHarnessSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentHarnessSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentMountSlotHint
        {
            get
            {
                return this._equipmentMountSlotHint;
            }
            set
            {
                if (value != this._equipmentMountSlotHint)
                {
                    this._equipmentMountSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentMountSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentWeaponSlotHint
        {
            get
            {
                return this._equipmentWeaponSlotHint;
            }
            set
            {
                if (value != this._equipmentWeaponSlotHint)
                {
                    this._equipmentWeaponSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentWeaponSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipmentBannerSlotHint
        {
            get
            {
                return this._equipmentBannerSlotHint;
            }
            set
            {
                if (value != this._equipmentBannerSlotHint)
                {
                    this._equipmentBannerSlotHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipmentBannerSlotHint");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel BuyAllHint
        {
            get
            {
                return this._buyAllHint;
            }
            set
            {
                if (value != this._buyAllHint)
                {
                    this._buyAllHint = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "BuyAllHint");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel SellAllHint
        {
            get
            {
                return this._sellAllHint;
            }
            set
            {
                if (value != this._sellAllHint)
                {
                    this._sellAllHint = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "SellAllHint");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel PreviousCharacterHint
        {
            get
            {
                return this._previousCharacterHint;
            }
            set
            {
                if (value != this._previousCharacterHint)
                {
                    this._previousCharacterHint = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "PreviousCharacterHint");
                }
            }
        }




        [DataSourceProperty]
        public BasicTooltipViewModel NextCharacterHint
        {
            get
            {
                return this._nextCharacterHint;
            }
            set
            {
                if (value != this._nextCharacterHint)
                {
                    this._nextCharacterHint = value;
                    base.OnPropertyChangedWithValue<BasicTooltipViewModel>(value, "NextCharacterHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel WeightHint
        {
            get
            {
                return this._weightHint;
            }
            set
            {
                if (value != this._weightHint)
                {
                    this._weightHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "WeightHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel PreviewHint
        {
            get
            {
                return this._previewHint;
            }
            set
            {
                if (value != this._previewHint)
                {
                    this._previewHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "PreviewHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel EquipHint
        {
            get
            {
                return this._equipHint;
            }
            set
            {
                if (value != this._equipHint)
                {
                    this._equipHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "EquipHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel UnequipHint
        {
            get
            {
                return this._unequipHint;
            }
            set
            {
                if (value != this._unequipHint)
                {
                    this._unequipHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "UnequipHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel SellHint
        {
            get
            {
                return this._sellHint;
            }
            set
            {
                if (value != this._sellHint)
                {
                    this._sellHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "SellHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel PlayerSideCapacityExceededHint
        {
            get
            {
                return this._playerSideCapacityExceededHint;
            }
            set
            {
                if (value != this._playerSideCapacityExceededHint)
                {
                    this._playerSideCapacityExceededHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "PlayerSideCapacityExceededHint");
                }
            }
        }




        [DataSourceProperty]
        public HintViewModel OtherSideCapacityExceededHint
        {
            get
            {
                return this._otherSideCapacityExceededHint;
            }
            set
            {
                if (value != this._otherSideCapacityExceededHint)
                {
                    this._otherSideCapacityExceededHint = value;
                    base.OnPropertyChangedWithValue<HintViewModel>(value, "OtherSideCapacityExceededHint");
                }
            }
        }




        [DataSourceProperty]
        public SelectorVM<InventoryCharacterSelectorItemVM> CharacterList
        {
            get
            {
                return this._characterList;
            }
            set
            {
                if (value != this._characterList)
                {
                    this._characterList = value;
                    base.OnPropertyChangedWithValue<SelectorVM<InventoryCharacterSelectorItemVM>>(value, "CharacterList");
                }
            }
        }




        [DataSourceProperty]
        public SPInventorySortControllerVM PlayerInventorySortController
        {
            get
            {
                return this._playerInventorySortController;
            }
            set
            {
                if (value != this._playerInventorySortController)
                {
                    this._playerInventorySortController = value;
                    base.OnPropertyChangedWithValue<SPInventorySortControllerVM>(value, "PlayerInventorySortController");
                }
            }
        }




        [DataSourceProperty]
        public SPInventorySortControllerVM OtherInventorySortController
        {
            get
            {
                return this._otherInventorySortController;
            }
            set
            {
                if (value != this._otherInventorySortController)
                {
                    this._otherInventorySortController = value;
                    base.OnPropertyChangedWithValue<SPInventorySortControllerVM>(value, "OtherInventorySortController");
                }
            }
        }




        [DataSourceProperty]
        public ItemPreviewVM ItemPreview
        {
            get
            {
                return this._itemPreview;
            }
            set
            {
                if (value != this._itemPreview)
                {
                    this._itemPreview = value;
                    base.OnPropertyChangedWithValue<ItemPreviewVM>(value, "ItemPreview");
                }
            }
        }




        [DataSourceProperty]
        public int ActiveFilterIndex
        {
            get
            {
                return (int)this._activeFilterIndex;
            }
            set
            {
                if (value != (int)this._activeFilterIndex)
                {
                    this._activeFilterIndex = (SPInventoryVM.Filters)value;
                    base.OnPropertyChangedWithValue(value, "ActiveFilterIndex");
                }
            }
        }




        [DataSourceProperty]
        public bool CompanionExists
        {
            get
            {
                return this._companionExists;
            }
            set
            {
                if (value != this._companionExists)
                {
                    this._companionExists = value;
                    base.OnPropertyChangedWithValue(value, "CompanionExists");
                }
            }
        }




        [DataSourceProperty]
        public bool IsTradingWithSettlement
        {
            get
            {
                return this._isTradingWithSettlement;
            }
            set
            {
                if (value != this._isTradingWithSettlement)
                {
                    this._isTradingWithSettlement = value;
                    base.OnPropertyChangedWithValue(value, "IsTradingWithSettlement");
                }
            }
        }




        [DataSourceProperty]
        public bool IsInWarSet
        {
            get
            {
                return this._isInWarSet;
            }
            set
            {
                if (value != this._isInWarSet)
                {
                    this._isInWarSet = value;
                    base.OnPropertyChangedWithValue(value, "IsInWarSet");
                    this.UpdateRightCharacter();
                    Game.Current.EventManager.TriggerEvent<InventoryEquipmentTypeChangedEvent>(new InventoryEquipmentTypeChangedEvent(value));
                }
            }
        }




        [DataSourceProperty]
        public bool IsMicsFilterHighlightEnabled
        {
            get
            {
                return this._isMicsFilterHighlightEnabled;
            }
            set
            {
                if (value != this._isMicsFilterHighlightEnabled)
                {
                    this._isMicsFilterHighlightEnabled = value;
                    base.OnPropertyChangedWithValue(value, "IsMicsFilterHighlightEnabled");
                }
            }
        }




        [DataSourceProperty]
        public bool IsCivilianFilterHighlightEnabled
        {
            get
            {
                return this._isCivilianFilterHighlightEnabled;
            }
            set
            {
                if (value != this._isCivilianFilterHighlightEnabled)
                {
                    this._isCivilianFilterHighlightEnabled = value;
                    base.OnPropertyChangedWithValue(value, "IsCivilianFilterHighlightEnabled");
                }
            }
        }




        [DataSourceProperty]
        public ItemMenuVM ItemMenu
        {
            get
            {
                return this._itemMenu;
            }
            set
            {
                if (value != this._itemMenu)
                {
                    this._itemMenu = value;
                    base.OnPropertyChangedWithValue<ItemMenuVM>(value, "ItemMenu");
                }
            }
        }




        [DataSourceProperty]
        public string PlayerSideCapacityExceededText
        {
            get
            {
                return this._playerSideCapacityExceededText;
            }
            set
            {
                if (value != this._playerSideCapacityExceededText)
                {
                    this._playerSideCapacityExceededText = value;
                    base.OnPropertyChangedWithValue<string>(value, "PlayerSideCapacityExceededText");
                }
            }
        }




        [DataSourceProperty]
        public string OtherSideCapacityExceededText
        {
            get
            {
                return this._otherSideCapacityExceededText;
            }
            set
            {
                if (value != this._otherSideCapacityExceededText)
                {
                    this._otherSideCapacityExceededText = value;
                    base.OnPropertyChangedWithValue<string>(value, "OtherSideCapacityExceededText");
                }
            }
        }




        [DataSourceProperty]
        public string LeftSearchText
        {
            get
            {
                return this._leftSearchText;
            }
            set
            {
                if (value != this._leftSearchText)
                {
                    this._leftSearchText = value;
                    base.OnPropertyChangedWithValue<string>(value, "LeftSearchText");
                    this.OnSearchTextChanged(true);
                }
            }
        }




        [DataSourceProperty]
        public string RightSearchText
        {
            get
            {
                return this._rightSearchText;
            }
            set
            {
                if (value != this._rightSearchText)
                {
                    this._rightSearchText = value;
                    base.OnPropertyChangedWithValue<string>(value, "RightSearchText");
                    this.OnSearchTextChanged(false);
                }
            }
        }




        [DataSourceProperty]
        public bool HasGainedExperience
        {
            get
            {
                return this._hasGainedExperience;
            }
            set
            {
                if (value != this._hasGainedExperience)
                {
                    this._hasGainedExperience = value;
                    base.OnPropertyChangedWithValue(value, "HasGainedExperience");
                }
            }
        }




        [DataSourceProperty]
        public bool IsDonationXpGainExceedsMax
        {
            get
            {
                return this._isDonationXpGainExceedsMax;
            }
            set
            {
                if (value != this._isDonationXpGainExceedsMax)
                {
                    this._isDonationXpGainExceedsMax = value;
                    base.OnPropertyChangedWithValue(value, "IsDonationXpGainExceedsMax");
                }
            }
        }




        [DataSourceProperty]
        public bool NoSaddleWarned
        {
            get
            {
                return this._noSaddleWarned;
            }
            set
            {
                if (value != this._noSaddleWarned)
                {
                    this._noSaddleWarned = value;
                    base.OnPropertyChangedWithValue(value, "NoSaddleWarned");
                }
            }
        }




        [DataSourceProperty]
        public bool PlayerEquipmentCountWarned
        {
            get
            {
                return this._playerEquipmentCountWarned;
            }
            set
            {
                if (value != this._playerEquipmentCountWarned)
                {
                    this._playerEquipmentCountWarned = value;
                    base.OnPropertyChangedWithValue(value, "PlayerEquipmentCountWarned");
                }
            }
        }




        [DataSourceProperty]
        public bool OtherEquipmentCountWarned
        {
            get
            {
                return this._otherEquipmentCountWarned;
            }
            set
            {
                if (value != this._otherEquipmentCountWarned)
                {
                    this._otherEquipmentCountWarned = value;
                    base.OnPropertyChangedWithValue(value, "OtherEquipmentCountWarned");
                }
            }
        }




        [DataSourceProperty]
        public string OtherEquipmentCountText
        {
            get
            {
                return this._otherEquipmentCountText;
            }
            set
            {
                if (value != this._otherEquipmentCountText)
                {
                    this._otherEquipmentCountText = value;
                    base.OnPropertyChangedWithValue<string>(value, "OtherEquipmentCountText");
                }
            }
        }




        [DataSourceProperty]
        public string PlayerEquipmentCountText
        {
            get
            {
                return this._playerEquipmentCountText;
            }
            set
            {
                if (value != this._playerEquipmentCountText)
                {
                    this._playerEquipmentCountText = value;
                    base.OnPropertyChangedWithValue<string>(value, "PlayerEquipmentCountText");
                }
            }
        }




        [DataSourceProperty]
        public string NoSaddleText
        {
            get
            {
                return this._noSaddleText;
            }
            set
            {
                if (value != this._noSaddleText)
                {
                    this._noSaddleText = value;
                    base.OnPropertyChangedWithValue<string>(value, "NoSaddleText");
                }
            }
        }




        [DataSourceProperty]
        public int TargetEquipmentIndex
        {
            get
            {
                return (int)this._targetEquipmentIndex;
            }
            set
            {
                if (value != (int)this._targetEquipmentIndex)
                {
                    this._targetEquipmentIndex = (EquipmentIndex)value;
                    base.OnPropertyChangedWithValue(value, "TargetEquipmentIndex");
                }
            }
        }




        public EquipmentIndex TargetEquipmentType
        {
            get
            {
                return this._targetEquipmentIndex;
            }
            set
            {
                if (value != this._targetEquipmentIndex)
                {
                    this._targetEquipmentIndex = value;
                    base.OnPropertyChanged("TargetEquipmentIndex");
                }
            }
        }




        [DataSourceProperty]
        public int TransactionCount
        {
            get
            {
                return this._transactionCount;
            }
            set
            {
                if (value != this._transactionCount)
                {
                    this._transactionCount = value;
                    base.OnPropertyChangedWithValue(value, "TransactionCount");
                }
                this.RefreshTransactionCost(value);
            }
        }




        [DataSourceProperty]
        public bool IsTrading
        {
            get
            {
                return this._isTrading;
            }
            set
            {
                if (value != this._isTrading)
                {
                    this._isTrading = value;
                    base.OnPropertyChangedWithValue(value, "IsTrading");
                }
            }
        }




        [DataSourceProperty]
        public bool EquipAfterBuy
        {
            get
            {
                return this._equipAfterBuy;
            }
            set
            {
                if (value != this._equipAfterBuy)
                {
                    this._equipAfterBuy = value;
                    base.OnPropertyChangedWithValue(value, "EquipAfterBuy");
                }
            }
        }




        [DataSourceProperty]
        public string TradeLbl
        {
            get
            {
                return this._tradeLbl;
            }
            set
            {
                if (value != this._tradeLbl)
                {
                    this._tradeLbl = value;
                    base.OnPropertyChangedWithValue<string>(value, "TradeLbl");
                }
            }
        }




        [DataSourceProperty]
        public string ExperienceLbl
        {
            get
            {
                return this._experienceLbl;
            }
            set
            {
                if (value != this._experienceLbl)
                {
                    this._experienceLbl = value;
                    base.OnPropertyChangedWithValue<string>(value, "ExperienceLbl");
                }
            }
        }




        [DataSourceProperty]
        public string CurrentCharacterName
        {
            get
            {
                return this._currentCharacterName;
            }
            set
            {
                if (value != this._currentCharacterName)
                {
                    this._currentCharacterName = value;
                    base.OnPropertyChangedWithValue<string>(value, "CurrentCharacterName");
                }
            }
        }




        [DataSourceProperty]
        public string RightInventoryOwnerName
        {
            get
            {
                return this._rightInventoryOwnerName;
            }
            set
            {
                if (value != this._rightInventoryOwnerName)
                {
                    this._rightInventoryOwnerName = value;
                    base.OnPropertyChangedWithValue<string>(value, "RightInventoryOwnerName");
                }
            }
        }




        [DataSourceProperty]
        public string LeftInventoryOwnerName
        {
            get
            {
                return this._leftInventoryOwnerName;
            }
            set
            {
                if (value != this._leftInventoryOwnerName)
                {
                    this._leftInventoryOwnerName = value;
                    base.OnPropertyChangedWithValue<string>(value, "LeftInventoryOwnerName");
                }
            }
        }




        [DataSourceProperty]
        public int RightInventoryOwnerGold
        {
            get
            {
                return this._rightInventoryOwnerGold;
            }
            set
            {
                if (value != this._rightInventoryOwnerGold)
                {
                    this._rightInventoryOwnerGold = value;
                    base.OnPropertyChangedWithValue(value, "RightInventoryOwnerGold");
                }
            }
        }




        [DataSourceProperty]
        public int LeftInventoryOwnerGold
        {
            get
            {
                return this._leftInventoryOwnerGold;
            }
            set
            {
                if (value != this._leftInventoryOwnerGold)
                {
                    this._leftInventoryOwnerGold = value;
                    base.OnPropertyChangedWithValue(value, "LeftInventoryOwnerGold");
                }
            }
        }




        [DataSourceProperty]
        public int ItemCountToBuy
        {
            get
            {
                return this._itemCountToBuy;
            }
            set
            {
                if (value != this._itemCountToBuy)
                {
                    this._itemCountToBuy = value;
                    base.OnPropertyChangedWithValue(value, "ItemCountToBuy");
                }
            }
        }




        [DataSourceProperty]
        public string CurrentCharacterTotalEncumbrance
        {
            get
            {
                return this._currentCharacterTotalEncumbrance;
            }
            set
            {
                if (value != this._currentCharacterTotalEncumbrance)
                {
                    this._currentCharacterTotalEncumbrance = value;
                    base.OnPropertyChangedWithValue<string>(value, "CurrentCharacterTotalEncumbrance");
                }
            }
        }




        [DataSourceProperty]
        public float CurrentCharacterLegArmor
        {
            get
            {
                return this._currentCharacterLegArmor;
            }
            set
            {
                if (TaleWorlds.Library.MathF.Abs(value - this._currentCharacterLegArmor) > 0.01f)
                {
                    this._currentCharacterLegArmor = value;
                    base.OnPropertyChangedWithValue(value, "CurrentCharacterLegArmor");
                }
            }
        }




        [DataSourceProperty]
        public float CurrentCharacterHeadArmor
        {
            get
            {
                return this._currentCharacterHeadArmor;
            }
            set
            {
                if (TaleWorlds.Library.MathF.Abs(value - this._currentCharacterHeadArmor) > 0.01f)
                {
                    this._currentCharacterHeadArmor = value;
                    base.OnPropertyChangedWithValue(value, "CurrentCharacterHeadArmor");
                }
            }
        }




        [DataSourceProperty]
        public float CurrentCharacterBodyArmor
        {
            get
            {
                return this._currentCharacterBodyArmor;
            }
            set
            {
                if (TaleWorlds.Library.MathF.Abs(value - this._currentCharacterBodyArmor) > 0.01f)
                {
                    this._currentCharacterBodyArmor = value;
                    base.OnPropertyChangedWithValue(value, "CurrentCharacterBodyArmor");
                }
            }
        }




        [DataSourceProperty]
        public float CurrentCharacterArmArmor
        {
            get
            {
                return this._currentCharacterArmArmor;
            }
            set
            {
                if (TaleWorlds.Library.MathF.Abs(value - this._currentCharacterArmArmor) > 0.01f)
                {
                    this._currentCharacterArmArmor = value;
                    base.OnPropertyChangedWithValue(value, "CurrentCharacterArmArmor");
                }
            }
        }




        [DataSourceProperty]
        public float CurrentCharacterHorseArmor
        {
            get
            {
                return this._currentCharacterHorseArmor;
            }
            set
            {
                if (TaleWorlds.Library.MathF.Abs(value - this._currentCharacterHorseArmor) > 0.01f)
                {
                    this._currentCharacterHorseArmor = value;
                    base.OnPropertyChangedWithValue(value, "CurrentCharacterHorseArmor");
                }
            }
        }




        [DataSourceProperty]
        public bool IsRefreshed
        {
            get
            {
                return this._isRefreshed;
            }
            set
            {
                if (this._isRefreshed != value)
                {
                    this._isRefreshed = value;
                    base.OnPropertyChangedWithValue(value, "IsRefreshed");
                }
            }
        }




        [DataSourceProperty]
        public bool IsExtendedEquipmentControlsEnabled
        {
            get
            {
                return this._isExtendedEquipmentControlsEnabled;
            }
            set
            {
                if (value != this._isExtendedEquipmentControlsEnabled)
                {
                    this._isExtendedEquipmentControlsEnabled = value;
                    base.OnPropertyChangedWithValue(value, "IsExtendedEquipmentControlsEnabled");
                }
            }
        }




        [DataSourceProperty]
        public bool IsFocusedOnItemList
        {
            get
            {
                return this._isFocusedOnItemList;
            }
            set
            {
                if (value != this._isFocusedOnItemList)
                {
                    this._isFocusedOnItemList = value;
                    base.OnPropertyChangedWithValue(value, "IsFocusedOnItemList");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CurrentFocusedItem
        {
            get
            {
                return this._currentFocusedItem;
            }
            set
            {
                if (value != this._currentFocusedItem)
                {
                    this._currentFocusedItem = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CurrentFocusedItem");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterHelmSlot
        {
            get
            {
                return this._characterHelmSlot;
            }
            set
            {
                if (value != this._characterHelmSlot)
                {
                    this._characterHelmSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterHelmSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterCloakSlot
        {
            get
            {
                return this._characterCloakSlot;
            }
            set
            {
                if (value != this._characterCloakSlot)
                {
                    this._characterCloakSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterCloakSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterTorsoSlot
        {
            get
            {
                return this._characterTorsoSlot;
            }
            set
            {
                if (value != this._characterTorsoSlot)
                {
                    this._characterTorsoSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterTorsoSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterGloveSlot
        {
            get
            {
                return this._characterGloveSlot;
            }
            set
            {
                if (value != this._characterGloveSlot)
                {
                    this._characterGloveSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterGloveSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterBootSlot
        {
            get
            {
                return this._characterBootSlot;
            }
            set
            {
                if (value != this._characterBootSlot)
                {
                    this._characterBootSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterBootSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterMountSlot
        {
            get
            {
                return this._characterMountSlot;
            }
            set
            {
                if (value != this._characterMountSlot)
                {
                    this._characterMountSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterMountSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterMountArmorSlot
        {
            get
            {
                return this._characterMountArmorSlot;
            }
            set
            {
                if (value != this._characterMountArmorSlot)
                {
                    this._characterMountArmorSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterMountArmorSlot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterWeapon1Slot
        {
            get
            {
                return this._characterWeapon1Slot;
            }
            set
            {
                if (value != this._characterWeapon1Slot)
                {
                    this._characterWeapon1Slot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterWeapon1Slot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterWeapon2Slot
        {
            get
            {
                return this._characterWeapon2Slot;
            }
            set
            {
                if (value != this._characterWeapon2Slot)
                {
                    this._characterWeapon2Slot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterWeapon2Slot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterWeapon3Slot
        {
            get
            {
                return this._characterWeapon3Slot;
            }
            set
            {
                if (value != this._characterWeapon3Slot)
                {
                    this._characterWeapon3Slot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterWeapon3Slot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterWeapon4Slot
        {
            get
            {
                return this._characterWeapon4Slot;
            }
            set
            {
                if (value != this._characterWeapon4Slot)
                {
                    this._characterWeapon4Slot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterWeapon4Slot");
                }
            }
        }




        [DataSourceProperty]
        public SPSkillItemVM CharacterBannerSlot
        {
            get
            {
                return this._characterBannerSlot;
            }
            set
            {
                if (value != this._characterBannerSlot)
                {
                    this._characterBannerSlot = value;
                    base.OnPropertyChangedWithValue<SPSkillItemVM>(value, "CharacterBannerSlot");
                }
            }
        }




        [DataSourceProperty]
        public HeroViewModel MainCharacter
        {
            get
            {
                return this._mainCharacter;
            }
            set
            {
                if (value != this._mainCharacter)
                {
                    this._mainCharacter = value;
                    base.OnPropertyChangedWithValue<HeroViewModel>(value, "MainCharacter");
                }
            }
        }




        [DataSourceProperty]
        public MBBindingList<SPSkillItemVM> RightItemListVM
        {
            get
            {
                return this._rightItemListVM;
            }
            set
            {
                if (value != this._rightItemListVM)
                {
                    this._rightItemListVM = value;
                    base.OnPropertyChangedWithValue<MBBindingList<SPSkillItemVM>>(value, "RightItemListVM");
                }
            }
        }




        [DataSourceProperty]
        public MBBindingList<SPSkillItemVM> LeftItemListVM
        {
            get
            {
                return this._leftItemListVM;
            }
            set
            {
                if (value != this._leftItemListVM)
                {
                    this._leftItemListVM = value;
                    base.OnPropertyChangedWithValue<MBBindingList<SPSkillItemVM>>(value, "LeftItemListVM");
                }
            }
        }




        [DataSourceProperty]
        public bool IsBannerItemsHighlightApplied
        {
            get
            {
                return this._isBannerItemsHighlightApplied;
            }
            set
            {
                if (value != this._isBannerItemsHighlightApplied)
                {
                    this._isBannerItemsHighlightApplied = value;
                    base.OnPropertyChangedWithValue(value, "IsBannerItemsHighlightApplied");
                }
            }
        }




        [DataSourceProperty]
        public int BannerTypeCode
        {
            get
            {
                return this._bannerTypeCode;
            }
            set
            {
                if (value != this._bannerTypeCode)
                {
                    this._bannerTypeCode = value;
                    base.OnPropertyChangedWithValue(value, "BannerTypeCode");
                }
            }
        }


        private TextObject GetPreviousCharacterKeyText()
        {
            if (this.PreviousCharacterInputKey == null || this._getKeyTextFromKeyId == null)
            {
                return TextObject.Empty;
            }
            return this._getKeyTextFromKeyId(this.PreviousCharacterInputKey.KeyID);
        }


        private TextObject GetNextCharacterKeyText()
        {
            if (this.NextCharacterInputKey == null || this._getKeyTextFromKeyId == null)
            {
                return TextObject.Empty;
            }
            return this._getKeyTextFromKeyId(this.NextCharacterInputKey.KeyID);
        }


        private TextObject GetBuyAllKeyText()
        {
            if (this.BuyAllInputKey == null || this._getKeyTextFromKeyId == null)
            {
                return TextObject.Empty;
            }
            return this._getKeyTextFromKeyId(this.BuyAllInputKey.KeyID);
        }


        private TextObject GetSellAllKeyText()
        {
            if (this.SellAllInputKey == null || this._getKeyTextFromKeyId == null)
            {
                return TextObject.Empty;
            }
            return this._getKeyTextFromKeyId(this.SellAllInputKey.KeyID);
        }


        public void SetResetInputKey(HotKey hotkey)
        {
            this.ResetInputKey = InputKeyItemVM.CreateFromHotKey(hotkey, true);
        }


        public void SetCancelInputKey(HotKey gameKey)
        {
            this.CancelInputKey = InputKeyItemVM.CreateFromHotKey(gameKey, true);
        }


        public void SetDoneInputKey(HotKey hotKey)
        {
            this.DoneInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
        }


        public void SetPreviousCharacterInputKey(HotKey hotKey)
        {
            this.PreviousCharacterInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
            this.SetPreviousCharacterHint();
        }


        public void SetNextCharacterInputKey(HotKey hotKey)
        {
            this.NextCharacterInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
            this.SetNextCharacterHint();
        }


        public void SetBuyAllInputKey(HotKey hotKey)
        {
            this.BuyAllInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
            this.SetBuyAllHint();
        }


        public void SetSellAllInputKey(HotKey hotKey)
        {
            this.SellAllInputKey = InputKeyItemVM.CreateFromHotKey(hotKey, true);
            this.SetSellAllHint();
        }


        public void SetGetKeyTextFromKeyIDFunc(Func<string, TextObject> getKeyTextFromKeyId)
        {
            this._getKeyTextFromKeyId = getKeyTextFromKeyId;
        }




        [DataSourceProperty]
        public InputKeyItemVM ResetInputKey
        {
            get
            {
                return this._resetInputKey;
            }
            set
            {
                if (value != this._resetInputKey)
                {
                    this._resetInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "ResetInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM CancelInputKey
        {
            get
            {
                return this._cancelInputKey;
            }
            set
            {
                if (value != this._cancelInputKey)
                {
                    this._cancelInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "CancelInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM DoneInputKey
        {
            get
            {
                return this._doneInputKey;
            }
            set
            {
                if (value != this._doneInputKey)
                {
                    this._doneInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "DoneInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM PreviousCharacterInputKey
        {
            get
            {
                return this._previousCharacterInputKey;
            }
            set
            {
                if (value != this._previousCharacterInputKey)
                {
                    this._previousCharacterInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "PreviousCharacterInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM NextCharacterInputKey
        {
            get
            {
                return this._nextCharacterInputKey;
            }
            set
            {
                if (value != this._nextCharacterInputKey)
                {
                    this._nextCharacterInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "NextCharacterInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM BuyAllInputKey
        {
            get
            {
                return this._buyAllInputKey;
            }
            set
            {
                if (value != this._buyAllInputKey)
                {
                    this._buyAllInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "BuyAllInputKey");
                }
            }
        }




        [DataSourceProperty]
        public InputKeyItemVM SellAllInputKey
        {
            get
            {
                return this._sellAllInputKey;
            }
            set
            {
                if (value != this._sellAllInputKey)
                {
                    this._sellAllInputKey = value;
                    base.OnPropertyChangedWithValue<InputKeyItemVM>(value, "SellAllInputKey");
                }
            }
        }


        void IInventoryStateHandler.ExecuteLootingScript()
        {
            this.ExecuteBuyAllItems();
        }


        void IInventoryStateHandler.ExecuteBuyConsumableItem()
        {
            this.ExecuteBuyItemTest();
        }


        void IInventoryStateHandler.ExecuteSellAllLoot()
        {
            for (int i = this.RightItemListVM.Count - 1; i >= 0; i--)
            {
                SPSkillItemVM SPSkillItemVM = (SPSkillItemVM)this.RightItemListVM[i];
                if (SPSkillItemVM.GetItemTypeWithItemObject() != EquipmentIndex.None)
                {
                    this.SellItem(SPSkillItemVM);
                }
            }
        }


        void IInventoryStateHandler.FilterInventoryAtOpening(InventoryManager.InventoryCategoryType inventoryCategoryType)
        {
            if (Game.Current == null)
            {
                Debug.FailedAssert("Game is not initialized when filtering inventory", "C:\\Develop\\MB3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\Inventory\\SPInventoryVM.cs", "FilterInventoryAtOpening", 4679);
                return;
            }
            switch (inventoryCategoryType)
            {
                case InventoryManager.InventoryCategoryType.Armors:
                    this.ExecuteFilterArmors();
                    return;
                case InventoryManager.InventoryCategoryType.Weapon:
                    this.ExecuteFilterWeapons();
                    return;
                case InventoryManager.InventoryCategoryType.Shield:
                    break;
                case InventoryManager.InventoryCategoryType.HorseCategory:
                    this.ExecuteFilterMounts();
                    return;
                case InventoryManager.InventoryCategoryType.Goods:
                    this.ExecuteFilterMisc();
                    break;
                default:
                    return;
            }
        }


        public bool DoNotSync;


        private readonly Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> _getItemUsageSetFlags;


        private readonly IViewDataTracker _viewDataTracker;


        public bool IsFiveStackModifierActive;


        public bool IsEntireStackModifierActive;


        private readonly int _donationMaxShareableXp;


        private readonly TroopRoster _rightTroopRoster;


        private InventoryMode _usageType = InventoryMode.Trade;


        private readonly TroopRoster _leftTroopRoster;


        private int _lastComparedItemIndex;


        private readonly Stack<SPSkillItemVM> _equipAfterTransferStack;


        private int _currentInventoryCharacterIndex;


        private bool _isTrading;


        private bool _isFinalized;


        private bool _isCharacterEquipmentDirty;


        private float _equipmentCount;


        private string _selectedTooltipItemStringID = "";


        private string _comparedTooltipItemStringID = "";


        private InventoryLogic _inventoryLogic;


        private CharacterObject _currentCharacter;


        private SPSkillItemVM _selectedItem;


        private string _fiveStackShortcutkeyText;


        private string _entireStackShortcutkeyText;


        private List<ItemVM> _comparedItemList;


        private List<string> _lockedItemIDs;


        private Func<string, TextObject> _getKeyTextFromKeyId;


        private readonly List<int> _everyItemType = new List<int>
        {
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            13,
            14,
            15,
            16,
            17,
            18,
            19,
            20,
            21,
            22,
            23,
            24
        };


        private readonly List<int> _weaponItemTypes = new List<int>
        {
            2,
            3,
            4
        };


        private readonly List<int> _armorItemTypes = new List<int>
        {
            12,
            13,
            14,
            15,
            21,
            22
        };


        private readonly List<int> _mountItemTypes = new List<int>
        {
            1,
            23
        };


        private readonly List<int> _shieldAndRangedItemTypes = new List<int>
        {
            7,
            5,
            6,
            8,
            9,
            10,
            16,
            17,
            18
        };


        private readonly List<int> _miscellaneousItemTypes = new List<int>
        {
            11,
            19,
            20,
            24
        };


        private readonly Dictionary<SPInventoryVM.Filters, List<int>> _filters;


        private int _selectedEquipmentIndex;


        private bool _isFoodTransferButtonHighlightApplied;


        private bool _isBannerItemsHighlightApplied;


        private string _latestTutorialElementID;


        private string _leftInventoryLabel;


        private string _rightInventoryLabel;


        private bool _otherSideHasCapacity;


        private bool _isDoneDisabled;


        private bool _isSearchAvailable;


        private bool _isOtherInventoryGoldRelevant;


        private string _doneLbl;


        private string _cancelLbl;


        private string _resetLbl;


        private string _typeText;


        private string _nameText;


        private string _quantityText;


        private string _costText;


        private string _searchPlaceholderText;


        private HintViewModel _resetHint;


        private HintViewModel _filterAllHint;


        private HintViewModel _filterWeaponHint;


        private HintViewModel _filterArmorHint;


        private HintViewModel _filterShieldAndRangedHint;


        private HintViewModel _filterMountAndHarnessHint;


        private HintViewModel _filterMiscHint;


        private HintViewModel _civilianOutfitHint;


        private HintViewModel _battleOutfitHint;


        private HintViewModel _equipmentHelmSlotHint;


        private HintViewModel _equipmentArmorSlotHint;


        private HintViewModel _equipmentBootSlotHint;


        private HintViewModel _equipmentCloakSlotHint;


        private HintViewModel _equipmentGloveSlotHint;


        private HintViewModel _equipmentHarnessSlotHint;


        private HintViewModel _equipmentMountSlotHint;


        private HintViewModel _equipmentWeaponSlotHint;


        private HintViewModel _equipmentBannerSlotHint;


        private BasicTooltipViewModel _buyAllHint;


        private BasicTooltipViewModel _sellAllHint;


        private BasicTooltipViewModel _previousCharacterHint;


        private BasicTooltipViewModel _nextCharacterHint;


        private HintViewModel _weightHint;


        private HintViewModel _armArmorHint;


        private HintViewModel _bodyArmorHint;


        private HintViewModel _headArmorHint;


        private HintViewModel _legArmorHint;


        private HintViewModel _horseArmorHint;


        private HintViewModel _previewHint;


        private HintViewModel _equipHint;


        private HintViewModel _unequipHint;


        private HintViewModel _sellHint;


        private HintViewModel _playerSideCapacityExceededHint;


        private HintViewModel _noSaddleHint;


        private HintViewModel _donationLblHint;


        private HintViewModel _otherSideCapacityExceededHint;


        private BasicTooltipViewModel _equipmentMaxCountHint;


        private BasicTooltipViewModel _currentCharacterSkillsTooltip;


        private BasicTooltipViewModel _productionTooltip;


        private HeroViewModel _mainCharacter;


        private bool _isExtendedEquipmentControlsEnabled;


        private bool _isFocusedOnItemList;


        private SPSkillItemVM _currentFocusedItem;


        private bool _equipAfterBuy;


        private MBBindingList<SPSkillItemVM> _leftItemListVM;


        private MBBindingList<SPSkillItemVM> _rightItemListVM;


        private ItemMenuVM _itemMenu;


        private SPSkillItemVM _characterHelmSlot;


        private SPSkillItemVM _characterCloakSlot;


        private SPSkillItemVM _characterTorsoSlot;


        private SPSkillItemVM _characterGloveSlot;


        private SPSkillItemVM _characterBootSlot;


        private SPSkillItemVM _characterMountSlot;


        private SPSkillItemVM _characterMountArmorSlot;


        private SPSkillItemVM _characterWeapon1Slot;


        private SPSkillItemVM _characterWeapon2Slot;


        private SPSkillItemVM _characterWeapon3Slot;


        private SPSkillItemVM _characterWeapon4Slot;


        private SPSkillItemVM _characterBannerSlot;


        private EquipmentIndex _targetEquipmentIndex = EquipmentIndex.None;


        private int _transactionCount = -1;


        private bool _isRefreshed;


        private string _tradeLbl = "";


        private string _experienceLbl = "";


        private bool _hasGainedExperience;


        private bool _isDonationXpGainExceedsMax;


        private bool _noSaddleWarned;


        private bool _otherEquipmentCountWarned;


        private bool _playerEquipmentCountWarned;


        private bool _isTradingWithSettlement;


        private string _otherEquipmentCountText;


        private string _playerEquipmentCountText;


        private string _noSaddleText;


        private string _leftSearchText = "";


        private string _playerSideCapacityExceededText;


        private string _otherSideCapacityExceededText;


        private string _rightSearchText = "";


        private bool _isInWarSet = true;


        private bool _companionExists;


        private SPInventoryVM.Filters _activeFilterIndex;


        private bool _isMicsFilterHighlightEnabled;


        private bool _isCivilianFilterHighlightEnabled;


        private ItemPreviewVM _itemPreview;


        private SelectorVM<InventoryCharacterSelectorItemVM> _characterList;


        private SPInventorySortControllerVM _otherInventorySortController;


        private SPInventorySortControllerVM _playerInventorySortController;


        private int _bannerTypeCode;


        private string _leftInventoryOwnerName;


        private int _leftInventoryOwnerGold;


        private string _rightInventoryOwnerName;


        private string _currentCharacterName;


        private int _rightInventoryOwnerGold;


        private int _itemCountToBuy;


        private float _currentCharacterArmArmor;


        private float _currentCharacterBodyArmor;


        private float _currentCharacterHeadArmor;


        private float _currentCharacterLegArmor;


        private float _currentCharacterHorseArmor;


        private string _currentCharacterTotalEncumbrance;


        private InputKeyItemVM _resetInputKey;


        private InputKeyItemVM _cancelInputKey;


        private InputKeyItemVM _doneInputKey;


        private InputKeyItemVM _previousCharacterInputKey;


        private InputKeyItemVM _nextCharacterInputKey;


        private InputKeyItemVM _buyAllInputKey;


        private InputKeyItemVM _sellAllInputKey;

        // 将父类列表转换为子类列表
        public static MBBindingList<SPSkillItemVM> ConvertToSkillItemList(MBBindingList<SPItemVM> parentList)
        {
            var childList = new MBBindingList<SPSkillItemVM>();

            foreach (var parentItem in parentList)
            {
                // 创建子类实例并复制属性
                var childItem = CreateSkillItemFromParent(parentItem);
                childList.Add(childItem);
            }

            return childList;
        }

        // 通过反射将父类对象转换为子类对象
        private static SPSkillItemVM CreateSkillItemFromParent(SPItemVM parentItem)
        {
            if (parentItem == null)
                throw new ArgumentNullException(nameof(parentItem));

            // 创建子类实例
            var childItem = new SPSkillItemVM();

            // 获取所有公共属性
            PropertyInfo[] properties = typeof(SPItemVM).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (property.CanRead && property.CanWrite)
                {
                    // 从父类读取值
                    object value = property.GetValue(parentItem);

                    // 设置子类属性
                    PropertyInfo childProperty = typeof(SPSkillItemVM).GetProperty(property.Name);
                    if (childProperty != null && childProperty.CanWrite)
                    {
                        childProperty.SetValue(childItem, value);
                    }
                }
            }

            return childItem;
        }
        // 创建适配器方法
        private MBBindingList<SPItemVM> ConvertToParentList(MBBindingList<SPSkillItemVM> childList)
        {
            // 创建新的父类列表
            var parentList = new MBBindingList<SPItemVM>();

            // 将子类列表中的元素逐个添加到父类列表
            foreach (var item in childList)
            {
                parentList.Add(item); // 子类可以隐式转换为父类
            }

            return parentList;
        }
        public enum Filters
        {

            All,

            Weapons,

            ShieldsAndRanged,

            Armors,

            Mounts,

            Miscellaneous
        }
    }

}
