using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;

namespace New_ZZZF.ContinuousCollision
{
    /// <summary>
    /// Per-mission host for continuous melee collision detection.
    /// It samples weapon motion every mission tick and feeds only confirmed contacts to the original combat calculation.
    /// </summary>
    public sealed class ContinuousCollisionMissionLogic : MissionLogic
    {
        private readonly ContinuousCollisionSettings _settings = new ContinuousCollisionSettings();
        private readonly Dictionary<int, AgentCollisionState> _states = new Dictionary<int, AgentCollisionState>();
        private readonly Dictionary<CellKey, List<Agent>> _grid = new Dictionary<CellKey, List<Agent>>();
        private float _gridTimer;
        private float _diagnosticTimer;
        private bool _loggedStartup;
        private int _eligibleAttackers;
        private int _candidateChecks;
        private int _geometryChecks;
        private int _confirmedContacts;
        private int _appliedHits;

        public override void AfterStart()
        {
            base.AfterStart();
            _states.Clear();
            _grid.Clear();
            _gridTimer = 0f;
            _diagnosticTimer = 0f;
            _loggedStartup = false;
            ResetDiagnostics();
            ContinuousCollisionLog.Initialize();
            ContinuousCollisionLog.Section("MISSION START");
            ContinuousCollisionLog.Info(
                "Enabled=" + _settings.Enabled
                + " | EnabledWithoutAttackAnimation=" + _settings.EnabledWithoutAttackAnimation
                + " | FriendlyFire=" + _settings.FriendlyFire
                + " | TickInterval=" + _settings.TickInterval
                + " | WeaponRadius=" + _settings.WeaponRadius
                + " | AdditionalReach=" + _settings.AdditionalReach
                + " | MinimumMovement=" + _settings.MinimumMovement
                + " | HitCooldown=" + _settings.HitCooldown
                + " | CellSize=" + _settings.CellSize
                + " | MaximumCandidateDistance=" + _settings.MaximumCandidateDistance
                + " | DefaultWeaponReach=" + _settings.DefaultWeaponReach);
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!_settings.Enabled || Mission.Current == null || Mission.Current.Agents == null)
            {
                return;
            }

            try
            {
                if (!_loggedStartup)
                {
                    ContinuousCollisionLog.Info(
                        "Enabled: MissionTick entered. Mission="
                        + Mission.Current.GetType().FullName
                        + " | AgentCount=" + Mission.Current.Agents.Count
                        + " | LogPath=" + ContinuousCollisionLog.LogPath);
                    _loggedStartup = true;
                }

                if (_settings.TickInterval > 0f)
                {
                    _gridTimer += dt;
                    if (_gridTimer < _settings.TickInterval)
                    {
                        return;
                    }
                    _gridTimer = 0f;
                }

                RebuildGrid();
                CleanupStates();
                ResetDiagnostics();

                foreach (Agent attacker in Mission.Current.Agents)
                {
                    if (!IsEligibleAttacker(attacker))
                    {
                        ResetState(attacker);
                        continue;
                    }

                    _eligibleAttackers++;
                    AgentCollisionState state = GetOrCreateState(attacker);
                    WeaponPose currentPose = ContinuousCollisionGeometry.GetWeaponPose(attacker, _settings);
                    if (currentPose.Weapon.CurrentUsageItem == null || currentPose.WeaponLength <= 0f)
                    {
                        state.HasPreviousPose = false;
                        continue;
                    }

                    bool attackAnimation = attacker.GetCurrentActionType(0).ToString().IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (_settings.EnabledWithoutAttackAnimation && attackAnimation)
                    {
                        state.PreviousPose = currentPose;
                        state.HasPreviousPose = true;
                        continue;
                    }

                    if (!state.HasPreviousPose)
                    {
                        state.PreviousPose = currentPose;
                        state.HasPreviousPose = true;
                        continue;
                    }

                    if (IsTooSmallMovement(state.PreviousPose, currentPose))
                    {
                        state.PreviousPose = currentPose;
                        continue;
                    }

                    ProcessAttacker(attacker, state, currentPose);
                    state.PreviousPose = currentPose;
                }

                _diagnosticTimer += dt;
                if (_diagnosticTimer >= 1f)
                {
                    ContinuousCollisionLog.Trace(
                        "MissionTick summary"
                        + " | agents=" + Mission.Current.Agents.Count
                        + " | gridCells=" + _grid.Count
                        + " | states=" + _states.Count
                        + " | eligibleAttackers=" + _eligibleAttackers
                        + " | candidates=" + _candidateChecks
                        + " | geometryChecks=" + _geometryChecks
                        + " | contacts=" + _confirmedContacts
                        + " | appliedHits=" + _appliedHits);
                    _diagnosticTimer = 0f;
                }
            }
            catch (Exception ex)
            {
                ContinuousCollisionLog.Error("Unhandled exception in ContinuousCollisionMissionLogic.OnMissionTick.", ex);
            }
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (affectedAgent != null)
            {
                _states.Remove(affectedAgent.Index);
                ContinuousCollisionLog.Trace("Agent state removed: index=" + affectedAgent.Index);
            }
            base.OnAgentDeleted(affectedAgent);
        }

