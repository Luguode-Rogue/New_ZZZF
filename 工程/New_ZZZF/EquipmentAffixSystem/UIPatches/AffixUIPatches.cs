using HarmonyLib;
using System;
using System.IO;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;

namespace New_ZZZF
{
    // ============================================================
    // 背包物品名称：拦截 get_ItemDescription，返回词缀名称
    // ============================================================

    /// <summary>
    /// 拦截 ItemVM.ItemDescription 的 getter。
    /// 无论何时 UI 或代码读取物品名称，都优先返回词缀名称。
    /// </summary>
    [HarmonyPatch(typeof(ItemVM), "get_ItemDescription")]
    public class AffixItemDescriptionGetterPatch
    {
        private static readonly string DebugLogPath =
            @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\affix_debug.log";

        private static void LogDebug(string msg)
        {
            try { File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [UI_Getter] {msg}{Environment.NewLine}"); } catch { }
        }

        [HarmonyPostfix]
        public static void Postfix(ItemVM __instance, ref string __result)
        {
            try
            {
                LogDebug($"ENTER: __result='{__result}', item={__instance?.ItemRosterElement.EquipmentElement.Item?.StringId ?? "null"}");

                if (string.IsNullOrEmpty(__result))
                {
                    LogDebug("SKIP: __result is null or empty");
                    return;
                }
                var element = __instance.ItemRosterElement.EquipmentElement;
                if (element.Item == null)
                {
                    LogDebug("SKIP: element.Item is null");
                    return;
                }
                var behavior = AffixCampaignBehavior.Current;
                if (behavior == null)
                {
                    LogDebug("SKIP: AffixCampaignBehavior.Current is null");
                    return;
                }

                LogDebug($"LOOKUP: modifierId='{element.ItemModifier?.StringId ?? "null"}', ModifierToInstanceMap.Count={behavior.ModifierToInstanceMap.Count}, ItemRecordMap.Count={behavior.ItemRecordMap.Count}");

                var affix = behavior.GetAffixForEquipmentElement(element);
                if (affix != null && affix.HasAnyAffix)
                {
                    string newName = affix.BuildFullName(element.Item.Name.ToString());
                    LogDebug($"OVERRIDE: '{__result}' -> '{newName}' (rarity={affix.Rarity})");
                    __result = newName;
                }
                else
                {
                    LogDebug($"NO_AFFIX: affix={(affix == null ? "null" : $"HasAnyAffix={affix.HasAnyAffix}")}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // ============================================================
    // 右侧详情面板：显示词缀名称
    // ============================================================

    [HarmonyPatch(typeof(ItemPreviewVM), "Open")]
    public class AffixItemPreviewPatch
    {
        private static readonly string DebugLogPath =
            @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\affix_debug.log";

        private static void LogDebug(string msg)
        {
            try { File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] [UI_Preview] {msg}{Environment.NewLine}"); } catch { }
        }

        [HarmonyPostfix]
        public static void Postfix(ItemPreviewVM __instance, EquipmentElement item)
        {
            try
            {
                LogDebug($"ENTER: item={item.Item?.StringId ?? "null"}, modifierId='{item.ItemModifier?.StringId ?? "null"}'");

                var behavior = AffixCampaignBehavior.Current;
                if (behavior == null)
                {
                    LogDebug("SKIP: AffixCampaignBehavior.Current is null");
                    return;
                }
                var affix = behavior.GetAffixForEquipmentElement(item);
                if (affix != null && affix.HasAnyAffix)
                {
                    string newName = affix.BuildFullName(item.Item.Name.ToString());
                    LogDebug($"OVERRIDE: '{__instance.ItemName}' -> '{newName}'");
                    __instance.ItemName = newName;
                }
                else
                {
                    LogDebug($"NO_AFFIX: affix={(affix == null ? "null" : $"HasAnyAffix={affix.HasAnyAffix}")}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
