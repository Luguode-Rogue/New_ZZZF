using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ContinuousCollision
{
    internal static class ContinuousCollisionGeometry
    {
        public static WeaponPose GetWeaponPose(Agent agent, ContinuousCollisionSettings settings)
        {
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None || agent.Equipment[slot].IsEmpty)
            {
                return default(WeaponPose);
            }

            MissionWeapon weapon = agent.Equipment[slot];
            WeaponComponentData usage = weapon.CurrentUsageItem;
            if (usage == null || !usage.WeaponFlags.HasAnyFlag(WeaponFlags.WeaponMask))
            {
                return default(WeaponPose);
            }

            float weaponLength = usage.WeaponLength > 0
                ? usage.WeaponLength / 100f
                : settings.DefaultWeaponReach;
            weaponLength += settings.AdditionalReach;

            sbyte handBone = ResolveHumanBone(agent, "RightHand", "HandRight", "RHand", "Right_Hand");
            if (handBone < 0)
            {
                return GetFallbackPose(agent, weapon, (int)slot, weaponLength, settings);
            }

            try
            {
                MatrixFrame globalFrame = agent.AgentVisuals.GetGlobalFrame();
                MatrixFrame handFrame = agent.AgentVisuals.GetBoneEntitialFrame(handBone, false);
                Vec3 handOrigin = TransformPoint(globalFrame, handFrame.origin);
                Vec3 forward = TransformVector(globalFrame, handFrame.rotation.f);
                forward = Normalize(forward);
                if (LengthSquared(forward) < 0.0001f)
                {
                    forward = Normalize(agent.LookFrame.rotation.f);
                }

                if (LengthSquared(forward) < 0.0001f)
                {
                    return default(WeaponPose);
                }

                return new WeaponPose(
                    handOrigin,
                    Add(handOrigin, Multiply(forward, weaponLength)),
                    forward,
                    handBone,
                    (int)slot,
                    weapon,
                    weaponLength);
            }
            catch (Exception ex)
            {
                Debug.Print("[ContinuousCollision] Weapon pose failed: " + ex.Message);
                return GetFallbackPose(agent, weapon, (int)slot, weaponLength, settings);
            }
        }

        public static bool TryFindCollision(
            WeaponPose previous,
            WeaponPose current,
            Agent victim,
            ContinuousCollisionSettings settings,
            out CollisionContact contact)
        {
            contact = default(CollisionContact);

            if (victim == null || !victim.IsActive() || !victim.IsHuman || victim.IsMount)
            {
                return false;
            }

            CapsuleData capsule;
            float capsuleScale;
            if (!TryGetWorldBodyCapsule(victim, out capsule, out capsuleScale))
            {
                return false;
            }

            float radius = capsule.Radius * capsuleScale + settings.WeaponRadius;
            SegmentHit best = default(SegmentHit);
            bool hit = false;

            TrySegmentCapsule(previous.Base, previous.Tip, capsule.P1, capsule.P2, radius, ref hit, ref best);
            TrySegmentCapsule(current.Base, current.Tip, capsule.P1, capsule.P2, radius, ref hit, ref best);
            TrySegmentCapsule(previous.Base, current.Base, capsule.P1, capsule.P2, radius, ref hit, ref best);
            TrySegmentCapsule(previous.Tip, current.Tip, capsule.P1, capsule.P2, radius, ref hit, ref best);

            if (!hit)
            {
                return false;
            }

            Vec3 normal = Subtract(best.WeaponPoint, best.CapsulePoint);
            float normalLength = Length(normal);
            if (normalLength < 0.0001f)
            {
                normal = Normalize(Multiply(current.Direction, -1f));
            }
            else
            {
                normal = Multiply(normal, 1f / normalLength);
            }

            Vec3 position = best.WeaponPoint;
            float height = position.z - capsule.P1.z;
            contact = new CollisionContact(victim, position, normal, best.WeaponDistance, height);
            return true;
        }

        private static WeaponPose GetFallbackPose(Agent agent, MissionWeapon weapon, int slot, float weaponLength, ContinuousCollisionSettings settings)
        {
            Vec3 forward = Normalize(agent.LookFrame.rotation.f);
            if (LengthSquared(forward) < 0.0001f)
            {
                return default(WeaponPose);
            }

            Vec3 root = Add(agent.Position, Multiply(forward, 0.65f));
            return new WeaponPose(root, Add(root, Multiply(forward, weaponLength)), forward, -1, slot, weapon, weaponLength);
        }

        private static sbyte ResolveHumanBone(Agent agent, params string[] candidates)
        {
            string[] names = Enum.GetNames(typeof(HumanBone));
            for (int i = 0; i < candidates.Length; i++)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    if (string.Equals(names[n], candidates[i], StringComparison.OrdinalIgnoreCase))
                    {
                        HumanBone humanBone = (HumanBone)Enum.Parse(typeof(HumanBone), names[n]);
                        return agent.AgentVisuals.GetRealBoneIndex(humanBone);
                    }
                }
            }

            for (int n = 0; n < names.Length; n++)
            {
                if (names[n].IndexOf("RightHand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    names[n].IndexOf("HandRight", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HumanBone humanBone = (HumanBone)Enum.Parse(typeof(HumanBone), names[n]);
                    return agent.AgentVisuals.GetRealBoneIndex(humanBone);
                }
            }

            return -1;
        }

        private static bool TryGetWorldBodyCapsule(Agent agent, out CapsuleData worldCapsule, out float scale)
        {
            worldCapsule = default(CapsuleData);
            scale = 1f;
            try
            {
                Monster monster = agent.Monster;
                bool crouched = agent.CrouchMode;
                float radius = crouched ? monster.CrouchedBodyCapsuleRadius : monster.BodyCapsuleRadius;
                Vec3 p1 = crouched ? monster.CrouchedBodyCapsulePoint1 : monster.BodyCapsulePoint1;
                Vec3 p2 = crouched ? monster.CrouchedBodyCapsulePoint2 : monster.BodyCapsulePoint2;
                scale = Math.Max(0.01f, agent.AgentScale);

                MatrixFrame globalFrame = agent.AgentVisuals.GetGlobalFrame();
                Vec3 wp1 = TransformPoint(globalFrame, p1);
                Vec3 wp2 = TransformPoint(globalFrame, p2);
                worldCapsule = new CapsuleData(radius * scale, wp1, wp2);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print("[ContinuousCollision] Body capsule failed: " + ex.Message);
                return false;
            }
        }

        private struct SegmentHit
        {
            public float DistanceSquared;
            public Vec3 WeaponPoint;
            public Vec3 CapsulePoint;
            public float WeaponDistance;
        }

        private static void TrySegmentCapsule(
            Vec3 a,
            Vec3 b,
            Vec3 c,
            Vec3 d,
            float radius,
            ref bool hit,
            ref SegmentHit best)
        {
            Vec3 closestOnSegment;
            Vec3 closestOnCapsuleAxis;
            float distanceSquared = ClosestDistanceSquared(a, b, c, d, out closestOnSegment, out closestOnCapsuleAxis);
            if (distanceSquared > radius * radius)
            {
                return;
            }

            if (!hit || distanceSquared < best.DistanceSquared)
            {
                hit = true;
                best.DistanceSquared = distanceSquared;
                best.WeaponPoint = closestOnSegment;
                best.CapsulePoint = closestOnCapsuleAxis;
                best.WeaponDistance = Length(Subtract(closestOnSegment, a));
            }
        }

        private static float ClosestDistanceSquared(
            Vec3 p1,
            Vec3 q1,
            Vec3 p2,
            Vec3 q2,
            out Vec3 c1,
            out Vec3 c2)
        {
            Vec3 d1 = Subtract(q1, p1);
            Vec3 d2 = Subtract(q2, p2);
            Vec3 r = Subtract(p1, p2);
            float a = Dot(d1, d1);
            float e = Dot(d2, d2);
            float f = Dot(d2, r);
            float s;
            float t;

            if (a <= 1e-8f && e <= 1e-8f)
            {
                c1 = p1;
                c2 = p2;
                return LengthSquared(Subtract(c1, c2));
            }

            if (a <= 1e-8f)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                float c = Dot(d1, r);
                if (e <= 1e-8f)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    float b = Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom != 0f ? Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Clamp01((b - c) / a);
                    }
                }
            }

            c1 = Add(p1, Multiply(d1, s));
            c2 = Add(p2, Multiply(d2, t));
            return LengthSquared(Subtract(c1, c2));
        }

        private static Vec3 TransformPoint(MatrixFrame frame, Vec3 local)
        {
            return new Vec3(
                frame.origin.x + frame.rotation.s.x * local.x + frame.rotation.t.x * local.y + frame.rotation.f.x * local.z,
                frame.origin.y + frame.rotation.s.y * local.x + frame.rotation.t.y * local.y + frame.rotation.f.y * local.z,
                frame.origin.z + frame.rotation.s.z * local.x + frame.rotation.t.z * local.y + frame.rotation.f.z * local.z,
                -1f);
        }

        private static Vec3 TransformVector(MatrixFrame frame, Vec3 local)
        {
            return new Vec3(
                frame.rotation.s.x * local.x + frame.rotation.t.x * local.y + frame.rotation.f.x * local.z,
                frame.rotation.s.y * local.x + frame.rotation.t.y * local.y + frame.rotation.f.y * local.z,
                frame.rotation.s.z * local.x + frame.rotation.t.z * local.y + frame.rotation.f.z * local.z,
                -1f);
        }

        private static Vec3 Add(Vec3 a, Vec3 b) => new Vec3(a.x + b.x, a.y + b.y, a.z + b.z, -1f);
        private static Vec3 Subtract(Vec3 a, Vec3 b) => new Vec3(a.x - b.x, a.y - b.y, a.z - b.z, -1f);
        private static Vec3 Multiply(Vec3 a, float b) => new Vec3(a.x * b, a.y * b, a.z * b, -1f);
        private static float Dot(Vec3 a, Vec3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        private static float LengthSquared(Vec3 a) => Dot(a, a);
        private static float Length(Vec3 a) => (float)Math.Sqrt(LengthSquared(a));
        private static Vec3 Normalize(Vec3 a)
        {
            float length = Length(a);
            return length > 1e-6f ? Multiply(a, 1f / length) : Vec3.Zero;
        }
        private static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
