using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 统一破格挡规则。保留原有 New_ZZZF 设定。
    /// 不在原生 DecideCrushedThrough 回调中直接修改 Agent 装备。
    /// </summary>
    internal static class ZZZFBlockBreakRules
    {
        public static bool Decide(Agent attackerAgent, Agent defenderAgent, float totalAttackEnergy, Agent.UsageDirection attackDirection, StrikeType strikeType, WeaponComponentData defendItem, bool isPassiveUsage)
        {
            if (attackerAgent == null || defenderAgent == null)
                return false;

            EquipmentIndex attackerOffHand = attackerAgent.GetOffhandWieldedItemIndex();
            EquipmentIndex attackerMainHand = attackerAgent.GetPrimaryWieldedItemIndex();
            WeaponComponentData attackerWeapon = attackerMainHand != EquipmentIndex.None
                ? attackerAgent.Equipment[attackerMainHand].CurrentUsageItem
                : null;

            if (attackerWeapon == null)
                return false;

            EquipmentIndex defenderIndex = defenderAgent.GetPrimaryWieldedItemIndex();
            WeaponComponentData defenderWeapon = defenderIndex != EquipmentIndex.None
                ? defenderAgent.Equipment[defenderIndex].CurrentUsageItem
                : null;

            if (defenderWeapon == null)
                return true;

            if (SkillSystemBehavior.ActiveComponents != null &&
                SkillSystemBehavior.ActiveComponents.TryGetValue(attackerAgent.Index, out var attackerComponent) &&
                attackerComponent.HasSkill("Power"))
                return true;

            if (defendItem != null && !defendItem.IsShield && strikeType == StrikeType.Thrust)
                return true;

            int proficiencyDifference = attackerAgent.Character.GetSkillValue(attackerWeapon.RelevantSkill)
                - defenderAgent.Character.GetSkillValue(defenderWeapon.RelevantSkill);
            int attackerMovementSkill = GetMovementSkill(attackerAgent);
            int defenderMovementSkill = GetMovementSkill(defenderAgent);
            float threshold = 58f;

            if (attackerWeapon.RelevantSkill == DefaultSkills.TwoHanded ||
                (attackerOffHand == EquipmentIndex.None && attackerWeapon.RelevantSkill == DefaultSkills.Polearm))
            {
                totalAttackEnergy *= 1.2f;
                if (proficiencyDifference > 0)
                    totalAttackEnergy *= 1f + proficiencyDifference / 500f;
            }

            if (defendItem != null && !defendItem.IsShield && defenderAgent.Mount == null && attackerAgent.Mount == null)
                threshold -= (attackerMovementSkill - defenderMovementSkill) * 0.05f;

            threshold -= proficiencyDifference * 0.05f;

            if (isPassiveUsage)
                threshold /= 2f;

            if (defendItem != null && defendItem.IsShield)
                threshold *= 1.2f;

            // 保留原有斧类缴械概率判定，但延迟实际装备修改。
            // RemoveEquippedWeapon 不能在 native 战斗判定回调中直接调用。
            if (attackerWeapon.WeaponClass == WeaponClass.OneHandedAxe || attackerWeapon.WeaponClass == WeaponClass.TwoHandedAxe)
            {
                float disarmChance = 0.2f + proficiencyDifference / 500f *
                    (1f + (attackerMovementSkill - defenderMovementSkill) / 1000f);

                _ = disarmChance > MBRandom.RandomFloat;
            }

            return totalAttackEnergy > threshold;
        }

        private static int GetMovementSkill(Agent agent)
        {
            if (agent == null || agent.Character == null)
                return 0;

            return agent.Mount != null
                ? agent.Character.GetSkillValue(DefaultSkills.Riding)
                : agent.Character.GetSkillValue(DefaultSkills.Athletics);
        }
    }
}
