using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 灵马哨笛的任务内骑乘过渡管理器。
    /// 它不管理召唤物，只保证同一骑手在原版上马或下马动作完成前不会重复创建坐骑。
    /// </summary>
    public sealed class SpiritSteedMissionLogic : MissionLogic
    {
        private enum TransitionKind
        {
            Mounting,
            Dismounting,
            WaitingToFade
        }

        private sealed class TransitionRecord
        {
            public Agent Rider;
            public Agent Mount;
            public TransitionKind Kind;
            public float Deadline;
            public float FadeAt;
            public float FallbackAt;
            public bool FallbackApplied;
        }

        private const float TransitionTimeout = 6f;
        private const float DismountFadeDelay = 0.15f;
        private const float NativeRequestGraceTime = 0.25f;

        private readonly Dictionary<int, TransitionRecord> _transitions = new Dictionary<int, TransitionRecord>();
        private readonly List<KeyValuePair<int, TransitionRecord>> _transitionSnapshot =
            new List<KeyValuePair<int, TransitionRecord>>();
        private readonly List<int> _completedRiderIndices = new List<int>();
        private readonly List<Agent> _mountsToFade = new List<Agent>();

        public static SpiritSteedMissionLogic Current { get; private set; }
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public override void OnCreated()
        {
            base.OnCreated();
            Current = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Current = this;
        }

        public static SpiritSteedMissionLogic GetForCurrentMission()
        {
            Mission mission = Mission.Current;
            if (mission == null)
                return null;
            if (Current != null && ReferenceEquals(Current.Mission, mission))
                return Current;

            Current = mission.GetMissionBehavior<SpiritSteedMissionLogic>();
            return Current;
        }

        public bool IsTransitioning(Agent rider)
        {
            return rider != null && _transitions.ContainsKey(rider.Index);
        }

        public bool TryToggleMount(Agent rider, out string failureReason)
        {
            failureReason = null;
            if (rider == null || !rider.IsActive() || rider.IsMount)
            {
                failureReason = "骑手当前不可用。";
                return false;
            }
            if (_transitions.ContainsKey(rider.Index))
            {
                failureReason = "上下马动作尚未完成。";
                return false;
            }

            if (rider.MountAgent != null)
                return BeginDismount(rider, rider.MountAgent, out failureReason);

            return BeginMount(rider, out failureReason);
        }

        private bool BeginMount(Agent rider, out string failureReason)
        {
            failureReason = null;
            if (Mission == null)
            {
                failureReason = "当前任务不可用。";
                return false;
            }

            Equipment equipment = rider.Character == null ? null : rider.Character.Equipment;
            ItemObject horseItem = equipment == null ? null : equipment[EquipmentIndex.Horse].Item;
            if (horseItem == null || !horseItem.HasHorseComponent)
            {
                failureReason = "角色没有装备有效坐骑。";
                return false;
            }

            ItemObject harnessItem = equipment[EquipmentIndex.HorseHarness].Item;
            ItemRosterElement horse = new ItemRosterElement(horseItem, 1, null);
            ItemRosterElement harness = harnessItem == null
                ? default(ItemRosterElement)
                : new ItemRosterElement(harnessItem, 1, null);

            Agent spawnedMount = null;
            try
            {
                MatrixFrame frame = rider.GetWorldFrame().ToGroundMatrixFrame();
                Vec2 direction = frame.rotation.f.AsVec2;
                spawnedMount = Mission.SpawnMonster(horse, harness, frame.origin, direction, -1);
                if (spawnedMount == null)
                {
                    failureReason = "坐骑生成失败。";
                    return false;
                }

                spawnedMount.FadeIn();
                _transitions.Add(rider.Index, new TransitionRecord
                {
                    Rider = rider,
                    Mount = spawnedMount,
                    Kind = TransitionKind.Mounting,
                    Deadline = Mission.CurrentTime + TransitionTimeout,
                    FallbackAt = Mission.CurrentTime + NativeRequestGraceTime
                });

                // 只发出原版上马请求，不直接改 MountAgent，由引擎完成动作与关系同步。
                rider.Mount(spawnedMount);
                return true;
            }
            catch (Exception ex)
            {
                _transitions.Remove(rider.Index);
                if (spawnedMount != null && spawnedMount.IsActive() && spawnedMount.RiderAgent == null)
                    spawnedMount.FadeOut(true, false);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                failureReason = "坐骑生成或上马失败。";
                return false;
            }
        }

        private bool BeginDismount(Agent rider, Agent mount, out string failureReason)
        {
            failureReason = null;
            if (mount == null || !mount.IsActive())
            {
                failureReason = "当前坐骑不可用。";
                return false;
            }

            _transitions.Add(rider.Index, new TransitionRecord
            {
                Rider = rider,
                Mount = mount,
                Kind = TransitionKind.Dismounting,
                Deadline = Mission.CurrentTime + TransitionTimeout,
                FallbackAt = Mission.CurrentTime + NativeRequestGraceTime
            });

            // 对当前坐骑再次调用 Mount 是原版下马入口。
            try
            {
                rider.Mount(mount);
                return true;
            }
            catch (Exception ex)
            {
                _transitions.Remove(rider.Index);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                failureReason = "下马请求失败。";
                return false;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_transitions.Count == 0)
                return;

            _completedRiderIndices.Clear();
            _mountsToFade.Clear();
            _transitionSnapshot.Clear();
            foreach (KeyValuePair<int, TransitionRecord> pair in _transitions)
                _transitionSnapshot.Add(pair);

            for (int transitionIndex = 0; transitionIndex < _transitionSnapshot.Count; transitionIndex++)
            {
                KeyValuePair<int, TransitionRecord> pair = _transitionSnapshot[transitionIndex];
                TransitionRecord record = pair.Value;
                if (!_transitions.TryGetValue(pair.Key, out TransitionRecord currentRecord) ||
                    !ReferenceEquals(currentRecord, record))
                    continue;

                Agent rider = record.Rider;
                Agent mount = record.Mount;

                if (rider == null || !rider.IsActive() || mount == null || !mount.IsActive())
                {
                    _completedRiderIndices.Add(pair.Key);
                    continue;
                }

                if (record.Kind == TransitionKind.Mounting)
                {
                    bool mountActionFinished = rider.GetCurrentActionType(0) != Agent.ActionCodeType.Mount;
                    if (rider.MountAgent == mount && mount.RiderAgent == rider && mountActionFinished)
                    {
                        _completedRiderIndices.Add(pair.Key);
                        continue;
                    }

                    if (!record.FallbackApplied && Mission.CurrentTime >= record.FallbackAt)
                    {
                        record.FallbackApplied = true;
                        bool nativeMountStarted = rider.GetCurrentActionType(0) == Agent.ActionCodeType.Mount;
                        if (!nativeMountStarted && rider.MountAgent == null && mount.RiderAgent == null)
                            ApplyLegacyMountFallback(rider, mount);
                    }

                    if (Mission.CurrentTime >= record.Deadline)
                    {
                        // 原版没有接受上马请求时，清理本次生成的空闲坐骑，避免下次施法累积多匹马。
                        if (mount.RiderAgent == null)
                            _mountsToFade.Add(mount);
                        _completedRiderIndices.Add(pair.Key);
                    }
                    continue;
                }

                if (record.Kind == TransitionKind.Dismounting)
                {
                    if (rider.MountAgent != mount && mount.RiderAgent != rider)
                    {
                        record.Kind = TransitionKind.WaitingToFade;
                        record.FadeAt = Mission.CurrentTime + DismountFadeDelay;
                        continue;
                    }

                    if (!record.FallbackApplied && Mission.CurrentTime >= record.FallbackAt)
                    {
                        record.FallbackApplied = true;
                        bool nativeDismountStarted = rider.GetCurrentActionType(0) == Agent.ActionCodeType.Dismount;
                        if (!nativeDismountStarted && rider.MountAgent == mount && mount.RiderAgent == rider)
                            ApplyLegacyDismountFallback(rider);
                    }

                    if (Mission.CurrentTime >= record.Deadline)
                        _completedRiderIndices.Add(pair.Key);
                    continue;
                }

                bool dismountActionFinished = rider.GetCurrentActionType(0) != Agent.ActionCodeType.Dismount;
                if (Mission.CurrentTime >= record.FadeAt && dismountActionFinished)
                {
                    _mountsToFade.Add(mount);
                    _completedRiderIndices.Add(pair.Key);
                }
            }

            for (int i = 0; i < _completedRiderIndices.Count; i++)
                _transitions.Remove(_completedRiderIndices[i]);

            // FadeOut 可能同步触发 OnAgentRemoved。必须等字典遍历结束后再执行，
            // 否则 OnAgentRemoved 会修改 _transitions 并使枚举器失效。
            for (int i = 0; i < _mountsToFade.Count; i++)
            {
                Agent mount = _mountsToFade[i];
                if (mount != null && mount.IsActive() && mount.RiderAgent == null)
                    mount.FadeOut(false, false);
            }
            _mountsToFade.Clear();
        }

        /// <summary>
        /// 某些原版任务不会接受与骑手重合生成的坐骑交互。
        /// 先给原版 Mount 一小段响应时间；仍未开始时，仅对本技能的骑手恢复旧版可用的绑定方式。
        /// 这不再是 Agent.Mount 的全局 Harmony 补丁。
        /// </summary>
        private static void ApplyLegacyMountFallback(Agent rider, Agent mount)
        {
            try
            {
                Traverse.Create(rider).Property("MountAgent").SetValue(mount);
                rider.SetActionChannel(
                    0,
                    ActionIndexCache.Create("act_mount_horse_from_left"),
                    false,
                    (AnimFlags)272UL,
                    0,
                    1f,
                    -0.2f,
                    0.4f,
                    0.25f);
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }

        private static void ApplyLegacyDismountFallback(Agent rider)
        {
            try
            {
                Traverse.Create(rider).Property("MountAgent").SetValue(null);
                rider.SetActionChannel(0, ActionIndexCache.Create("act_horse_fall_roll"));
                rider.SetCurrentActionProgress(0, 0.3f);
                rider.SetCurrentActionSpeed(0, 2f);
            }
            catch (Exception ex)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow killingBlow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
            if (affectedAgent == null || _transitions.Count == 0)
                return;

            _completedRiderIndices.Clear();
            foreach (KeyValuePair<int, TransitionRecord> pair in _transitions)
            {
                if (pair.Value.Rider == affectedAgent || pair.Value.Mount == affectedAgent)
                    _completedRiderIndices.Add(pair.Key);
            }
            for (int i = 0; i < _completedRiderIndices.Count; i++)
                _transitions.Remove(_completedRiderIndices[i]);
        }

        protected override void OnEndMission()
        {
            ClearState();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            ClearState();
            base.OnRemoveBehavior();
        }

        private void ClearState()
        {
            _transitions.Clear();
            _transitionSnapshot.Clear();
            _completedRiderIndices.Clear();
            _mountsToFade.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
        }
    }
}
