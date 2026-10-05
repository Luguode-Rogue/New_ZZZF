using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    /// <summary>武器规则的任务级状态。原生回调只入队，丢装备、位移及追加受击在任务更新执行。</summary>
    public sealed class WeaponCombatMissionLogic : MissionLogic
    {
        internal static WeaponCombatMissionLogic Current { get; private set; }
        internal sealed class Hit
        {
            internal Agent Attacker, Victim;
            internal MissionWeapon Weapon;
            internal Blow Blow;
            internal AttackCollisionData Collision;
            internal int FullDamage;
            internal int DelayedDamage;
            internal float HealthBefore;
            internal float PreArmorDamage;
            internal bool SpearSplit, Synthetic;
            internal bool ActualDamage;
            internal float Time;
            internal WeaponCombatThrustGeometry.Pose ThrustPose;
        }
        private sealed class ArmorPart { internal int Layers; internal float Amount; }
        private sealed class VictimState
        {
            internal readonly Dictionary<BoneBodyPartType, ArmorPart> Armor = new Dictionary<BoneBodyPartType, ArmorPart>();
            internal float LastDamage, SlowUntil;
        }
        private sealed class ShieldState { internal ItemObject Item; internal ItemModifier Modifier; internal float Stacks; }
        private sealed class AttackState
        {
            internal ActionIndexCache Action = ActionIndexCache.act_none;
            internal Agent.ActionStage Stage = Agent.ActionStage.None;
            internal float Progress;
            internal bool Contact, SpeedChanged, KnifeUsed;
            internal Agent.MovementControlFlag PendingDirection;
            internal float KnifeReleaseAt;
            internal bool KnifePending;
        }
        private sealed class Push
        {
            internal Hit Hit;
            internal Vec3 Direction, Origin;
            internal float Distance, Radius, Deadline, LastHit, OffNavigationDistance, PeakProgress, StartTime;
            internal int Attempts;
            internal bool ReleaseEnded;
        }
        private sealed class Bleed
        {
            internal Hit Hit;
            internal int Total, Delivered;
            internal float Start, End;
        }
        private readonly object _gate = new object();
        private readonly Queue<Hit> _hits = new Queue<Hit>();
        private readonly Queue<Tuple<Agent, ActionIndexCache, float>> _contacts = new Queue<Tuple<Agent, ActionIndexCache, float>>();
        private readonly Queue<Tuple<Agent, Agent, EquipmentIndex>> _blocks = new Queue<Tuple<Agent, Agent, EquipmentIndex>>();
        private readonly Queue<Tuple<Agent, WeaponClass>> _shieldHits = new Queue<Tuple<Agent, WeaponClass>>();
        private readonly Dictionary<Agent, VictimState> _victims = new Dictionary<Agent, VictimState>();
        private readonly Dictionary<Agent, AttackState> _attacks = new Dictionary<Agent, AttackState>();
        private readonly Dictionary<Agent, Push> _pushes = new Dictionary<Agent, Push>();
        private readonly Dictionary<Agent, Agent> _pushOwners = new Dictionary<Agent, Agent>();
        private readonly Dictionary<Agent, Dictionary<EquipmentIndex, ShieldState>> _shields = new Dictionary<Agent, Dictionary<EquipmentIndex, ShieldState>>();
        private readonly List<Bleed> _bleeds = new List<Bleed>();
        private readonly List<Agent> _scratch = new List<Agent>();
        private readonly List<Agent> _pushObstacles = new List<Agent>();
        private readonly List<EquipmentIndex> _slotScratch = new List<EquipmentIndex>();
        [ThreadStatic] internal static bool DeliveringBleed;
        [ThreadStatic] internal static bool AdditionalThrust;

        public override void OnCreated()
        {
            base.OnCreated();
            if (!GameNetwork.IsMultiplayer) Current = this;
            foreach (var agent in Mission.Agents)
                if (agent.IsHuman && agent.GetComponent<WeaponCombatAgentComponent>() == null) OnAgentBuild(agent, null);
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            if (!GameNetwork.IsMultiplayer) Current = this;
        }
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            if (Current != this || !agent.IsHuman) return;
            _attacks[agent] = new AttackState();
            if (agent.GetComponent<WeaponCombatAgentComponent>() == null)
                agent.AddComponent(new WeaponCombatAgentComponent(agent));
            if (agent.IsAIControlled) agent.SetHasOnAiInputSetCallback(true);
        }
        internal float ArmorLoss(Agent agent, BoneBodyPartType part) =>
            _victims.TryGetValue(agent, out var state) && state.Armor.TryGetValue(part, out var armor) &&
            Mission.CurrentTime - state.LastDamage < WeaponCombatRules.ArmorResetDelay ? armor.Amount : 0f;
        internal bool IsFootSlowed(Agent agent) => _victims.TryGetValue(agent, out var state) && state.SlowUntil > Mission.CurrentTime;
        internal void EnqueueHit(Hit hit) { lock (_gate) _hits.Enqueue(hit); }
        internal void Contact(Agent agent)
        {
            if (!CanUseWeapons(agent)) return;
            var contact = Tuple.Create(agent, agent.GetCurrentAction(1), agent.GetCurrentActionProgress(1));
            lock (_gate) _contacts.Enqueue(contact);
        }
        internal void Block(Agent attacker, Agent defender, EquipmentIndex slot)
        { if (attacker != null && defender != null && slot != EquipmentIndex.None) lock (_gate) _blocks.Enqueue(Tuple.Create(attacker, defender, slot)); }
        public override void OnRegisterBlow(Agent attacker, Agent victim, WeakGameEntity realHitEntity, Blow b,
            ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon)
        {
            if (Current != this || victim == null || !collisionData.IsMissile || !collisionData.AttackBlockedWithShield ||
                attackerWeapon.CurrentUsageItem == null) return;
            lock (_gate) _shieldHits.Enqueue(Tuple.Create(victim, attackerWeapon.CurrentUsageItem.WeaponClass));
        }
        private AttackState Attack(Agent agent)
        {
            if (!_attacks.TryGetValue(agent, out var state)) _attacks[agent] = state = new AttackState();
            return state;
        }
        private VictimState Victim(Agent agent)
        {
            if (!_victims.TryGetValue(agent, out var state)) _victims[agent] = state = new VictimState();
            return state;
        }
        private static bool Alive(Agent agent) => agent != null && agent.IsActive() && agent.Health > 0f;
        // 坐骑也能存活并产生接触事件，但不具有人类武器装备容器。
        private static bool CanUseWeapons(Agent agent) => Alive(agent) && agent.IsHuman && agent.Equipment != null;
        internal static MissionWeapon HeldWeapon(Agent agent)
        {
            if (!CanUseWeapons(agent)) return MissionWeapon.Invalid;
            MissionEquipment equipment = agent.Equipment;
            if (equipment == null) return MissionWeapon.Invalid;
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot < EquipmentIndex.WeaponItemBeginSlot || slot >= EquipmentIndex.NumAllWeaponSlots)
                return MissionWeapon.Invalid;
            MissionWeapon weapon = equipment[slot];
            if (!weapon.IsEmpty && (weapon.CurrentUsageIndex < 0 || weapon.CurrentUsageIndex >= weapon.WeaponsCount))
                return MissionWeapon.Invalid;
            return weapon;
        }
        private static bool Body(in AttackCollisionData c) => c.CollisionResult == CombatCollisionResult.StrikeAgent &&
            !c.AttackBlockedWithShield && !c.CollidedWithShieldOnBack && !c.IsFallDamage && !c.IsHorseCharge && !c.IsAlternativeAttack;

        public override void OnMissionTick(float dt)
        {
            if (Current != this || dt <= 0f || Mission.Mode != MissionMode.Battle) return;
            DrainEvents();
            TickBleeds();
            TickVictims();
            TickShields(dt);
            TickAttacks();
            TickPushes(dt);
        }
        private void DrainEvents()
        {
            // 出队时不持锁执行原生调用；追加受击可以再次入队。
            while (true)
            {
                Tuple<Agent, ActionIndexCache, float> record;
                lock (_gate) { if (_contacts.Count == 0) break; record = _contacts.Dequeue(); }
                Agent contact = record.Item1;
                // 延迟消费的上一次接触不能让新攻击免除打空惩罚。
                if (CanUseWeapons(contact) && contact.GetCurrentAction(1) == record.Item2 &&
                    contact.GetCurrentActionProgress(1) + 0.1f >= record.Item3)
                {
                    var state = Attack(contact); ObserveAction(contact, state); state.Contact = true;
                    if (state.SpeedChanged && contact.GetCurrentActionStage(1) == Agent.ActionStage.AttackRelease)
                    { contact.SetCurrentActionSpeed(1, 1f); state.SpeedChanged = false; }
                }
            }
            var blockPairs = new HashSet<Tuple<Agent, Agent>>();
            while (true)
            {
                Tuple<Agent, Agent, EquipmentIndex> block;
                lock (_gate) { if (_blocks.Count == 0) break; block = _blocks.Dequeue(); }
                if (!CanUseWeapons(block.Item1) || !CanUseWeapons(block.Item2) || !blockPairs.Add(Tuple.Create(block.Item1, block.Item2))) continue;
                if (MBRandom.RandomFloat < WeaponCombatRules.ProcChance(block.Item1, block.Item2, HeldWeapon(block.Item1).CurrentUsageItem))
                    DeferredDisarmExecutor.Mark(block.Item2, block.Item3);
            }
            while (true)
            {
                Hit hit;
                lock (_gate) { if (_hits.Count == 0) break; hit = _hits.Dequeue(); }
                if (hit.Victim == null) continue;
                if (hit.ActualDamage) Victim(hit.Victim).LastDamage = hit.Time;
                if (hit.SpearSplit && Alive(hit.Victim))
                    _bleeds.Add(new Bleed { Hit = hit, Total = hit.DelayedDamage, Start = hit.Time, End = hit.Time + WeaponCombatRules.BleedDuration });
                if (!hit.ActualDamage || !Alive(hit.Attacker) || !Alive(hit.Victim) || !Body(hit.Collision)) continue;
                ProcessHit(hit);
            }
            while (true)
            {
                Tuple<Agent, WeaponClass> hit;
                lock (_gate) { if (_shieldHits.Count == 0) break; hit = _shieldHits.Dequeue(); }
                ShieldHit(hit.Item1, hit.Item2);
            }
        }
        private void ProcessHit(Hit hit)
        {
            var usage = hit.Weapon.CurrentUsageItem;
            if (usage == null || !hit.Attacker.IsEnemyOf(hit.Victim)) return;
            var state = Victim(hit.Victim);
            var part = hit.Collision.VictimHitBodyPart;
            bool axe = usage.WeaponClass == WeaponClass.TwoHandedAxe || WeaponCombatRules.Spear(usage) && hit.Blow.StrikeType == StrikeType.Swing;
            float armor = hit.Victim.GetBaseArmorEffectivenessForBodyPart(part);
            // AbsorbedByArmor由原生管线计算，恢复本击护甲前伤害用于破甲门槛，不拿面板冒充命中伤害。
            float preArmor = hit.PreArmorDamage > 0f ? hit.PreArmorDamage : hit.FullDamage + hit.Collision.AbsorbedByArmor;
            if (axe && !hit.Collision.IsMissile && preArmor > armor)
            {
                if (!state.Armor.TryGetValue(part, out var loss)) state.Armor[part] = loss = new ArmorPart();
                if (loss.Layers < 3) { loss.Layers++; loss.Amount += WeaponCombatRules.BreakAmount(hit.Weapon); }
            }
            // 坐骑没有人类装备容器及手部骨骼，不参与缴械和脚部减速。
            if (CanUseWeapons(hit.Victim) && hit.Victim.AgentVisuals != null && hit.FullDamage > 30 &&
                WeaponCombatRules.Hammer(usage) && MBRandom.RandomFloat < WeaponCombatRules.ProcChance(hit.Attacker, hit.Victim, usage))
            {
                sbyte bone = hit.Collision.CollisionBoneIndex;
                var mainSide = hit.Victim.AgentVisuals.GetBoneTypeData(hit.Victim.Monster.MainHandBoneIndex).BodyPartType;
                var offSide = hit.Victim.AgentVisuals.GetBoneTypeData(hit.Victim.Monster.OffHandBoneIndex).BodyPartType;
                if (bone == hit.Victim.Monster.MainHandBoneIndex || bone == hit.Victim.Monster.MainHandItemBoneIndex ||
                    part == mainSide && (part == BoneBodyPartType.ArmLeft || part == BoneBodyPartType.ArmRight))
                    DeferredDisarmExecutor.Mark(hit.Victim, hit.Victim.GetPrimaryWieldedItemIndex());
                else if (bone == hit.Victim.Monster.OffHandBoneIndex || bone == hit.Victim.Monster.OffHandItemBoneIndex ||
                    part == offSide && (part == BoneBodyPartType.ArmLeft || part == BoneBodyPartType.ArmRight))
                    DeferredDisarmExecutor.Mark(hit.Victim, hit.Victim.GetOffhandWieldedItemIndex());
                if (hit.Blow.DamageType == DamageTypes.Blunt && IsFoot(hit.Victim, bone))
                { state.SlowUntil = Mission.CurrentTime + WeaponCombatRules.FootSlowDuration; hit.Victim.UpdateAgentProperties(); }
            }
            if (hit.SpearSplit && !hit.Synthetic && armor > hit.Weapon.GetModifiedThrustDamageForCurrentUsage()) StartPush(hit);
        }
        private static bool IsFoot(Agent agent, sbyte bone) => bone >= 0 &&
            (bone == agent.Monster.PrimaryFootBoneIndex || bone == agent.Monster.SecondaryFootBoneIndex ||
             bone == agent.Monster.LeftFootIkEndEffectorBoneIndex || bone == agent.Monster.RightFootIkEndEffectorBoneIndex ||
             bone == agent.Monster.LeftFootIkTipBoneIndex || bone == agent.Monster.RightFootIkTipBoneIndex);

        internal void HalveRemainingBleedDuration(Agent victim)
        {
            float now = Mission.CurrentTime;
            foreach (Bleed record in _bleeds)
                if (record.Hit.Victim == victim && record.End > now)
                    record.End = now + (record.End - now) * 0.5f;
        }

        private void TickBleeds()
        {
            for (int i = _bleeds.Count - 1; i >= 0; i--)
            {
                var record = _bleeds[i];
                if (!Alive(record.Hit.Victim)) { _bleeds.RemoveAt(i); continue; }
                float elapsed = Math.Max(0f, Math.Min(Mission.CurrentTime, record.End) - record.Start);
                int due = (int)Math.Floor(record.Total * Math.Min(1f, elapsed / WeaponCombatRules.BleedDuration));
                int damage = due - record.Delivered;
                if (damage > 0)
                {
                    var blow = record.Hit.Blow;
                    blow.InflictedDamage = damage; blow.BaseMagnitude = 0f;
                    blow.AttackerStunPeriod = blow.DefenderStunPeriod = 0f;
                    blow.BlowFlag = BlowFlags.NoSound | BlowFlags.ShrugOff;
                    blow.DamageCalculated = true;
                    blow.GlobalPosition = record.Hit.Victim.Position;
                    if (Mission.FindAgentWithIndex(blow.OwnerId) != record.Hit.Attacker) blow.OwnerId = -1;
                    var collision = record.Hit.Collision;
                    collision.InflictedDamage = damage; collision.AbsorbedByArmor = 0;
                    float healthBefore = record.Hit.Victim.Health;
                    DeliveringBleed = true;
                    try { record.Hit.Victim.RegisterBlow(blow, collision); }
                    finally { DeliveringBleed = false; }
                    record.Delivered = due;
                    if (record.Hit.Victim.Health < healthBefore) Victim(record.Hit.Victim).LastDamage = Mission.CurrentTime;
                }
                if (Mission.CurrentTime >= record.End) _bleeds.RemoveAt(i);
            }
        }
        private void TickVictims()
        {
            _scratch.Clear();
            foreach (var pair in _victims)
            {
                if (!Alive(pair.Key)) { _scratch.Add(pair.Key); continue; }
                if (Mission.CurrentTime - pair.Value.LastDamage >= WeaponCombatRules.ArmorResetDelay) pair.Value.Armor.Clear();
                if (pair.Value.SlowUntil > 0f && pair.Value.SlowUntil <= Mission.CurrentTime)
                { pair.Value.SlowUntil = 0f; pair.Key.UpdateAgentProperties(); }
            }
            foreach (var agent in _scratch) _victims.Remove(agent);
        }

        internal void ShieldHit(Agent victim, WeaponClass weaponClass)
        {
            // 仅投掷斧/标枪；盾伤回调没有安全的装备变更，不做丢盾。
            if (!CanUseWeapons(victim) || weaponClass != WeaponClass.ThrowingAxe && weaponClass != WeaponClass.Javelin) return;
            var slot = victim.GetOffhandWieldedItemIndex();
            if (slot == EquipmentIndex.None) return;
            var shield = victim.Equipment[slot];
            if (shield.CurrentUsageItem?.IsShield != true) return;
            if (!_shields.TryGetValue(victim, out var slots)) _shields[victim] = slots = new Dictionary<EquipmentIndex, ShieldState>();
            if (!slots.TryGetValue(slot, out var state) || state.Item != shield.Item || state.Modifier != shield.ItemModifier)
                slots[slot] = state = new ShieldState { Item = shield.Item, Modifier = shield.ItemModifier };
            state.Stacks += 1f;
        }
        private void TickShields(float dt)
        {
            _scratch.Clear();
            foreach (var pair in _shields)
            {
                if (!CanUseWeapons(pair.Key)) { _scratch.Add(pair.Key); continue; }
                _slotScratch.Clear();
                foreach (var slot in pair.Value)
                {
                    var equipment = pair.Key.Equipment[slot.Key];
                    if (equipment.Item != slot.Value.Item || equipment.ItemModifier != slot.Value.Modifier || equipment.CurrentUsageItem?.IsShield != true)
                    { _slotScratch.Add(slot.Key); continue; }
                    // dN/dt=-1/(1+0.1N)，解析步进避免帧率造成恢复速度差异。
                    float debt = slot.Value.Stacks + 0.05f * slot.Value.Stacks * slot.Value.Stacks;
                    slot.Value.Stacks = 10f * ((float)Math.Sqrt(1f + 0.2f * Math.Max(0f, debt - dt)) - 1f);
                    if (slot.Value.Stacks <= 0f) _slotScratch.Add(slot.Key);
                }
                foreach (var slot in _slotScratch) pair.Value.Remove(slot);
            }
            foreach (var agent in _scratch) _shields.Remove(agent);
        }
        private float ShieldMultiplier(Agent agent)
        {
            var slot = agent.GetOffhandWieldedItemIndex();
            return slot != EquipmentIndex.None && _shields.TryGetValue(agent, out var slots) && slots.TryGetValue(slot, out var shield)
                ? WeaponCombatRules.ShieldSpeed(shield.Stacks) : 1f;
        }

        private void StartPush(Hit hit)
        {
            // 命中回调已保存姿态；不再等到下一帧才要求受击瞬间的动作仍然存在。
            if (hit.Synthetic || hit.ThrustPose == null || Mission.CurrentTime >= hit.Time + WeaponCombatRules.PushMaximumDuration ||
                !CanUseWeapons(hit.Attacker) || !Alive(hit.Victim) ||
                !hit.Victim.IsHuman || hit.Victim.MountAgent != null || !WeaponCombatThrustGeometry.SameWeapon(hit) ||
                _pushes.ContainsKey(hit.Attacker) || _pushOwners.ContainsKey(hit.Victim)) return;
            Vec3 direction = hit.ThrustPose.VictimAtHit - hit.ThrustPose.AttackerAtHit;
            direction.z = 0f;
            if (direction.LengthSquared < 0.001f) return;
            direction.Normalize();
            float radius = Math.Max(0.15f, hit.Victim.CollisionCapsule.Radius);
            var push = new Push { Hit = hit, Direction = direction, Origin = hit.ThrustPose.VictimAtHit,
                Radius = radius, StartTime = Mission.CurrentTime, Deadline = Mission.CurrentTime + WeaponCombatRules.PushMaximumDuration,
                LastHit = hit.Time, Attempts = hit.Attacker.IsAIControlled ? WeaponCombatRules.Skill(hit.Attacker, hit.Weapon.CurrentUsageItem) / 50 : 0 };
            // 命中以后再观察枪尖，无法得到已经过去或被碰撞中断的动作峰值。
            // 从当前动作的实际动画片段求出整段释放的最远位置，启动时就固定目标终点。
            if (!WeaponCombatThrustGeometry.Maximum(hit, direction, out Vec3 maximumTip, out float peakProgress)) return;
            push.PeakProgress = peakProgress;
            TrackTip(push, maximumTip);
            if (push.Distance <= 0.01f) return;
            _pushes.Add(hit.Attacker, push);
            _pushOwners.Add(hit.Victim, hit.Attacker);
        }
        private static void TrackTip(Push push, Vec3 tip)
        {
            // 沿固定命中方向向外推，保留目标当前侧向位置；收枪、转身和攻击者后退均不会把目标拉回来。
            float distance = Vec3.DotProduct(tip - push.Origin, push.Direction) + push.Radius;
            push.Distance = Math.Max(push.Distance, Math.Max(0f, distance));
        }
        private void TickPushes(float dt)
        {
            _scratch.Clear();
            foreach (var pair in _pushes)
            {
                var push = pair.Value;
                Agent attacker = pair.Key, victim = push.Hit.Victim;
                if (!CanUseWeapons(attacker) || !Alive(victim) || victim.MountAgent != null ||
                    !WeaponCombatThrustGeometry.SameWeapon(push.Hit))
                { _scratch.Add(attacker); continue; }
                bool releasing = !push.ReleaseEnded && WeaponCombatThrustGeometry.InRelease(push.Hit);
                if (!releasing) push.ReleaseEnded = true;
                // 优先跟随攻击进度；碰撞让动作停止或直接转入后摇时，也要完成本次已触发的短推。
                // 终点始终是命中时求出的动作峰值，不因攻击者走动而不断延伸。
                float motion = Math.Min(1f, Math.Max(0f, (Mission.CurrentTime - push.StartTime) / WeaponCombatRules.PushTravelDuration));
                if (releasing && push.PeakProgress > push.Hit.ThrustPose.Progress + 0.001f)
                    motion = Math.Max(motion, Math.Min(1f, Math.Max(0f,
                        (attacker.GetCurrentActionProgress(1) - push.Hit.ThrustPose.Progress) /
                        (push.PeakProgress - push.Hit.ThrustPose.Progress))));
                Vec3 destination = push.Origin + push.Direction * (push.Distance * motion);
                victim.MovementInputVector = Vec2.Zero;
                if (!SafePush(push, destination, dt)) { _scratch.Add(attacker); continue; }
                if (!releasing && Vec3.DotProduct(victim.Position - push.Origin, push.Direction) >= push.Distance - 0.01f)
                { _scratch.Add(attacker); continue; }
                // 先完成本帧的位移，再处理截止时间，避免最后一帧没有到达已记录的枪尖位置。
                if (Mission.CurrentTime >= push.Deadline) { _scratch.Add(attacker); continue; }
                int skill = WeaponCombatRules.Skill(attacker, push.Hit.Weapon.CurrentUsageItem);
                if (releasing && !push.ReleaseEnded && attacker.IsAIControlled && push.Attempts > 0 &&
                    Mission.CurrentTime - push.LastHit >= WeaponCombatRules.RepeatInterval(skill))
                {
                    push.Attempts--; push.LastHit = Mission.CurrentTime;
                    if (MBRandom.RandomFloat < 0.5f) WeaponCombatNativeHit.Repeat(push.Hit);
                }
            }
            foreach (var agent in _scratch)
            {
                _pushOwners.Remove(_pushes[agent].Hit.Victim);
                _pushes.Remove(agent);
            }
        }
        private bool SafePush(Push push, Vec3 destination, float dt)
        {
            Agent victim = push.Hit.Victim;
            float distance = Vec3.DotProduct(destination - victim.Position, push.Direction);
            if (distance <= 0.01f) return true;
            // 真实位移，每段最多12厘米；大帧间隔也逐段检测地面、墙和其他人物。
            float speed = Math.Max(12f, push.Distance / WeaponCombatRules.PushTravelDuration);
            float travel = Math.Min(distance, speed * Math.Min(dt, 0.1f));
            // 每次短推只扫描一次人物列表；按各自实际胶囊半径筛选，兼容大型单位。
            _pushObstacles.Clear();
            foreach (var other in Mission.Agents)
            {
                if (other == victim || !other.IsActive()) continue;
                float radius = travel + push.Radius + Math.Max(0.15f, other.CollisionCapsule.Radius);
                if ((other.Position - victim.Position).AsVec2.LengthSquared <= radius * radius)
                    _pushObstacles.Add(other);
            }
            while (travel > 0.001f)
            {
                float step = Math.Min(travel, 0.12f);
                Vec3 candidate = victim.Position + push.Direction * step;
                Vec3 top = candidate + Vec3.Up * 1.3f, bottom = candidate - Vec3.Up * 1.3f;
                bool support = Mission.Scene.RayCastForClosestEntityOrTerrain(top, bottom, out _, out Vec3 ground,
                    0.01f, BodyFlags.CommonCollisionExcludeFlags);
                candidate.z = support ? ground.z : Mission.Scene.GetGroundHeightAtPosition(candidate, BodyFlags.CommonCollisionExcludeFlags);
                if (!WeaponCombatThrustGeometry.Finite(candidate) || Math.Abs(candidate.z - victim.Position.z) > 0.35f) return false;
                bool navigable = Mission.Scene.GetNavigationMeshForPosition(candidate, out _, 0.5f, false) != UIntPtr.Zero;
                // 真实支撑面上的短推不因缺一小块导航网格立即失效，仍限制离网距离和落差。
                if (!navigable && (!support || push.OffNavigationDistance + step > 0.75f)) return false;
                Vec3 from = victim.Position + Vec3.Up * 0.8f, to = candidate + Vec3.Up * 0.8f;
                if (Mission.Scene.RayCastForClosestEntityOrTerrain(from, to, out float wallDistance,
                    Math.Min(0.2f, push.Radius * 0.8f), BodyFlags.CommonCollisionExcludeFlags) &&
                    wallDistance + 0.01f < (to - from).Length) return false;
                foreach (var other in _pushObstacles)
                {
                    if (other == victim || !other.IsActive() || Math.Abs(other.Position.z - candidate.z) > 1f) continue;
                    float before = (other.Position - victim.Position).AsVec2.LengthSquared;
                    float after = (other.Position - candidate).AsVec2.LengthSquared;
                    float clearance = push.Radius + Math.Max(0.15f, other.CollisionCapsule.Radius);
                    if (after < clearance * clearance && after + 0.0001f < before) return false;
                }
                victim.TeleportToPosition(candidate);
                push.OffNavigationDistance = navigable ? 0f : push.OffNavigationDistance + step;
                travel -= step;
            }
            return true;
        }
        private void TickAttacks()
        {
            _scratch.Clear();
            foreach (var pair in _attacks)
            {
                if (!CanUseWeapons(pair.Key)) { _scratch.Add(pair.Key); continue; }
                if (pair.Value.KnifePending)
                {
                    pair.Value.KnifePending = false;
                    if (pair.Key.GetCurrentActionStage(1) == Agent.ActionStage.AttackReady ||
                        pair.Key.GetCurrentActionStage(1) == Agent.ActionStage.AttackQuickReady) ThrowKnife(pair.Key);
                    pair.Value.KnifeReleaseAt = Mission.CurrentTime + WeaponCombatRules.KnifeLeadTime;
                }
                if (pair.Value.PendingDirection != Agent.MovementControlFlag.None)
                {
                    var direction = pair.Value.PendingDirection; pair.Value.PendingDirection = Agent.MovementControlFlag.None;
                    if (pair.Key.GetCurrentActionStage(1) == Agent.ActionStage.AttackRelease) Rearm(pair.Key, direction);
                }
                ObserveAction(pair.Key, pair.Value);
            }
            foreach (var agent in _scratch) _attacks.Remove(agent);
        }
        private void ObserveAction(Agent agent, AttackState state)
        {
            if (!CanUseWeapons(agent) || state == null) return;
            var stage = agent.GetCurrentActionStage(1);
            var action = agent.GetCurrentAction(1);
            float progress = agent.GetCurrentActionProgress(1);
            bool changed = action != state.Action || stage != state.Stage || progress + 0.1f < state.Progress;
            if (changed)
            {
                // 新攻击的速度由原生初始化；只恢复仍处于同一个动作的本系统写入。
                state.SpeedChanged = false;
                if ((stage == Agent.ActionStage.AttackReady || stage == Agent.ActionStage.AttackQuickReady) &&
                    state.Stage != Agent.ActionStage.AttackReady && state.Stage != Agent.ActionStage.AttackQuickReady) state.KnifeUsed = false;
                if (stage == Agent.ActionStage.AttackRelease) state.Contact = false;
            }
            var usage = HeldWeapon(agent).CurrentUsageItem;
            if (stage == Agent.ActionStage.AttackRelease && !state.Contact && progress >= WeaponCombatRules.ReleaseTailStart &&
                WeaponCombatRules.HeavyMiss(agent, usage) && !state.SpeedChanged &&
                JiFengLianZhanMissionLogic.Current?.IsActive(agent) != true)
            {
                agent.SetCurrentActionSpeed(1, WeaponCombatRules.MissSpeed(WeaponCombatRules.Skill(agent, usage)));
                state.SpeedChanged = true;
            }
            // 原生防御起手动作与持续举盾有不同ActionCode；仅减慢起手，不延迟AI的举盾决定。
            if (stage == Agent.ActionStage.Defend && changed && action.GetName().IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0 &&
                action.GetName().IndexOf("active", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                float multiplier = ShieldMultiplier(agent);
                if (multiplier < 1f) { agent.SetCurrentActionSpeed(1, multiplier); state.SpeedChanged = true; }
            }
            if (state.KnifeReleaseAt > 0f && Mission.CurrentTime >= state.KnifeReleaseAt) state.KnifeReleaseAt = 0f;
            state.Stage = stage; state.Action = action; state.Progress = progress;
        }
        internal void PlayerControl()
        {
            var agent = Mission.MainAgent;
            var screen = ScreenManager.TopScreen as MissionScreen;
            if (!CanUseWeapons(agent) || screen?.SceneLayer == null || Mission.Mode != MissionMode.Battle) return;
            var input = screen.SceneLayer.Input;
            if (_pushOwners.ContainsKey(agent)) agent.MovementInputVector = Vec2.Zero;
            var state = Attack(agent);
            var stage = agent.GetCurrentActionStage(1);
            var usage = HeldWeapon(agent).CurrentUsageItem;
            if (input.IsGameKeyPressed(9) && _pushes.TryGetValue(agent, out var push))
            {
                if (!push.ReleaseEnded && Mission.CurrentTime < push.Deadline && WeaponCombatThrustGeometry.InRelease(push.Hit) &&
                    Mission.CurrentTime - push.LastHit >= WeaponCombatRules.RepeatInterval(WeaponCombatRules.Skill(agent, usage)))
                { push.LastHit = Mission.CurrentTime; WeaponCombatNativeHit.Repeat(push.Hit); }
                return;
            }
            bool lockedTail = stage == Agent.ActionStage.AttackRelease && agent.GetCurrentActionProgress(1) >= WeaponCombatRules.ReleaseTailStart &&
                !state.Contact && WeaponCombatRules.HeavyMiss(agent, usage);
            if (lockedTail)
            {
                agent.MovementFlags &= ~(Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask | Agent.MovementControlFlag.DefendBlock);
                return;
            }
            bool cancelling = input.IsGameKeyPressed(10) &&
                (stage == Agent.ActionStage.AttackReady || stage == Agent.ActionStage.AttackQuickReady ||
                 stage == Agent.ActionStage.AttackRelease && agent.GetCurrentActionProgress(1) >= WeaponCombatRules.ReleaseTailStart && WeaponCombatRules.CanFeint(usage));
            if (cancelling && !state.KnifeUsed) { state.KnifeUsed = ThrowKnife(agent); }
            if (stage == Agent.ActionStage.AttackRelease && agent.GetCurrentActionProgress(1) >= WeaponCombatRules.ReleaseTailStart &&
                WeaponCombatRules.CanFeint(usage) && input.IsGameKeyPressed(9) && JiFengLianZhanMissionLogic.Current?.IsActive(agent) != true)
                Rearm(agent, agent.AttackDirectionToMovementFlag(agent.GetAttackDirection()));
        }
        private void Rearm(Agent agent, Agent.MovementControlFlag direction)
        {
            if (direction == Agent.MovementControlFlag.None) return;
            var state = Attack(agent);
            if (agent.IsMainAgent && !state.KnifeUsed) state.KnifeUsed = ThrowKnife(agent);
            if (agent.SetActionChannel(1, ActionIndexCache.act_none, true, (AnimFlags)0UL, 0f, 1f, 0f, 0f))
                agent.MovementFlags = (agent.MovementFlags & ~Agent.MovementControlFlag.AttackMask) | direction;
        }
        internal void AiInput(Agent agent, ref Agent.MovementControlFlag flags)
        {
            if (!CanUseWeapons(agent) || Mission.Mode != MissionMode.Battle || JiFengLianZhanMissionLogic.Current?.IsActive(agent) == true) return;
            var state = Attack(agent);
            var stage = agent.GetCurrentActionStage(1);
            var usage = HeldWeapon(agent).CurrentUsageItem;
            if ((stage == Agent.ActionStage.AttackReady || stage == Agent.ActionStage.AttackQuickReady) &&
                state.Stage != Agent.ActionStage.AttackReady && state.Stage != Agent.ActionStage.AttackQuickReady)
            {
                state.KnifeUsed = false;
                state.Stage = stage; state.Action = agent.GetCurrentAction(1); state.Progress = agent.GetCurrentActionProgress(1);
            }
            if (stage == Agent.ActionStage.AttackRelease && agent.GetCurrentActionProgress(1) >= WeaponCombatRules.ReleaseTailStart)
            {
                if (!state.Contact && WeaponCombatRules.HeavyMiss(agent, usage))
                    flags &= ~(Agent.MovementControlFlag.AttackMask | Agent.MovementControlFlag.DefendMask | Agent.MovementControlFlag.DefendBlock);
                else if (WeaponCombatRules.CanFeint(usage) && (flags & Agent.MovementControlFlag.AttackMask) != 0)
                { state.PendingDirection = flags & Agent.MovementControlFlag.AttackMask; }
            }
            // AI准备放开攻击输入时先保留原方向，任务Tick投刀，80ms后允许主武器自然释放。
            if ((stage == Agent.ActionStage.AttackReady || stage == Agent.ActionStage.AttackQuickReady) &&
                usage?.IsMeleeWeapon == true && (flags & Agent.MovementControlFlag.AttackMask) == 0 &&
                (flags & Agent.MovementControlFlag.DefendMask) == 0 && !state.KnifeUsed && HasKnife(agent))
            {
                state.KnifeReleaseAt = float.PositiveInfinity;
                state.KnifeUsed = true;
                state.KnifePending = true;
            }
            if (state.KnifeReleaseAt > Mission.CurrentTime) flags |= agent.AttackDirectionToMovementFlag(agent.GetCurrentActionDirection(1));
        }
        internal bool BeingPushed(Agent agent) => _pushOwners.ContainsKey(agent);
        private static bool HasKnife(Agent agent)
        {
            if (!CanUseWeapons(agent)) return false;
            for (int i = 0; i < 4; i++)
            {
                var weapon = agent.Equipment[(EquipmentIndex)i];
                if (!weapon.IsEmpty && weapon.Amount > 0 && weapon.Item.PrimaryWeapon?.WeaponClass == WeaponClass.ThrowingKnife) return true;
            }
            return false;
        }
        private bool ThrowKnife(Agent agent)
        {
            if (!CanUseWeapons(agent)) return false;
            for (int i = 0; i < 4; i++)
            {
                var slot = (EquipmentIndex)i;
                var weapon = agent.Equipment[slot];
                if (weapon.IsEmpty || weapon.Amount <= 0 || weapon.Item.PrimaryWeapon?.WeaponClass != WeaponClass.ThrowingKnife) continue;
                weapon.CurrentUsageIndex = 0;
                for (int u = 0; u < weapon.WeaponsCount; u++)
                    if (weapon.GetWeaponStatsDataForUsage(u).WeaponClass == (int)WeaponClass.ThrowingKnife) { weapon.CurrentUsageIndex = u; break; }
                if (weapon.CurrentUsageItem?.IsRangedWeapon != true) continue;
                Vec3 start = agent.Frame.TransformToParent(agent.GetBoneEntitialFrame(agent.Monster.MainHandItemBoneIndex, true).origin);
                Vec3 direction = agent.LookDirection;
                Agent target = agent.IsAIControlled ? agent.GetTargetAgent() : null;
                if (Alive(target)) direction = target.Position + Vec3.Up * 1.2f - start;
                direction.Normalize();
                float speed = Math.Max(1f, weapon.GetModifiedMissileSpeedForCurrentUsage());
                short amount = weapon.Amount;
                weapon.Amount = 1;
                Mission.AddCustomMissile(agent, weapon, start, direction, agent.LookRotation, speed, speed, true, null);
                agent.SetWeaponAmountInSlot(slot, (short)(amount - 1), true);
                return true;
            }
            return false;
        }
        private void Cleanup()
        {
            foreach (var pair in _attacks)
                if (Alive(pair.Key) && pair.Value.SpeedChanged && pair.Key.GetCurrentAction(1) == pair.Value.Action) pair.Key.SetCurrentActionSpeed(1, 1f);
            lock (_gate) { _hits.Clear(); _contacts.Clear(); _blocks.Clear(); _shieldHits.Clear(); }
            _attacks.Clear(); _victims.Clear(); _pushes.Clear(); _pushOwners.Clear(); _bleeds.Clear(); _shields.Clear();
            _pushObstacles.Clear();
            WeaponCombatHitContext.Clear();
            if (Current == this) Current = null;
        }
        protected override void OnEndMission() { Cleanup(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Cleanup(); base.OnRemoveBehavior(); }
    }

    internal sealed class WeaponCombatAgentComponent : AgentComponent
    {
        internal WeaponCombatAgentComponent(Agent agent) : base(agent) { }
        public override void OnAIInputSet(ref Agent.EventControlFlag eventFlag, ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector)
        {
            var logic = WeaponCombatMissionLogic.Current;
            if (logic == null) return;
            logic.AiInput(Agent, ref movementFlag);
            if (logic.BeingPushed(Agent)) inputVector = Vec2.Zero;
        }
    }
}
