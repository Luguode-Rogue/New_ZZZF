using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    // 当前枪尖由持械骨骼校准；最远伸展在任务Tick用实际动画片段及映射后的骨骼采样。
    // 不将ActionIndex当作AnimationIndex，也不调用Agent的未来姿态接口。
    internal static class WeaponCombatThrustGeometry
    {
        internal sealed class Pose
        {
            internal ActionIndexCache Action;
            internal EquipmentIndex Slot;
            internal int Usage;
            internal Vec3 LocalTip, LocalAxis, TipAtHit, VictimAtHit, AttackerAtHit;
            internal MatrixFrame WorldAtHit;
            internal float Progress, MaximumReach;
        }

        internal static void Capture(WeaponCombatMissionLogic.Hit hit)
        {
            Agent agent = hit.Attacker;
            if (agent == null || !agent.IsHuman || !agent.IsActive() || hit.Weapon.IsEmpty ||
                agent.GetCurrentActionStage(1) != Agent.ActionStage.AttackRelease) return;
            var pose = new Pose { Action = agent.GetCurrentAction(1), Slot = agent.GetPrimaryWieldedItemIndex(),
                Usage = hit.Weapon.CurrentUsageIndex, Progress = agent.GetCurrentActionProgress(1),
                AttackerAtHit = agent.Position, VictimAtHit = hit.Victim.Position };
            if (pose.Slot == EquipmentIndex.None || pose.Action == ActionIndexCache.act_none) return;
            if (!Frame(agent, pose, out MatrixFrame frame)) return;
            pose.WorldAtHit = agent.AgentVisuals.GetGlobalFrame();
            if (!Valid(pose.WorldAtHit) || float.IsNaN(pose.Progress) || pose.Progress < 0f || pose.Progress > 1f) return;
            Vec3 axis = hit.Collision.WeaponRotUp;
            if (!Finite(axis) || axis.LengthSquared < 0.001f) axis = hit.Collision.WeaponBlowDir;
            if (!Finite(axis) || axis.LengthSquared < 0.001f) return;
            axis.Normalize();
            if (Vec3.DotProduct(axis, hit.Collision.CollisionGlobalPosition - agent.Position) < 0f) axis = -axis;
            // 原生接触位置和沿武器距离校准实际枪尖，兼容不同握持偏移、武器模型轴和角色大小。
            float length = Math.Max(0f, hit.Weapon.CurrentUsageItem.GetRealWeaponLength() + agent.GetCurWeaponOffset().z);
            float beyondContact = Math.Max(0f, length - hit.Collision.CollisionDistanceOnWeapon);
            pose.TipAtHit = hit.Collision.CollisionGlobalPosition + axis * beyondContact;
            if (!Finite(pose.TipAtHit)) return;
            pose.LocalTip = frame.TransformToLocalNonOrthogonal(pose.TipAtHit);
            pose.LocalAxis = frame.Inverse().rotation.TransformToParent(axis);
            hit.ThrustPose = pose;
        }

        internal static bool SameWeapon(WeaponCombatMissionLogic.Hit hit)
        {
            var pose = hit.ThrustPose;
            var weapon = WeaponCombatMissionLogic.HeldWeapon(hit.Attacker);
            return pose != null && !weapon.IsEmpty && hit.Attacker.GetPrimaryWieldedItemIndex() == pose.Slot &&
                weapon.Item == hit.Weapon.Item && weapon.ItemModifier == hit.Weapon.ItemModifier && weapon.CurrentUsageIndex == pose.Usage;
        }

        internal static bool InRelease(WeaponCombatMissionLogic.Hit hit) => SameWeapon(hit) &&
            hit.Attacker.GetCurrentActionStage(1) == Agent.ActionStage.AttackRelease &&
            hit.Attacker.GetCurrentAction(1) == hit.ThrustPose.Action &&
            hit.Attacker.GetCurrentActionProgress(1) + 0.02f >= hit.ThrustPose.Progress;

        internal static bool Sample(WeaponCombatMissionLogic.Hit hit, out Vec3 tip, out Vec3 axis)
        {
            tip = axis = Vec3.Zero;
            if (!SameWeapon(hit) || !Frame(hit.Attacker, hit.ThrustPose, out MatrixFrame frame)) return false;
            tip = frame.TransformToParent(hit.ThrustPose.LocalTip);
            axis = frame.rotation.TransformToParent(hit.ThrustPose.LocalAxis);
            if (!Finite(tip) || !Finite(axis) || axis.LengthSquared < 0.001f) return false;
            axis.Normalize();
            return true;
        }

        internal static bool Maximum(WeaponCombatMissionLogic.Hit hit, Vec3 direction, out Vec3 tip, out float progress)
        {
            var pose = hit.ThrustPose;
            tip = Vec3.Zero; progress = 0f;
            if (pose == null) return false;
            tip = pose.TipAtHit; progress = pose.Progress;
            Agent agent = hit.Attacker;
            if (!SameWeapon(hit) || agent.AgentVisuals == null || !agent.ActionSet.IsValid ||
                !MBActionSet.CheckActionAnimationClipExists(agent.ActionSet, pose.Action)) return false;
            var skeleton = agent.AgentVisuals.GetSkeleton();
            if (skeleton == null || !skeleton.IsValid) return false;
            sbyte bone = skeleton.GetSkeletonBoneMapping(agent.Monster.MainHandItemBoneIndex);
            if (bone < 0 || bone >= skeleton.GetBoneCount()) return false;
            int animation = MBActionSet.GetAnimationIndexOfAction(agent.ActionSet, pose.Action);
            if (animation < 0) return false;
            // 使用片段自身坐标校准首次接触点，避免动画混合、模型轴及握持偏移造成整体位置偏差。
            MatrixFrame atHit = skeleton.GetBoneEntitialFrameAtAnimationProgress(bone, animation, pose.Progress);
            if (!Valid(atHit)) return false;
            Vec3 localTip = atHit.TransformToLocalNonOrthogonal(pose.WorldAtHit.TransformToLocalNonOrthogonal(pose.TipAtHit));
            if (!Finite(localTip)) return false;
            float maximum = Vec3.DotProduct(tip - pose.AttackerAtHit, direction);
            const int samples = 48;
            for (int i = 0; i <= samples; i++)
            {
                float p = i / (float)samples;
                MatrixFrame sampled = skeleton.GetBoneEntitialFrameAtAnimationProgress(bone, animation, p);
                if (!Valid(sampled)) return false;
                Vec3 candidate = pose.WorldAtHit.TransformToParent(sampled.TransformToParent(localTip));
                if (!Finite(candidate)) return false;
                float reach = Vec3.DotProduct(candidate - pose.AttackerAtHit, direction);
                if (reach > maximum) { maximum = reach; tip = candidate; progress = p; }
            }
            // 细化峰值附近，峰值在命中之前也要计入整段动作的最远伸展。
            float left = Math.Max(0f, progress - 1f / samples), right = Math.Min(1f, progress + 1f / samples);
            for (int i = 0; i <= 12; i++)
            {
                float p = left + (right - left) * i / 12f;
                MatrixFrame sampled = skeleton.GetBoneEntitialFrameAtAnimationProgress(bone, animation, p);
                if (!Valid(sampled)) return false;
                Vec3 candidate = pose.WorldAtHit.TransformToParent(sampled.TransformToParent(localTip));
                if (!Finite(candidate)) return false;
                float reach = Vec3.DotProduct(candidate - pose.AttackerAtHit, direction);
                if (reach > maximum) { maximum = reach; tip = candidate; progress = p; }
            }
            pose.MaximumReach = (tip - pose.AttackerAtHit).AsVec2.Length;
            return true;
        }

        private static bool Frame(Agent agent, Pose pose, out MatrixFrame frame)
        {
            frame = MatrixFrame.Identity;
            if (agent == null || !agent.IsActive() || agent.AgentVisuals == null) return false;
            sbyte bone = agent.Monster.MainHandItemBoneIndex;
            if (bone < 0) return false;
            frame = agent.AgentVisuals.GetGlobalFrame().TransformToParent(agent.GetBoneEntitialFrame(bone, true));
            return Valid(frame);
        }

        private static bool Valid(MatrixFrame frame) => Finite(frame.origin) && Finite(frame.rotation.s) &&
            Finite(frame.rotation.f) && Finite(frame.rotation.u) && frame.rotation.s.LengthSquared > 0.0001f &&
            frame.rotation.f.LengthSquared > 0.0001f && frame.rotation.u.LengthSquared > 0.0001f &&
            Math.Abs(Vec3.DotProduct(Vec3.CrossProduct(frame.rotation.s, frame.rotation.f), frame.rotation.u)) > 0.000001f;

        internal static bool Finite(Vec3 value) => !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
            float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }
}
