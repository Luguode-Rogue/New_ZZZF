using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ContinuousCollision
{
    internal static class ContinuousCombatBridge
    {
        public static bool TryApplyContact(Agent attacker, in WeaponPose pose, in CollisionContact contact, ContinuousCollisionSettings settings)
        {
            Agent victim = contact.Victim;
            if (attacker == null || victim == null || attacker == victim || !attacker.IsActive() || !victim.IsActive())
            {
                return false;
            }

            if (!settings.FriendlyFire && attacker.IsFriendOf(victim))
            {
                return false;
            }

            MissionWeapon weapon = pose.Weapon;
            WeaponComponentData weaponData = weapon.CurrentUsageItem;
            if (weaponData == null || !weaponData.WeaponFlags.HasAnyFlag(WeaponFlags.WeaponMask))
            {
                return false;
            }

            bool thrust = IsLikelyThrust(pose, contact.Position);
            StrikeType strikeType = thrust ? StrikeType.Thrust : StrikeType.Swing;
            DamageTypes damageType = thrust ? weaponData.ThrustDamageType : weaponData.SwingDamageType;
            Agent.UsageDirection attackDirection = thrust
                ? Agent.UsageDirection.AttackUp
                : Agent.UsageDirection.AttackLeft;

            sbyte victimBoneIndex = ResolveVictimBone(victim, contact.VictimHeight);
            BoneBodyPartType bodyPart = ResolveBodyPart(victim, victimBoneIndex, contact.VictimHeight);
            sbyte weaponAttachBone = ResolveWeaponAttachBone(attacker, weapon);

            Vec3 blowDirection = Normalize(Subtract(pose.Tip, pose.Base));
            if (LengthSquared(blowDirection) < 1e-5f)
            {
                blowDirection = pose.Direction;
            }

            Vec3 victimVelocity = victim.Velocity;
            AttackCollisionData collisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false,
                false,
                false,
                true,
                false,
                false,
                false,
                false,
                false,
                thrust,
                false,
                false,
                CombatCollisionResult.StrikeAgent,
                pose.WeaponSlot,
                (int)strikeType,
                (int)damageType,
                victimBoneIndex,
                bodyPart,
                weaponAttachBone,
                attackDirection,
                -1,
                (CombatHitResultFlags)0,
                0.5f,
                Math.Max(0f, contact.WeaponDistance),
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                Vec3.Zero,
                blowDirection,
                contact.Position,
                Vec3.Zero,
                contact.Position,
                victimVelocity,
                contact.Normal,
                Vec3.Zero,
                Vec3.Zero);

            AttackInformation attackInformation = new AttackInformation(
                attacker,
                victim,
                WeakGameEntity.Invalid,
                collisionData,
                weapon);

            CombatLogData combatLog;
            int speedBonus;
            MissionCombatMechanicsHelper.GetAttackCollisionResults(
                attackInformation,
                false,
                1f,
                false,
                ref collisionData,
                out combatLog,
                out speedBonus);

            if (collisionData.InflictedDamage > 0)
            {
                float calculatedDamage = MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(
                    attackInformation,
                    collisionData,
                    collisionData.InflictedDamage);
                collisionData.InflictedDamage = Math.Max(0, (int)Math.Round(calculatedDamage));
            }

            if (collisionData.InflictedDamage <= 0)
            {
                return false;
            }

            Blow blow = new Blow(attacker.Index);
            blow.WeaponRecord.FillAsMeleeBlow(weapon.Item, weaponData, pose.WeaponSlot, weaponAttachBone);
            blow.GlobalPosition = contact.Position;
            blow.Direction = blowDirection;
            blow.SwingDirection = blowDirection;
            blow.InflictedDamage = collisionData.InflictedDamage;
            blow.BaseMagnitude = collisionData.BaseMagnitude;
            blow.DefenderStunPeriod = collisionData.DefenderStunPeriod;
            blow.AttackerStunPeriod = collisionData.AttackerStunPeriod;
            blow.AbsorbedByArmor = collisionData.AbsorbedByArmor;
            blow.MovementSpeedDamageModifier = collisionData.MovementSpeedDamageModifier;
            blow.StrikeType = strikeType;
            blow.AttackType = (AgentAttackType)0;
            blow.BlowFlag = BlowFlags.None;
            blow.OwnerId = attacker.Index;
            blow.BoneIndex = victimBoneIndex;
            blow.VictimBodyPart = bodyPart;
            blow.DamageType = damageType;
            blow.DamageCalculated = true;
            blow.IsFallDamage = false;

            victim.RegisterBlow(blow, collisionData);
            return true;
        }

        private static bool IsLikelyThrust(in WeaponPose pose, Vec3 hitPosition)
        {
            Vec3 toHit = Normalize(Subtract(hitPosition, pose.Base));
            float alignment = Dot(pose.Direction, toHit);
            return alignment > 0.82f;
        }

        private static sbyte ResolveVictimBone(Agent victim, float relativeHeight)
        {
            string target = relativeHeight > 1.45f ? "Head" : (relativeHeight > 0.8f ? "Spine1" : "LeftFoot");
            return ResolveHumanBone(victim, target, target == "Head" ? "Neck" : "Spine", "RightFoot", "LeftHand");
        }

        private static BoneBodyPartType ResolveBodyPart(Agent victim, sbyte boneIndex, float relativeHeight)
        {
            if (boneIndex >= 0)
            {
                try
                {
                    return victim.AgentVisuals.GetBoneTypeData(boneIndex).BodyPartType;
                }
                catch
                {
                }
            }

            if (relativeHeight > 1.45f)
            {
                return BoneBodyPartType.Head;
            }
            if (relativeHeight > 0.8f)
            {
                return BoneBodyPartType.Chest;
            }
            return BoneBodyPartType.Legs;
        }

        private static sbyte ResolveWeaponAttachBone(Agent attacker, MissionWeapon weapon)
        {
            if (attacker.Monster == null || weapon.IsEmpty || weapon.Item == null)
            {
                return -1;
            }
            try
            {
                return (sbyte)attacker.Monster.GetBoneToAttachForItemFlags(weapon.Item.ItemFlags);
            }
            catch
            {
                return -1;
            }
        }

        private static sbyte ResolveHumanBone(Agent agent, params string[] candidates)
        {
            string[] names = Enum.GetNames(typeof(HumanBone));
            for (int i = 0; i < candidates.Length; i++)
            {
                for (int n = 0; n < names.Length; n++)
                {
                    if (!string.Equals(names[n], candidates[i], StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    try
                    {
                        HumanBone bone = (HumanBone)Enum.Parse(typeof(HumanBone), names[n]);
                        return agent.AgentVisuals.GetRealBoneIndex(bone);
                    }
                    catch
                    {
                        return -1;
                    }
                }
            }
            return -1;
        }

        private static Vec3 Subtract(Vec3 a, Vec3 b) => new Vec3(a.x - b.x, a.y - b.y, a.z - b.z, -1f);
        private static Vec3 Normalize(Vec3 a)
        {
            float length = (float)Math.Sqrt(LengthSquared(a));
            return length > 1e-6f ? new Vec3(a.x / length, a.y / length, a.z / length, -1f) : Vec3.Zero;
        }
        private static float Dot(Vec3 a, Vec3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        private static float LengthSquared(Vec3 a) => Dot(a, a);
    }
}
