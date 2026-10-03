using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace New_ZZZF
{
    /// <summary>仅接管已整体迁入的友军穿透补丁，防止独立版再次跳过恐虐碰撞。</summary>
    internal static class FriendlyMeleePatchOwnership
    {
        private static bool _navalPatched;
        private static readonly HashSet<string> MigratedPatches = new HashSet<string>(StringComparer.Ordinal)
        {
            "SocialSkillsOverhaul.Patch_MeleeHitCallback",
            "SocialSkillsOverhaul.Patch_OnAgentHitBlocked",
            "SocialSkillsOverhaul.Patch_GetDefendCollisionResults",
            "SocialSkillsOverhaul.Patch_CalculateDefaultRemainingMomentum",
            "SocialSkillsOverhaul.Patch_SandboxDecideCrushedThrough",
            "SocialSkillsOverhaul.Patch_CustomDecideCrushedThrough",
            "SocialSkillsOverhaul.FriendlyFirePenetration"
        };

        internal static void EnsureOwnership(Harmony harmony)
        {
            if (harmony == null) return;
            var originals = new List<MethodBase>(Harmony.GetAllPatchedMethods());
            foreach (MethodBase original in originals)
            {
                var info = Harmony.GetPatchInfo(original);
                if (info == null) continue;
                var patches = new List<Patch>();
                patches.AddRange(info.Prefixes);
                patches.AddRange(info.Postfixes);
                foreach (Patch patch in patches)
                    if (patch.owner == "com.mod.SocialSkillsOverhaul" &&
                        MigratedPatches.Contains(patch.PatchMethod.DeclaringType?.FullName))
                        harmony.Unpatch(original, patch.PatchMethod);
            }
            if (!_navalPatched)
            {
                FriendlyFirePenetration.TryPatchNavalDecideCrushedThrough(harmony);
                _navalPatched = true;
            }
        }
    }
}