using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HarmonyLib;
using New_ZZZF.Systems;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal sealed class TianFaZhiJianMissionLogic : MissionLogic
    {
        private sealed class Cast
        {
            internal Agent Caster;
            internal Vec3 Landing;
            internal float Power, Started;
            internal TianFaVisual Visual;
        }
        private static TianFaZhiJianMissionLogic _current;
        internal static TianFaZhiJianMissionLogic Current => _current;
        // 原版并行受击与 Health 写入可查询；记录只由任务主线程创建和清理。
        private readonly ConcurrentDictionary<Agent, Cast> _leaps = new ConcurrentDictionary<Agent, Cast>();
        private readonly List<TianFaVisual> _bursts = new List<TianFaVisual>();
        private readonly MBList<Agent> _victims = new MBList<Agent>();
        private bool _closing;
        public override void OnBehaviorInitialize() { base.OnBehaviorInitialize(); _current = this; }
        public override void OnCreated() { base.OnCreated(); _current = this; }
        internal static bool IsLeaping(Agent agent) => agent != null && _current != null && !_current._closing && _current._leaps.ContainsKey(agent);

        internal bool Begin(Agent caster, Vec3 desired, out string reason)
        {
            reason = null;
            var movement = RushMovementMissionLogic.Current;
            if (_closing || movement == null || movement.IsRushing(caster) || _leaps.ContainsKey(caster)) {
                reason = "当前无法开始天罚之剑。"; return false;
            }
            if (!movement.TryGetSafeLandingPosition(desired, out Vec3 landing) ||
                !caster.IsPlayerControlled &&
                ((desired - caster.Position).LengthSquared > TianFaZhiJian.Range * TianFaZhiJian.Range ||
                 (landing - caster.Position).LengthSquared > TianFaZhiJian.Range * TianFaZhiJian.Range)) {
                reason = "目标没有安全落点，或超出NPC施法范围。"; return false;
            }
            Cast cast = new Cast { Caster = caster, Landing = landing,
                Power = MagicDamageSystem.GetSpellPowerCoefficient(caster), Started = Mission.CurrentTime };
            if (caster.MountAgent != null) { Arrive(cast); return true; }
            Vec2 away = caster.Position.AsVec2 - desired.AsVec2;
            if (away.LengthSquared < 0.001f) away = -caster.LookDirection.AsVec2;
            if (away.LengthSquared < 0.001f) { reason = "后跃方向无效。"; return false; }
            if (!_leaps.TryAdd(caster, cast)) { reason = "已在后跃中。"; return false; }
            var options = new ParabolicLeapOptions { Duration = 1.5f, Distance = 6f, ApexHeight = 15f, RiseAndHold = true, ApexHoldDuration = 0.35f,
                OnEnded = (mover, target, end) => Finish(cast, end) };
            if (!movement.TryParabolicLeap(caster, null, away, options, out reason)) {
                _leaps.TryRemove(caster, out _); return false;
            }
            cast.Visual = TianFaVisual.Create(caster, caster.Position, false);
            caster.SetActionChannel(0, ActionIndexCache.Create("act_climb_ladder"), true, RushMovementMissionLogic.LeapActionFlags);
            return true;
        }
        private void Finish(Cast cast, RushEndReason end)
        {
            if (!_leaps.TryRemove(cast.Caster, out _)) return;
            cast.Visual?.Remove();
            if (!_closing && cast.Caster.IsActive() && cast.Caster.Health > 0f &&
                (end == RushEndReason.Arrived || end == RushEndReason.Stuck)) Arrive(cast);
        }
        private void Arrive(Cast cast)
        {
            Agent caster = cast.Caster;
            Agent mount = caster.MountAgent;
            // 瞬移前只取角色自身的腰部相对高度，位置始终以最终落点为准。
            float waistHeight = Math.Max(0.5f * caster.AgentScale,
                caster.GetEyeGlobalPosition().z - caster.Position.z - 0.65f * caster.AgentScale);
            Vec3 arrivalPosition = cast.Landing;
            if (mount != null && mount.IsActive()) {
                Vec3 offset = caster.Position - mount.Position;
                mount.TeleportToPosition(cast.Landing);
                arrivalPosition = cast.Landing + offset;
                caster.TeleportToPosition(arrivalPosition);
            } else {
                caster.TeleportToPosition(cast.Landing);
                caster.SetActionChannel(0, ActionIndexCache.Create("act_jump_end"), true);
            }
            // EyeGlobalPosition 在同一帧可能仍是瞬移前的模型位置，不能用作爆炸世界坐标。
            Vec3 waist = arrivalPosition + Vec3.Up * waistHeight;
            TianFaVisual burst = TianFaVisual.Create(caster, waist, true);
            if (burst != null) _bursts.Add(burst);
            var projectile = SpellProjectileMissionLogic.GetForCurrentMission();
            projectile?.SpawnTimedParticle("psys_burning_projectile_stone_coll", waist, 2f, 1.2f);
            projectile?.QueueOneShotSound("event:/mission/combat/boulder/high", cast.Landing);
            _victims.Clear();
            if (caster.Team != null) Mission.GetNearbyEnemyAgents(cast.Landing.AsVec2, TianFaZhiJian.Radius, caster.Team, _victims);
            else Mission.GetNearbyAgents(cast.Landing.AsVec2, TianFaZhiJian.Radius, _victims);
            foreach (Agent victim in _victims) {
                if (!TianFaZhiJian.IsTarget(caster, victim) ||
                    (victim.Position - cast.Landing).LengthSquared > TianFaZhiJian.Radius * TianFaZhiJian.Radius) continue;
                MagicDamageSystem.Apply(caster, victim, TianFaZhiJian.BaseDamage, cast.Power,
                    DamageType.None, MagicDamageFlags.Area | MagicDamageFlags.Explosion, victim.Position + Vec3.Up);
            }
        }
        public override void OnMissionTick(float dt)
        {
            foreach (Cast cast in _leaps.Values) {
                if (!cast.Caster.IsActive() || Mission.CurrentTime - cast.Started > 3f) {
                    RushMovementMissionLogic.Current?.CancelRush(cast.Caster);
                    Finish(cast, RushEndReason.Cancelled); continue;
                }
                cast.Visual?.Update(dt, cast.Caster.Position);
            }
            for (int i = _bursts.Count - 1; i >= 0; i--)
                if (!_bursts[i].Update(dt, Vec3.Invalid)) _bursts.RemoveAt(i);
        }
        private void Clear()
        {
            _closing = true;
            foreach (Cast cast in _leaps.Values) { cast.Visual?.Remove(); RushMovementMissionLogic.Current?.CancelRush(cast.Caster); }
            _leaps.Clear();
            foreach (var visual in _bursts) visual.Remove();
            _bursts.Clear(); _victims.Clear();
            if (ReferenceEquals(_current, this)) _current = null;
        }
        protected override void OnEndMission() { Clear(); base.OnEndMission(); }
        public override void OnRemoveBehavior() { Clear(); base.OnRemoveBehavior(); }
    }
    [HarmonyPatch(typeof(Agent), "HandleBlow")]
    internal static class TianFaLeapBlowImmunityPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Prefix(Agent __instance, ref Blow b, ref AttackCollisionData collisionData)
        {
            if (!TianFaZhiJianMissionLogic.IsLeaping(__instance)) return true;
            b.InflictedDamage = 0; collisionData.InflictedDamage = 0;
            return false;
        }
    }
    [HarmonyPatch(typeof(Agent), "set_Health")]
    internal static class TianFaLeapDirectDamageImmunityPatch
    {
        [HarmonyPrefix, HarmonyPriority(Priority.Last)]
        private static void Prefix(Agent __instance, ref float value)
        {
            // 兼容旧状态直接扣血的路径，允许回血；不改变原来的 MortalityState。
            if (TianFaZhiJianMissionLogic.IsLeaping(__instance) && value < __instance.Health)
                value = __instance.Health;
        }
    }
}
