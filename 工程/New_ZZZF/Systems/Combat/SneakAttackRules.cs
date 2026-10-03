using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
namespace New_ZZZF
{
    internal static class SneakAttackRules
    {
        public static bool CanSneak(in AttackInformation info, WeaponComponentData weapon) {
            Agent attacker = info.AttackerAgent;
            Agent victim = info.VictimAgent;
            if (attacker == null || victim == null || !victim.IsHuman || !attacker.IsEnemyOf(victim) ||
                weapon == null || (!weapon.IsMeleeWeapon && !weapon.IsRangedWeapon)) return false;
            if (SoulBarrageNativeHit.ForcesSneak(attacker, victim)) return true;
            if (JingXia.IsFrightened(victim)) return true;
            Vec2 offset = attacker.Position.AsVec2 - victim.Position.AsVec2;
            Vec2 forward = victim.LookDirection.AsVec2;
            return offset.LengthSquared > 0.001f && forward.LengthSquared > 0.001f &&
                Vec2.DotProduct(offset.Normalized(), forward.Normalized()) <= -0.5f;
        }
    }
}
