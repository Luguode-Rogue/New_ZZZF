using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// Deferred disarm executor. The combat callback only queues a request;
    /// the native equipment mutation happens on a later Mission tick.
    /// </summary>
    public sealed class ZZZFDisarmMissionBehavior : MissionLogic
    {
        private static readonly Queue<DisarmRequest> Pending = new Queue<DisarmRequest>();

        internal static void QueueDisarm(Agent defenderAgent, EquipmentIndex equipmentIndex)
        {
            if (defenderAgent == null || equipmentIndex == EquipmentIndex.None)
                return;

            Pending.Enqueue(new DisarmRequest(defenderAgent, equipmentIndex));
        }

        public override void OnMissionTick(float dt)
        {
            int count = Pending.Count;
            for (int i = 0; i < count; i++)
                Execute(Pending.Dequeue());
        }

        private static void Execute(DisarmRequest request)
        {
            try
            {
                Agent agent = request.Agent;
                if (agent == null || agent.Health <= 0f)
                    return;

                EquipmentIndex currentWielded = agent.GetOffhandWieldedItemIndex();
                if (currentWielded == EquipmentIndex.None)
                    currentWielded = agent.GetPrimaryWieldedItemIndex();

                // Cancel if the defender changed weapon after the hit.
                if (currentWielded != request.EquipmentIndex)
                    return;

                MissionWeapon weapon = agent.Equipment[request.EquipmentIndex];
                if (weapon.IsEmpty)
                    return;

                agent.RemoveEquippedWeapon(request.EquipmentIndex);
            }
            catch (Exception)
            {
                // Disarm is auxiliary logic; never break Mission tick.
            }
        }

        private readonly struct DisarmRequest
        {
            public readonly Agent Agent;
            public readonly EquipmentIndex EquipmentIndex;

            public DisarmRequest(Agent agent, EquipmentIndex equipmentIndex)
            {
                Agent = agent;
                EquipmentIndex = equipmentIndex;
            }
        }
    }
}
