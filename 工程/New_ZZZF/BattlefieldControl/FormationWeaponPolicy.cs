using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.BattlefieldControl
{
    [Flags]
    internal enum ControlledWeaponKind
    {
        None = 0, Sword = 1, Axe = 2, Mace = 4, Polearm = 8,
        Bow = 16, Crossbow = 32, Throwing = 64, Shield = 128, OtherRanged = 256
    }

    internal sealed class FormationWeaponPolicy
    {
        internal readonly ControlledWeaponKind Allowed;
        internal readonly ControlledWeaponKind Denied;
        internal FormationWeaponPolicy(ControlledWeaponKind allowed, ControlledWeaponKind denied)
        {
            Allowed = allowed;
            Denied = denied;
        }

        internal static ControlledWeaponKind Kind(WeaponComponentData usage)
        {
            if (usage == null || usage.IsAmmo) return ControlledWeaponKind.None;
            switch (usage.WeaponClass)
            {
                case WeaponClass.Dagger:
                case WeaponClass.OneHandedSword:
                case WeaponClass.TwoHandedSword: return ControlledWeaponKind.Sword;
                case WeaponClass.OneHandedAxe:
                case WeaponClass.TwoHandedAxe: return ControlledWeaponKind.Axe;
                case WeaponClass.Mace:
                case WeaponClass.TwoHandedMace:
                case WeaponClass.Pick: return ControlledWeaponKind.Mace;
                case WeaponClass.OneHandedPolearm:
                case WeaponClass.TwoHandedPolearm:
                case WeaponClass.LowGripPolearm: return ControlledWeaponKind.Polearm;
                case WeaponClass.Bow: return ControlledWeaponKind.Bow;
                case WeaponClass.Crossbow: return ControlledWeaponKind.Crossbow;
                case WeaponClass.Javelin:
                case WeaponClass.ThrowingAxe:
                case WeaponClass.ThrowingKnife:
                case WeaponClass.Stone: return ControlledWeaponKind.Throwing;
                case WeaponClass.SmallShield:
                case WeaponClass.LargeShield: return ControlledWeaponKind.Shield;
                default: return usage.IsRangedWeapon ? ControlledWeaponKind.OtherRanged : ControlledWeaponKind.None;
            }
        }

        private bool Allows(WeaponComponentData usage, bool applyWhitelist)
        {
            ControlledWeaponKind kind = Kind(usage);
            // Noncombat items (e.g. the formation banner) are not weapon restrictions.
            if (kind == ControlledWeaponKind.None) return true;
            if ((Denied & kind) != 0) return false;
            // Shields are independent: allowing swords does not implicitly ban shields.
            if (kind == ControlledWeaponKind.Shield)
                return true;
            return !applyWhitelist || (Allowed & kind) != 0;
        }

        private static bool Usable(Agent agent, EquipmentIndex slot, MissionWeapon weapon, WeaponComponentData usage)
        {
            if (usage.IsShield) return weapon.HitPoints > 0;
            if (usage.IsRangedWeapon)
            {
                if (usage.IsConsumable) return weapon.Amount > 0;
                return agent.Equipment.HasAmmo(slot, out _, out _, out _);
            }
            return usage.IsMeleeWeapon;
        }

        private bool CarriesAllowedWeapon(Agent agent)
        {
            if (Allowed == ControlledWeaponKind.None) return false;
            // Whitelist affects soldiers carrying a selected kind; other soldiers keep
            // their equipment and native behavior, rather than becoming unarmed.
            for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty) continue;
                for (int u = 0; u < weapon.WeaponsCount; u++)
                    if ((Allowed & Kind(weapon.GetWeaponComponentDataForUsage(u))) != 0) return true;
            }
            return false;
        }

        internal bool Affects(Agent agent)
        {
            if (CarriesAllowedWeapon(agent)) return true;
            if (Denied == ControlledWeaponKind.None) return false;
            for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon weapon = agent.Equipment[(EquipmentIndex)i];
                if (weapon.IsEmpty) continue;
                for (int u = 0; u < weapon.WeaponsCount; u++)
                    if ((Denied & Kind(weapon.GetWeaponComponentDataForUsage(u))) != 0) return true;
            }
            return false;
        }

        private void EnsureShield(Agent agent, WeaponComponentData mainUsage)
        {
            // Automatic equipment input is suppressed while we own selection, so also
            // maintain the offhand. Preserve banners and other existing offhand items.
            if (mainUsage == null || !mainUsage.IsMeleeWeapon ||
                (mainUsage.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0 ||
                (Denied & ControlledWeaponKind.Shield) != 0 ||
                agent.GetOffhandWieldedItemIndex() != EquipmentIndex.None) return;
            for (int i = 0; i < (int)EquipmentIndex.NumAllWeaponSlots; i++)
            {
                EquipmentIndex slot = (EquipmentIndex)i;
                MissionWeapon weapon = agent.Equipment[slot];
                if (!weapon.IsEmpty && weapon.CurrentUsageItem != null &&
                    weapon.CurrentUsageItem.IsShield && weapon.HitPoints > 0)
                {
                    agent.TryToWieldWeaponInSlot(slot, Agent.WeaponWieldActionType.Instant, false);
                    return;
                }
            }
        }

        internal void Apply(Agent agent)
        {
            bool applyWhitelist = CarriesAllowedWeapon(agent);
            if (!applyWhitelist && Denied == ControlledWeaponKind.None) return;
            EquipmentIndex current = agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex offhand = agent.GetOffhandWieldedItemIndex();
            if (offhand != EquipmentIndex.None && !Allows(agent.Equipment[offhand].CurrentUsageItem, applyWhitelist))
                agent.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);

            if (current != EquipmentIndex.None)
            {
                MissionWeapon held = agent.Equipment[current];
                if (!held.IsEmpty && held.CurrentUsageItem != null)
                {
                    if (Kind(held.CurrentUsageItem) == ControlledWeaponKind.None) return;
                    if (Allows(held.CurrentUsageItem, applyWhitelist) && Usable(agent, current, held, held.CurrentUsageItem))
                    {
                        EnsureShield(agent, held.CurrentUsageItem);
                        return;
                    }
                }
            }

            EquipmentIndex best = EquipmentIndex.None;
            int bestUsage = -1;
            // Prefer an allowed alternate usage on the current weapon, then other slots.
            for (int pass = -1; pass < (int)EquipmentIndex.NumAllWeaponSlots; pass++)
            {
                EquipmentIndex slot = pass < 0 ? current : (EquipmentIndex)pass;
                if (slot == EquipmentIndex.None) continue;
                MissionWeapon weapon = agent.Equipment[slot];
                if (weapon.IsEmpty) continue;
                for (int u = 0; u < weapon.WeaponsCount; u++)
                {
                    WeaponComponentData usage = weapon.GetWeaponComponentDataForUsage(u);
                    if (Kind(usage) == ControlledWeaponKind.None || usage.IsShield ||
                        !Allows(usage, applyWhitelist) || !Usable(agent, slot, weapon, usage)) continue;
                    best = slot;
                    bestUsage = u;
                    break;
                }
                if (best != EquipmentIndex.None) break;
            }
            if (best == EquipmentIndex.None)
            {
                if (current != EquipmentIndex.None)
                    agent.TryToSheathWeaponInHand(Agent.HandIndex.MainHand, Agent.WeaponWieldActionType.Instant);
                return;
            }
            MissionWeapon next = agent.Equipment[best];
            WeaponComponentData nextUsage = next.GetWeaponComponentDataForUsage(bestUsage);
            if ((nextUsage.WeaponFlags & WeaponFlags.NotUsableWithOneHand) != 0 && offhand != EquipmentIndex.None)
                agent.TryToSheathWeaponInHand(Agent.HandIndex.OffHand, Agent.WeaponWieldActionType.Instant);
            if (next.CurrentUsageIndex != bestUsage)
            {
                // Native usage setter, also synchronize the managed MissionEquipment cache.
                agent.SetUsageIndexOfWeaponInSlotAsClient(best, bestUsage);
                agent.Equipment.SetUsageIndexOfSlot(best, bestUsage);
            }
            if (current != best)
                agent.TryToWieldWeaponInSlot(best, Agent.WeaponWieldActionType.Instant, false);
            EnsureShield(agent, nextUsage);
        }
    }
}
