using System.Collections.Generic;
using New_ZZZF.Skills;
using New_ZZZF.Systems;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal sealed class FlameRushMissionLogic : MissionLogic
    {
        private sealed class Record
        {
            internal Agent Caster, Target;
            internal Vec3 Last;
            internal float Power;
            internal bool Finished;
            internal readonly Dictionary<int, float> Hits = new Dictionary<int, float>();
        }
        private readonly Dictionary<int, Record> _records = new Dictionary<int, Record>();
        private readonly List<Record> _snapshot = new List<Record>();
        private readonly HongShiZiHuoYan _flame = new HongShiZiHuoYan();
        internal bool Begin(Agent caster, Agent target, out string reason)
        {
            reason = null;
            RushMovementMissionLogic movement = RushMovementMissionLogic.Current;
            if (movement == null || movement.IsRushing(caster) || _records.ContainsKey(caster.Index))
            { reason = "当前无法开始突袭。"; return false; }
            Record record = new Record { Caster = caster, Target = target, Last = caster.Position,
                Power = MagicDamageSystem.GetSpellPowerCoefficient(caster) };
            if (!TryLanding(record, out Vec3 landing)) { reason = "目标附近没有安全落点。"; return false; }
            if (caster.MountAgent != null || (target.Position - caster.Position).LengthSquared > 100f ||
                !RushMovementMissionLogic.HasLineOfSight(caster, target)) return Teleport(record, landing);
            _records.Add(caster.Index, record);
            var options = new RushMovementOptions { Duration = 1.75f, StopDistance = 0.5f, SpeedLimit = 5f,
                SpeedLimitIsMultiplier = true, RequireLineOfSight = false, AllowMounted = false,
                UseSafeKinematicMovement = true, KinematicSpeed = 30.2f, MaximumKinematicSlopeDegrees = 60f,
                OnEnded = (mover, ignored, end) => Finish(record, end) };
            if (movement.TryRushToPosition(caster, landing, options, out reason)) return true;
            _records.Remove(caster.Index);
            return Teleport(record, landing);
        }
        private bool TryLanding(Record record, out Vec3 landing)
        {
            Vec3 forward = record.Target.LookDirection.AsVec2.ToVec3();
            if (forward.LengthSquared < 0.001f) forward = Vec3.Forward;
            forward.Normalize();
            float radius = record.Caster.MountAgent != null ? 2.5f : 1.5f;
            for (int i = 0; i < 8; i++)
            {
                Mat3 rotation = Mat3.Identity; rotation.RotateAboutUp(i * 0.785398f);
                Vec3 desired = record.Target.Position + rotation.TransformToParent(forward) * radius;
                if (RushMovementMissionLogic.Current.TryGetSafeLandingPosition(desired, out landing)) return true;
            }
            landing = Vec3.Invalid; return false;
        }
        private bool Teleport(Record record, Vec3 landing)
        {
            Agent caster = record.Caster;
            if (!caster.IsActive()) return false;
            SpellProjectileMissionLogic.GetForCurrentMission()?.SpawnTimedParticle("psys_campfire_sparks", caster.Position + Vec3.Up, 0.4f, 1f);
            Agent mount = caster.MountAgent;
            if (mount != null)
            {
                Vec3 riderOffset = caster.Position - mount.Position;
                mount.TeleportToPosition(landing);
                caster.TeleportToPosition(landing + riderOffset);
            }
            else caster.TeleportToPosition(landing);
            Arrive(record);
            return true;
        }
        private void Arrive(Record record)
        {
            if (record.Finished || !record.Caster.IsActive()) return;
            record.Finished = true;
            Vec3 direction = record.Target.Position - record.Caster.Position; direction.z = 0f;
            if (direction.LengthSquared > 0.001f) { direction.Normalize(); record.Caster.LookDirection = direction; }
            _flame.ReleaseAtArrival(record.Caster, record.Power);
        }
        private void AddTrail(Record record)
        {
            Vec3 current = record.Caster.Position;
            if ((current - record.Last).LengthSquared < 0.0025f) return;
            FireWallSkill.CreateTrailSegment(record.Caster, record.Last, current, record.Power, record.Hits);
            record.Last = current;
        }
        private void Finish(Record record, RushEndReason reason)
        {
            _records.Remove(record.Caster.Index);
            if (!record.Caster.IsActive()) return;
            AddTrail(record);
            if (reason == RushEndReason.Arrived) Arrive(record);
            else if ((reason == RushEndReason.NoNavigation || reason == RushEndReason.Stuck) &&
                HuoYanTuXi.IsTarget(record.Caster, record.Target) && TryLanding(record, out Vec3 landing)) Teleport(record, landing);
        }
        public override void OnMissionTick(float dt)
        {
            _snapshot.Clear(); _snapshot.AddRange(_records.Values);
            foreach (Record record in _snapshot)
                if (record.Caster.IsActive() && (record.Caster.Position - record.Last).LengthSquared >= 1f) AddTrail(record);
        }
        public override void OnRemoveBehavior() { _records.Clear(); _snapshot.Clear(); base.OnRemoveBehavior(); }
    }
}