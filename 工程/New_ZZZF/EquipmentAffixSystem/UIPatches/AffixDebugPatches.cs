using HarmonyLib;
using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace New_ZZZF
{
    /// <summary>
    /// 调试用：日志记录 GetModifiedItemName 的返回值和 ItemModifier.Name 的状态。
    /// 验证通过后应移除。
    /// </summary>
    [HarmonyPatch(typeof(EquipmentElement), "GetModifiedItemName")]
    public class AffixDebug_GetModifiedItemName
    {
        private static readonly string _logPath =
            @"E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\New_ZZZF\工程\affix_debug.log";

        private static void Log(string msg)
        {
            try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [GetModName] {msg}{Environment.NewLine}"); } catch { }
        }

        [HarmonyPostfix]
        public static void Postfix(EquipmentElement __instance, ref TextObject __result)
        {
            try
            {
                string modifierId = __instance.ItemModifier?.StringId ?? "null";
                string modifierNameToString = __instance.ItemModifier?.Name?.ToString() ?? "null_mod";
                string resultToString = __result?.ToString() ?? "null";
                string itemId = __instance.Item?.StringId ?? "null";

                Log($"item='{itemId}', modifierId='{modifierId}', modifier.Name='{modifierNameToString}', __result.ToString()='{resultToString}'");
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex.Message}");
            }
        }
    }
}
