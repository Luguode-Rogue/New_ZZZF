using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    /// <summary>弧形波的扫掠碰撞；不依赖帧率，不注册虚构原生飞弹。</summary>
    internal sealed class ArcWaveMissionLogic : MissionLogic
    {
        private sealed class Hits { internal float Distance; internal int Count; }
        private sealed class Wave
        {
            internal ArcWeaponNativeHit.Snapshot Cast;
            internal Vec3 Origin, Direction, Right, Up;
            internal Mat3 Rotation;
            internal GameEntity Visual;
            internal bool Expanding;
            internal float Radius, Speed, Range, Distance;
            internal readonly Dictionary<Agent, Hits> Hits = new Dictionary<Agent, Hits>();
            internal readonly HashSet<Agent> Struck = new HashSet<Agent>();
        }
        private sealed class AttackObserver
        {
            internal Agent Agent;
            internal bool WasRelease;
            internal float Progress;
            internal ActionIndexCache Action;
        }
        private readonly List<Wave> _waves = new List<Wave>();
        private readonly Dictionary<Agent, AttackObserver> _observers = new Dictionary<Agent, AttackObserver>();
        private readonly List<AttackObserver> _observerTick = new List<AttackObserver>();
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        internal static ArcWaveMissionLogic Current => Mission.Current?.GetMissionBehavior<ArcWaveMissionLogic>();

        internal bool TrySpawn(ArcWeaponNativeHit.Snapshot cast, Vec3 origin, Vec3 direction,
            bool expanding, float radius, float speed, float range, out string reason)
        {
            reason = null;
            if (cast == null || direction.LengthSquared < 0.001f || Mission?.Scene == null) {
                reason = "剑气方向或任务无效。"; return false;
            }
            direction.Normalize();
            Mat3 rotation = Mat3.CreateMat3WithForward(direction);
            GameEntity visual = ArcWaveVisual.Create(cast.Caster, expanding);
            if (visual == null) { reason = "无法生成弧形剑气。"; return false; }
            Wave wave = new Wave { Cast = cast, Origin = origin, Direction = direction,
                Rotation = rotation, Right = rotation.s, Up = rotation.u, Visual = visual,
                Expanding = expanding, Radius = radius, Speed = speed, Range = range };
            ArcWaveVisual.Update(visual, origin, rotation, expanding ? 0.05f : radius);
            _waves.Add(wave);
            return true;
        }
        internal void Observe(Agent agent)
        {
            if (agent != null && !_observers.ContainsKey(agent))
                _observers.Add(agent, new AttackObserver { Agent = agent });
        }
        internal void StopObserving(Agent agent) { if (agent != null) _observers.Remove(agent); }

        public override void OnMissionTick(float dt)
        {
            if (dt <= 0f) return;
            _observerTick.Clear();
            _observerTick.AddRange(_observers.Values);
            foreach (AttackObserver observer in _observerTick) {
                Agent agent = observer.Agent;
                if (!agent.IsActive() || agent.Equipment == null ||
                    Script.GetActiveComponents(agent)?.StateContainer.HasState("ZhenYinZhanBuff") != true) {
                    _observers.Remove(agent); continue;
                }
                bool release = agent.GetCurrentActionType(1) == Agent.ActionCodeType.ReleaseMelee;
                float progress = agent.GetCurrentActionProgress(1);
                ActionIndexCache action = agent.GetCurrentAction(1);
                if (release && (!observer.WasRelease || action != observer.Action || progress + 0.05f < observer.Progress) &&
                    ArcWeaponNativeHit.TryCapture(agent, out var cast)) {
                    if (cast.Strike == TaleWorlds.Core.StrikeType.Swing)
                        cast.AttackDirection = agent.GetCurrentActionDirection(1);
                    // 每次实际挥击时取镜头方向，保留俯仰；视觉与扫掠碰撞共用这个方向。
                    Vec3 forward = agent.IsMainAgent ? agent.LookDirection : agent.LookDirection.AsVec2.ToVec3();
                    if (agent.IsMainAgent && ScreenManager.TopScreen is MissionScreen screen) {
                        Vec3 cameraDirection = screen.CombatCamera.Direction;
                        if (cameraDirection.LengthSquared >= 0.001f) forward = cameraDirection;
                    }
                    TrySpawn(cast, Chest(agent), forward, true, 1f, 30f, 10f, out _);
                }
                observer.WasRelease = release; observer.Progress = progress; observer.Action = action;
            }
            // 反向遍历，命中事件生成的新波留到下次 Tick；避免修改枚举集合。
            for (int i = _waves.Count - 1; i >= 0; i--) {
                Wave wave = _waves[i];
                if (!wave.Cast.Caster.IsActive()) { RemoveAt(i); continue; }
                float previous = wave.Distance;
                float next = Math.Min(wave.Range, previous + wave.Speed * dt);
                bool wall = false;
                if (!wave.Expanding) {
                    Vec3 from = wave.Origin + wave.Direction * previous;
                    Vec3 to = wave.Origin + wave.Direction * next;
                    wall = Mission.Scene.RayCastForClosestEntityOrTerrain(from, to, out float distance,
                        0.05f, BodyFlags.CommonCollisionExcludeFlags);
                    if (wall) next = previous + Math.Max(0f, Math.Min(next - previous, distance));
                }
                Sweep(wave, previous, next);
                wave.Distance = next;
                ArcWaveVisual.Update(wave.Visual, wave.Expanding ? wave.Origin : wave.Origin + wave.Direction * next,
                    wave.Rotation, wave.Expanding ? Math.Max(0.05f, next) : wave.Radius);
                if (wall || next >= wave.Range) RemoveAt(i);
            }
        }
        private void Sweep(Wave wave, float previous, float next)
        {
            if (next <= previous) return;
            Agent caster = wave.Cast.Caster;
            float middle = (previous + next) * 0.5f;
            Vec3 query = wave.Expanding ? wave.Origin : wave.Origin + wave.Direction * (middle - wave.Radius);
            float search = wave.Expanding ? next + 1f : (next - previous) * 0.5f + wave.Radius * 2f + 1f;
            _nearby.Clear();
            if (caster.Team != null) Mission.GetNearbyEnemyAgents(query.AsVec2, search, caster.Team, _nearby);
            else Mission.GetNearbyAgents(query.AsVec2, search, _nearby);
            foreach (Agent target in _nearby) {
                if (target == null || !target.IsActive() || !target.IsHuman || target.Health <= 0f ||
                    !caster.IsEnemyOf(target) || SkillTargetProtection.IsProtected(target)) continue;
                Vec3 position = Chest(target);
                Vec3 relative = position - wave.Origin;
                if (Math.Abs(Vec3.DotProduct(relative, wave.Up)) > 1.1f) continue;
                float x = Vec3.DotProduct(relative, wave.Right);
                float y = Vec3.DotProduct(relative, wave.Direction);
                if (wave.Expanding) {
                    float radius = (float)Math.Sqrt(x * x + y * y);
                    if (y < -0.35f || radius + 0.35f < previous || radius - 0.35f > next || wave.Struck.Contains(target)) continue;
                    if (!ClearPath(wave.Origin, position)) continue;
                    wave.Struck.Add(target);
                    Vec3 direction = position - wave.Origin;
                    if (direction.LengthSquared < 0.001f) direction = wave.Direction;
                    ArcWeaponNativeHit.Register(wave.Cast, target, position, direction, 2f, false);
                } else {
                    if (Math.Abs(x) > wave.Radius + 0.35f) continue;
                    float side = Math.Min(wave.Radius, Math.Abs(x));
                    float offset = wave.Radius - (float)Math.Sqrt(wave.Radius * wave.Radius - side * side);
                    float entry = y + offset - 0.35f;
                    float exit = y + offset + wave.Radius * 2f + 0.35f;
                    float overlap = Math.Min(next, exit) - Math.Max(previous, entry);
                    if (overlap <= 0f || !ClearPath(wave.Origin, position)) continue;
                    if (!wave.Hits.TryGetValue(target, out Hits hits)) {
                        hits = new Hits(); wave.Hits.Add(target, hits);
                    }
                    hits.Distance += overlap;
                    // 每穿过一米再命中一次；不设置单目标硬上限。
                    int allowed = 1 + (int)(hits.Distance + 0.0001f);
                    while (hits.Count < allowed && target.IsActive() && target.Health > 0f) {
                        hits.Count++;
                        ArcWeaponNativeHit.Register(wave.Cast, target, position, wave.Direction, 5f, true);
                    }
                }
            }
        }
        private bool ClearPath(Vec3 from, Vec3 to)
        {
            float length = (to - from).Length;
            return length < 0.05f || !Mission.Scene.RayCastForClosestEntityOrTerrain(from, to,
                out float hit, 0.02f, BodyFlags.CommonCollisionExcludeFlags) || hit >= length - 0.15f;
        }
        private static Vec3 Chest(Agent agent) => agent.GetEyeGlobalPosition() - Vec3.Up * 0.45f;
        private void RemoveAt(int index) { DualLayerParticleVisual.Remove(_waves[index].Visual); _waves.RemoveAt(index); }
        private void Clear() {
            for (int i = _waves.Count - 1; i >= 0; i--) RemoveAt(i);
            _observers.Clear(); _observerTick.Clear(); _nearby.Clear();
        }
        protected override void OnEndMission() { Clear(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Clear(); base.OnRemoveBehavior(); }
    }
}
