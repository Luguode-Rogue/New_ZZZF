using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>仅当前 Agent 的临时用法；共享 ItemObject/WeaponComponentData 不发生修改。</summary>
    internal sealed class ThrustSwingWeaponLease
    {
        private static readonly FieldInfo UsagesField = typeof(MissionWeapon).GetField("_weapons", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<int, ThrustSwingWeaponLease> Active = new Dictionary<int, ThrustSwingWeaponLease>();

        // 只对正在使用临时副本的武器切换伤害通道，其他攻击不受影响。
        internal static WeaponComponentData GetOriginalUsage(Agent agent)
        {
            if (agent == null || !Active.TryGetValue(agent.Index, out ThrustSwingWeaponLease lease)) return null;
            return agent.GetPrimaryWieldedItemIndex() == lease._slot &&
                ReferenceEquals(agent.WieldedWeapon.CurrentUsageItem, lease._runtimeUsage)
                ? lease.OriginalUsage : null;
        }

        private readonly Agent _agent;
        private readonly EquipmentIndex _slot;
        private readonly MissionWeapon _original;
        private readonly WeaponComponentData _runtimeUsage;
        private bool _restored;
        internal WeaponComponentData OriginalUsage => _original.CurrentUsageItem;

        private ThrustSwingWeaponLease(Agent agent, EquipmentIndex slot, MissionWeapon original, WeaponComponentData runtimeUsage)
        { _agent = agent; _slot = slot; _original = original; _runtimeUsage = runtimeUsage; }

        private static void Set(WeaponComponentData usage, string property, object value)
        {
            PropertyInfo info = typeof(WeaponComponentData).GetProperty(property);
            MethodInfo setter = info?.GetSetMethod(true);
            if (setter == null) throw new InvalidOperationException("无法构建临时武器用法：" + property);
            setter.Invoke(usage, new[] { value });
        }

        internal static bool TryApply(Agent agent, MissionWeapon original, out ThrustSwingWeaponLease lease, out string reason)
        {
            lease = null;
            reason = null;
            EquipmentIndex slot = agent.GetPrimaryWieldedItemIndex();
            if (slot == EquipmentIndex.None || UsagesField == null || CloneMethod == null) {
                reason = "无法创建临时右横扫武器用法。"; return false;
            }
            if (Active.TryGetValue(agent.Index, out ThrustSwingWeaponLease previous)) {
                if (GetOriginalUsage(agent) != null) {
                    reason = "已有技能正在使用临时挥砍武器用法。"; return false;
                }
                previous.Restore();
            }
            WeaponComponentData source = original.CurrentUsageItem;
            WeaponComponentData copy;
            MissionWeapon runtime;
            try {
                copy = (WeaponComponentData)CloneMethod.Invoke(source, null);
                bool polearm = source.WeaponClass == WeaponClass.OneHandedPolearm ||
                    source.WeaponClass == WeaponClass.TwoHandedPolearm || source.WeaponClass == WeaponClass.LowGripPolearm;
                EquipmentIndex offhand = agent.GetOffhandWieldedItemIndex();
                bool hasShield = offhand != EquipmentIndex.None && agent.Equipment[offhand].CurrentUsageItem?.IsShield == true;
                string itemUsage = source.IsTwoHanded
                    ? (polearm ? "polearm_block_swing_thrust" : "twohanded_block_swing_thrust")
                    : (hasShield ? "onehanded_block_shield_swing_thrust" : "onehanded_block_swing_thrust");
                if (MBItem.GetItemUsageIndex(itemUsage) < 0) {
                    reason = "原版右横扫武器用法不存在。"; return false;
                }
                Set(copy, nameof(WeaponComponentData.ItemUsage), itemUsage);
                Set(copy, nameof(WeaponComponentData.SwingDamage), source.ThrustDamage);
                Set(copy, nameof(WeaponComponentData.SwingDamageType), source.ThrustDamageType);
                Set(copy, nameof(WeaponComponentData.SwingSpeed), Math.Max(1, source.ThrustSpeed));
                Set(copy, nameof(WeaponComponentData.SwingDamageFactor), source.ThrustDamageFactor);
                copy.WeaponFlags = source.WeaponFlags | WeaponFlags.MeleeWeapon;
                MissionWeapon ammo = original.AmmoWeapon;
                runtime = new MissionWeapon(original.Item, original.ItemModifier, original.Banner,
                    original.Amount, original.ReloadPhase, ammo.IsEmpty ? (MissionWeapon?)null : ammo);
                runtime.CurrentUsageIndex = original.CurrentUsageIndex;
                object boxedRuntime = runtime;
                typeof(MissionWeapon).GetProperty(nameof(MissionWeapon.GlossMultiplier)).GetSetMethod(true)
                    .Invoke(boxedRuntime, new object[] { original.GlossMultiplier });
                runtime = (MissionWeapon)boxedRuntime;
                // 公共构造函数创建新的 usages 列表，替换列表元素不会污染物品模板。
                var usages = (List<WeaponComponentData>)UsagesField.GetValue(runtime);
                usages[runtime.CurrentUsageIndex] = copy;
            } catch (Exception) {
                reason = "当前版本不支持临时武器用法适配。"; return false;
            }
            lease = new ThrustSwingWeaponLease(agent, slot, original, copy);
            Active[agent.Index] = lease;
            EquipPreservingHands(agent, slot, ref runtime);
            if (agent.GetPrimaryWieldedItemIndex() != slot ||
                !ReferenceEquals(agent.WieldedWeapon.CurrentUsageItem, copy)) {
                lease.Restore();
                lease = null;
                reason = "当前姿态不能持握临时右横扫武器用法。";
                return false;
            }
            return true;
        }

        // 替换装备实体可能解除该槽位的持握；装备与持握是两个原生操作。
        // 保存替换当下的双手状态，结束时不会强制切回施法前的其他武器。
        private static void EquipPreservingHands(Agent agent, EquipmentIndex slot, ref MissionWeapon weapon)
        {
            EquipmentIndex mainHand = agent.GetPrimaryWieldedItemIndex();
            EquipmentIndex offHand = agent.GetOffhandWieldedItemIndex();
            agent.EquipWeaponWithNewEntity(slot, ref weapon);
            // 原版 WieldInitialWeapons 同样先处理副手，再处理主手。
            if (offHand != EquipmentIndex.None && agent.GetOffhandWieldedItemIndex() != offHand)
                agent.TryToWieldWeaponInSlot(offHand, Agent.WeaponWieldActionType.InstantAfterPickUp, false);
            if (mainHand != EquipmentIndex.None && agent.GetPrimaryWieldedItemIndex() != mainHand)
                agent.TryToWieldWeaponInSlot(mainHand, Agent.WeaponWieldActionType.InstantAfterPickUp, false);
        }

        internal void Restore()
        {
            if (_restored) return;
            _restored = true;
            if (_agent != null && Active.TryGetValue(_agent.Index, out ThrustSwingWeaponLease active) && ReferenceEquals(active, this))
                Active.Remove(_agent.Index);
            if (_agent == null || !_agent.IsActive()) return;
            MissionWeapon current = _agent.Equipment[_slot];
            if (current.IsEmpty || current.Item != _original.Item) return;
            bool hasCopy = false;
            for (int i = 0; i < current.WeaponsCount; i++)
                hasCopy |= ReferenceEquals(current.GetWeaponComponentDataForUsage(i), _runtimeUsage);
            if (!hasCopy) return;
            MissionWeapon restored = _original;
            restored.CurrentUsageIndex = current.CurrentUsageIndex;
            EquipPreservingHands(_agent, _slot, ref restored);
        }
    }
}