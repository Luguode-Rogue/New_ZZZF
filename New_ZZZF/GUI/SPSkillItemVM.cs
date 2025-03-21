using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::TaleWorlds.CampaignSystem.CharacterDevelopment;
using global::TaleWorlds.CampaignSystem.Inventory;
using global::TaleWorlds.CampaignSystem.Settlements;
using global::TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using global::TaleWorlds.CampaignSystem.ViewModelCollection;
using global::TaleWorlds.CampaignSystem;
using global::TaleWorlds.Core.ViewModelCollection.Information;
using global::TaleWorlds.Core.ViewModelCollection;
using global::TaleWorlds.Core;
using global::TaleWorlds.Library;
using global::TaleWorlds.Localization;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using System.Reflection;
namespace New_ZZZF
{

        // Token: 0x02000088 RID: 136
    public class SPSkillItemVM : SPItemVM
    {
        /// <summary>
        /// 将 SPSkillItemVM 转换为 SPItemVM。
        /// </summary>
        /// <param name="skillItem">子类实例。</param>
        /// <returns>父类实例。</returns>
        public static SPItemVM ConvertToParentItem(SPSkillItemVM skillItem)
        {
            if (skillItem == null)
            {
                throw new ArgumentNullException(nameof(skillItem), "子类实例不能为空");
            }

            // 使用反射动态创建父类实例
            Type parentType = typeof(SPItemVM);
            SPItemVM parentItem = (SPItemVM)Activator.CreateInstance(parentType);

            // 获取子类的所有公共实例属性
            PropertyInfo[] properties = typeof(SPSkillItemVM).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                // 确保属性可读且可写
                if (property.CanRead && property.CanWrite)
                {
                    // 从子类获取属性值
                    object value = property.GetValue(skillItem);

                    // 设置父类的对应属性值
                    PropertyInfo parentProperty = parentType.GetProperty(property.Name);
                    if (parentProperty != null && parentProperty.CanWrite)
                    {
                        parentProperty.SetValue(parentItem, value);
                    }
                }
            }

