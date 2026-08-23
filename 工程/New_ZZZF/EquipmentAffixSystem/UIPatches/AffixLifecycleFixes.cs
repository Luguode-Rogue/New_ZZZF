using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace New_ZZZF
{
    /// <summary>
    /// 装备词缀实例生命周期修复补丁。
    /// 本文件集中放置本轮修复，便于后续一次性删除本文件撤销整组热修。
    /// </summary>
    internal static class AffixLifecycleFixes
    {
        internal static void ProcessInventoryExchange(
            AffixCampaignBehavior behavior,
            List<(ItemRosterElement, int)> purchasedItems,
            List<(ItemRosterElement, int)> soldItems)
        {
            if (behavior == null) return;

            var newRecords = new List<AffixedItemRecord>();

            foreach (var (itemElement, amount) in purchasedItems ?? new List<(ItemRosterElement, int)>())
            {
                if (itemElement.EquipmentElement.Item == null || amount <= 0)
                    continue;

                ItemModifier modifier = itemElement.EquipmentElement.ItemModifier;
                if (modifier != null &&
                    !string.IsNullOrEmpty(modifier.StringId) &&
                    behavior.ModifierToInstanceMap.ContainsKey(modifier.StringId))
                {
                    continue;
                }

                AffixedItemRecord record = behavior.CreateAffixedRecord(
                    itemElement.EquipmentElement.Item,
                    amount,
                    "PlayerInventory");

                if (record != null)
                    newRecords.Add(record);
            }

            AssignNewRecordsToRoster(behavior, newRecords);

            // 当前没有找到可以安全证明“售出后立即删除实例记录”的生命周期入口，因此暂不清理。
            // 只记录证据，后续根据日志确认实际生命周期后再决定是否处理。
            if (soldItems != null && soldItems.Count > 0)
            {
                foreach (var (itemElement, amount) in soldItems)
                {
                    string itemId = itemElement.EquipmentElement.Item?.StringId ?? "null";
                    string modifierId = itemElement.EquipmentElement.ItemModifier?.StringId ?? "null";
                    AffixLifecycleDebugLog.Info(
                        $"出售事件: item={itemId}, amount={amount}, modifier={modifierId}, 当前阶段不执行实例清理。");
                }
            }

            behavior.SyncHeroEquipmentBindings();
        }

        internal static void RepairExistingRoster(AffixCampaignBehavior behavior)
        {
            if (behavior == null || MobileParty.MainParty?.ItemRoster == null)
                return;

            var roster = MobileParty.MainParty.ItemRoster;
            var presentInstances = new HashSet<string>();

            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                string modifierId = element.EquipmentElement.ItemModifier?.StringId;
                if (string.IsNullOrEmpty(modifierId))
                    continue;

                if (behavior.ModifierToInstanceMap.TryGetValue(modifierId, out string instanceId) &&
                    !string.IsNullOrEmpty(instanceId))
                {
                    presentInstances.Add(instanceId);
                }
            }

            var recordsByBase = behavior.ItemRecordMap.Values
                .Where(r => r != null && !presentInstances.Contains(r.InstanceId))
                .GroupBy(r => r.BaseItemId);

            foreach (var group in recordsByBase)
            {
                var missing = group.ToList();
                if (missing.Count != 1)
                {
                    if (missing.Count > 1)
                    {
                        AffixLifecycleDebugLog.Warn(
                            $"读档/修复存在歧义: baseItem={group.Key}, 未绑定实例={missing.Count}。不自动分配，避免实例串号。");
                    }
                    continue;
                }

                AffixedItemRecord record = missing[0];
                int available = GetUnmodifiedAmount(roster, record.BaseItemId);
                if (available != record.StackCount)
                {
                    AffixLifecycleDebugLog.Warn(
                        $"读档/修复未执行: instance={record.InstanceId}, baseItem={record.BaseItemId}, " +
                        $"记录数量={record.StackCount}, 当前无修饰符数量={available}。无法证明属于同一实例。");
                    continue;
                }

                int assigned = AssignRecordAmount(roster, record, record.StackCount, behavior);
                if (assigned == record.StackCount)
                {
                    AffixLifecycleDebugLog.Info(
                        $"读档/修复完成: instance={record.InstanceId}, baseItem={record.BaseItemId}, amount={assigned}。");
                }
            }
        }

        private static void AssignNewRecordsToRoster(
            AffixCampaignBehavior behavior,
            IReadOnlyList<AffixedItemRecord> newRecords)
        {
            if (newRecords == null || newRecords.Count == 0 || MobileParty.MainParty?.ItemRoster == null)
                return;

            var roster = MobileParty.MainParty.ItemRoster;
            foreach (AffixedItemRecord record in newRecords)
            {
                int assigned = AssignRecordAmount(roster, record, record.StackCount, behavior);
                if (assigned != record.StackCount)
                {
                    AffixLifecycleDebugLog.Error(
                        $"新增词缀实例无法完整绑定到库存: instance={record.InstanceId}, " +
                        $"baseItem={record.BaseItemId}, requested={record.StackCount}, assigned={assigned}。");
                }
            }
        }

        private static int AssignRecordAmount(
            ItemRoster roster,
            AffixedItemRecord record,
            int requestedAmount,
            AffixCampaignBehavior behavior)
        {
            if (requestedAmount <= 0 || roster == null || record == null)
                return 0;

            ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(record.BaseItemId);
            if (item == null)
            {
                AffixLifecycleDebugLog.Error(
                    $"无法找到基础物品: instance={record.InstanceId}, baseItem={record.BaseItemId}。");
                return 0;
            }

            string modifierId = "zzzf_affix_" + record.InstanceId;
            ItemModifier modifier = behavior.CreateOrGetItemModifier(modifierId, record.InstanceId);
            if (modifier == null)
                return 0;

            int remaining = requestedAmount;
            for (int i = 0; i < roster.Count && remaining > 0; i++)
            {
                ItemRosterElement oldElement = roster.GetElementCopyAtIndex(i);
                EquipmentElement equipmentElement = oldElement.EquipmentElement;

                if (equipmentElement.Item != item || equipmentElement.ItemModifier != null)
                    continue;

                int take = Math.Min(remaining, oldElement.Amount);
                int remainder = oldElement.Amount - take;

                roster.Remove(oldElement);

                if (remainder > 0)
                    roster.AddToCounts(new EquipmentElement(item), remainder);

                roster.AddToCounts(
                    new EquipmentElement(item, modifier),
                    take);

                remaining -= take;
                i = -1;
            }

            return requestedAmount - remaining;
        }

        private static int GetUnmodifiedAmount(ItemRoster roster, string baseItemId)
        {
            if (roster == null || string.IsNullOrEmpty(baseItemId))
                return 0;

            int total = 0;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.EquipmentElement.Item?.StringId != baseItemId)
                    continue;
                if (element.EquipmentElement.ItemModifier != null)
                    continue;

                total += element.Amount;
            }

            return total;
        }
    }

    [HarmonyPatch(typeof(AffixCampaignBehavior), "OnPlayerInventoryExchange")]
    internal static class AffixInventoryExchangePatch
    {
        private static bool Prefix(
            AffixCampaignBehavior __instance,
            List<(ItemRosterElement, int)> purchasedItems,
            List<(ItemRosterElement, int)> soldItems,
            bool isTrading)
        {
            AffixLifecycleFixes.ProcessInventoryExchange(
                __instance,
                purchasedItems,
                soldItems);

            return false;
        }
    }

    [HarmonyPatch(typeof(AffixCampaignBehavior), "SyncAffixModifiersToPlayerRoster")]
    internal static class AffixRosterSyncPatch
    {
        private static bool Prefix(AffixCampaignBehavior __instance)
        {
            AffixLifecycleFixes.RepairExistingRoster(__instance);
            return false;
        }
    }

    [HarmonyPatch(
        typeof(AffixCampaignBehavior),
        "GetAffixDamageMultiplier",
        new[] { typeof(string), typeof(ItemObject), typeof(string) })]
    internal static class AffixDamageLookupPatch
    {
        private static bool Prefix(
            string instanceId,
            ItemObject item,
            string statKey,
            ref float __result)
        {
            __result = 1f;

            if (AffixCampaignBehavior.Current == null || string.IsNullOrEmpty(instanceId) || item == null)
                return false;

            AffixInstance affix = AffixCampaignBehavior.Current.GetAffixByInstanceId(instanceId);
            if (affix == null || !affix.HasAnyAffix)
                return false;

            if (affix.FinalStatModifiers.TryGetValue(statKey, out float bonus) && bonus != 0f)
                __result = 1f + bonus * 0.01f;

            return false;
        }
    }

    [HarmonyPatch(typeof(AffixCampaignBehavior), "RerollAffix")]
    internal static class AffixRerollDiagnosticPatch
    {
        private static void Prefix(string instanceId)
        {
            var behavior = AffixCampaignBehavior.Current;
            if (behavior == null || string.IsNullOrEmpty(instanceId))
                return;

            var bindings = behavior.BindingMap.Values
                .Where(b => b != null && b.InstanceId == instanceId)
                .ToList();

            if (bindings.Count > 0)
            {
                AffixLifecycleDebugLog.Warn(
                    $"重铸已装备实例: instance={instanceId}, bindings={bindings.Count}。" +
                    "当前代码只明确替换玩家库存元素，是否同步已装备槽位仍需运行时确认。");
            }
        }
    }
}
