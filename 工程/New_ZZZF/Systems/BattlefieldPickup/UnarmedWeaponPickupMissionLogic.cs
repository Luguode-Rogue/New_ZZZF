using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Systems.BattlefieldPickup
{
    /// <summary>
    /// 为完全没有可用攻击武器的 AI 补充原生拾取行为。
    /// 只负责选择目标与发出 MoveTo/UseGameObject，实际拾取、换槽、网络事件仍由引擎处理。
    /// </summary>
    public sealed class UnarmedWeaponPickupMissionLogic : MissionLogic
    {
        private const float SearchInterval = 0.45f;
        private const float SearchRadiusSquared = 625f;
        private const float AssignmentTimeout = 12f;
        private const float SummaryLogInterval = 5f;
        private const int AgentsPerSearch = 6;

        private readonly Dictionary<Agent, Assignment> _assignments = new Dictionary<Agent, Assignment>();
        private float _searchTimer;
        private float _summaryLogTimer;
        private int _roundRobinIndex;
        private int _eligibleSinceLastSummary;
        private int _noCandidateSinceLastSummary;

        public override void AfterStart()
        {
            base.AfterStart();
            Mission.OnItemPickUp += OnItemPickedUp;
            Log("MissionLogic started");
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (GameNetwork.IsClientOrReplay || Mission.Mode == MissionMode.Conversation || Mission.Mode == MissionMode.CutScene) return;

            TickAssignments(dt);
            TickSummaryLog(dt);
            _searchTimer -= dt;
            if (_searchTimer > 0f) return;
            _searchTimer = SearchInterval;
            SearchForAgents();
        }

        private void SearchForAgents()
        {
            var agents = Mission.Agents;
            if (agents == null || agents.Count == 0) return;

            int checkedCount = 0;
            int visited = 0;
            while (visited < agents.Count && checkedCount < AgentsPerSearch)
            {
                if (_roundRobinIndex >= agents.Count) _roundRobinIndex = 0;
                Agent agent = agents[_roundRobinIndex++];
                visited++;
                if (!CanSearch(agent)) continue;
                checkedCount++;
                _eligibleSinceLastSummary++;

                CandidateSelection selection = FindBestWeapon(agent);
                if (selection == null)
                {
                    _noCandidateSinceLastSummary++;
                    continue;
                }

                try
                {
                    ClearUnusableReplacementSlot(agent, selection.Slot);
                    agent.HumanAIComponent.MoveToUsableGameObject(selection.Item, null, Agent.AIScriptedFrameFlags.NoAttack);
                    _assignments[agent] = new Assignment(selection.Item);
                    Log("assigned agent=" + AgentName(agent) + " item=" + ItemName(selection.Item) +
                        " slot=" + selection.Slot);
                }
                catch (Exception ex)
                {
                    Log("assign failed agent=" + AgentName(agent) + " item=" + ItemName(selection.Item) + " error=" + ex);
                    ClearAssignment(agent);
                }
            }
        }

        private bool CanSearch(Agent agent)
        {
            return agent != null && agent != Agent.Main && agent.IsActive() && agent.IsHuman && !agent.IsMount &&
                   agent.IsAIControlled && !agent.IsRunningAway && agent.HumanAIComponent != null &&
                   !_assignments.ContainsKey(agent) && !HasOffensiveWeapon(agent) &&
                   agent.CanBeAssignedForScriptedMovement() && !agent.IsInWater();
        }

        private CandidateSelection FindBestWeapon(Agent agent)
        {
            SpawnedItemEntity best = null;
            EquipmentIndex bestSlot = EquipmentIndex.None;
            float bestScore = float.MinValue;
            foreach (MissionObject missionObject in Mission.ActiveMissionObjects)
            {
                SpawnedItemEntity item = missionObject as SpawnedItemEntity;
                if (!IsCandidate(agent, item)) continue;

                float distanceSquared = item.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                if (distanceSquared > SearchRadiusSquared) continue;
                EquipmentIndex slot = SelectPickupSlot(agent, item);
                if (slot == EquipmentIndex.None) continue;
                if (!agent.CanMoveDirectlyToPosition(item.GameEntityWithWorldPosition.AsVec2)) continue;

                float score = MissionGameModels.Current.ItemPickupModel.GetItemScoreForAgent(item, agent);
                score += item.WeaponCopy.Item.Tierf * 25f;
                score -= MathF.Sqrt(distanceSquared) * 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                    bestSlot = slot;
                }
            }
            return best == null ? null : new CandidateSelection(best, bestSlot);
        }

        private static EquipmentIndex SelectPickupSlot(Agent agent, SpawnedItemEntity item)
        {
            EquipmentIndex selected = MissionEquipment.SelectWeaponPickUpSlot(agent, item.WeaponCopy, item.IsStuckMissile());
            if (selected != EquipmentIndex.None &&
                (agent.Equipment[selected].IsEmpty || IsUnusableOccupiedSlot(agent, selected)))
                return selected;

            for (int i = 0; i < 4; i++)
            {
                EquipmentIndex slot = (EquipmentIndex)i;
                if (agent.Equipment[slot].IsEmpty) return slot;
            }

            for (int i = 0; i < 4; i++)
            {
                EquipmentIndex slot = (EquipmentIndex)i;
                if (IsUnusableOccupiedSlot(agent, slot)) return slot;
            }

            return selected;
        }

        private static bool IsUnusableOccupiedSlot(Agent agent, EquipmentIndex slot)
        {
            MissionWeapon weapon = agent.Equipment[slot];
            if (weapon.IsEmpty || weapon.Item == null) return false;
            WeaponComponentData usage = weapon.CurrentUsageItem ?? weapon.Item.PrimaryWeapon;
            if (usage == null || usage.IsAmmo) return weapon.Amount <= 0;
            if (weapon.IsAnyConsumable()) return weapon.Amount <= 0;
            return usage.IsRangedWeapon && !HasMatchingAmmo(agent, usage.AmmoClass);
        }

        private static void ClearUnusableReplacementSlot(Agent agent, EquipmentIndex slot)
        {
            if (slot == EquipmentIndex.None || agent.Equipment[slot].IsEmpty || !IsUnusableOccupiedSlot(agent, slot)) return;
            string oldItem = agent.Equipment[slot].Item?.StringId ?? "unknown";
            agent.RemoveEquippedWeapon(slot);
            Log("cleared unusable slot agent=" + AgentName(agent) + " slot=" + slot + " item=" + oldItem);
        }

        private static bool IsCandidate(Agent agent, SpawnedItemEntity item)
        {
            if (item == null || item.IsDeactivated || item.IsDisabled || item.HasUser) return false;
            if (item.HasAIMovingTo && !item.IsAIMovingTo(agent)) return false;
            if (item.GameEntityWithWorldPosition.GetNavMesh() == UIntPtr.Zero) return false;

            MissionWeapon weapon = item.WeaponCopy;
            if (weapon.IsEmpty || weapon.Item == null || weapon.IsShield() || weapon.IsBanner()) return false;
            if (weapon.Item.ItemFlags.HasAnyFlag(ItemFlags.CannotBePickedUp)) return false;
            WeaponComponentData usage = weapon.CurrentUsageItem ?? weapon.Item.PrimaryWeapon;
            if (usage == null || usage.IsAmmo) return false;
            if (usage.IsMeleeWeapon || weapon.IsAnyConsumable()) return true;
            return usage.IsRangedWeapon && HasMatchingAmmo(agent, usage.AmmoClass);
        }

        private static bool HasOffensiveWeapon(Agent agent)
        {
            for (int i = 0; i < 4; i++)
            {
                MissionWeapon weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty || weapon.Item == null || weapon.IsShield()) continue;
                WeaponComponentData usage = weapon.CurrentUsageItem ?? weapon.Item.PrimaryWeapon;
                if (usage == null || usage.IsAmmo) continue;
                if (usage.IsMeleeWeapon) return true;
                if (weapon.IsAnyConsumable() && weapon.Amount > 0) return true;
                if (usage.IsRangedWeapon && HasMatchingAmmo(agent, usage.AmmoClass)) return true;
            }
            return false;
        }

        private static bool HasMatchingAmmo(Agent agent, WeaponClass ammoClass)
        {
            for (int i = 0; i < 4; i++)
            {
                MissionWeapon candidate = agent.Equipment[(EquipmentIndex)i];
                if (candidate.IsEmpty || candidate.Amount <= 0 || candidate.CurrentUsageItem == null) continue;
                if (candidate.CurrentUsageItem.IsAmmo && candidate.CurrentUsageItem.WeaponClass == ammoClass) return true;
            }
            return false;
        }

        private void TickAssignments(float dt)
        {
            if (_assignments.Count == 0) return;
            foreach (Agent agent in _assignments.Keys.ToArray())
            {
                Assignment assignment = _assignments[agent];
                assignment.Age += dt;
                SpawnedItemEntity item = assignment.Item;
                bool timedOut = assignment.Age >= AssignmentTimeout;
                if (agent == null || !agent.IsActive() || HasOffensiveWeapon(agent) || item == null || item.IsDeactivated ||
                    item.IsDisabled || timedOut || (item.HasAIMovingTo && !item.IsAIMovingTo(agent)))
                {
                    if (timedOut)
                        Log("assignment timeout agent=" + AgentName(agent) + " item=" + ItemName(item));
                    ClearAssignment(agent);
                    continue;
                }

                try
                {
                    WorldFrame frame = item.GetUserFrameForAgent(agent);
                    float distanceSquared = frame.Origin.GetGroundVec3().DistanceSquared(agent.Position);
                    if (agent.CanReachAndUseObject(item, distanceSquared))
                    {
                        if (!assignment.UseRequested)
                        {
                            assignment.UseRequested = true;
                            Log("use requested agent=" + AgentName(agent) + " item=" + ItemName(item));
                        }
                        agent.UseGameObject(item, -1);
                    }
                }
                catch (Exception ex)
                {
                    Log("use failed agent=" + AgentName(agent) + " item=" + ItemName(item) + " error=" + ex);
                    ClearAssignment(agent);
                }
            }
        }

        private void OnItemPickedUp(Agent agent, SpawnedItemEntity item)
        {
            if (agent != null && _assignments.ContainsKey(agent))
                Log("pickup succeeded agent=" + AgentName(agent) + " item=" + ItemName(item));
            if (agent != null && _assignments.ContainsKey(agent)) ClearAssignment(agent);
            foreach (Agent other in _assignments.Where(x => ReferenceEquals(x.Value.Item, item)).Select(x => x.Key).ToArray())
                ClearAssignment(other);
        }

        private void ClearAssignment(Agent agent)
        {
            if (agent == null) return;
            if (_assignments.Remove(agent) && agent.HumanAIComponent != null)
            {
                try
                {
                    if (agent.HumanAIComponent.GetCurrentlyMovingGameObject() is SpawnedItemEntity)
                        agent.HumanAIComponent.MoveToClear();
                }
                catch (Exception ex) { Log("clear movement failed agent=" + AgentName(agent) + " error=" + ex); }
            }
        }

        private void TickSummaryLog(float dt)
        {
            _summaryLogTimer -= dt;
            if (_summaryLogTimer > 0f) return;
            _summaryLogTimer = SummaryLogInterval;
            if (_eligibleSinceLastSummary > 0)
                Log("search summary eligible=" + _eligibleSinceLastSummary + " noCandidate=" +
                    _noCandidateSinceLastSummary + " activeAssignments=" + _assignments.Count);
            _eligibleSinceLastSummary = 0;
            _noCandidateSinceLastSummary = 0;
        }

        private static string AgentName(Agent agent)
        {
            return agent?.Name?.ToString() ?? "null";
        }

        private static string ItemName(SpawnedItemEntity item)
        {
            return item?.WeaponCopy.Item?.StringId ?? "null";
        }

        private static void Log(string message)
        {
            UnarmedWeaponPickupLog.Info(message);
        }

        protected override void OnEndMission()
        {
            Mission.OnItemPickUp -= OnItemPickedUp;
            foreach (Agent agent in _assignments.Keys.ToArray()) ClearAssignment(agent);
            _assignments.Clear();
            Log("MissionLogic ended");
            base.OnEndMission();
        }

        private sealed class CandidateSelection
        {
            public SpawnedItemEntity Item { get; }
            public EquipmentIndex Slot { get; }
            public CandidateSelection(SpawnedItemEntity item, EquipmentIndex slot)
            {
                Item = item;
                Slot = slot;
            }
        }

        private sealed class Assignment
        {
            public SpawnedItemEntity Item { get; }
            public float Age { get; set; }
            public bool UseRequested { get; set; }
            public Assignment(SpawnedItemEntity item) { Item = item; }
        }
    }
}
