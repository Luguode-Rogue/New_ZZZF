using System;
using System.Collections.Generic;

namespace New_ZZZF.BattleHud
{
    /// <summary>只读取玩家现有状态；同名状态合并一行，内部标记不展示。</summary>
    internal static class BattleBuffHudSnapshot
    {
        internal sealed class Entry
        {
            public string id;
            public string name;
            public float remaining;
            public float maximum;
        }

        private static bool Visible(AgentBuff state)
        {
            if (state == null || state.IsMarker || !state.AffectsOwner || state.Duration <= 0f ||
                string.IsNullOrEmpty(state.StateId)) return false;
            // 兼容旧状态；后续新标记用 IsMarker 显式声明。
            return state.StateId.IndexOf("Marker", StringComparison.OrdinalIgnoreCase) < 0 &&
                state.StateId.IndexOf("标记", StringComparison.Ordinal) < 0 &&
                !state.StateId.StartsWith("SkillRecastWindow:", StringComparison.Ordinal);
        }

        public static bool HasVisibleState(AgentSkillComponent component)
        {
            if (component == null) return false;
            foreach (AgentBuff state in component.StateContainer.States)
                if (Visible(state)) return true;
            return false;
        }

        public static List<Entry> Build(AgentSkillComponent component)
        {
            var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (component != null)
                foreach (AgentBuff state in component.StateContainer.States) {
                    if (!Visible(state)) continue;
                    if (!entries.TryGetValue(state.StateId, out Entry entry)) {
                        entry = new Entry { id = state.StateId, name = GetName(state) };
                        entries.Add(state.StateId, entry);
                    }
                    if (state.Duration > entry.remaining) {
                        entry.remaining = state.Duration;
                        entry.maximum = Math.Max(state.Duration, state.MaximumDuration);
                    }
                }
            var result = new List<Entry>(entries.Values);
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        private static string GetName(AgentBuff state)
        {
            if (!string.IsNullOrEmpty(state.BattleHudName)) return state.BattleHudName;
            switch (state.StateId) {
                case "du": case "forge_poison": return "中毒";
                case "forge_freeze": return "冻结";
                case "forge_heal": return "持续治疗";
                case "ShadowStepStagger": return "暗影步控制";
                case "MagicReflectionBuff": return "魔法反射";
                case "MagicReflectionPenetrationBuff": return "穿透魔法反射";
            }
            SkillBase matching = null;
            int prefixLength = 0;
            foreach (var pair in SkillFactory._skillRegistry)
                if (pair.Key.Length > prefixLength && state.StateId.StartsWith(pair.Key, StringComparison.Ordinal)) {
                    matching = pair.Value;
                    prefixLength = pair.Key.Length;
                }
            if (matching == null) return state.StateId;
            string name = matching.Text?.ToString() ?? matching.SkillID;
            if (state.StateId.IndexOf("Enemy", StringComparison.Ordinal) >= 0) name += "（减益）";
            return name;
        }
    }
}
