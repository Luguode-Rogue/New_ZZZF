using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>跟随施法者的通用光环。每次扫描只查询附近单位；技能提供筛选和首次触及回调。</summary>
    internal sealed class AgentAuraRequest
    {
        public Agent Caster;
        public float Radius;
        public float Duration;
        public float ScanInterval = 0.5f;
        public bool EnemiesOnly = true;
        public Func<Agent, Agent, bool> CanAffect;
        public Action<Agent, Agent, float> OnFirstContact;
        public Action<Agent, Agent, float> OnStay;
        public Action<Agent, Agent> OnAuraEndedForTarget;
    }

    internal sealed class AgentAuraMissionLogic : MissionLogic
    {
        private const int MaximumQueriesPerFrame = 8;

        private sealed class AuraRecord
        {
            public int Id;
            public AgentAuraRequest Request;
            public float Remaining;
            public float UntilScan;
            public readonly HashSet<Agent> Affected = new HashSet<Agent>();
        }

        private readonly List<AuraRecord> _auras = new List<AuraRecord>();
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private int _nextId = 1;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public static AgentAuraMissionLogic GetForCurrentMission()
        {
            return Mission.Current?.GetMissionBehavior<AgentAuraMissionLogic>();
        }

        public bool TryStart(AgentAuraRequest request, out int auraId)
        {
            auraId = 0;
            if (request?.Caster == null || !request.Caster.IsActive() ||
                !ReferenceEquals(request.Caster.Mission, Mission) || request.Radius <= 0f ||
                request.Duration <= 0f || request.ScanInterval <= 0f ||
                request.OnFirstContact == null)
                return false;

            AuraRecord record = new AuraRecord
            {
                Id = _nextId++, Request = request, Remaining = request.Duration,
                UntilScan = request.ScanInterval
            };
            _auras.Add(record);
            try { Scan(record); } // 施放时立即覆盖当前敌人，之后持续纳入新进入者。
            catch (Exception)
            {
                End(record);
                _auras.Remove(record);
                return false;
            }
            auraId = record.Id;
            return true;
        }

        public void Stop(int auraId)
        {
            for (int i = _auras.Count - 1; i >= 0; i--)
            {
                if (_auras[i].Id != auraId)
                    continue;
                End(_auras[i]);
                _auras.RemoveAt(i);
                break;
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (dt <= 0f || _auras.Count == 0)
                return;
            int queries = 0;
            for (int i = _auras.Count - 1; i >= 0; i--)
            {
                AuraRecord record = _auras[i];
                Agent caster = record.Request.Caster;
                record.Remaining -= dt;
                if (record.Remaining <= 0f || caster == null || !caster.IsActive())
                {
                    End(record);
                    _auras.RemoveAt(i);
                    continue;
                }

                record.UntilScan -= dt;
                if (record.UntilScan > 0f || queries >= MaximumQueriesPerFrame)
                    continue;
                record.UntilScan += record.Request.ScanInterval;
                if (record.UntilScan <= 0f)
                    record.UntilScan = record.Request.ScanInterval;
                try { Scan(record); }
                catch (Exception) { /* 一次空间查询失败不终止光环剩余时间。 */ }
                queries++;
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            for (int i = _auras.Count - 1; i >= 0; i--)
            {
                AuraRecord record = _auras[i];
                if (record.Request.Caster == affectedAgent)
                {
                    End(record);
                    _auras.RemoveAt(i);
                }
                else
                    record.Affected.Remove(affectedAgent);
            }
        }

        protected override void OnEndMission()
        {
            _auras.Clear(); // 场景卸载后不访问 Agent 的原生状态。
            _nearby.Clear();
            base.OnEndMission();
        }

        private void Scan(AuraRecord record)
        {
            AgentAuraRequest request = record.Request;
            Agent caster = request.Caster;
            if (caster?.Mission == null || !caster.IsActive())
                return;

            _nearby.Clear();
            if (request.EnemiesOnly && caster.Team != null)
                Mission.GetNearbyEnemyAgents(caster.Position.AsVec2, request.Radius,
                    caster.Team, _nearby);
            else
                Mission.GetNearbyAgents(caster.Position.AsVec2, request.Radius, _nearby);

            float radiusSquared = request.Radius * request.Radius;
            foreach (Agent target in _nearby)
            {
                if (target == null || target == caster || !target.IsActive() ||
                    (target.Position.AsVec2 - caster.Position.AsVec2).LengthSquared > radiusSquared ||
                    request.EnemiesOnly && !caster.IsEnemyOf(target))
                    continue;
                bool canAffect;
                try { canAffect = request.CanAffect == null || request.CanAffect(caster, target); }
                catch (Exception) { continue; }
                if (!canAffect)
                    continue;
                if (!record.Affected.Add(target))
                {
                    try { request.OnStay?.Invoke(caster, target, record.Remaining); }
                    catch (Exception) { /* 单个目标的持续回调失败不阻断其他目标。 */ }
                    continue;
                }
                try { request.OnFirstContact(caster, target, record.Remaining); }
                catch (Exception)
                {
                    // 目标处理失败时允许下次扫描重试，且不影响同一光环的其他目标。
                    record.Affected.Remove(target);
                }
            }
        }

        private static void End(AuraRecord record)
        {
            Action<Agent, Agent> onEnd = record.Request.OnAuraEndedForTarget;
            if (onEnd == null)
                return;
            foreach (Agent target in record.Affected)
            {
                try { onEnd(record.Request.Caster, target); }
                catch (Exception) { /* 单个目标清理失败不能阻断剩余目标。 */ }
            }
        }
    }
}
