using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Systems.BattleEquipment
{
    internal sealed class BattleEquipmentSession
    {
        private static readonly EquipmentIndex[] EditableSlots =
        {
            EquipmentIndex.WeaponItemBeginSlot,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3,
            EquipmentIndex.NumAllWeaponSlots,
            EquipmentIndex.Body,
            EquipmentIndex.Leg,
            EquipmentIndex.Gloves,
            EquipmentIndex.Cape
        };

        private readonly Agent _player;
        private readonly Agent _target;
        private readonly Dictionary<string, ItemSnapshot> _items = new Dictionary<string, ItemSnapshot>();

        public string Id { get; } = Guid.NewGuid().ToString("N");
        public int Revision { get; private set; } = 1;

        public BattleEquipmentSession(Agent player, Agent target)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _target = target ?? throw new ArgumentNullException(nameof(target));
            CaptureSide("player", player);
            CaptureSide("target", target);
        }

        public object BuildState()
        {
            return new
            {
                sessionId = Id,
                revision = Revision,
                title = "战场装备交换",
                player = BuildSide("player", _player),
                target = BuildSide("target", _target),
                slots = EditableSlots.Select(s => new { index = (int)s, name = GetSlotName(s), kind = (int)s < 4 ? "weapon" : "armor" }).ToArray()
            };
        }

        public object Commit(JToken payload)
        {
            if (!_player.IsActive() || !_target.IsActive())
                return Failure("角色状态已变化，无法交换装备。");
            if (!string.Equals(payload?["sessionId"]?.Value<string>(), Id, StringComparison.Ordinal))
                return Failure("换装会话已失效。");
            if ((payload?["revision"]?.Value<int>() ?? -1) != Revision)
                return Failure("装备数据版本已变化，请重新打开界面。");

            JArray assignments = payload?["assignments"] as JArray;
            if (assignments == null || assignments.Count != EditableSlots.Length * 2)
                return Failure("提交的数据不完整。");

            var destinations = new HashSet<string>(StringComparer.Ordinal);
            var usedTokens = new HashSet<string>(StringComparer.Ordinal);
            var parsed = new List<Assignment>();
            foreach (JToken row in assignments)
            {
                string side = row?["side"]?.Value<string>();
                int slotValue = row?["slot"]?.Value<int>() ?? -1;
                string token = row?["token"]?.Type == JTokenType.Null ? null : row?["token"]?.Value<string>();
                if ((side != "player" && side != "target") || !EditableSlots.Any(s => (int)s == slotValue))
                    return Failure("目标槽位非法。");
                if (!destinations.Add(side + ":" + slotValue))
                    return Failure("目标槽位重复。");

                ItemSnapshot item = null;
                if (!string.IsNullOrEmpty(token))
                {
                    if (!_items.TryGetValue(token, out item) || !usedTokens.Add(token))
                        return Failure("装备令牌无效或被重复使用。");
                    if (!Equipment.IsItemFitsToSlot((EquipmentIndex)slotValue, item.Item))
                        return Failure("“" + item.Name + "”不能放入该槽位。");
                }
                parsed.Add(new Assignment(side, (EquipmentIndex)slotValue, item));
            }

            if (usedTokens.Count != _items.Count)
                return Failure("必须为所有装备选择一个目标槽位。");

            Equipment playerOriginal = new Equipment(_player.SpawnEquipment);
            Equipment targetOriginal = new Equipment(_target.SpawnEquipment);
            MissionWeapon[] playerWeapons = SnapshotWeapons(_player);
            MissionWeapon[] targetWeapons = SnapshotWeapons(_target);

            try
            {
                ApplyAssignments(_player, playerOriginal, playerWeapons, parsed.Where(x => x.Side == "player"));
                ApplyAssignments(_target, targetOriginal, targetWeapons, parsed.Where(x => x.Side == "target"));
                AffixMissionBehavior.RefreshAgentEquipmentBindings(_player);
                AffixMissionBehavior.RefreshAgentEquipmentBindings(_target);
                Revision++;
                return new { ok = true, message = "装备交换完成。" };
            }
            catch (Exception ex)
            {
                Restore(_player, playerOriginal, playerWeapons);
                Restore(_target, targetOriginal, targetWeapons);
                return Failure("装备交换失败，已恢复原装备：" + ex.Message);
            }
        }

        private void CaptureSide(string side, Agent agent)
        {
            foreach (EquipmentIndex slot in EditableSlots)
            {
                if ((int)slot < 4)
                {
                    MissionWeapon weapon = agent.Equipment[slot];
                    if (!weapon.IsEmpty)
                        AddSnapshot(side, slot, weapon.Item, new EquipmentElement(weapon.Item, weapon.ItemModifier), weapon);
                }
                else
                {
                    EquipmentElement element = agent.SpawnEquipment[slot];
                    if (!element.IsEmpty)
                        AddSnapshot(side, slot, element.Item, new EquipmentElement(element), MissionWeapon.Invalid);
                }
            }
        }

        private void AddSnapshot(string side, EquipmentIndex slot, ItemObject item, EquipmentElement element, MissionWeapon weapon)
        {
            string token = side + "-" + (int)slot;
            _items[token] = new ItemSnapshot(token, item?.Name?.ToString() ?? item?.StringId ?? "未知装备", item, element, weapon, (int)slot < 4);
        }

        private object BuildSide(string side, Agent agent)
        {
            return new
            {
                id = side,
                name = agent.Name ?? (side == "player" ? "玩家" : "友军"),
                entries = EditableSlots.Select(slot =>
                {
                    string token = side + "-" + (int)slot;
                    _items.TryGetValue(token, out ItemSnapshot item);
                    return new
                    {
                        slot = (int)slot,
                        token = item?.Token,
                        item = item == null ? null : new
                        {
                            name = item.Name,
                            type = item.Item.ItemType.ToString(),
                            modifier = item.Element.ItemModifier?.Name?.ToString() ?? string.Empty,
                            amount = item.IsWeapon ? item.Weapon.Amount : 0,
                            maxAmount = item.IsWeapon ? item.Weapon.ModifiedMaxAmount : 0,
                            tier = item.Item.Tierf,
                            weight = item.Element.Weight
                        }
                    };
                }).ToArray()
            };
        }

        private static void ApplyAssignments(Agent agent, Equipment original, MissionWeapon[] originalWeapons, IEnumerable<Assignment> assignments)
        {
            Equipment next = new Equipment(original);
            MissionWeapon[] weapons = (MissionWeapon[])originalWeapons.Clone();
            foreach (Assignment assignment in assignments)
            {
                if ((int)assignment.Slot < 4)
                {
                    weapons[(int)assignment.Slot] = assignment.Item?.Weapon ?? MissionWeapon.Invalid;
                    next[assignment.Slot] = assignment.Item?.Element ?? EquipmentElement.Invalid;
                }
                else
                {
                    next[assignment.Slot] = assignment.Item?.Element ?? EquipmentElement.Invalid;
                }
            }

            agent.UpdateSpawnEquipmentAndRefreshVisuals(next);
            RestoreWeapons(agent, weapons);
            agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
        }

        private static void Restore(Agent agent, Equipment equipment, MissionWeapon[] weapons)
        {
            try
            {
                agent.UpdateSpawnEquipmentAndRefreshVisuals(equipment);
                RestoreWeapons(agent, weapons);
                agent.WieldInitialWeapons(Agent.WeaponWieldActionType.InstantAfterPickUp, Equipment.InitialWeaponEquipPreference.Any);
                AffixMissionBehavior.RefreshAgentEquipmentBindings(agent);
            }
            catch { }
        }

        private static MissionWeapon[] SnapshotWeapons(Agent agent)
        {
            var result = new MissionWeapon[5];
            for (int i = 0; i < result.Length; i++) result[i] = agent.Equipment[(EquipmentIndex)i];
            return result;
        }

        private static void RestoreWeapons(Agent agent, MissionWeapon[] weapons)
        {
            for (int i = 0; i < weapons.Length; i++) agent.RemoveEquippedWeapon((EquipmentIndex)i);
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].IsEmpty) continue;
                MissionWeapon weapon = weapons[i];
                agent.EquipWeaponWithNewEntity((EquipmentIndex)i, ref weapon);
            }
        }

        private static object Failure(string message) => new { ok = false, message };

        private static string GetSlotName(EquipmentIndex slot)
        {
            switch (slot)
            {
                case EquipmentIndex.WeaponItemBeginSlot: return "武器 1";
                case EquipmentIndex.Weapon1: return "武器 2";
                case EquipmentIndex.Weapon2: return "武器 3";
                case EquipmentIndex.Weapon3: return "武器 4";
                case EquipmentIndex.NumAllWeaponSlots: return "头部";
                case EquipmentIndex.Body: return "身体";
                case EquipmentIndex.Leg: return "腿部";
                case EquipmentIndex.Gloves: return "手部";
                case EquipmentIndex.Cape: return "披风";
                default: return slot.ToString();
            }
        }

        private sealed class ItemSnapshot
        {
            public string Token { get; }
            public string Name { get; }
            public ItemObject Item { get; }
            public EquipmentElement Element { get; }
            public MissionWeapon Weapon { get; }
            public bool IsWeapon { get; }

            public ItemSnapshot(string token, string name, ItemObject item, EquipmentElement element, MissionWeapon weapon, bool isWeapon)
            {
                Token = token;
                Name = name;
                Item = item;
                Element = element;
                Weapon = weapon;
                IsWeapon = isWeapon;
            }
        }

        private sealed class Assignment
        {
            public string Side { get; }
            public EquipmentIndex Slot { get; }
            public ItemSnapshot Item { get; }
            public Assignment(string side, EquipmentIndex slot, ItemSnapshot item) { Side = side; Slot = slot; Item = item; }
        }
    }
}
