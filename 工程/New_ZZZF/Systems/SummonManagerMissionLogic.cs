using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>管理战场临时召唤物、到期遣散和施法者生命上限债务。</summary>
    public sealed class SummonManagerMissionLogic : MissionLogic
    {
        public const float DefaultLifetimeSeconds = 30f;
        public const float HealthLimitCostPerTier = 1f;

        private static readonly string[] EmpireTroopIds =
        {
            null,
            "imperial_recruit",
            "imperial_infantryman",
            "imperial_trained_infantryman",
            "imperial_veteran_infantryman",
            "imperial_legionary",
            "imperial_elite_cataphract"
        };

        private sealed class SummonRecord
        {
            public Agent Summoner;
            public Agent SummonedAgent;
            public int Tier;
            public float RemainingLifetime;
            public bool ReservationReleased;
        }

        private sealed class HealthDebtRecord
        {
            public Agent Summoner;
            public float BaseHealthLimit;
            public float Debt;
        }

        /// <summary>
        /// SimpleAgentOrigin 在没有 Party 时会把 IsUnderPlayersCommand 固定判为 false。
        /// 召唤物没有战役 Party，因此在这里显式继承施法者的指挥归属，同时仍保留
        /// SimpleAgentOrigin 的空伤亡结算行为，保证召唤物不会进入战役兵员统计。
        /// </summary>
        private sealed class SummonAgentOrigin : SimpleAgentOrigin, IAgentOriginBase
        {
            private readonly bool _isUnderPlayersCommand;

            public SummonAgentOrigin(BasicCharacterObject troop, bool isUnderPlayersCommand)
                : base(troop, -1, null, default(UniqueTroopDescriptor))
            {
                _isUnderPlayersCommand = isUnderPlayersCommand;
            }

            bool IAgentOriginBase.IsUnderPlayersCommand
            {
                get { return _isUnderPlayersCommand; }
            }
        }

        private readonly Dictionary<int, SummonRecord> _summonsByAgentIndex = new Dictionary<int, SummonRecord>();
        private readonly Dictionary<int, List<SummonRecord>> _summonsBySummonerIndex = new Dictionary<int, List<SummonRecord>>();
        private readonly Dictionary<int, HealthDebtRecord> _healthDebts = new Dictionary<int, HealthDebtRecord>();

        public static SummonManagerMissionLogic Current { get; private set; }

        /// <summary>扩展预留：开启后，新批次完整生成成功时遣散同一施法者的旧召唤物。当前关闭。</summary>
        public bool DismissExistingOnNewSummon { get; set; }

        /// <summary>扩展接口：默认召唤持续时间。</summary>
        public float SummonLifetimeSeconds { get; set; } = DefaultLifetimeSeconds;

        public override void OnCreated()
        {
            base.OnCreated();
            // 本管理器由 SubModule.OnMissionBehaviorInitialize 动态加入。
            // 原版此时已经跑完 OnBehaviorInitialize 阶段，但 AddMissionBehavior 一定会调用 OnCreated。
            Current = this;
            DismissExistingOnNewSummon = false;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Current = this;
            DismissExistingOnNewSummon = false;
        }

        /// <summary>从当前任务恢复管理器引用，避免静态引用因加载顺序或热重载而丢失。</summary>
        public static SummonManagerMissionLogic GetForCurrentMission()
        {
            Mission mission = TaleWorlds.MountAndBlade.Mission.Current;
            if (mission == null)
                return null;

            if (Current != null && ReferenceEquals(Current.Mission, mission))
                return Current;

            SummonManagerMissionLogic manager = mission.GetMissionBehavior<SummonManagerMissionLogic>();
            if (manager != null)
                Current = manager;
            return manager;
        }

        public bool TrySummon(Agent summoner, Vec3 targetPosition)
        {
            string ignored;
            return TrySummon(summoner, targetPosition, out ignored);
        }

        public bool TrySummon(Agent summoner, Vec3 targetPosition, out string failureReason)
        {
            int ignoredCount;
            return TrySummonBatch(summoner, targetPosition, 1, string.Empty, out ignoredCount, out failureReason);
        }

        /// <summary>
        /// 创建经过当前被动栏修饰的通用召唤调用。未来的召唤技能即使使用不同兵种或生成逻辑，
        /// 也可调用本方法取得最终数量和旧召唤物替换策略。
        /// </summary>
        public SummonInvocation CreateSummonInvocation(Agent summoner, string sourceSkillId, int baseCount)
        {
            SummonInvocation invocation = new SummonInvocation(summoner, sourceSkillId, baseCount);
            AgentSkillComponent component = summoner != null ? summoner.GetComponent<AgentSkillComponent>() : null;
            if (component == null && summoner != null)
                SkillSystemBehavior.ActiveComponents.TryGetValue(summoner.Index, out component);

            ISummonInvocationModifier modifier = component?.PassiveSkill as ISummonInvocationModifier;
            modifier?.ModifySummonInvocation(invocation);
            return invocation;
        }

        /// <summary>
        /// 执行一批帝国系召唤。整批采用同一次技能结算；生成不完整时回滚本批，
        /// 全部成功后才按调用策略遣散旧召唤物。
        /// </summary>
        public bool TrySummonBatch(
            Agent summoner,
            Vec3 targetPosition,
            int baseCount,
            string sourceSkillId,
            out int summonedCount,
            out string failureReason)
        {
            summonedCount = 0;
            failureReason = null;
            if (summoner == null)
            {
                failureReason = "施法者为空。";
                return false;
            }
            if (!summoner.IsActive())
            {
                failureReason = "施法者当前不处于活动状态。";
                return false;
            }
            if (Mission == null)
            {
                failureReason = "当前召唤管理器没有关联战场任务。";
                return false;
            }
            if (summoner.Team == null)
            {
                failureReason = "施法者没有有效队伍，无法确定召唤物阵营。";
                return false;
            }
            if (Game.Current?.ObjectManager == null)
            {
                failureReason = "游戏对象管理器尚未初始化。";
                return false;
            }

            int tier = ResolveSummonTier(summoner);
            CharacterObject troop = Game.Current?.ObjectManager?.GetObject<CharacterObject>(EmpireTroopIds[tier]);
            if (troop == null)
            {
                failureReason = "未找到阶级 " + tier + " 对应的帝国兵种：" + EmpireTroopIds[tier] + "。";
                Debug.Print("[New_ZZZF][召唤] " + failureReason);
                return false;
            }

            return TrySummonTroopBatch(
                summoner,
                targetPosition,
                troop,
                tier,
                baseCount,
                sourceSkillId,
                false,
                out summonedCount,
                out failureReason);
        }

        /// <summary>
        /// 通用兵种批量召唤入口。未来的召唤技能传入自己的兵种、阶级和基础数量，
        /// 即可自动获得被动修饰、临时单位管理、编队、生命上限债务和安全遣散。
        /// </summary>
        public bool TrySummonTroopBatch(
            Agent summoner,
            Vec3 targetPosition,
            CharacterObject troop,
            int tier,
            int baseCount,
            string sourceSkillId,
            bool allowMount,
            out int summonedCount,
            out string failureReason)
        {
            summonedCount = 0;
            failureReason = null;
            if (summoner == null)
            {
                failureReason = "施法者为空。";
                return false;
            }
            if (!summoner.IsActive())
            {
                failureReason = "施法者当前不处于活动状态。";
                return false;
            }
            if (Mission == null)
            {
                failureReason = "当前召唤管理器没有关联战场任务。";
                return false;
            }
            if (summoner.Team == null)
            {
                failureReason = "施法者没有有效队伍，无法确定召唤物阵营。";
                return false;
            }
            if (troop == null)
            {
                failureReason = "召唤兵种为空。";
                return false;
            }
            if (tier <= 0)
            {
                failureReason = "召唤物阶级必须大于零。";
                return false;
            }

            SummonInvocation invocation;
            try
            {
                invocation = CreateSummonInvocation(summoner, sourceSkillId, baseCount);
            }
            catch (Exception ex)
            {
                failureReason = "计算召唤数量时发生异常：" + ex.Message;
                Debug.Print("[New_ZZZF][召唤] 被动修饰召唤调用失败: " + ex);
                return false;
            }

            if (invocation.Count <= 0)
            {
                failureReason = "召唤数量必须大于零。";
                return false;
            }

            Vec3 destination = NormalizeGroundPosition(targetPosition, summoner.Position);
            Vec2 initialDirection = summoner.LookDirection.AsVec2;
            if (initialDirection.LengthSquared < 0.001f)
                initialDirection = new Vec2(0f, 1f);
            initialDirection.Normalize();

            bool initializeFormationWithStopOrder;
            Formation summonFormation = ResolveSummonFormation(
                summoner.Team,
                out initializeFormationWithStopOrder);
            if (initializeFormationWithStopOrder)
                summonFormation.SetMovementOrder(MovementOrder.MovementOrderStop);
            bool isUnderPlayersCommand = IsSummonerUnderPlayersCommand(summoner);

            bool dismissExisting = DismissExistingOnNewSummon || invocation.DismissExistingSummons;
            List<SummonRecord> oldSummons = dismissExisting ? GetSummonSnapshot(summoner) : null;
            List<SummonRecord> newSummons = new List<SummonRecord>(invocation.Count);

            for (int i = 0; i < invocation.Count; i++)
            {
                Vec3 spawnPosition = GetBatchSpawnPosition(destination, i, invocation.Count);
                SummonRecord record;
                string spawnFailure;
                if (!TrySpawnOne(
                    summoner,
                    troop,
                    tier,
                    spawnPosition,
                    initialDirection,
                    summonFormation,
                    isUnderPlayersCommand,
                    allowMount,
                    out record,
                    out spawnFailure))
                {
                    for (int rollbackIndex = 0; rollbackIndex < newSummons.Count; rollbackIndex++)
                        Dismiss(newSummons[rollbackIndex], true);

                    failureReason = string.Format(
                        "批量召唤在第 {0}/{1} 个单位处失败：{2}",
                        i + 1,
                        invocation.Count,
                        spawnFailure);
                    return false;
                }

                newSummons.Add(record);
            }

            // 空编队在首个单位加入时可能从战场逻辑继承缓存命令；生成完成后再次确认默认停止。
            if (initializeFormationWithStopOrder)
                summonFormation.SetMovementOrder(MovementOrder.MovementOrderStop);

            // oldSummons 是生成前的快照，不会误删本批刚生成的单位。
            if (oldSummons != null)
            {
                for (int i = 0; i < oldSummons.Count; i++)
                    Dismiss(oldSummons[i], true);
            }

            summonedCount = newSummons.Count;
            Debug.Print(string.Format(
                "[New_ZZZF][召唤] {0} 完成批量召唤：{1} 个，阶级 {2}，替换旧召唤物 {3} 个。",
                summoner.Name,
                summonedCount,
                tier,
                oldSummons != null ? oldSummons.Count : 0));
            return true;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            // 某些原版/模组属性刷新会重设 HealthLimit；有债务时每帧重新施加，保证上限占用稳定。
            if (_healthDebts.Count > 0)
            {
                List<HealthDebtRecord> debts = new List<HealthDebtRecord>(_healthDebts.Values);
                for (int i = 0; i < debts.Count; i++)
                {
                    ApplyHealthLimit(debts[i]);
                    if (debts[i].Debt <= 0f && debts[i].Summoner != null && debts[i].Summoner.IsActive())
                        _healthDebts.Remove(debts[i].Summoner.Index);
                }
            }

            if (_summonsByAgentIndex.Count == 0)
                return;

            List<SummonRecord> snapshot = new List<SummonRecord>(_summonsByAgentIndex.Values);
            for (int i = 0; i < snapshot.Count; i++)
            {
                SummonRecord record = snapshot[i];
                Agent summon = record.SummonedAgent;
                if (summon == null || !summon.IsActive())
                {
                    RemoveRecord(record);
                    continue;
                }

                record.RemainingLifetime -= dt;
                if (record.RemainingLifetime <= 0f)
                    Dismiss(record, true);
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent == null)
                return;

            SummonRecord record;
            if (_summonsByAgentIndex.TryGetValue(affectedAgent.Index, out record))
                RemoveRecord(record);

            // 施法者死亡不遣散召唤物；召唤物继续按各自寿命战斗。
        }

        /// <summary>主动遣散指定施法者的全部召唤物，供后续策略调用。</summary>
        public void DismissSummons(Agent summoner, bool killSummons)
        {
            if (summoner == null)
                return;

            List<SummonRecord> records;
            if (!_summonsBySummonerIndex.TryGetValue(summoner.Index, out records))
                return;

            List<SummonRecord> snapshot = new List<SummonRecord>(records);
            for (int i = 0; i < snapshot.Count; i++)
                Dismiss(snapshot[i], killSummons);
        }

        public int GetActiveSummonCount(Agent summoner)
        {
            if (summoner == null)
                return 0;
            List<SummonRecord> records;
            return _summonsBySummonerIndex.TryGetValue(summoner.Index, out records) ? records.Count : 0;
        }

        public float GetHealthLimitDebt(Agent summoner)
        {
            if (summoner == null)
                return 0f;
            HealthDebtRecord record;
            return _healthDebts.TryGetValue(summoner.Index, out record) ? record.Debt : 0f;
        }

        /// <summary>供战场结算补丁识别临时召唤物。</summary>
        public static bool IsManagedSummon(Agent agent)
        {
            SummonManagerMissionLogic manager = GetForCurrentMission();
            return manager != null && agent != null && manager._summonsByAgentIndex.ContainsKey(agent.Index);
        }

        public static int ResolveSummonTier(Agent summoner)
        {
            if (summoner.IsHero)
            {
                int level = Math.Max(1, summoner.Character.Level);
                return Math.Min(6, Math.Max(1, (level + 4) / 5));
            }

            CharacterObject troop = summoner.Character as CharacterObject;
            int troopTier = troop != null ? troop.Tier : 1;
            return Math.Min(6, Math.Max(1, troopTier));
        }

        private Vec3 NormalizeGroundPosition(Vec3 requested, Vec3 fallback)
        {
            Vec3 result = requested;
            if (!result.IsValid || result == Vec3.Zero)
                result = fallback;
            result.z = Mission.Scene.GetGroundHeightAtPosition(result, BodyFlags.CommonCollisionExcludeFlags);
            return result;
        }

        private List<SummonRecord> GetSummonSnapshot(Agent summoner)
        {
            List<SummonRecord> records;
            if (summoner == null || !_summonsBySummonerIndex.TryGetValue(summoner.Index, out records))
                return new List<SummonRecord>();
            return new List<SummonRecord>(records);
        }

        /// <summary>
        /// 单体召唤严格落在目视点；批量召唤的第一个位于中心，其余单位紧凑环绕中心，
        /// 避免多个 Agent 完全重叠。这里只做出生排布，不包含位移或入场冲锋。
        /// </summary>
        private Vec3 GetBatchSpawnPosition(Vec3 center, int index, int count)
        {
            if (count <= 1 || index == 0)
                return center;

            int surroundingCount = Math.Max(1, count - 1);
            int ringIndex = index - 1;
            const float unitSpacing = 1.25f;
            int ringCapacity = 8;
            int ring = ringIndex / ringCapacity;
            int indexInRing = ringIndex % ringCapacity;
            int unitsInThisRing = Math.Min(ringCapacity, surroundingCount - ring * ringCapacity);
            float angle = (float)(Math.PI * 2.0 * indexInRing / Math.Max(1, unitsInThisRing));
            float radius = unitSpacing * (ring + 1);

            Vec3 candidate = center + new Vec3(
                (float)Math.Cos(angle) * radius,
                (float)Math.Sin(angle) * radius,
                0f);
            candidate.z = Mission.Scene.GetGroundHeightAtPosition(candidate, BodyFlags.CommonCollisionExcludeFlags);

            int faceGroupId;
            if (Mission.Scene.GetNavigationMeshForPosition(candidate, out faceGroupId, 1.5f, false) == UIntPtr.Zero)
                return center;
            return candidate;
        }

        private bool TrySpawnOne(
            Agent summoner,
            CharacterObject troop,
            int tier,
            Vec3 spawnPosition,
            Vec2 initialDirection,
            Formation summonFormation,
            bool isUnderPlayersCommand,
            bool allowMount,
            out SummonRecord record,
            out string failureReason)
        {
            record = null;
            failureReason = null;

            SummonAgentOrigin origin = new SummonAgentOrigin(troop, isUnderPlayersCommand);
            AgentBuildData buildData = new AgentBuildData(troop)
                .TroopOrigin(origin)
                .Team(summoner.Team)
                .Formation(summonFormation)
                .InitialPosition(spawnPosition)
                .InitialDirection(initialDirection)
                .Controller(AgentControllerType.AI)
                .CivilianEquipment(false)
                .NoWeapons(false)
                .NoHorses(!allowMount)
                .ClothingColor1(summoner.Team.Color)
                .ClothingColor2(summoner.Team.Color2);

            Agent summonedAgent;
            try
            {
                summonedAgent = Mission.SpawnAgent(buildData, false);
            }
            catch (Exception ex)
            {
                failureReason = "生成士兵时发生异常：" + ex.Message;
                Debug.Print("[New_ZZZF][召唤] SpawnAgent失败: " + ex);
                return false;
            }

            if (summonedAgent == null)
            {
                failureReason = "游戏的 SpawnAgent 返回了空对象。";
                return false;
            }

            InitializeSummonedCombatAgent(summonedAgent, summonFormation, summoner, isUnderPlayersCommand);
            record = new SummonRecord
            {
                Summoner = summoner,
                SummonedAgent = summonedAgent,
                Tier = tier,
                RemainingLifetime = Math.Max(0.1f, SummonLifetimeSeconds)
            };
            Register(record);
            ReserveHealthLimit(record);
            return true;
        }

        /// <summary>
        /// 每个队伍的召唤物共用一个专属编队。已有活动召唤物时优先复用其编队；
        /// 否则使用第一个空编队；八个常规编队均被占用时按策划落入第8编队（索引7）。
        /// </summary>
        private Formation ResolveSummonFormation(Team team, out bool initializeWithStopOrder)
        {
            foreach (SummonRecord record in _summonsByAgentIndex.Values)
            {
                Agent existingSummon = record.SummonedAgent;
                if (existingSummon != null && existingSummon.IsActive() &&
                    existingSummon.Team == team && existingSummon.Formation != null)
                {
                    initializeWithStopOrder = false;
                    return existingSummon.Formation;
                }
            }

            for (int i = 0; i < team.FormationsIncludingEmpty.Count; i++)
            {
                Formation formation = team.FormationsIncludingEmpty[i];
                if (formation.CountOfUnits == 0)
                {
                    initializeWithStopOrder = true;
                    return formation;
                }
            }

            int fallbackIndex = Math.Min(7, team.FormationsIncludingEmpty.Count - 1);
            initializeWithStopOrder = true;
            return team.FormationsIncludingEmpty[fallbackIndex];
        }

        private static bool IsSummonerUnderPlayersCommand(Agent summoner)
        {
            if (summoner == null)
                return false;
            if (summoner.IsMainAgent)
                return true;
            return summoner.Origin != null && summoner.Origin.IsUnderPlayersCommand;
        }

        /// <summary>补齐原版 SpawnTroopWithAgentBuildData 在 SpawnAgent 之后执行的战斗初始化。</summary>
        private void InitializeSummonedCombatAgent(
            Agent summon,
            Formation formation,
            Agent summoner,
            bool isUnderPlayersCommand)
        {
            summon.Controller = AgentControllerType.AI;
            if (formation != null && summon.Formation != formation)
                summon.Formation = formation;

            // 玩家作为全军指挥官时，该编队必须接受玩家命令；作为队长时则归属玩家。
            if (isUnderPlayersCommand && summon.Team == Mission.PlayerTeam && formation != null)
            {
                if (summon.Team.IsPlayerGeneral)
                    formation.SetControlledByAI(false, false);
                else if (summoner != null && summoner.IsMainAgent && formation.CountOfUnits == 1)
                    formation.PlayerOwner = summoner;
            }

            summon.SetIsAIPaused(false);
            summon.SetWatchState(Agent.WatchState.Alarmed);
            summon.WieldInitialWeapons(
                Agent.WeaponWieldActionType.InstantAfterPickUp,
                Equipment.InitialWeaponEquipPreference.Any);
            summon.ResetEnemyCaches();
            summon.ForceAiBehaviorSelection();
        }

        private void Register(SummonRecord record)
        {
            _summonsByAgentIndex[record.SummonedAgent.Index] = record;
            List<SummonRecord> records;
            if (!_summonsBySummonerIndex.TryGetValue(record.Summoner.Index, out records))
            {
                records = new List<SummonRecord>();
                _summonsBySummonerIndex.Add(record.Summoner.Index, records);
            }
            records.Add(record);
        }

        private void ReserveHealthLimit(SummonRecord summon)
        {
            Agent summoner = summon.Summoner;
            HealthDebtRecord debt;
            if (!_healthDebts.TryGetValue(summoner.Index, out debt))
            {
                debt = new HealthDebtRecord
                {
                    Summoner = summoner,
                    BaseHealthLimit = Math.Max(1f, summoner.HealthLimit)
                };
                _healthDebts.Add(summoner.Index, debt);
            }
            debt.Debt += summon.Tier * HealthLimitCostPerTier;
            ApplyHealthLimit(debt);
        }

        private void ReleaseHealthLimit(SummonRecord summon)
        {
            if (summon.ReservationReleased)
                return;
            summon.ReservationReleased = true;

            HealthDebtRecord debt;
            if (!_healthDebts.TryGetValue(summon.Summoner.Index, out debt))
                return;
            debt.Debt = Math.Max(0f, debt.Debt - summon.Tier * HealthLimitCostPerTier);
            ApplyHealthLimit(debt);
            // 若施法者此时已死亡，保留零债务记录；其在本任务内复活时仍可恢复原上限。
            if (debt.Debt <= 0f && debt.Summoner != null && debt.Summoner.IsActive())
                _healthDebts.Remove(summon.Summoner.Index);
        }

        private static void ApplyHealthLimit(HealthDebtRecord debt)
        {
            Agent summoner = debt.Summoner;
            if (summoner == null || !summoner.IsActive())
                return;

            // 理论债务允许超过基础上限；引擎显示值至少保留1点，所以生命不足也不阻止召唤。
            float effectiveLimit = Math.Max(1f, debt.BaseHealthLimit - debt.Debt);
            summoner.HealthLimit = effectiveLimit;
            if (summoner.Health > effectiveLimit)
                summoner.Health = effectiveLimit;
        }

        private void Dismiss(SummonRecord record, bool killSummon)
        {
            Agent summon = record.SummonedAgent;
            if (!killSummon || summon == null || !summon.IsActive())
            {
                RemoveRecord(record);
                return;
            }

            try
            {
                // 不使用 MakeDead/Die：它们会把临时单位当作战役伤亡送进士气模型。
                // 当前 SandboxBattleMoraleModel 对“无击杀者”的强制死亡缺少空值保护，
                // 会在 affectorAgent.Formation 处抛 NullReferenceException。
                // FadeOut 是原版用于无伤亡结算移除活动 Agent 的路径，最终状态为 Deleted，
                // 既符合临时召唤物离场语义，也不会污染击杀、士气和战役兵员统计。
                summon.FadeOut(false, true);
            }
            catch (Exception ex)
            {
                Debug.Print("[New_ZZZF][召唤] 安全移除召唤物失败: " + ex);
            }
            finally
            {
                // 放在 FadeOut 后回收，使同步触发的原版移除回调仍能识别该 Agent 为召唤物。
                RemoveRecord(record);
            }
        }

        private void RemoveRecord(SummonRecord record)
        {
            if (record == null)
                return;

            ReleaseHealthLimit(record);
            if (record.SummonedAgent != null)
            {
                _summonsByAgentIndex.Remove(record.SummonedAgent.Index);
                RushMovementMissionLogic.Current?.CancelRush(record.SummonedAgent);
            }

            List<SummonRecord> records;
            if (record.Summoner != null && _summonsBySummonerIndex.TryGetValue(record.Summoner.Index, out records))
            {
                records.Remove(record);
                if (records.Count == 0)
                    _summonsBySummonerIndex.Remove(record.Summoner.Index);
            }
        }

        protected override void OnEndMission()
        {
            _summonsByAgentIndex.Clear();
            _summonsBySummonerIndex.Clear();
            _healthDebts.Clear();
            if (ReferenceEquals(Current, this))
                Current = null;
            base.OnEndMission();
        }
    }
}
