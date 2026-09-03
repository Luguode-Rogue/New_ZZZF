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
    /// 旧方案：Harmony Prefix 截断 TryDisarm。现已废弃——TryDisarm 本体已改为直接
    /// 调用 DeferredDisarmExecutor.Mark（见 NewDamageModel.cs），不再依赖补丁在场。
    /// 保留空壳仅为避免引用残留，可直接删除。
    /// </summary>
}

