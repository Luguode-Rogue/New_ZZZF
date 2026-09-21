using System;

namespace New_ZZZF.KillSkillXpMultiplier
{
    internal static class CombatHitXpContext
    {
        [ThreadStatic]
        private static float _multiplier;

        internal static float Multiplier => _multiplier <= 0f ? 1f : _multiplier;

        internal static float Set(float multiplier)
        {
            float previousMultiplier = _multiplier;
            _multiplier = multiplier;
            return previousMultiplier;
        }

        internal static void Restore(float multiplier)
        {
            _multiplier = multiplier;
        }
    }
}
