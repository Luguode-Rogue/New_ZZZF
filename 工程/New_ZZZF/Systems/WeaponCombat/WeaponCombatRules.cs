using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    // 四阶面板50/65/80/100只是装备设计参考；这里不覆写共享物品或面板伤害。
    internal static class WeaponCombatRules
    {
        internal const float LowArmorThreshold = 20f;
        internal const float ReleaseTailStart = 0.70f;
        internal const float BleedDuration = 3f;
        internal const float ArmorResetDelay = 10f;
        internal const float FootSlowDuration = 20f;
        internal const float FootSpeedMultiplier = 0.75f;
        internal const float KnifeLeadTime = 0.08f;
        internal const float PushMaximumDuration = 0.65f;
        internal const float PushTravelDuration = 0.22f;
        // 在原生伤害 helper 的作用域内记录真实攻击类型，兼容同时有挥砍和戳刺的长杆。
        [ThreadStatic] internal static StrikeType? CurrentStrike;

        internal static bool Enabled => WeaponCombatMissionLogic.Current?.Mission.Mode == MissionMode.Battle && !GameNetwork.IsMultiplayer;
        internal static int Skill(Agent agent, WeaponComponentData weapon)
        {
            if (agent?.Character == null || weapon == null) return 0;
            // 本mod的矛精通存放在Throwing槽位；长杆挥砍不能因为参考斧数值而读取斧精通。
            SkillObject skill = Spear(weapon) || weapon.WeaponClass == WeaponClass.Javelin
                ? DefaultSkills.Throwing : weapon.RelevantSkill;
            return skill == null ? 0 : Math.Max(0, MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkill(agent, skill));
        }
        internal static bool Spear(WeaponComponentData w) => w != null &&
            (w.WeaponClass == WeaponClass.OneHandedPolearm || w.WeaponClass == WeaponClass.TwoHandedPolearm ||
             w.WeaponClass == WeaponClass.LowGripPolearm);
        internal static bool Axe(WeaponComponentData w) => w != null &&
            (w.WeaponClass == WeaponClass.OneHandedAxe || w.WeaponClass == WeaponClass.TwoHandedAxe);
        internal static bool Sword(WeaponComponentData w) => w != null &&
            (w.WeaponClass == WeaponClass.OneHandedSword || w.WeaponClass == WeaponClass.TwoHandedSword ||
             w.WeaponClass == WeaponClass.ThrowingKnife);
        internal static bool Hammer(WeaponComponentData w) => w != null &&
            (w.WeaponClass == WeaponClass.Mace || w.WeaponClass == WeaponClass.TwoHandedMace ||
             w.WeaponClass == WeaponClass.Dagger);
        internal static bool CanFeint(WeaponComponentData w) => w != null && w.IsMeleeWeapon &&
            !w.IsTwoHanded && !Hammer(w);
        internal static bool HeavyMiss(Agent agent, WeaponComponentData weapon)
        {
            if (Axe(weapon) || Hammer(weapon)) return true;
            if (!Spear(weapon) || weapon.SwingDamage <= 0 || agent == null) return false;
            // 原版长杆既有上刺也有下刺，不能仅凭 AttackUp/AttackDown 判断挥砍。
            string action = agent.GetCurrentAction(1).GetName();
            return !string.IsNullOrEmpty(action) && action.IndexOf("swing", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        internal static float RepeatInterval(int skill) => Math.Max(0.12f, 0.32f / (1f + skill / 200f));
        internal static float MissSpeed(int skill) => Math.Min(0.85f, 0.45f + 0.002f * skill);
        internal static float ShieldSpeed(float stacks) => 1f / (1f + 0.1f * Math.Max(0f, stacks));
        internal static int Quality(MissionWeapon weapon) => weapon.ItemModifier == null ? 0 :
            Math.Max(0, (int)weapon.ItemModifier.ItemQuality - (int)ItemQuality.Common);
        internal static float BreakAmount(MissionWeapon weapon) =>
            Math.Max(1, (int)weapon.Item.Tier + 1) + Quality(weapon);

        internal static float Armor(in AttackInformation info, in AttackCollisionData collision, float armor)
        {
            if (!Enabled || info.VictimAgent == null || collision.IsFallDamage || collision.IsHorseCharge ||
                collision.IsAlternativeAttack || collision.AttackBlockedWithShield || collision.CollidedWithShieldOnBack)
                return armor;
            var weapon = info.AttackerWeapon.CurrentUsageItem;
            armor = Math.Max(0f, armor - WeaponCombatMissionLogic.Current.ArmorLoss(info.VictimAgent, collision.VictimHitBodyPart));
            if (!collision.IsMissile && Hammer(weapon))
                armor = Math.Max(0f, armor - 10f - ((int)info.AttackerWeapon.Item.Tier + 1) - Quality(info.AttackerWeapon) -
                    (info.AttackerAgent?.Character?.GetBattleTier() ?? 0));
            if (!collision.IsMissile && Spear(weapon) && collision.StrikeType == (int)StrikeType.Thrust &&
                info.VictimAgent.GetBaseArmorEffectivenessForBodyPart(collision.VictimHitBodyPart) < LowArmorThreshold)
                armor = 0f;
            return armor;
        }

        internal static float DamageMultiplier(Agent agent, WeaponComponentData weapon, float value)
        {
            if (!Enabled || weapon == null || !weapon.IsMeleeWeapon) return value;
            // 斧的额外熟练增伤；锤不再提供低熟练度伤害补偿。
            if (Axe(weapon) || Spear(weapon) && CurrentStrike == StrikeType.Swing)
                value *= 1f + 0.003f * Skill(agent, weapon);
            MissionWeapon wielded = WeaponCombatMissionLogic.HeldWeapon(agent);
            if (!wielded.IsEmpty && wielded.Item.PrimaryWeapon.WeaponClass == WeaponClass.ThrowingAxe)
            {
                // 使用投掷斧的近战用法时，补齐模板为近战面板设置的折损；不改共享模板。
                int thrownPanel = wielded.Item.PrimaryWeapon.GetModifiedThrustDamage(wielded.ItemModifier);
                int meleePanel = Math.Max(wielded.GetModifiedSwingDamageForCurrentUsage(), wielded.GetModifiedThrustDamageForCurrentUsage());
                if (meleePanel > 0 && thrownPanel > meleePanel) value *= thrownPanel / (float)meleePanel;
            }
            return value;
        }

        internal static float FinalDamage(in AttackInformation info, in AttackCollisionData collision, float damage)
        {
            if (!Enabled || collision.IsAlternativeAttack || collision.IsFallDamage || collision.IsHorseCharge) return damage;
            var weapon = info.AttackerWeapon.CurrentUsageItem;
            if (!collision.IsMissile && collision.StrikeType == (int)StrikeType.Thrust && collision.VictimHitBodyPart == BoneBodyPartType.Neck)
                damage *= 1.25f;
            if (!collision.IsMissile && Spear(weapon) && collision.StrikeType == (int)StrikeType.Thrust &&
                (collision.VictimHitBodyPart == BoneBodyPartType.ArmLeft || collision.VictimHitBodyPart == BoneBodyPartType.ArmRight ||
                 collision.VictimHitBodyPart == BoneBodyPartType.Legs)) damage *= 0.75f;
            if (collision.IsMissile && weapon?.WeaponClass == WeaponClass.Dagger && info.VictimAgent?.IsMount == true) damage *= 1.5f;
            return damage;
        }

        internal static void Stats(Agent agent, AgentDrivenProperties properties)
        {
            if (!Enabled || agent == null || !agent.IsHuman) return;
            var weapon = WeaponCombatMissionLogic.HeldWeapon(agent).CurrentUsageItem;
            int skill = Skill(agent, weapon);
            if (Sword(weapon))
            {
                properties.SwingSpeedMultiplier *= 1f + 0.002f * skill;
                properties.ThrustOrRangedReadySpeedMultiplier *= 1f + 0.002f * skill;
                properties.HandlingMultiplier *= 1f + 0.002f * skill;
            }
            if (Spear(weapon)) properties.ThrustOrRangedReadySpeedMultiplier *= 1f + 0.003f * skill;
            if (WeaponCombatMissionLogic.Current.IsFootSlowed(agent))
            {
                properties.MaxSpeedMultiplier *= FootSpeedMultiplier;
                properties.CombatMaxSpeedMultiplier *= FootSpeedMultiplier;
            }
        }

        internal static float ShieldDamage(in AttackInformation info, float damage)
        {
            if (!Enabled) return damage;
            var w = info.AttackerWeapon.CurrentUsageItem;
            if (w?.WeaponClass == WeaponClass.TwoHandedAxe || Spear(w) && CurrentStrike == StrikeType.Swing) return damage * 2f;
            if (w?.WeaponClass == WeaponClass.Dagger) return damage * 0.5f;
            return damage;
        }

        internal static bool Crush(Agent attacker, bool nativeResult)
        {
            var weapon = WeaponCombatMissionLogic.HeldWeapon(attacker).CurrentUsageItem;
            if (!Enabled || nativeResult || !Hammer(weapon)) return nativeResult;
            return MBRandom.RandomFloat < Math.Min(0.50f, 0.10f + 0.0015f * Skill(attacker, weapon));
        }

        internal static float Momentum(Agent attacker, MissionWeapon weapon, bool crushed, float original, float native)
        {
            if (!Enabled || !crushed || !Hammer(weapon.CurrentUsageItem)) return native;
            return Math.Max(native, original * Math.Min(0.80f, 0.20f + 0.002f * Skill(attacker, weapon.CurrentUsageItem)));
        }

        internal static void Stun(WeaponComponentData weapon, StrikeType strike, ref float attackerStun)
        {
            if (!Enabled) return;
            if (strike == StrikeType.Thrust || Hammer(weapon)) attackerStun = 0f;
            else if (Sword(weapon)) attackerStun *= 0.5f;
        }

        internal static float ProcChance(Agent attacker, Agent victim, WeaponComponentData weapon)
        {
            // 此概率用于人类缴械/部位效果；存活的坐骑也可能进入命中队列。
            if (attacker == null || victim == null || !attacker.IsHuman || !victim.IsHuman ||
                !attacker.IsActive() || !victim.IsActive() || attacker.Mission == null) return 0f;
            int skillDifference = Skill(attacker, weapon) -
                Skill(victim, WeaponCombatMissionLogic.HeldWeapon(victim).CurrentUsageItem);
            int tierDifference = (attacker.Character?.GetBattleTier() ?? 0) - (victim.Character?.GetBattleTier() ?? 0);
            float chance = 0.10f + 0.001f * skillDifference + 0.05f * tierDifference;
            // 斧缴械只比较熟练度和阶级；不再受附近人数影响。
            if (Axe(weapon)) return Math.Max(0.05f, Math.Min(0.60f, chance));
            int allies = 0, enemies = 0;
            foreach (var agent in attacker.Mission.Agents)
            {
                if (!agent.IsHuman || !agent.IsActive() || (agent.Position - victim.Position).LengthSquared > 36f) continue;
                if (agent.Team == attacker.Team) allies++;
                else if (agent.Team == victim.Team) enemies++;
            }
            float ratio = Math.Max(1, allies) / (float)Math.Max(1, enemies);
            return Math.Max(0.05f, Math.Min(0.60f, chance +
                0.10f * (float)(Math.Log(ratio) / Math.Log(2))));
        }
    }
}