            return parentItem;
        }
        /// <summary>
        /// 将 SPItemVM 转换为 SPSkillItemVM。
        /// </summary>
        /// <param name="item">父类实例。</param>
        /// <returns>子类实例。</returns>
        public static SPSkillItemVM ConvertToSkillItem(SPItemVM item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item), "父类实例不能为空");
            }

            // 使用反射动态创建子类实例
            Type skillItemType = typeof(SPSkillItemVM);
            SPSkillItemVM skillItem = (SPSkillItemVM)Activator.CreateInstance(skillItemType);

            // 获取父类的所有公共实例属性
            PropertyInfo[] properties = typeof(SPItemVM).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                // 确保属性可读且可写
                if (property.CanRead && property.CanWrite)
                {
                    // 从父类获取属性值
                    object value = property.GetValue(item);

                    // 设置子类的对应属性值
                    PropertyInfo skillItemProperty = skillItemType.GetProperty(property.Name);
                    if (skillItemProperty != null && skillItemProperty.CanWrite)
                    {
                        skillItemProperty.SetValue(skillItem, value);
                    }
                }
            }

            return skillItem;
        }
        // Token: 0x1700045A RID: 1114
        // (get) Token: 0x06000D51 RID: 3409 RVA: 0x0003624D File Offset: 0x0003444D
        // (set) Token: 0x06000D52 RID: 3410 RVA: 0x00036255 File Offset: 0x00034455
        public InventoryLogic.InventorySide InventorySide { get; private set; }

        // Token: 0x06000D53 RID: 3411 RVA: 0x0003625E File Offset: 0x0003445E
        public SPSkillItemVM()
        {
            base.StringId = "";
            base.ImageIdentifier = new ImageIdentifierVM(ImageIdentifierType.Null);
            this._itemType = EquipmentIndex.None;
        }


        // Token: 0x06000D54 RID: 3412 RVA: 0x0003628C File Offset: 0x0003448C
        public SPSkillItemVM(InventoryLogic inventoryLogic, bool isHeroFemale, bool canCharacterUseItem, InventoryMode usageType, ItemRosterElement newItem, InventoryLogic.InventorySide inventorySide, string fiveStackShortcutKeyText, string entireStackShortcutKeyText, int itemCost = 0, EquipmentIndex? itemType = EquipmentIndex.None)
            {
                if (newItem.EquipmentElement.Item == null)
                {
                    return;
                }
                this._fiveStackShortcutKeyText = fiveStackShortcutKeyText;
                this._entireStackShortcutKeyText = entireStackShortcutKeyText;
                this._usageType = usageType;
                this._tradeGoodConceptObj = Concept.All.SingleOrDefault((Concept c) => c.StringId == "str_game_objects_trade_goods");
                this._itemConceptObj = Concept.All.SingleOrDefault((Concept c) => c.StringId == "str_game_objects_item");
                this._inventoryLogic = inventoryLogic;
                this.ItemRosterElement = new ItemRosterElement(newItem.EquipmentElement, newItem.Amount);
                base.ItemCost = itemCost;
                this.ItemCount = newItem.Amount;
                this.TransactionCount = 1;
                this.ItemLevel = newItem.EquipmentElement.Item.Difficulty;
                this.InventorySide = inventorySide;
                if (itemType != null)
                {
                    EquipmentIndex? equipmentIndex = itemType;
                    EquipmentIndex equipmentIndex2 = EquipmentIndex.None;
                    if (!(equipmentIndex.GetValueOrDefault() == equipmentIndex2 & equipmentIndex != null))
                    {
                        this._itemType = itemType.Value;
                    }
                }
                base.SetItemTypeId();
                base.ItemDescription = newItem.EquipmentElement.GetModifiedItemName().ToString();
                base.StringId = CampaignUIHelper.GetItemLockStringID(newItem.EquipmentElement);
                ItemObject item = newItem.EquipmentElement.Item;
                Clan playerClan = Clan.PlayerClan;
                base.ImageIdentifier = new ImageIdentifierVM(item, (playerClan != null) ? playerClan.Banner.Serialize() : null);
                this.IsCivilianItem = newItem.EquipmentElement.Item.ItemFlags.HasAnyFlag(ItemFlags.Civilian);
                this.IsGenderDifferent = ((isHeroFemale && this.ItemRosterElement.EquipmentElement.Item.ItemFlags.HasAnyFlag(ItemFlags.NotUsableByFemale)) || (!isHeroFemale && this.ItemRosterElement.EquipmentElement.Item.ItemFlags.HasAnyFlag(ItemFlags.NotUsableByMale)));
                this.CanCharacterUseItem = canCharacterUseItem;
                this.IsArtifact = newItem.EquipmentElement.Item.IsUniqueItem;
                this.UpdateCanBeSlaughtered();
                this.UpdateHintTexts();
                InventoryLogic inventoryLogic2 = this._inventoryLogic;
                this.CanBeDonated = (inventoryLogic2 != null && inventoryLogic2.CanDonateItem(this.ItemRosterElement, this.InventorySide));
                this.TradeData = new InventoryTradeVM(this._inventoryLogic, this.ItemRosterElement, inventorySide, new Action<int, bool>(this.OnTradeApplyTransaction));
                this.IsTransferable = !this.ItemRosterElement.EquipmentElement.IsQuestItem;
                this.TradeData.IsTradeable = this.IsTransferable;
                this.IsEquipableItem = ((InventoryManager.GetInventoryItemTypeOfItem(newItem.EquipmentElement.Item) & InventoryItemType.Equipable) > InventoryItemType.None);
                this.UpdateProfitType();
            }

            // Token: 0x06000D55 RID: 3413 RVA: 0x00036568 File Offset: 0x00034768
            public override void RefreshValues()
            {
                //base.RefreshValues();
                if (this.ItemRosterElement.EquipmentElement.Item != null)
                {
                    TextObject modifiedItemName = this.ItemRosterElement.EquipmentElement.GetModifiedItemName();
                    base.ItemDescription = (((modifiedItemName != null) ? modifiedItemName.ToString() : null) ?? "");
                    return;
                }
                base.ItemDescription = "";
            }

            // Token: 0x06000D56 RID: 3414 RVA: 0x000365CC File Offset: 0x000347CC
            public void RefreshWith(SPSkillItemVM itemVM, InventoryLogic.InventorySide inventorySide)
            {
                this.InventorySide = inventorySide;
                if (itemVM == null)
                {
                    this.Reset();
                    return;
                }
                base.ItemDescription = itemVM.ItemDescription;
                base.ItemCost = itemVM.ItemCost;
                base.TypeId = itemVM.TypeId;
                this._itemType = itemVM.ItemType;
                this.ItemCount = itemVM.ItemCount;
                this.TransactionCount = itemVM.TransactionCount;
                this.ItemLevel = itemVM.ItemLevel;
                base.StringId = itemVM.StringId;
                base.ImageIdentifier = itemVM.ImageIdentifier.Clone();
                this.ItemRosterElement = itemVM.ItemRosterElement;
                this.IsCivilianItem = itemVM.IsCivilianItem;
                this.IsGenderDifferent = itemVM.IsGenderDifferent;
                this.IsEquipableItem = itemVM.IsEquipableItem;
                this.CanCharacterUseItem = this.CanCharacterUseItem;
                this.IsArtifact = itemVM.IsArtifact;
                this.UpdateCanBeSlaughtered();
                this.UpdateHintTexts();
                InventoryLogic inventoryLogic = this._inventoryLogic;
                this.CanBeDonated = (inventoryLogic != null && inventoryLogic.CanDonateItem(this.ItemRosterElement, this.InventorySide));
                this.TradeData = new InventoryTradeVM(this._inventoryLogic, itemVM.ItemRosterElement, inventorySide, new Action<int, bool>(this.OnTradeApplyTransaction));
                this.UpdateProfitType();
            }

            // Token: 0x06000D57 RID: 3415 RVA: 0x00036700 File Offset: 0x00034900
            private void Reset()
            {
                base.ItemDescription = "";
                base.ItemCost = 0;
                base.TypeId = 0;
                this._itemType = EquipmentIndex.None;
                this.ItemCount = 0;
                this.TransactionCount = 0;
                this.ItemLevel = 0;
                base.StringId = "";
                base.ImageIdentifier = new ImageIdentifierVM(ImageIdentifierType.Null);
                this.ItemRosterElement = default(ItemRosterElement);
                this.ProfitType = 0;
                this.IsCivilianItem = true;
                this.IsGenderDifferent = false;
                this.IsEquipableItem = true;
                this.IsArtifact = false;
                this.TradeData = new InventoryTradeVM(this._inventoryLogic, this.ItemRosterElement, InventoryLogic.InventorySide.None, new Action<int, bool>(this.OnTradeApplyTransaction));
            }

            // Token: 0x06000D58 RID: 3416 RVA: 0x000367AC File Offset: 0x000349AC
            private void UpdateProfitType()
            {
                this.ProfitType = 0;
                if (Campaign.Current != null)
                {
                    if (this.InventorySide == InventoryLogic.InventorySide.PlayerInventory)
                    {
                        Hero mainHero = Hero.MainHero;
                        if (mainHero == null || !mainHero.GetPerkValue(DefaultPerks.Trade.Appraiser))
                        {
                            Hero mainHero2 = Hero.MainHero;
                            if (mainHero2 == null || !mainHero2.GetPerkValue(DefaultPerks.Trade.WholeSeller))
                            {
                                return;
                            }
                        }
                        IPlayerTradeBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IPlayerTradeBehavior>();
                        if (campaignBehavior != null)
                        {
                            int num = -campaignBehavior.GetProjectedProfit(this.ItemRosterElement, base.ItemCost) + base.ItemCost;
                            this.ProfitType = (int)SPSkillItemVM.GetProfitTypeFromDiff((float)num, (float)base.ItemCost);
                            return;
                        }
                    }
                    else if (this.InventorySide == InventoryLogic.InventorySide.OtherInventory && Settlement.CurrentSettlement != null && (Settlement.CurrentSettlement.IsFortification || Settlement.CurrentSettlement.IsVillage))
                    {
                        Hero mainHero3 = Hero.MainHero;
                        if (mainHero3 == null || !mainHero3.GetPerkValue(DefaultPerks.Trade.CaravanMaster))
                        {
                            Hero mainHero4 = Hero.MainHero;
                            if (mainHero4 == null || !mainHero4.GetPerkValue(DefaultPerks.Trade.MarketDealer))
                            {
                                return;
                            }
                        }
                        float averagePriceFactorItemCategory = this._inventoryLogic.GetAveragePriceFactorItemCategory(this.ItemRosterElement.EquipmentElement.Item.ItemCategory);
                        Town town = Settlement.CurrentSettlement.IsVillage ? Settlement.CurrentSettlement.Village.Bound.Town : Settlement.CurrentSettlement.Town;
                        if (averagePriceFactorItemCategory != -99f)
                        {
                            this.ProfitType = (int)SPSkillItemVM.GetProfitTypeFromDiff(town.MarketData.GetPriceFactor(this.ItemRosterElement.EquipmentElement.Item.ItemCategory), averagePriceFactorItemCategory);
                        }
                    }
                }
            }

            // Token: 0x06000D59 RID: 3417 RVA: 0x00036934 File Offset: 0x00034B34
            public void ExecuteBuySingle()
            {
                this.TransactionCount = 1;
                ItemVM.ProcessBuyItem(this, false);
            }

            // Token: 0x06000D5A RID: 3418 RVA: 0x00036949 File Offset: 0x00034B49
            public void ExecuteSellSingle()
            {
                this.TransactionCount = 1;
                SPSkillItemVM.ProcessSellItem(this, false);
            }

            // Token: 0x06000D5B RID: 3419 RVA: 0x0003695E File Offset: 0x00034B5E
            private void OnTradeApplyTransaction(int amount, bool isBuying)
            {
                this.TransactionCount = amount;
                if (isBuying)
                {
                    ItemVM.ProcessBuyItem(this, true);
                    return;
                }
                SPSkillItemVM.ProcessSellItem(this, true);
            }

            // Token: 0x06000D5C RID: 3420 RVA: 0x00036983 File Offset: 0x00034B83
            public void ExecuteSellItem()
            {
               return;
                SPSkillItemVM.ProcessSellItem(this, false);
            }

            // Token: 0x06000D5D RID: 3421 RVA: 0x00036994 File Offset: 0x00034B94
            public void ExecuteConcept()
            {
                if (this._tradeGoodConceptObj != null)
                {
                    ItemObject item = this.ItemRosterElement.EquipmentElement.Item;
                    if (item != null && item.Type == ItemObject.ItemTypeEnum.Goods)
                    {
                        Campaign.Current.EncyclopediaManager.GoToLink(this._tradeGoodConceptObj.EncyclopediaLink);
                        return;
                    }
                }
                if (this._itemConceptObj != null)
                {
                    Campaign.Current.EncyclopediaManager.GoToLink(this._itemConceptObj.EncyclopediaLink);
                }
            }

            // Token: 0x06000D5E RID: 3422 RVA: 0x00036A0B File Offset: 0x00034C0B
            public void ExecuteResetTrade()
            {
                this.TradeData.ExecuteReset();
            }

            // Token: 0x06000D5F RID: 3423 RVA: 0x00036A18 File Offset: 0x00034C18
            private void UpdateTotalCost()
            {
                if (this.TransactionCount <= 0 || this._inventoryLogic == null || this.InventorySide == InventoryLogic.InventorySide.Equipment)
                {
                    return;
                }
                int num;
                this.TotalCost = this._inventoryLogic.GetItemTotalPrice(this.ItemRosterElement, this.TransactionCount, out num, this.InventorySide == InventoryLogic.InventorySide.OtherInventory);
            }

            // Token: 0x06000D60 RID: 3424 RVA: 0x00036A68 File Offset: 0x00034C68
            public void UpdateTradeData(bool forceUpdateAmounts)
            {
                InventoryTradeVM tradeData = this.TradeData;
                if (tradeData != null)
                {
                    tradeData.UpdateItemData(this.ItemRosterElement, this.InventorySide, forceUpdateAmounts);
                }
                this.UpdateProfitType();
            }

            // Token: 0x06000D61 RID: 3425 RVA: 0x00036A8E File Offset: 0x00034C8E
            public void ExecuteSlaughterItem()
            {
                if (this.CanBeSlaughtered)
                {
                    SPSkillItemVM.ProcessItemSlaughter(this);
                }
            }

            // Token: 0x06000D62 RID: 3426 RVA: 0x00036AA3 File Offset: 0x00034CA3
            public void ExecuteDonateItem()
            {
                if (this.CanBeDonated)
                {
                    SPSkillItemVM.ProcessItemDonate(this);
                }
            }

            // Token: 0x06000D63 RID: 3427 RVA: 0x00036AB8 File Offset: 0x00034CB8
            public void ExecuteSetFocused()
            {
                this.IsFocused = true;
                Action<SPSkillItemVM> onFocus = SPSkillItemVM.OnFocus;
                if (onFocus == null)
                {
                    return;
                }
                onFocus(this);
            }

            // Token: 0x06000D64 RID: 3428 RVA: 0x00036AD1 File Offset: 0x00034CD1
            public void ExecuteSetUnfocused()
            {
                this.IsFocused = false;
                Action<SPSkillItemVM> onFocus = SPSkillItemVM.OnFocus;
                if (onFocus == null)
                {
                    return;
                }
                onFocus(null);
            }

            // Token: 0x06000D65 RID: 3429 RVA: 0x00036AEC File Offset: 0x00034CEC
            public void UpdateCanBeSlaughtered()
            {
                InventoryLogic inventoryLogic = this._inventoryLogic;
                this.CanBeSlaughtered = (inventoryLogic != null && inventoryLogic.CanSlaughterItem(this.ItemRosterElement, this.InventorySide) && !this.ItemRosterElement.EquipmentElement.IsQuestItem);
            }

            // Token: 0x06000D66 RID: 3430 RVA: 0x00036B38 File Offset: 0x00034D38
            private string GetStackModifierString()
            {
                GameTexts.SetVariable("newline", "\n");
                GameTexts.SetVariable("STR1", "");
                GameTexts.SetVariable("STR2", "");
                if (!string.IsNullOrEmpty(this._entireStackShortcutKeyText))
                {
                    GameTexts.SetVariable("KEY_NAME", this._entireStackShortcutKeyText);
                    string content = (this.InventorySide == InventoryLogic.InventorySide.PlayerInventory) ? GameTexts.FindText("str_entire_stack_shortcut_discard_items", null).ToString() : GameTexts.FindText("str_entire_stack_shortcut_take_items", null).ToString();
                    GameTexts.SetVariable("STR1", content);
                    GameTexts.SetVariable("STR2", "");
                    if (this.ItemCount >= 5 && !string.IsNullOrEmpty(this._fiveStackShortcutKeyText))
                    {
                        GameTexts.SetVariable("KEY_NAME", this._fiveStackShortcutKeyText);
                        string content2 = (this.InventorySide == InventoryLogic.InventorySide.PlayerInventory) ? GameTexts.FindText("str_five_stack_shortcut_discard_items", null).ToString() : GameTexts.FindText("str_five_stack_shortcut_take_items", null).ToString();
                        GameTexts.SetVariable("STR2", content2);
                    }
                }
                return GameTexts.FindText("str_string_newline_string", null).ToString();
            }

            // Token: 0x06000D67 RID: 3431 RVA: 0x00036C4C File Offset: 0x00034E4C
            public void UpdateHintTexts()
            {
                base.SlaughterHint = new BasicTooltipViewModel(delegate ()
                {
                    string stackModifierString = this.GetStackModifierString();
                    GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_slaughter", null));
                    GameTexts.SetVariable("STR2", stackModifierString);
                    return GameTexts.FindText("str_string_newline_string", null).ToString();
                });
                base.DonateHint = new BasicTooltipViewModel(delegate ()
                {
                    string stackModifierString = this.GetStackModifierString();
                    GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_donate", null));
                    GameTexts.SetVariable("STR2", stackModifierString);
                    return GameTexts.FindText("str_string_newline_string", null).ToString();
                });
                base.PreviewHint = new HintViewModel(GameTexts.FindText("str_inventory_preview", null), null);
                base.EquipHint = new HintViewModel(GameTexts.FindText("str_inventory_equip", null), null);
                base.LockHint = new HintViewModel(GameTexts.FindText("str_inventory_lock", null), null);
                if (this._usageType == InventoryMode.Loot || this._usageType == InventoryMode.Stash)
                {
                    base.BuyAndEquipHint = new BasicTooltipViewModel(() => GameTexts.FindText("str_inventory_take_and_equip", null).ToString());
                    base.SellHint = new BasicTooltipViewModel(delegate ()
                    {
                        string stackModifierString = this.GetStackModifierString();
                        GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_give", null));
                        GameTexts.SetVariable("STR2", stackModifierString);
                        return GameTexts.FindText("str_string_newline_string", null).ToString();
                    });
                    base.BuyHint = new BasicTooltipViewModel(delegate ()
                    {
                        string stackModifierString = this.GetStackModifierString();
                        GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_take", null));
                        GameTexts.SetVariable("STR2", stackModifierString);
                        return GameTexts.FindText("str_string_newline_string", null).ToString();
                    });
                    return;
                }
                if (this._usageType == InventoryMode.Default)
                {
                    base.BuyAndEquipHint = new BasicTooltipViewModel(() => GameTexts.FindText("str_inventory_take_and_equip", null).ToString());
                    base.SellHint = new BasicTooltipViewModel(delegate ()
                    {
                        string stackModifierString = this.GetStackModifierString();
                        GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_discard", null));
                        GameTexts.SetVariable("STR2", stackModifierString);
                        return GameTexts.FindText("str_string_newline_string", null).ToString();
                    });
                    base.BuyHint = new BasicTooltipViewModel(delegate ()
                    {
                        string stackModifierString = this.GetStackModifierString();
                        GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_take", null));
                        GameTexts.SetVariable("STR2", stackModifierString);
                        return GameTexts.FindText("str_string_newline_string", null).ToString();
                    });
                    return;
                }
                base.BuyAndEquipHint = new BasicTooltipViewModel(() => GameTexts.FindText("str_inventory_buy_and_equip", null).ToString());
                base.SellHint = new BasicTooltipViewModel(delegate ()
                {
                    GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_sell", null));
                    GameTexts.SetVariable("STR2", string.Empty);
                    return GameTexts.FindText("str_string_newline_string", null).ToString();
                });
                base.BuyHint = new BasicTooltipViewModel(delegate ()
                {
                    GameTexts.SetVariable("STR1", GameTexts.FindText("str_inventory_buy", null));
                    GameTexts.SetVariable("STR2", string.Empty);
                    return GameTexts.FindText("str_string_newline_string", null).ToString();
                });
            }

            // Token: 0x06000D68 RID: 3432 RVA: 0x00036E16 File Offset: 0x00035016
            public static SPSkillItemVM.ProfitTypes GetProfitTypeFromDiff(float averageValue, float currentValue)
            {
                if (averageValue == 0f)
                {
                    return SPSkillItemVM.ProfitTypes.Default;
                }
                if (averageValue < currentValue * 0.8f)
                {
                    return SPSkillItemVM.ProfitTypes.HighProfit;
                }
                if (averageValue < currentValue * 0.95f)
                {
                    return SPSkillItemVM.ProfitTypes.Profit;
                }
                if (averageValue > currentValue * 1.05f)
                {
                    return SPSkillItemVM.ProfitTypes.Loss;
                }
                if (averageValue > currentValue * 1.2f)
                {
                    return SPSkillItemVM.ProfitTypes.HighLoss;
                }
                return SPSkillItemVM.ProfitTypes.Default;
            }

            // Token: 0x1700045B RID: 1115
            // (get) Token: 0x06000D69 RID: 3433 RVA: 0x00036E54 File Offset: 0x00035054
            // (set) Token: 0x06000D6A RID: 3434 RVA: 0x00036E5C File Offset: 0x0003505C
            [DataSourceProperty]
            public bool IsFocused
            {
                get
                {
                    return this._isFocused;
                }
                set
                {
                    if (value != this._isFocused)
                    {
                        this._isFocused = value;
                        base.OnPropertyChangedWithValue(value, "IsFocused");
                    }
                }
            }

            // Token: 0x1700045C RID: 1116
            // (get) Token: 0x06000D6B RID: 3435 RVA: 0x00036E7A File Offset: 0x0003507A
            // (set) Token: 0x06000D6C RID: 3436 RVA: 0x00036E82 File Offset: 0x00035082
            [DataSourceProperty]
            public bool IsArtifact
            {
                get
                {
                    return this._isArtifact;
                }
                set
                {
                    if (value != this._isArtifact)
                    {
                        this._isArtifact = value;
                        base.OnPropertyChangedWithValue(value, "IsArtifact");
                    }
                }
            }

            // Token: 0x1700045D RID: 1117
            // (get) Token: 0x06000D6D RID: 3437 RVA: 0x00036EA0 File Offset: 0x000350A0
            // (set) Token: 0x06000D6E RID: 3438 RVA: 0x00036EA8 File Offset: 0x000350A8
            [DataSourceProperty]
            public bool IsTransferable
            {
                get
                {
                    return this._isTransferable;
                }
                set
                {
                    if (value != this._isTransferable)
                    {
                        this._isTransferable = value;
                        base.OnPropertyChangedWithValue(value, "IsTransferable");
                    }
                }
            }

            // Token: 0x1700045E RID: 1118
            // (get) Token: 0x06000D6F RID: 3439 RVA: 0x00036EC6 File Offset: 0x000350C6
            // (set) Token: 0x06000D70 RID: 3440 RVA: 0x00036ECE File Offset: 0x000350CE
            [DataSourceProperty]
            public bool IsTransferButtonHighlighted
            {
                get
                {
                    return this._isTransferButtonHighlighted;
                }
                set
                {
                    if (value != this._isTransferButtonHighlighted)
                    {
                        this._isTransferButtonHighlighted = value;
                        base.OnPropertyChangedWithValue(value, "IsTransferButtonHighlighted");
                    }
                }
            }

            // Token: 0x1700045F RID: 1119
            // (get) Token: 0x06000D71 RID: 3441 RVA: 0x00036EEC File Offset: 0x000350EC
            // (set) Token: 0x06000D72 RID: 3442 RVA: 0x00036EF4 File Offset: 0x000350F4
            [DataSourceProperty]
            public bool IsItemHighlightEnabled
            {
                get
                {
                    return this._isItemHighlightEnabled;
                }
                set
                {
                    if (value != this._isItemHighlightEnabled)
                    {
                        this._isItemHighlightEnabled = value;
                        base.OnPropertyChangedWithValue(value, "IsItemHighlightEnabled");
                    }
                }
            }

            // Token: 0x17000460 RID: 1120
            // (get) Token: 0x06000D73 RID: 3443 RVA: 0x00036F12 File Offset: 0x00035112
            // (set) Token: 0x06000D74 RID: 3444 RVA: 0x00036F1A File Offset: 0x0003511A
            [DataSourceProperty]
            public bool IsCivilianItem
            {
                get
                {
                    return this._isCivilianItem;
                }
                set
                {
                    if (value != this._isCivilianItem)
                    {
                        this._isCivilianItem = value;
                        base.OnPropertyChangedWithValue(value, "IsCivilianItem");
                    }
                }
            }

            // Token: 0x17000461 RID: 1121
            // (get) Token: 0x06000D75 RID: 3445 RVA: 0x00036F38 File Offset: 0x00035138
            // (set) Token: 0x06000D76 RID: 3446 RVA: 0x00036F40 File Offset: 0x00035140
            [DataSourceProperty]
            public bool IsNew
            {
                get
                {
                    return this._isNew;
                }
                set
                {
                    if (value != this._isNew)
                    {
                        this._isNew = value;
                        base.OnPropertyChangedWithValue(value, "IsNew");
                    }
                }
            }

            // Token: 0x17000462 RID: 1122
            // (get) Token: 0x06000D77 RID: 3447 RVA: 0x00036F5E File Offset: 0x0003515E
            // (set) Token: 0x06000D78 RID: 3448 RVA: 0x00036F66 File Offset: 0x00035166
            [DataSourceProperty]
            public bool IsGenderDifferent
            {
                get
                {
                    return this._isGenderDifferent;
                }
                set
                {
                    if (value != this._isGenderDifferent)
                    {
                        this._isGenderDifferent = value;
                        base.OnPropertyChangedWithValue(value, "IsGenderDifferent");
                    }
                }
            }

            // Token: 0x17000463 RID: 1123
            // (get) Token: 0x06000D79 RID: 3449 RVA: 0x00036F84 File Offset: 0x00035184
            // (set) Token: 0x06000D7A RID: 3450 RVA: 0x00036F8C File Offset: 0x0003518C
            [DataSourceProperty]
            public bool CanBeSlaughtered
            {
                get
                {
                    return this._canBeSlaughtered;
                }
                set
                {
                    if (value != this._canBeSlaughtered)
                    {
                        this._canBeSlaughtered = value;
                        base.OnPropertyChangedWithValue(value, "CanBeSlaughtered");
                    }
                }
            }

            // Token: 0x17000464 RID: 1124
            // (get) Token: 0x06000D7B RID: 3451 RVA: 0x00036FAA File Offset: 0x000351AA
            // (set) Token: 0x06000D7C RID: 3452 RVA: 0x00036FB2 File Offset: 0x000351B2
            [DataSourceProperty]
            public bool CanBeDonated
            {
                get
                {
                    return this._canBeDonated;
                }
                set
                {
                    if (value != this._canBeDonated)
                    {
                        this._canBeDonated = value;
                        base.OnPropertyChangedWithValue(value, "CanBeDonated");
                    }
                }
            }

            // Token: 0x17000465 RID: 1125
            // (get) Token: 0x06000D7D RID: 3453 RVA: 0x00036FD0 File Offset: 0x000351D0
            // (set) Token: 0x06000D7E RID: 3454 RVA: 0x00036FD8 File Offset: 0x000351D8
            [DataSourceProperty]
            public bool IsEquipableItem
            {
                get
                {
                    return this._isEquipableItem;
                }
                set
                {
                    if (value != this._isEquipableItem)
                    {
                        this._isEquipableItem = value;
                        base.OnPropertyChangedWithValue(value, "IsEquipableItem");
                    }
                }
            }

            // Token: 0x17000466 RID: 1126
            // (get) Token: 0x06000D7F RID: 3455 RVA: 0x00036FF6 File Offset: 0x000351F6
            // (set) Token: 0x06000D80 RID: 3456 RVA: 0x00036FFE File Offset: 0x000351FE
            [DataSourceProperty]
            public bool CanCharacterUseItem
            {
                get
                {
                    return this._canCharacterUseItem;
                }
                set
                {
                    if (value != this._canCharacterUseItem)
                    {
                        this._canCharacterUseItem = value;
                        base.OnPropertyChangedWithValue(value, "CanCharacterUseItem");
                    }
                }
            }

            // Token: 0x17000467 RID: 1127
            // (get) Token: 0x06000D81 RID: 3457 RVA: 0x0003701C File Offset: 0x0003521C
            // (set) Token: 0x06000D82 RID: 3458 RVA: 0x00037024 File Offset: 0x00035224
            [DataSourceProperty]
            public bool IsLocked
            {
                get
                {
                    return this._isLocked;
                }
                set
                {
                    if (value != this._isLocked)
                    {
                        this._isLocked = value;
                        base.OnPropertyChangedWithValue(value, "IsLocked");
                        SPSkillItemVM.ProcessLockItem(this, value);
                    }
                }
            }

            // Token: 0x17000468 RID: 1128
            // (get) Token: 0x06000D83 RID: 3459 RVA: 0x0003704E File Offset: 0x0003524E
            // (set) Token: 0x06000D84 RID: 3460 RVA: 0x00037056 File Offset: 0x00035256
            [DataSourceProperty]
            public int ItemCount
            {
                get
                {
                    return this._count;
                }
                set
                {
                    if (value != this._count)
                    {
                        this._count = value;
                        base.OnPropertyChangedWithValue(value, "ItemCount");
                        this.UpdateTotalCost();
                        this.UpdateTradeData(false);
                    }
                }
            }

            // Token: 0x17000469 RID: 1129
            // (get) Token: 0x06000D85 RID: 3461 RVA: 0x00037081 File Offset: 0x00035281
            // (set) Token: 0x06000D86 RID: 3462 RVA: 0x00037089 File Offset: 0x00035289
            [DataSourceProperty]
            public int ItemLevel
            {
                get
                {
                    return this._level;
                }
                set
                {
                    if (value != this._level)
                    {
                        this._level = value;
                        base.OnPropertyChangedWithValue(value, "ItemLevel");
                    }
                }
            }

            // Token: 0x1700046A RID: 1130
            // (get) Token: 0x06000D87 RID: 3463 RVA: 0x000370A7 File Offset: 0x000352A7
            // (set) Token: 0x06000D88 RID: 3464 RVA: 0x000370AF File Offset: 0x000352AF
            [DataSourceProperty]
            public int ProfitType
            {
                get
                {
                    return this._profitType;
                }
                set
                {
                    if (value != this._profitType)
                    {
                        this._profitType = value;
                        base.OnPropertyChangedWithValue(value, "ProfitType");
                    }
                }
            }

            // Token: 0x1700046B RID: 1131
            // (get) Token: 0x06000D89 RID: 3465 RVA: 0x000370CD File Offset: 0x000352CD
            // (set) Token: 0x06000D8A RID: 3466 RVA: 0x000370D5 File Offset: 0x000352D5
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
                        this.UpdateTotalCost();
                    }
                }
            }

            // Token: 0x1700046C RID: 1132
            // (get) Token: 0x06000D8B RID: 3467 RVA: 0x000370F9 File Offset: 0x000352F9
            // (set) Token: 0x06000D8C RID: 3468 RVA: 0x00037101 File Offset: 0x00035301
            [DataSourceProperty]
            public int TotalCost
            {
                get
                {
                    return this._totalCost;
                }
                set
                {
                    if (value != this._totalCost)
                    {
                        this._totalCost = value;
                        base.OnPropertyChangedWithValue(value, "TotalCost");
                    }
                }
            }

            // Token: 0x1700046D RID: 1133
            // (get) Token: 0x06000D8D RID: 3469 RVA: 0x0003711F File Offset: 0x0003531F
            // (set) Token: 0x06000D8E RID: 3470 RVA: 0x00037127 File Offset: 0x00035327
            [DataSourceProperty]
            public InventoryTradeVM TradeData
            {
                get
                {
                    return this._tradeData;
                }
                set
                {
                    if (value != this._tradeData)
                    {
                        this._tradeData = value;
                        base.OnPropertyChangedWithValue<InventoryTradeVM>(value, "TradeData");
                    }
                }
            }

            // Token: 0x0400061D RID: 1565
            public static Action<SPSkillItemVM> OnFocus;

            // Token: 0x0400061E RID: 1566
            public static Action<SPSkillItemVM, bool> ProcessSellItem;

            // Token: 0x0400061F RID: 1567
            public static Action<SPSkillItemVM> ProcessItemSlaughter;

            // Token: 0x04000620 RID: 1568
            public static Action<SPSkillItemVM> ProcessItemDonate;

            // Token: 0x04000621 RID: 1569
            public static Action<SPSkillItemVM, bool> ProcessLockItem;

            // Token: 0x04000622 RID: 1570
            private readonly string _fiveStackShortcutKeyText;

            // Token: 0x04000623 RID: 1571
            private readonly string _entireStackShortcutKeyText;

            // Token: 0x04000624 RID: 1572
            private readonly InventoryMode _usageType;

            // Token: 0x04000625 RID: 1573
            private Concept _tradeGoodConceptObj;

            // Token: 0x04000626 RID: 1574
            private Concept _itemConceptObj;

            // Token: 0x04000628 RID: 1576
            private InventoryLogic _inventoryLogic;

            // Token: 0x04000629 RID: 1577
            private bool _isFocused;

            // Token: 0x0400062A RID: 1578
            private int _level;

            // Token: 0x0400062B RID: 1579
            private bool _isTransferable;

            // Token: 0x0400062C RID: 1580
            private bool _isCivilianItem;

            // Token: 0x0400062D RID: 1581
            private bool _isGenderDifferent;

            // Token: 0x0400062E RID: 1582
            private bool _isEquipableItem;

            // Token: 0x0400062F RID: 1583
            private bool _canCharacterUseItem;

            // Token: 0x04000630 RID: 1584
            private bool _isLocked;

            // Token: 0x04000631 RID: 1585
            private bool _isArtifact;

            // Token: 0x04000632 RID: 1586
            private bool _canBeSlaughtered;

            // Token: 0x04000633 RID: 1587
            private bool _canBeDonated;

            // Token: 0x04000634 RID: 1588
            private int _count;

            // Token: 0x04000635 RID: 1589
            private int _profitType = -5;

            // Token: 0x04000636 RID: 1590
            private int _transactionCount;

            // Token: 0x04000637 RID: 1591
            private int _totalCost;

            // Token: 0x04000638 RID: 1592
            private bool _isTransferButtonHighlighted;

            // Token: 0x04000639 RID: 1593
            private bool _isItemHighlightEnabled;

            // Token: 0x0400063A RID: 1594
            private bool _isNew;

            // Token: 0x0400063B RID: 1595
            private InventoryTradeVM _tradeData;

            // Token: 0x020001D2 RID: 466
            public enum ProfitTypes
            {
                // Token: 0x0400103D RID: 4157
                HighLoss = -2,
                // Token: 0x0400103E RID: 4158
                Loss,
                // Token: 0x0400103F RID: 4159
                Default,
                // Token: 0x04001040 RID: 4160
                Profit,
                // Token: 0x04001041 RID: 4161
                HighProfit
            }
        }
    

}
