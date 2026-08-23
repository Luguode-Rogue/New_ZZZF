using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 将缴械从伤害/格挡判定调用栈中延迟到 Mission Tick 安全阶段执行。
    /// 原因：Agent.DropItem 会触碰原生 Agent/装备状态，不应在伤害模型计算期间直接修改。
    /// </summary>
    internal static class DeferredDisarmExecutor
    {
        private readonly struct Request
        {
            public readonly int AgentIndex;
            public readonly EquipmentIndex WeaponIndex;

            public Request(int agentIndex, EquipmentIndex weaponIndex)
            {
                AgentIndex = agentIndex;
                WeaponIndex = weaponIndex;
            }
        }

        private static readonly object Sync = new object();
        private static readonly List<Request> Pending = new List<Request>();

        public static void Mark(Agent defenderAgent, EquipmentIndex weaponIndex)
        {
            if (defenderAgent == null || !defenderAgent.IsActive() || weaponIndex == EquipmentIndex.None)
                return;

            lock (Sync)
            {
                for (int i = 0; i < Pending.Count; i++)
                {
                    if (Pending[i].AgentIndex == defenderAgent.Index &&
                        Pending[i].WeaponIndex == weaponIndex)
                    {
                        return;
                    }
                }

                Pending.Add(new Request(defenderAgent.Index, weaponIndex));
            }
        }

        public static void Execute(Mission mission)
        {
            if (mission == null)
                return;

            Request[] requests;
            lock (Sync)
            {
                if (Pending.Count == 0)
                    return;

                requests = Pending.ToArray();
                Pending.Clear();
            }

            foreach (Request request in requests)
            {
                Agent target = mission.Agents.FirstOrDefault(agent =>
                    agent != null &&
                    agent.Index == request.AgentIndex &&
                    agent.IsActive());

                if (target == null)
                    continue;

                EquipmentIndex primaryIndex = target.GetPrimaryWieldedItemIndex();
                EquipmentIndex offhandIndex = target.GetOffhandWieldedItemIndex();

                // 目标在等待执行期间已经换手/收起/死亡时，不强行碰原生装备状态。
                if (primaryIndex != request.WeaponIndex && offhandIndex != request.WeaponIndex)
                    continue;

                target.DropItem(request.WeaponIndex, WeaponClass.Undefined);
            }
        }
    }

    /// <summary>
    /// 截断原有 TryDisarm 的直接 DropItem，改为只记录待缴械请求。
    /// </summary>
    [HarmonyPatch(typeof(ZZZFBlockBreakRules), "TryDisarm")]
    internal static class ZZZFBlockBreakRules_TryDisarm_Patch
    {
        private static bool Prefix(
            Agent attackerAgent,
            Agent defenderAgent,
            WeaponComponentData attackerWeapon,
            int proficiencyDifference,
            int attackerMovementSkill,
            int defenderMovementSkill)
        {
            if (defenderAgent == null || !defenderAgent.IsActive())
                return false;

            float disarmChance =
                0.2f +
                proficiencyDifference / 500f *
                (1f + (attackerMovementSkill - defenderMovementSkill) / 1000f);

            if (disarmChance <= MBRandom.RandomFloat)
                return false;

            EquipmentIndex wieldedIndex = defenderAgent.GetOffhandWieldedItemIndex();
            if (wieldedIndex == EquipmentIndex.None)
                wieldedIndex = defenderAgent.GetPrimaryWieldedItemIndex();

            if (wieldedIndex != EquipmentIndex.None)
                DeferredDisarmExecutor.Mark(defenderAgent, wieldedIndex);

            // 原方法中的 DropItem 不能在伤害计算调用栈中执行。
            return false;
        }
    }

    /// <summary>
    /// 在技能系统本帧 Tick 完成后执行上一帧/本帧排队的缴械。
    /// </summary>
    [HarmonyPatch(typeof(SkillSystemBehavior), nameof(SkillSystemBehavior.OnMissionTick))]
    internal static class SkillSystemBehavior_OnMissionTick_Disarm_Patch
    {
        private static void Postfix()
        {
            DeferredDisarmExecutor.Execute(Mission.Current);
        }
    }
}
