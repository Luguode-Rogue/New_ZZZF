using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 疾风连斩的逐 Agent 状态机。每一段都通过玩家/AI原生输入入口重新建立
    /// AttackReady -> ReleaseMelee，避免直接重播动作导致碰撞窗口不重置。
    /// </summary>
    public sealed class JiFengLianZhanMissionLogic : MissionLogic, IPlayerInputEffector
    {
        private const int BaseSegments = 10;
        private const int BonusBatchSegments = 5;
        private const float BonusSegmentStaminaCost = 5f;
        private const float SpeedStep = 0.10f;
        public const float MaximumActionSpeed = 1.80f;
        private const float ContactTimeout = 2.50f;
        private const float FinishTimeout = 0.50f;
        private const float AttackStartTimeout = 0.40f;

        private enum ChainPhase
        {
            WaitingForInjection,
            WaitingForAttack,
            WaitingForContact,
            FinishPending,
            WaitingForFinish,
            RearmPending
        }

        private sealed class ChainRecord
        {
            public Agent Agent;
            public ItemObject WeaponItem;
            public string WeaponUsage;
            public ChainPhase Phase;
            public int Segment;
            public float ActionSpeed;
            public float Deadline;
            public Agent.MovementControlFlag Direction;
            public Agent.MovementControlFlag PreviousDirection;
            public Agent.MovementControlFlag LastSuccessfulDirection;
            public bool RetryingLastSuccessfulDirection;
            public readonly List<Agent.MovementControlFlag> TriedDirections =
                new List<Agent.MovementControlFlag>(4);
            public string LastSpeedAction;
            public Agent.ActionCodeType LastSpeedType;
        }

        private readonly Dictionary<int, ChainRecord> _records =
            new Dictionary<int, ChainRecord>();
        private readonly List<int> _recordKeys = new List<int>();
        private readonly List<Agent.MovementControlFlag> _directionScratch =
            new List<Agent.MovementControlFlag>(4);

        public static JiFengLianZhanMissionLogic Current { get; private set; }
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public static JiFengLianZhanMissionLogic GetForCurrentMission()
        {
            Mission mission = TaleWorlds.MountAndBlade.Mission.Current;
            if (mission == null)
                return null;

            if (Current != null && ReferenceEquals(Current.Mission, mission))
                return Current;

            JiFengLianZhanMissionLogic logic =
                mission.GetMissionBehavior<JiFengLianZhanMissionLogic>();
            if (logic != null)
                Current = logic;

            return logic;
        }

        public override void OnCreated()
        {
            base.OnCreated();
            // 本逻辑由 SubModule.OnMissionBehaviorInitialize 动态加入；此时任务可能已经
            // 跑完 OnBehaviorInitialize，但 AddMissionBehavior 一定会调用 OnCreated。
            Current = this;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Current = this;
        }

        public bool TryStart(Agent agent, out string reason)
        {
            if (agent == null || !agent.IsActive() || agent.IsMount)
            {
                reason = "施法者当前不可用。";
                return false;
            }
            if (_records.ContainsKey(agent.Index))
            {
                reason = "疾风连斩已经在执行。";
                return false;
            }
            if (!TryGetWeapon(agent, out MissionWeapon weapon, out reason))
                return false;

            var record = new ChainRecord
            {
                Agent = agent,
                WeaponItem = weapon.Item,
                WeaponUsage = GetUsageId(weapon),
                Phase = ChainPhase.RearmPending,
                Segment = 1,
                ActionSpeed = 1f,
                PreviousDirection = Agent.MovementControlFlag.None,
                LastSpeedType = Agent.ActionCodeType.Other
            };
            _records.Add(agent.Index, record);

            if (!QueueRandomAttack(record, out reason))
            {
                _records.Remove(agent.Index);
                return false;
            }

            CancelCurrentAiCombatAction(agent);

            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            reason = null;
            return true;
        }

        public bool IsActive(Agent agent)
        {
            return agent != null && _records.ContainsKey(agent.Index);
        }

        public static bool HasUsableAttackDirection(Agent agent)
        {
            return TryGetWeapon(agent, out MissionWeapon weapon, out _) &&
                (CanSwing(weapon.CurrentUsageItem) || CanThrust(weapon.CurrentUsageItem));
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_records.Count == 0 || Mission == null)
                return;

            _recordKeys.Clear();
            foreach (int key in _records.Keys)
                _recordKeys.Add(key);

            for (int i = 0; i < _recordKeys.Count; i++)
            {
                if (_records.TryGetValue(_recordKeys[i], out ChainRecord record))
                    TickRecord(record);
            }
        }

        private void TickRecord(ChainRecord record)
        {
            if (!ValidateAgentAndWeapon(record, out string invalidReason))
            {
                End(record, invalidReason);
                return;
            }

            switch (record.Phase)
            {
                case ChainPhase.WaitingForInjection:
                    if (Mission.CurrentTime > record.Deadline)
                        End(record, "等待原生攻击输入回调超时");
                    break;
                case ChainPhase.WaitingForAttack:
                    ObserveAttackStart(record);
                    break;
                case ChainPhase.WaitingForContact:
                    ApplyCappedActionSpeed(record);
                    if (Mission.CurrentTime > record.Deadline)
                        End(record, "本段攻击未发生有效接触");
                    break;
                case ChainPhase.FinishPending:
                    EndCurrentAttack(record);
                    break;
                case ChainPhase.WaitingForFinish:
                    ObserveAttackFinished(record);
                    break;
                case ChainPhase.RearmPending:
                    if (!QueueRandomAttack(record, out string reason))
                        End(record, reason);
                    break;
            }
        }

        private void ObserveAttackStart(ChainRecord record)
        {
            Agent.ActionCodeType type = record.Agent.GetCurrentActionType(1);
            if (IsReadyOrRelease(type))
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                record.LastSuccessfulDirection = record.Direction;
                record.RetryingLastSuccessfulDirection = false;
                record.Phase = ChainPhase.WaitingForContact;
                record.Deadline = Mission.CurrentTime + ContactTimeout;
                ApplyCappedActionSpeed(record);
                return;
            }

            if (Mission.CurrentTime <= record.Deadline)
                return;

            // 随机方向被当前姿态拒绝时，优先退回最近一次真正被引擎接受的动作。
            // 若这个已成功过的方向在当前姿态下仍被拒绝，则结束，避免无限重试。
            if (record.LastSuccessfulDirection != Agent.MovementControlFlag.None)
            {
                if (record.RetryingLastSuccessfulDirection ||
                    record.Direction == record.LastSuccessfulDirection)
                {
                    End(record, "当前姿态拒绝上次成功的攻击动作");
                    return;
                }

                Agent.MovementControlFlag rejectedDirection = record.Direction;
                record.Direction = record.LastSuccessfulDirection;
                record.RetryingLastSuccessfulDirection = true;
                record.Phase = ChainPhase.WaitingForInjection;
                record.Deadline = Mission.CurrentTime + AttackStartTimeout;
                record.LastSpeedAction = null;
                record.LastSpeedType = Agent.ActionCodeType.Other;
                if (record.Agent.IsAIControlled)
                    record.Agent.SetHasOnAiInputSetCallback(true);

                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                return;
            }

            // 第一段还没有成功动作可回退时，才继续探测其他合法方向。
            if (!record.TriedDirections.Contains(record.Direction))
                record.TriedDirections.Add(record.Direction);
            record.Phase = ChainPhase.RearmPending;
        }

        private void ApplyCappedActionSpeed(ChainRecord record)
        {
            Agent.ActionCodeType type = record.Agent.GetCurrentActionType(1);
            if (!IsReadyOrRelease(type))
                return;

            string action = record.Agent.GetCurrentAction(1).GetName();
            if (string.Equals(action, record.LastSpeedAction, StringComparison.Ordinal) &&
                type == record.LastSpeedType)
                return;

            // 当前工程其余 SetCurrentActionSpeed 写入都在 channel 0；这里对 channel 1
            // 写入最终倍率并在入口硬限制为 1.80，防止连乘或重复应用越界。
            float speed = MathF.Min(MaximumActionSpeed, MathF.Max(0.01f, record.ActionSpeed));
            record.Agent.SetCurrentActionSpeed(1, speed);
            record.LastSpeedAction = action;
            record.LastSpeedType = type;
        }

        public override void OnMeleeHit(
            Agent attacker,
            Agent victim,
            bool isCanceled,
            AttackCollisionData collisionData)
        {
            base.OnMeleeHit(attacker, victim, isCanceled, collisionData);
            if (attacker == null || !_records.TryGetValue(attacker.Index, out ChainRecord record) ||
                record.Phase != ChainPhase.WaitingForContact)
                return;

            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;

            // 疾风连砍激活期间，伤害模型会将原本的 Blocked 强制判定为突破格挡，
            // 但接触数据仍保留原始的 Blocked 结果。本次攻击照常完成突破与伤害，
            // 这里只终止自动续击，避免突破格挡后立即衔接下一刀。
            if (collisionData.CollisionResult == CombatCollisionResult.Blocked)
            {
                End(record, "本段突破格挡，停止自动续击");
                return;
            }

            // 同一挥击可能接触多个碰撞体；第一次接触后立即离开 WaitingForContact，
            // 因而同一段最多只会安排一次续击。第10段发生有效接触后，以及此后
            // 每完成5段时，支付5点耐力继续下一批5段；不设置总段数上限。
            bool reachedExtensionBoundary = record.Segment >= BaseSegments &&
                (record.Segment - BaseSegments) % BonusBatchSegments == 0;
            if (reachedExtensionBoundary)
            {
                AgentSkillComponent component = Script.GetActiveComponents(record.Agent);
                if (component == null || component._currentStamina < BonusSegmentStaminaCost)
                {
                    End(record, "耐力不足以继续追加下一批5段攻击");
                    return;
                }

                component.ChangeStamina(-BonusSegmentStaminaCost);
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
            }

            record.PreviousDirection = record.Direction;
            record.Segment++;
            record.ActionSpeed = MathF.Min(
                MaximumActionSpeed,
                1f + (record.Segment - 1) * SpeedStep);
            record.TriedDirections.Clear();
            record.Phase = ChainPhase.FinishPending;
            record.Deadline = Mission.CurrentTime + FinishTimeout;
        }

        private void EndCurrentAttack(ChainRecord record)
        {
            bool accepted = record.Agent.SetActionChannel(
                1, ActionIndexCache.act_none, true, (AnimFlags)0UL,
                0f, 1f, 0f, 0f, 0f, false, 0f, 0, false);
            if (!accepted)
            {
                End(record, "引擎拒绝结束上一段攻击");
                return;
            }
            record.Phase = ChainPhase.WaitingForFinish;
            record.Deadline = Mission.CurrentTime + FinishTimeout;
        }

        private void ObserveAttackFinished(ChainRecord record)
        {
            if (!IsAttackExecutionState(record.Agent.GetCurrentActionType(1)))
            {
                record.Phase = ChainPhase.RearmPending;
                return;
            }
            if (Mission.CurrentTime > record.Deadline)
                End(record, "等待上一段攻击结束超时");
        }

        private bool QueueRandomAttack(ChainRecord record, out string reason)
        {
            if (!TryGetWeapon(record.Agent, out MissionWeapon weapon, out reason))
                return false;

            BuildAvailableDirections(weapon.CurrentUsageItem, _directionScratch);
            for (int i = _directionScratch.Count - 1; i >= 0; i--)
            {
                Agent.MovementControlFlag direction = _directionScratch[i];
                if (record.TriedDirections.Contains(direction) ||
                    (_directionScratch.Count > 1 && direction == record.PreviousDirection))
                    _directionScratch.RemoveAt(i);
            }

            // 只有一个合法动作时允许连续使用同一方向。
            if (_directionScratch.Count == 0)
            {
                BuildAvailableDirections(weapon.CurrentUsageItem, _directionScratch);
                for (int i = _directionScratch.Count - 1; i >= 0; i--)
                {
                    if (record.TriedDirections.Contains(_directionScratch[i]))
                        _directionScratch.RemoveAt(i);
                }
            }
            if (_directionScratch.Count == 0)
            {
                reason = "当前武器没有剩余可用的近战攻击方向。";
                return false;
            }

            record.Direction = _directionScratch[MBRandom.RandomInt(_directionScratch.Count)];
            record.RetryingLastSuccessfulDirection = false;
            record.Phase = ChainPhase.WaitingForInjection;
            record.Deadline = Mission.CurrentTime + AttackStartTimeout;
            record.LastSpeedAction = null;
            record.LastSpeedType = Agent.ActionCodeType.Other;
            if (record.Agent.IsAIControlled)
                record.Agent.SetHasOnAiInputSetCallback(true);

            reason = null;
            return true;
        }

        public Agent.EventControlFlag OnCollectPlayerEventControlFlags()
        {
            Agent agent = Mission?.MainAgent;
            if (agent != null && _records.TryGetValue(agent.Index, out ChainRecord record) &&
                record.Phase == ChainPhase.WaitingForInjection)
            {
                agent.MovementFlags |= record.Direction;
                MarkInjected(record);
            }
            return Agent.EventControlFlag.None;
        }

        public void ApplyPendingAiAttack(
            Agent agent,
            ref Agent.MovementControlFlag movementFlags)
        {
            if (agent == null || !_records.TryGetValue(agent.Index, out ChainRecord record))
                return;

            Agent.MovementControlFlag previousFlags = movementFlags;

            // 连斩存续期间完整接管 AI 的战斗输入。无论当前处于起手、命中等待还是
            // 段间过渡，都禁止原生 AI 自行攻击或格挡；只有状态机请求下一段时才写入
            // 唯一的技能攻击方向。记录移除后本方法不再清理，原生 AI 会自然恢复。
            movementFlags &= ~(Agent.MovementControlFlag.AttackMask |
                               Agent.MovementControlFlag.DefendMask |
                               Agent.MovementControlFlag.DefendBlock);

            if (record.Phase != ChainPhase.WaitingForInjection &&
                record.Phase != ChainPhase.WaitingForAttack)
                return;

            bool firstInjection = record.Phase == ChainPhase.WaitingForInjection;
            movementFlags |= record.Direction;

            FaceCurrentTarget(agent);
            if (firstInjection)
            {
                /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
                MarkInjected(record);
            }
        }

        private static void CancelCurrentAiCombatAction(Agent agent)
        {
            if (agent == null || !agent.IsAIControlled)
                return;

            Agent.ActionCodeType actionType = agent.GetCurrentActionType(1);
            bool isDefending =
                (int)actionType >= (int)Agent.ActionCodeType.DefendAllBegin &&
                (int)actionType < (int)Agent.ActionCodeType.DefendAllEnd;
            if (!IsAttackExecutionState(actionType) && !isDefending)
                return;

            bool accepted = agent.SetActionChannel(
                1, ActionIndexCache.act_none, true, (AnimFlags)0UL,
                0f, 1f, 0f, 0f, 0f, false, 0f, 0, false);
            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
        }

        private static void FaceCurrentTarget(Agent agent)
        {
            Agent target = agent?.GetTargetAgent();
            if (target == null || !target.IsActive() || !agent.IsEnemyOf(target))
                return;

            Vec2 direction = target.Position.AsVec2 - agent.Position.AsVec2;
            if (direction.LengthSquared > 0.001f)
                agent.LookDirection = direction.Normalized().ToVec3();
            agent.SetLookAgent(target);
            agent.SetTargetAgent(target);
        }

        private void MarkInjected(ChainRecord record)
        {
            record.Phase = ChainPhase.WaitingForAttack;
            record.Deadline = Mission.CurrentTime + AttackStartTimeout;
        }

        private bool ValidateAgentAndWeapon(ChainRecord record, out string reason)
        {
            if (record.Agent == null || !record.Agent.IsActive())
            {
                reason = "施法者失效";
                return false;
            }
            if (!TryGetWeapon(record.Agent, out MissionWeapon weapon, out reason))
                return false;
            if (weapon.Item != record.WeaponItem ||
                !string.Equals(GetUsageId(weapon), record.WeaponUsage, StringComparison.Ordinal))
            {
                reason = "连斩期间切换了武器或使用方式";
                return false;
            }
            return true;
        }

        private static bool TryGetWeapon(
            Agent agent,
            out MissionWeapon weapon,
            out string reason)
        {
            weapon = agent == null ? MissionWeapon.Invalid : agent.WieldedWeapon;
            if (agent == null || !agent.IsActive() || weapon.IsEmpty || weapon.Item == null ||
                weapon.CurrentUsageItem == null || !weapon.CurrentUsageItem.IsMeleeWeapon)
            {
                reason = "需要手持具有有效攻击动作的近战武器。";
                return false;
            }
            if (!CanSwing(weapon.CurrentUsageItem) && !CanThrust(weapon.CurrentUsageItem))
            {
                reason = "当前武器既不能挥砍也不能突刺。";
                return false;
            }
            reason = null;
            return true;
        }

        private static string GetUsageId(MissionWeapon weapon)
        {
            return weapon.CurrentUsageItem?.WeaponDescriptionId ?? string.Empty;
        }

        private static bool CanSwing(WeaponComponentData usage)
        {
            return usage != null && usage.SwingDamageType != DamageTypes.Invalid && usage.SwingDamage > 0;
        }

        private static bool CanThrust(WeaponComponentData usage)
        {
            return usage != null && usage.ThrustDamageType != DamageTypes.Invalid && usage.ThrustDamage > 0;
        }

        private static void BuildAvailableDirections(
            WeaponComponentData usage,
            List<Agent.MovementControlFlag> output)
        {
            output.Clear();
            if (CanSwing(usage))
            {
                output.Add(Agent.MovementControlFlag.AttackLeft);
                output.Add(Agent.MovementControlFlag.AttackRight);
                return;
            }
            if (CanThrust(usage))
                output.Add(Agent.MovementControlFlag.AttackDown);
        }

        private static bool IsReadyOrRelease(Agent.ActionCodeType type)
        {
            return type == Agent.ActionCodeType.AttackMeleeAllBegin ||
                type == Agent.ActionCodeType.ReadyMelee ||
                type == Agent.ActionCodeType.ReleaseMelee;
        }

        private static bool IsAttackExecutionState(Agent.ActionCodeType type)
        {
            return IsReadyOrRelease(type) ||
                type == Agent.ActionCodeType.BlockedMelee ||
                type == Agent.ActionCodeType.ParriedMelee;
        }

        private void End(ChainRecord record, string reason)
        {
            if (record == null || record.Agent == null || !_records.Remove(record.Agent.Index))
                return;
            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow killingBlow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
            if (affectedAgent != null && _records.TryGetValue(affectedAgent.Index, out ChainRecord record))
                End(record, "施法者被移除");
        }

        protected override void OnEndMission()
        {
            _records.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            _records.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnRemoveBehavior();
        }
    }
}
