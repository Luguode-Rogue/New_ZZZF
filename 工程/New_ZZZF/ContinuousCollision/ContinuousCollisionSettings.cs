using System;

namespace New_ZZZF.ContinuousCollision
{
    /// <summary>
    /// Continuous melee collision detector configuration.
    /// Geometry is owned by this subsystem; damage is delegated to Bannerlord's combat models.
    /// </summary>
    public sealed class ContinuousCollisionSettings
    {
        public bool Enabled { get; set; } = true;
        public bool EnabledWithoutAttackAnimation { get; set; } = true;
        public bool FriendlyFire { get; set; } = false;
        public float TickInterval { get; set; } = 0.0f;
        public float WeaponRadius { get; set; } = 0.045f;
        public float AdditionalReach { get; set; } = 0.05f;
        public float MinimumMovement { get; set; } = 0.0125f;
        public float HitCooldown { get; set; } = 0.12f;
        public float CellSize { get; set; } = 2.5f;
        public float MaximumCandidateDistance { get; set; } = 4.5f;
        public float DefaultWeaponReach { get; set; } = 1.35f;
        public bool DebugDraw { get; set; } = false;
    }
}
