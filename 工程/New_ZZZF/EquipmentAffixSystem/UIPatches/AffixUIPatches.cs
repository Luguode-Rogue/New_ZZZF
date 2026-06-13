using HarmonyLib;
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
        [HarmonyPostfix]
        public static void Postfix(ItemVM __instance, ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result)) return;
                var item = __instance.ItemRosterElement.EquipmentElement.Item;
                if (item == null) return;
                var behavior = AffixCampaignBehavior.Current;
                if (behavior == null) return;
                var affix = behavior.GetAffixByBaseItemId(item.StringId);
                if (affix != null && affix.HasAnyAffix)
                    __result = affix.BuildFullName(item.Name.ToString());
            }
            catch { }
        }
    }

    // ============================================================
    // 右侧详情面板：显示词缀名称
    // ============================================================

    [HarmonyPatch(typeof(ItemPreviewVM), "Open")]
    public class AffixItemPreviewPatch
    {
        [HarmonyPostfix]
        public static void Postfix(ItemPreviewVM __instance, EquipmentElement item)
        {
            try
            {
                var behavior = AffixCampaignBehavior.Current;
                if (behavior == null) return;
                var affix = behavior.GetAffixByBaseItemId(item.Item.StringId);
                if (affix != null && affix.HasAnyAffix)
                    __instance.ItemName = affix.BuildFullName(item.Item.Name.ToString());
            }
            catch { }
        }
    }
}
