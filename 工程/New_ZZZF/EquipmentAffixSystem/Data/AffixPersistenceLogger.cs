using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace New_ZZZF
{
    /// <summary>
    /// Diagnostic logging for affix save/load lifecycle.
    /// Use these messages to distinguish serialization failure from runtime restore failure.
    /// </summary>
    public static class AffixPersistenceLogger
    {
        public static void LogSaveRecord(string instanceId, AffixInstance affix)
        {
            if (affix == null)
            {
                Log("SAVE", instanceId, "affix=null");
                return;
            }

            Log("SAVE", instanceId,
                $"prefix={Count(affix.PrefixIds)} suffix={Count(affix.SuffixIds)}");
        }

        public static void LogLoadRecord(string instanceId, AffixInstance affix)
        {
            if (affix == null)
            {
                Log("LOAD", instanceId, "affix=null");
                return;
            }

            Log("LOAD", instanceId,
                $"prefix={Count(affix.PrefixIds)} suffix={Count(affix.SuffixIds)}");
        }

        public static void LogResolve(string instanceId, AffixInstance affix, bool success)
        {
            Log("RESOLVE", instanceId,
                success ? "runtime definitions restored" : "runtime definitions restore failed");
        }

        public static void LogError(string stage, string instanceId, Exception exception)
        {
            Log("ERROR", instanceId,
                $"stage={stage} type={exception.GetType().Name} msg={exception.Message}");
        }

        private static int Count<T>(ICollection<T> collection)
        {
            return collection == null ? -1 : collection.Count;
        }

        private static void Log(string stage, string instanceId, string detail)
        {
            InformationManager.DisplayMessage(
                new InformationMessage(
                    $"[AffixPersistence][{stage}] id={instanceId} {detail}"));
        }
    }
}
