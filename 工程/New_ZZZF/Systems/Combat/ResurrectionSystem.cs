using HarmonyLib;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>通用复活资源。技能只增加次数；死亡来源不决定能否消费。</summary>
    internal static class ResurrectionSystem
    {
        internal const int BaseMaximumCharges = 3;
        [ThreadStatic] internal static int StructuralRemovalDepth;
        [ThreadStatic] internal static int BlowDepth;
        internal static bool TryConsume(Agent agent, out float restoredHealth) {
            restoredHealth = 0f;
            if (agent == null || !agent.IsActive() || !agent.IsHuman) return false;
            AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
            if (component == null || component._lifeResurgenceCount <= 0) return false;
            restoredHealth = agent.HealthLimit > 0f ? agent.HealthLimit : component.MaxHP;
            if (restoredHealth < 1f || float.IsNaN(restoredHealth) || float.IsInfinity(restoredHealth)) return false;
            component._lifeResurgenceCount--;
            if (agent.IsPlayerControlled)
                InformationManager.DisplayMessage(new InformationMessage("死而复生！剩余" + component._lifeResurgenceCount + "次", Colors.Green));
            return true;
        }
    }
    // 原生 HandleBlow、魔法和直接扣血型 DOT 都写入此属性；在死亡通知前恢复生命。
    [HarmonyPatch(typeof(Agent), nameof(Agent.Health), MethodType.Setter)]
    internal static class ResurrectionHealthPatch
    {
        private static void Prefix(Agent __instance, ref float value) {
            // 原生命中还会在 OnAgentHit 中结算护盾、纳垢免死；不能提前消费复活。
            if (ResurrectionSystem.StructuralRemovalDepth == 0 && ResurrectionSystem.BlowDepth == 0 &&
                value <= 0f && __instance.Health > 0f &&
                ResurrectionSystem.TryConsume(__instance, out float health)) value = health;
        }
    }
    [HarmonyPatch(typeof(Agent), "HandleBlow")]
    internal static class ResurrectionBlowScopePatch
    {
        private static void Prefix(out bool __state) { __state = true; ResurrectionSystem.BlowDepth++; }
        private static Exception Finalizer(Exception __exception, bool __state) {
            if (__state) ResurrectionSystem.BlowDepth--;
            return __exception;
        }
    }
    // 明确调用 Die 的致死来源也接入；前缀在注册阵亡和 native Die 之前拦截。
    [HarmonyPatch(typeof(Agent), nameof(Agent.Die))]
    internal static class ResurrectionDeathPatch
    {
        private static bool Prefix(Agent __instance, Agent.KillInfo overrideKillInfo, out bool __state) {
            __state = overrideKillInfo == Agent.KillInfo.TeamSwitch;
            // 换队属于结构性移除，不能用复活阻止。
            if (__state) { ResurrectionSystem.StructuralRemovalDepth++; return true; }
            if (!ResurrectionSystem.TryConsume(__instance, out float health)) return true;
            __instance.Health = health;
            return false;
        }
        private static Exception Finalizer(Exception __exception, bool __state) {
            if (__state) ResurrectionSystem.StructuralRemovalDepth--;
            return __exception;
        }
    }
}
