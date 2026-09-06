using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.ContinuousCollision
{
    internal struct WeaponPose
    {
        public Vec3 Base;
        public Vec3 Tip;
        public Vec3 Direction;
        public sbyte AttackBoneIndex;
        public int WeaponSlot;
        public MissionWeapon Weapon;
        public float WeaponLength;

        public WeaponPose(Vec3 @base, Vec3 tip, Vec3 direction, sbyte attackBoneIndex, int weaponSlot, MissionWeapon weapon, float weaponLength)
        {
            Base = @base;
            Tip = tip;
            Direction = direction;
            AttackBoneIndex = attackBoneIndex;
            WeaponSlot = weaponSlot;
            Weapon = weapon;
            WeaponLength = weaponLength;
        }
    }

    internal struct CollisionContact
    {
        public Agent Victim;
        public Vec3 Position;
        public Vec3 Normal;
        public float WeaponDistance;
        public float VictimHeight;

        public CollisionContact(Agent victim, Vec3 position, Vec3 normal, float weaponDistance, float victimHeight)
        {
            Victim = victim;
            Position = position;
            Normal = normal;
            WeaponDistance = weaponDistance;
            VictimHeight = victimHeight;
        }
    }

    internal sealed class AgentCollisionState
    {
        public bool HasPreviousPose;
        public WeaponPose PreviousPose;
        public float LastCleanupTime;
        public readonly Dictionary<int, float> LastHitTimes = new Dictionary<int, float>();
    }
}