        private void ProcessAttacker(Agent attacker, AgentCollisionState state, WeaponPose currentPose)
        {
            float queryRadius = Math.Max(_settings.MaximumCandidateDistance, currentPose.WeaponLength + 0.75f);
            Vec3 center = new Vec3(
                (state.PreviousPose.Tip.x + currentPose.Tip.x) * 0.5f,
                (state.PreviousPose.Tip.y + currentPose.Tip.y) * 0.5f,
                (state.PreviousPose.Tip.z + currentPose.Tip.z) * 0.5f,
                -1f);

            int minX = Cell(center.x - queryRadius);
            int maxX = Cell(center.x + queryRadius);
            int minY = Cell(center.y - queryRadius);
            int maxY = Cell(center.y + queryRadius);

            float now = Mission.Current.CurrentTime;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    List<Agent> candidates;
                    if (!_grid.TryGetValue(new CellKey(x, y), out candidates))
                    {
                        continue;
                    }

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        Agent victim = candidates[i];
                        if (!IsEligibleVictim(attacker, victim) || HasHitCooldown(state, victim.Index, now))
                        {
                            continue;
                        }

                        _candidateChecks++;
                        float distance = DistanceSquared(victim.Position, center);
                        float broadRadius = _settings.MaximumCandidateDistance + currentPose.WeaponLength;
                        if (distance > broadRadius * broadRadius)
                        {
                            continue;
                        }

                        _geometryChecks++;
                        CollisionContact contact;
                        if (!ContinuousCollisionGeometry.TryFindCollision(state.PreviousPose, currentPose, victim, _settings, out contact))
                        {
                            continue;
                        }

                        _confirmedContacts++;
                        if (ContinuousCombatBridge.TryApplyContact(attacker, currentPose, contact, _settings))
                        {
                            _appliedHits++;
                            state.LastHitTimes[victim.Index] = now;
                        }
                    }
                }
            }
        }

        private void RebuildGrid()
        {
            _grid.Clear();
            int inserted = 0;
            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsHuman || agent.IsMount)
                {
                    continue;
                }

                Vec3 p = agent.Position;
                CellKey key = new CellKey(Cell(p.x), Cell(p.y));
                List<Agent> list;
                if (!_grid.TryGetValue(key, out list))
                {
                    list = new List<Agent>();
                    _grid.Add(key, list);
                }
                list.Add(agent);
                inserted++;
            }

            ContinuousCollisionLog.Trace("Victim grid rebuilt: cells=" + _grid.Count + " agents=" + inserted);
        }

        private bool IsEligibleAttacker(Agent agent)
        {
            if (agent == null || !agent.IsActive() || !agent.IsHuman || agent.IsMount)
            {
                return false;
            }
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None || agent.Equipment[slot].IsEmpty)
            {
                return false;
            }
            WeaponComponentData weapon = agent.Equipment[slot].CurrentUsageItem;
            return weapon != null && weapon.WeaponFlags.HasAnyFlag(WeaponFlags.WeaponMask);
        }

        private bool IsEligibleVictim(Agent attacker, Agent victim)
        {
            if (victim == null || !victim.IsActive() || !victim.IsHuman || victim.IsMount)
            {
                return false;
            }
            if (attacker == victim)
            {
                return false;
            }
            return _settings.FriendlyFire || !attacker.IsFriendOf(victim);
        }

        private bool HasHitCooldown(AgentCollisionState state, int victimIndex, float now)
        {
            float last;
            return state.LastHitTimes.TryGetValue(victimIndex, out last) && now - last < _settings.HitCooldown;
        }

        private AgentCollisionState GetOrCreateState(Agent attacker)
        {
            AgentCollisionState state;
            if (!_states.TryGetValue(attacker.Index, out state))
            {
                state = new AgentCollisionState();
                _states.Add(attacker.Index, state);
                ContinuousCollisionLog.Info("Tracking attacker: index=" + attacker.Index);
            }
            return state;
        }

        private void ResetState(Agent attacker)
        {
            if (attacker != null)
            {
                AgentCollisionState state;
                if (_states.TryGetValue(attacker.Index, out state))
                {
                    state.HasPreviousPose = false;
                    state.LastHitTimes.Clear();
                }
            }
        }

        private void CleanupStates()
        {
            if (Mission.Current.CurrentTime < 1f)
            {
                return;
            }

            List<int> remove = null;
            foreach (KeyValuePair<int, AgentCollisionState> pair in _states)
            {
                Agent agent = Mission.Current.FindAgentWithIndex(pair.Key);
                if (agent == null || !agent.IsActive())
                {
                    if (remove == null)
                    {
                        remove = new List<int>();
                    }
                    remove.Add(pair.Key);
                }
                else
                {
                    List<int> staleVictims = null;
                    foreach (KeyValuePair<int, float> hit in pair.Value.LastHitTimes)
                    {
                        if (Mission.Current.CurrentTime - hit.Value > 2f)
                        {
                            if (staleVictims == null)
                            {
                                staleVictims = new List<int>();
                            }
                            staleVictims.Add(hit.Key);
                        }
                    }
                    if (staleVictims != null)
                    {
                        for (int i = 0; i < staleVictims.Count; i++)
                        {
                            pair.Value.LastHitTimes.Remove(staleVictims[i]);
                        }
                    }
                }
            }

            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    _states.Remove(remove[i]);
                }
            }
        }

        private void ResetDiagnostics()
        {
            _eligibleAttackers = 0;
            _candidateChecks = 0;
            _geometryChecks = 0;
            _confirmedContacts = 0;
            _appliedHits = 0;
        }

        private bool IsTooSmallMovement(WeaponPose previous, WeaponPose current)
        {
            return DistanceSquared(previous.Base, current.Base) < _settings.MinimumMovement * _settings.MinimumMovement &&
                   DistanceSquared(previous.Tip, current.Tip) < _settings.MinimumMovement * _settings.MinimumMovement;
        }

        private int Cell(float value)
        {
            return (int)Math.Floor(value / _settings.CellSize);
        }

        private static float DistanceSquared(Vec3 a, Vec3 b)
        {
            float x = a.x - b.x;
            float y = a.y - b.y;
            float z = a.z - b.z;
            return x * x + y * y + z * z;
        }

        private readonly struct CellKey : IEquatable<CellKey>
        {
            public readonly int X;
            public readonly int Y;

            public CellKey(int x, int y)
            {
                X = x;
                Y = y;
            }

            public bool Equals(CellKey other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is CellKey && Equals((CellKey)obj);
            public override int GetHashCode() => (X * 397) ^ Y;
        }
    }
}
