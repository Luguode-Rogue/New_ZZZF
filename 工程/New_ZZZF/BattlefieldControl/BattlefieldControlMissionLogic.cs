using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.BattlefieldControl
{
    internal sealed class FormationReferenceComparer : IEqualityComparer<Formation>
    {
        internal static readonly FormationReferenceComparer Instance = new FormationReferenceComparer();

        public bool Equals(Formation x, Formation y) => ReferenceEquals(x, y);

        public int GetHashCode(Formation obj) => obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }

    public sealed class BattlefieldControlMissionLogic : MissionLogic
    {
        internal static BattlefieldControlMissionLogic Active { get; private set; }
        private readonly Dictionary<Formation, Formation> _targets =
            new Dictionary<Formation, Formation>(FormationReferenceComparer.Instance);
        private readonly Dictionary<Formation, FormationWeaponPolicy> _weapons =
            new Dictionary<Formation, FormationWeaponPolicy>(FormationReferenceComparer.Instance);
        // Published snapshots are never mutated: Harmony readers may run during native AI work.
        private volatile Dictionary<Agent, Formation> _restricted = new Dictionary<Agent, Formation>();
        private volatile Dictionary<Agent, Formation> _weaponOwners = new Dictionary<Agent, Formation>();
        private readonly HashSet<Agent> _inputCallbackAgents = new HashSet<Agent>();
        private readonly List<Formation> _expired = new List<Formation>();
        private readonly Dictionary<Agent, Action> _weaponSubscriptions = new Dictionary<Agent, Action>();
        private readonly ConcurrentQueue<Agent> _weaponChanges = new ConcurrentQueue<Agent>();
        private readonly HashSet<Agent> _changedWeapons = new HashSet<Agent>();
        private volatile bool _applyingWeapons;
        private OrderController _controller;
        private float _nextUpdate;
        private bool _ownMovement;
        private bool _keyDown;
        private bool _installationFailed;

        internal bool IsBattle => Mission != null && ReferenceEquals(Mission, Mission.Current) &&
            !GameNetwork.IsSessionActive && Mission.Mode == MissionMode.Battle && !Mission.MissionEnded;

        public override void OnCreated()
        {
            base.OnCreated();
            Active = this;
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (!IsBattle)
            {
                if (_restricted.Count > 0) Publish(new Dictionary<Agent, Formation>());
                ClearWeaponSubscriptions();
                _keyDown = false;
                return;
            }
            if (_installationFailed) return;
            if (!BattlefieldControlBootstrap.IsInstalled)
            {
                // First valid battle tick: native mission initialization has finished.
                // Do not detour MovementOrder-dependent methods during submodule load.
                try { BattlefieldControlBootstrap.Install(); }
                catch (Exception ex)
                {
                    _installationFailed = true;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "战场控制初始化失败，本场已停用：" + ex, Colors.Red));
                    return;
                }
            }
            OrderController controller = Mission.PlayerTeam?.PlayerOrderController;
            if (!ReferenceEquals(controller, _controller))
            {
                if (_controller != null) _controller.OnOrderIssued -= OnOrder;
                _controller = controller;
                if (_controller != null) _controller.OnOrderIssued += OnOrder;
            }
            bool down = Input.IsKeyDown(InputKey.K) &&
                (Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl));
            if (down && !_keyDown && ScreenManager.TopScreen is MissionScreen &&
                !InformationManager.IsAnyInquiryActive()) BattlefieldControlPanel.Open(this);
            _keyDown = down;
            DrainWeaponChanges();
            if (Mission.CurrentTime < _nextUpdate) return;
            _nextUpdate = Mission.CurrentTime + 0.25f;
            UpdateControl();
        }

        internal List<Formation> Selected()
        {
            var result = new List<Formation>();
            if (!IsBattle || _controller?.SelectedFormations == null) return result;
            foreach (Formation f in _controller.SelectedFormations)
                if (Commandable(f)) result.Add(f);
            return result;
        }

        private bool Commandable(Formation f) => f != null && f.Team == Mission.PlayerTeam &&
            f.CountOfUnits > 0 && _controller != null && _controller.IsFormationSelectable(f);

        private void OnOrder(OrderType type, MBReadOnlyList<Formation> formations,
            OrderController controller, params object[] parameters)
        {
            if (!IsBattle || !ReferenceEquals(controller, _controller)) return;
            if ((type == OrderType.Charge || type == OrderType.Advance || type == OrderType.ChargeWithTarget) &&
                parameters.Length > 0 && parameters[0] is Formation target && Enemy(target))
            {
                foreach (Formation f in formations)
                    if (Commandable(f)) _targets[f] = target;
                UpdateControl();
            }
            else if (type == OrderType.AIControlOn)
            {
                foreach (Formation f in formations) { _targets.Remove(f); _weapons.Remove(f); }
                UpdateControl();
            }
        }

        internal void OnMovementChanged(Formation formation)
        {
            // Native order preview can call SetMovementOrder with a partially initialized
            // formation. The reference-keyed table avoids Formation.GetHashCode() here.
            if (_ownMovement || !IsBattle || formation == null || !_targets.Remove(formation)) return;
            var next = new Dictionary<Agent, Formation>(_restricted);
            foreach (var pair in _restricted)
                if (pair.Key.Formation == formation) next.Remove(pair.Key);
            Publish(next);
        }

        private bool Enemy(Formation f) => f != null && f.Team != null &&
            Mission.PlayerTeam != null && f.Team.IsEnemyOf(Mission.PlayerTeam);

        internal void Attack(List<Formation> formations, Formation target)
        {
            if (!IsBattle || !Enemy(target) || target.CountOfUnits <= 0) return;
            _ownMovement = true;
            try
            {
                foreach (Formation f in formations)
                {
                    if (!Commandable(f)) continue;
                    if (f.IsAIControlled) f.SetControlledByAI(false, false);
                    f.SetMovementOrder(MovementOrder.MovementOrderCharge);
                    f.SetTargetFormation(target); // SetMovementOrder clears TargetFormation.
                    _targets[f] = target;
                }
            }
            finally { _ownMovement = false; }
            UpdateControl();
            Notify("已锁定目标编队；目标清空后恢复自由选敌。");
        }

        internal void SetWeapons(List<Formation> formations, bool allowOnly, ControlledWeaponKind kinds)
        {
            if (!IsBattle || kinds == ControlledWeaponKind.None) return;
            foreach (Formation f in formations)
            {
                if (!Commandable(f)) continue;
                if (f.IsAIControlled) f.SetControlledByAI(false, false);
                _weapons.TryGetValue(f, out FormationWeaponPolicy old);
                _weapons[f] = new FormationWeaponPolicy(
                    allowOnly ? kinds : old?.Allowed ?? ControlledWeaponKind.None,
                    allowOnly ? old?.Denied ?? ControlledWeaponKind.None : kinds);
            }
            UpdateControl();
            Notify((allowOnly ? "只使用" : "禁用") + "所选武器规则已应用。");
        }

        internal void ClearWeaponSide(List<Formation> formations, bool allowOnly)
        {
            if (!IsBattle) return;
            foreach (Formation f in formations)
            {
                if (!_weapons.TryGetValue(f, out FormationWeaponPolicy old)) continue;
                ControlledWeaponKind allowed = allowOnly ? ControlledWeaponKind.None : old.Allowed;
                ControlledWeaponKind denied = allowOnly ? old.Denied : ControlledWeaponKind.None;
                if (allowed == ControlledWeaponKind.None && denied == ControlledWeaponKind.None) _weapons.Remove(f);
                else _weapons[f] = new FormationWeaponPolicy(allowed, denied);
            }
            UpdateControl();
            Notify(allowOnly ? "武器白名单已解除，保留禁用规则。" : "武器黑名单已解除，保留允许规则。");
        }

        internal void Clear(List<Formation> formations, bool targets, bool weapons)
        {
            if (!IsBattle) return;
            foreach (Formation f in formations)
            {
                if (targets) { _targets.Remove(f); f.SetTargetFormation(null); }
                if (weapons) _weapons.Remove(f);
            }
            UpdateControl();
            Notify("所选编队的控制规则已解除。");
        }

        internal Formation RestrictedTarget(Agent agent)
        {
            var snapshot = _restricted;
            return agent != null && snapshot.TryGetValue(agent, out Formation target) ? target : null;
        }

        internal bool OwnsWeaponSelection(Agent agent)
        {
            var snapshot = _weaponOwners;
            return IsBattle && agent != null && snapshot.TryGetValue(agent, out Formation formation) &&
                ReferenceEquals(agent.Formation, formation) && !formation.IsAIControlled && Controllable(agent);
        }

        private static bool Controllable(Agent agent) => agent.IsActive() && agent.IsHuman &&
            agent.IsAIControlled && !agent.IsRunningAway &&
            !agent.IsUsingGameObject &&
            (agent.GetScriptedFlags() & Agent.AIScriptedFrameFlags.GoToPosition) == 0;

        // Native close-combat task forces detach soldiers and try to retaliate against
        // nearby enemies. Keep the target restriction on those soldiers as well.
        private static bool TargetControllable(Agent agent) => agent.IsActive() && agent.IsHuman &&
            agent.IsAIControlled && !agent.IsRunningAway;

        private void Publish(Dictionary<Agent, Formation> next)
        {
            var old = _restricted;
            _restricted = next; // Remove guards before restoring native selection.
            foreach (var pair in old)
            {
                Agent agent = pair.Key;
                if (!next.ContainsKey(agent) && agent.IsActive())
                {
                    agent.SetTargetAgent(null);
                    agent.SetAutomaticTargetSelection(true);
                }
            }
        }

        private void UpdateControl()
        {
            if (!IsBattle) return;
            if (_targets.Count == 0 && _weapons.Count == 0)
            {
                if (_restricted.Count > 0) Publish(new Dictionary<Agent, Formation>());
                ClearWeaponSubscriptions();
                return;
            }
            var candidates = new Dictionary<Formation, List<Agent>>(FormationReferenceComparer.Instance);
            foreach (var pair in _targets)
                if (!candidates.ContainsKey(pair.Value)) candidates.Add(pair.Value, new List<Agent>());
            foreach (Agent agent in Mission.Agents)
                if (agent.IsActive() && agent.IsHuman && agent.Formation != null &&
                    candidates.TryGetValue(agent.Formation, out List<Agent> list)) list.Add(agent);

            _expired.Clear();
            foreach (var pair in _targets)
                if (!Commandable(pair.Key) || pair.Key.IsAIControlled || !Enemy(pair.Value) ||
                    candidates[pair.Value].Count == 0) _expired.Add(pair.Key);
            foreach (Formation f in _expired)
            {
                _targets.Remove(f);
                f.SetTargetFormation(null);
            }
            var next = new Dictionary<Agent, Formation>();
            foreach (Agent agent in Mission.Agents)
            {
                if (!TargetControllable(agent) || agent.Formation == null) continue;
                if (_targets.TryGetValue(agent.Formation, out Formation target)) next[agent] = target;
            }
            Publish(next);
            int offset = 0;
            foreach (var pair in next)
            {
                Agent agent = pair.Key;
                agent.SetAutomaticTargetSelection(false);
                Agent current = agent.GetTargetAgent();
                if (current != null && current.IsActive() && current.Formation == pair.Value && agent.IsEnemyOf(current)) continue;
                // Clear a stale out-of-formation target even while operating a siege device.
                // Device aiming remains native; do not issue an infantry target to its user.
                if (agent.IsUsingGameObject)
                {
                    agent.SetTargetAgent(null);
                    continue;
                }
                List<Agent> list = candidates[pair.Value];
                Agent best = null;
                float distance = float.MaxValue;
                // Bounded sampling avoids a quadratic scan in large battles. Keep valid
                // targets stable; stagger the candidate window between soldiers/updates.
                int count = Math.Min(32, list.Count);
                int start = (offset++ * 17 + (int)(Mission.CurrentTime * 4f)) % list.Count;
                for (int i = 0; i < count; i++)
                {
                    Agent candidate = list[(start + i * Math.Max(1, list.Count / count)) % list.Count];
                    if (!candidate.IsActive() || !agent.IsEnemyOf(candidate)) continue;
                    float d = (agent.Position - candidate.Position).LengthSquared;
                    if (d < distance) { distance = d; best = candidate; }
                }
                agent.SetTargetAgent(best);
            }
            var weaponAgents = new HashSet<Agent>();
            var weaponOwners = new Dictionary<Agent, Formation>();
            // Publish ownership before correcting equipment. Native callbacks only read
            // this immutable snapshot; they never touch the rule tables or equipment.
            foreach (Agent agent in Mission.Agents)
                if (Controllable(agent) && agent.Formation != null && !agent.Formation.IsAIControlled &&
                    _weapons.TryGetValue(agent.Formation, out FormationWeaponPolicy policy) && policy.Affects(agent))
                    weaponOwners.Add(agent, agent.Formation);
            _weaponOwners = weaponOwners;
            foreach (Agent agent in Mission.Agents)
            {
                if (!weaponOwners.ContainsKey(agent) ||
                    !_weapons.TryGetValue(agent.Formation, out FormationWeaponPolicy policy)) continue;
                weaponAgents.Add(agent);
                if (_inputCallbackAgents.Add(agent)) agent.SetHasOnAiInputSetCallback(true);
                if (!_weaponSubscriptions.ContainsKey(agent))
                {
                    Agent captured = agent;
                    Action changed = () =>
                    {
                        // The callback can come from native AI work. Only enqueue here;
                        // all equipment operations are performed on the mission main tick.
                        if (!_applyingWeapons) _weaponChanges.Enqueue(captured);
                    };
                    _weaponSubscriptions.Add(agent, changed);
                    agent.OnAgentWieldedItemChange += changed;
                }
                ApplyWeapons(agent, policy);
            }
            var remove = new List<Agent>();
            foreach (var pair in _weaponSubscriptions)
                if (!weaponAgents.Contains(pair.Key)) remove.Add(pair.Key);
            foreach (Agent agent in remove)
            {
                agent.OnAgentWieldedItemChange -= _weaponSubscriptions[agent];
                _weaponSubscriptions.Remove(agent);
            }
        }

        private void ApplyWeapons(Agent agent, FormationWeaponPolicy policy)
        {
            _applyingWeapons = true;
            try { policy.Apply(agent); }
            finally { _applyingWeapons = false; }
        }

        private void DrainWeaponChanges()
        {
            _changedWeapons.Clear();
            for (int i = 0; i < 4096 && _weaponChanges.TryDequeue(out Agent agent); i++)
                _changedWeapons.Add(agent);
            foreach (Agent agent in _changedWeapons)
                if (Controllable(agent) && agent.Formation != null && !agent.Formation.IsAIControlled &&
                    _weapons.TryGetValue(agent.Formation, out FormationWeaponPolicy policy)) ApplyWeapons(agent, policy);
        }

        private void ClearWeaponSubscriptions()
        {
            _weaponOwners = new Dictionary<Agent, Formation>();
            foreach (var pair in _weaponSubscriptions) pair.Key.OnAgentWieldedItemChange -= pair.Value;
            _weaponSubscriptions.Clear();
            while (_weaponChanges.TryDequeue(out _)) { }
        }

        internal static void Notify(string message) =>
            InformationManager.DisplayMessage(new InformationMessage("战场控制：" + message));

        internal void Release()
        {
            if (_controller != null) _controller.OnOrderIssued -= OnOrder;
            _controller = null;
            _targets.Clear();
            _weapons.Clear();
            ClearWeaponSubscriptions();
            // Do not disable the native callback: other AgentComponents may need it.
            // With no ownership snapshot, our postfix passes all native input through.
            _inputCallbackAgents.Clear();
            Publish(new Dictionary<Agent, Formation>());
            if (ReferenceEquals(Active, this)) Active = null;
        }

        protected override void OnEndMission() { Release(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Release(); base.OnRemoveBehavior(); }
    }
}
