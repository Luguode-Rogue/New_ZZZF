using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀系统调试工具。
    /// 提供快速生成词缀物品、查看已有词缀、批量测试等功能。
    /// 仅在战役模式（Campaign）下工作。
    /// </summary>
    public static class AffixDebugHelper
    {
        // ============================================================
        // 一、快速生成测试物品（核心功能）
        // ============================================================

        /// <summary>
        /// 生成一件随机词缀武器并加入玩家背包。
        /// 在 ItemObject 全表中随机选取一件武器，生成词缀后加入。
        /// </summary>
        public static void GiveRandomAffixWeapon()
        {
            if (Campaign.Current == null || Hero.MainHero == null)
            {
                ShowDebugMsg("仅在战役模式下可用", false);
                return;
            }

            var allWeapons = Campaign.Current.ObjectManager.GetObjectTypeList<ItemObject>()
                .Where(item => IsWeapon(item) && !IsAmmo(item))
                .ToList();

            if (allWeapons.Count == 0)
            {
                ShowDebugMsg("未找到可用武器", false);
                return;
            }

            int idx = MBRandom.RandomInt(allWeapons.Count);
            ItemObject weapon = allWeapons[idx];

            GiveAffixItem(weapon, "Ctrl+F5");
        }

        /// <summary>
        /// 生成一件随机词缀防具并加入玩家背包。
        /// </summary>
        public static void GiveRandomAffixArmor()
        {
            if (Campaign.Current == null || Hero.MainHero == null)
            {
                ShowDebugMsg("仅在战役模式下可用", false);
                return;
            }

            var allArmors = Campaign.Current.ObjectManager.GetObjectTypeList<ItemObject>()
                .Where(item => IsArmor(item))
                .ToList();

            if (allArmors.Count == 0)
            {
                ShowDebugMsg("未找到可用防具", false);
                return;
            }

            int idx = MBRandom.RandomInt(allArmors.Count);
            ItemObject armor = allArmors[idx];

            GiveAffixItem(armor, "Ctrl+F6");
        }

        /// <summary>
        /// 为指定物品生成词缀并加入玩家背包。
        /// 关键：直接创建带 Modifier 的 EquipmentElement 入包，
        /// 避免先入包再补 Modifier 的竞态问题和 struct 副本问题。
        /// </summary>
        private static void GiveAffixItem(ItemObject item, string source)
        {
            var behavior = AffixCampaignBehavior.Current;
            if (behavior == null)
            {
                ShowDebugMsg("AffixCampaignBehavior 未初始化", false);
                return;
            }

            AffixedItemRecord record = behavior.ForceAffixItem(item, $"Debug:{source}");
            AffixInstance affix = record.Affix;

            string modifierId = "zzzf_affix_" + record.InstanceId;
            ItemModifier modifier = behavior.CreateOrGetItemModifier(modifierId, record.InstanceId);

            MobileParty.MainParty?.ItemRoster.AddToCounts(
                new TaleWorlds.Core.EquipmentElement(item, modifier), 1);

            string displayName = affix.BuildFullName(item.Name.ToString());
            string rarity = affix.Rarity;
            uint color = AffixCampaignBehavior.GetRarityColor(rarity);

            string msg = $"[测试] 获得: {displayName} [{rarity}]";
            msg += $"\n  前缀: {(affix.GetPrefixDefinitions().Count > 0 ? string.Join(", ", affix.GetPrefixDefinitions().Select(d => d.DisplayName)) : "无")}";
            msg += $"\n  后缀: {(affix.GetSuffixDefinitions().Count > 0 ? string.Join(", ", affix.GetSuffixDefinitions().Select(d => d.DisplayName)) : "无")}";
            msg += $"\n  属性修正: {string.Join(", ", affix.FinalStatModifiers.Select(kv => $"{kv.Key} {kv.Value:+0;-0}"))}";

            ShowDebugMsg(msg, true);
        }

        // ============================================================
        // 二、查看已拥有的词缀物品
        // ============================================================

        /// <summary>
        /// 列出玩家背包中所有拥有词缀的物品。
        /// </summary>
        public static void ListPlayerAffixItems()
        {
            if (Campaign.Current == null || Hero.MainHero == null)
            {
                ShowDebugMsg("仅在战役模式下可用", false);
                return;
            }

            var behavior = AffixCampaignBehavior.Current;
            if (behavior == null || behavior.ItemRecordMap.Count == 0)
            {
                ShowDebugMsg("[词缀系统] 当前没有任何词缀物品", false);
                return;
            }

            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null)
            {
                ShowDebugMsg("无法获取玩家物品栏", false);
                return;
            }

            int foundCount = 0;
            var lines = new List<string> { "===== 背包中词缀物品列表 =====" };

            for (int i = 0; i < roster.Count; i++)
            {
                ItemRosterElement element = roster.GetElementCopyAtIndex(i);
                ItemObject item = element.EquipmentElement.Item;
                if (item == null) continue;

                var affix = behavior.GetAffixForEquipmentElement(element.EquipmentElement);
                if (affix != null && affix.HasAnyAffix)
                {
                    foundCount++;
                    string displayName = affix.BuildFullName(item.Name.ToString());
                    lines.Add($" [{affix.Rarity}] {displayName} (数量:{element.Amount})");
                    lines.Add($"   前缀:{string.Join(",", affix.GetPrefixDefinitions().Select(d => d.DisplayName))} | 后缀:{string.Join(",", affix.GetSuffixDefinitions().Select(d => d.DisplayName))}");
                }
            }

            if (foundCount == 0)
            {
                lines.Add("（无词缀物品）");
            }
            else
            {
                lines.Add($"共 {foundCount} 件词缀物品 (全局记录表: {behavior.ItemRecordMap.Count} 件)");
            }

            string fullMsg = string.Join("\n", lines);
            ShowDebugMsg(fullMsg, true);
        }

        // ============================================================
        // 三、词缀系统状态信息
        // ============================================================

        /// <summary>
        /// 打印词缀系统的运行状态。
        /// </summary>
        public static void PrintSystemStatus()
        {
            var behavior = AffixCampaignBehavior.Current;
            var db = AffixDatabase.Instance;

            var lines = new List<string>
            {
                "===== 词缀系统状态 =====",
                $"初始化状态: {(behavior?.IsInitialized == true ? "已初始化" : "未初始化")}",
                $"词缀定义总数: {db.AffixMap.Count}",
                $"  - 前缀: {db.AffixMap.Values.Count(d => d.IsPrefix)}",
                $"  - 后缀: {db.AffixMap.Values.Count(d => !d.IsPrefix)}",
                $"已生成词缀物品: {behavior?.ItemRecordMap.Count ?? 0}",
                $"  - 普通(Normal): {behavior?.ItemRecordMap.Values.Count(a => a.Affix.Rarity == "Normal") ?? 0}",
                $"  - 魔法(Magic):  {behavior?.ItemRecordMap.Values.Count(a => a.Affix.Rarity == "Magic") ?? 0}",
                $"  - 稀有(Rare):   {behavior?.ItemRecordMap.Values.Count(a => a.Affix.Rarity == "Rare") ?? 0}",
                $"  - 暗金(Unique): {behavior?.ItemRecordMap.Values.Count(a => a.Affix.Rarity == "Unique") ?? 0}",
            };

            var rarityDist = db.AffixMap.Values
                .GroupBy(d => d.Rarity)
                .ToDictionary(g => g.Key, g => g.Count());
            lines.AddRange(rarityDist.Select(kv => $"定义稀有度分布 - {kv.Key}: {kv.Value}"));

            string fullMsg = string.Join("\n", lines);
            ShowDebugMsg(fullMsg, true);
        }

        // ============================================================
        // 四、重随词缀（测试修改功能）
        // ============================================================

        /// <summary>从背包中随机选一个词缀物品，重随其前后缀</summary>
        public static void RerollRandomItemAffix()
        {
            if (Campaign.Current == null || Hero.MainHero == null)
            {
                ShowDebugMsg("仅在战役模式下可用", false);
                return;
            }

            var behavior = AffixCampaignBehavior.Current;
            if (behavior == null || behavior.ItemRecordMap.Count == 0)
            {
                ShowDebugMsg("[词缀系统] 当前没有任何词缀物品可重随", false);
                return;
            }

            var roster = MobileParty.MainParty?.ItemRoster;
            var candidateRecords = new List<(ItemObject Item, string InstanceId, AffixInstance Affix)>();
            for (int i = 0; i < roster?.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                ItemObject item = element.EquipmentElement.Item;
                if (item == null) continue;

                var affix = behavior.GetAffixForEquipmentElement(element.EquipmentElement);
                if (affix != null && affix.HasAnyAffix && !string.IsNullOrEmpty(affix.InstanceId))
                    candidateRecords.Add((item, affix.InstanceId, affix));
            }

            if (candidateRecords.Count == 0)
            {
                ShowDebugMsg("[词缀系统] 背包中没有词缀物品，请先用 Ctrl+F5/F6 生成", false);
                return;
            }

            var (targetItem, instanceId, oldAffix) = candidateRecords[MBRandom.RandomInt(candidateRecords.Count)];
            string oldName = oldAffix?.BuildFullName(targetItem.Name.ToString()) ?? targetItem.Name.ToString();

            var newAffix = behavior.RerollAffix(instanceId);
            string newName = newAffix?.BuildFullName(targetItem.Name.ToString()) ?? targetItem.Name.ToString();

            string msg = $"[重随词缀] {oldName}\n        → {newName} [{newAffix?.Rarity}]";
            if (newAffix != null)
            {
                msg += $"\n  新前缀: {(newAffix.GetPrefixDefinitions().Count > 0 ? string.Join(", ", newAffix.GetPrefixDefinitions().Select(d => d.DisplayName)) : "无")}";
                msg += $"\n  新后缀: {(newAffix.GetSuffixDefinitions().Count > 0 ? string.Join(", ", newAffix.GetSuffixDefinitions().Select(d => d.DisplayName)) : "无")}";
                msg += $"\n  新属性: {string.Join(", ", newAffix.FinalStatModifiers.Select(kv => $"{kv.Key} {kv.Value:+0;-0}"))}";
            }

            ShowDebugMsg(msg, true);
        }

        // ============================================================
        // 五、批量测试（生成指定数量的词缀物品并统计）
        // ============================================================

        /// <summary>
        /// 生成N件词缀物品用于统计分析（不加入背包，仅打印结果）。
        /// </summary>
        public static void BatchTest(string itemType, int itemLevel, int count)
        {
            if (count <= 0) count = 20;
            if (count > 200) count = 200;

            var db = AffixDatabase.Instance;
            db.Initialize();

            var lines = new List<string>
            {
                $"===== 批量测试: {itemType} Lv{itemLevel} x{count} ====="
            };

            int normalCount = 0, magicCount = 0, rareCount = 0, uniqueCount = 0;
            int totalPrefixes = 0, totalSuffixes = 0;
            var affixFrequency = new Dictionary<string, int>();

            for (int i = 0; i < count; i++)
            {
                var instance = AffixGenerator.Generate($"Test_{itemType}_{i}", itemType, itemLevel);

                switch (instance.Rarity)
                {
                    case "Normal": normalCount++; break;
                    case "Magic": magicCount++; break;
                    case "Rare": rareCount++; break;
                    case "Unique": uniqueCount++; break;
                }

                totalPrefixes += instance.PrefixIds.Count;
                totalSuffixes += instance.SuffixIds.Count;

                foreach (var id in instance.AllAffixIds)
                {
                    affixFrequency.TryGetValue(id, out int f);
                    affixFrequency[id] = f + 1;
                }
            }

            lines.Add($"稀有度分布:");
            lines.Add($"  普通: {normalCount} ({100f * normalCount / count:F1}%)");
            lines.Add($"  魔法: {magicCount} ({100f * magicCount / count:F1}%)");
            lines.Add($"  稀有: {rareCount} ({100f * rareCount / count:F1}%)");
            lines.Add($"  暗金: {uniqueCount} ({100f * uniqueCount / count:F1}%)");
            lines.Add($"平均前缀数: {(float)totalPrefixes / count:F2}");
            lines.Add($"平均后缀数: {(float)totalSuffixes / count:F2}");
            lines.Add($"词缀出现频率 Top10:");

            foreach (var kv in affixFrequency.OrderByDescending(kv => kv.Value).Take(10))
            {
                var def = db.GetDefinition(kv.Key);
                string displayName = def?.DisplayName ?? kv.Key;
                lines.Add($"  {displayName}: {kv.Value}次 ({100f * kv.Value / count:F1}%)");
            }

            string fullMsg = string.Join("\n", lines);
            ShowDebugMsg(fullMsg, true);
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private static bool IsWeapon(ItemObject item)
        {
            if (item.WeaponComponent == null) return false;
            var wc = item.WeaponComponent.PrimaryWeapon.WeaponClass;
            return wc != WeaponClass.Undefined
                && wc != WeaponClass.Arrow
                && wc != WeaponClass.Bolt
                && wc != WeaponClass.Stone
                && wc != WeaponClass.Boulder
                && wc != WeaponClass.Cartridge;
        }

        private static bool IsAmmo(ItemObject item)
        {
            if (item.WeaponComponent == null) return false;
            var wc = item.WeaponComponent.PrimaryWeapon.WeaponClass;
            return wc == WeaponClass.Arrow || wc == WeaponClass.Bolt
                || wc == WeaponClass.Stone || wc == WeaponClass.Boulder;
        }

        private static bool IsArmor(ItemObject item)
        {
            return item.ArmorComponent != null;
        }

        private static void ShowDebugMsg(string message, bool isPositive)
        {
            InformationManager.DisplayMessage(new InformationMessage(message, Colors.White));
        }
    }
}
