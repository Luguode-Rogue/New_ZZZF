using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.GUI
{
    /// <summary>
    /// 战场状态 HUD 的纯数据构建器。
    /// 不负责页面、输入或窗口生命周期；这些职责由 BannerlordHtmlUI Framework 管理。
    /// </summary>
    internal static class MissionAgentStatusHtmlState
    {
        private static readonly FieldInfo SelectedSpellSlotField =
            typeof(AgentSkillComponent).GetField(
                "_selectedSpellSlot",
                BindingFlags.Instance | BindingFlags.NonPublic);

        public static object Build(Agent agent)
        {
            AgentSkillComponent component = agent == null
                ? null
                : Script.GetActiveComponents(agent);

            if (agent == null || component == null)
            {
                return new
                {
                    active = false,
                    alive = false,
                    heroName = string.Empty,
                    health = 0f,
                    maxHealth = 0f,
                    mana = 0f,
                    maxMana = 100f,
                    stamina = 0f,
                    maxStamina = 100f,
                    shield = 0f,
                    resurgence = 0,
                    globalCooldown = 0f,
                    combatArtReady = false,
                    selectedSpellSlot = -1,
                    selectedSpell = (object)null,
                    skills = new List<object>()
                };
            }

            int selectedSlot = GetSelectedSpellSlot(component);
            SkillBase selectedSpell = selectedSlot >= 0 && selectedSlot < component.SpellSlots.Length
                ? component.SpellSlots[selectedSlot]
                : null;

            return new
            {
                active = true,
                alive = agent.IsActive(),
                heroName = agent.Character?.Name?.ToString() ?? string.Empty,
                health = Math.Max(0f, agent.Health),
                maxHealth = Math.Max(1f, component.MaxHP),
                mana = ClampResource(component._currentMana),
                maxMana = 100f,
                stamina = ClampResource(component._currentStamina),
                maxStamina = 100f,
                shield = Math.Max(0f, component._shieldStrength),
                resurgence = component._lifeResurgenceCount,
                globalCooldown = Math.Max(0f, component._globalCooldownTimer),
                combatArtReady = !component._isInCombatArtState,
                selectedSpellSlot = selectedSlot,
                selectedSpell = BuildSkill(component, selectedSpell),
                skills = BuildSkills(component)
            };
        }

        private static int GetSelectedSpellSlot(AgentSkillComponent component)
        {
            if (SelectedSpellSlotField == null)
                return 0;

            try
            {
                return Math.Max(0, Math.Min(3, (int)SelectedSpellSlotField.GetValue(component)));
            }
            catch
            {
                return 0;
            }
        }

        private static List<object> BuildSkills(AgentSkillComponent component)
        {
            var result = new List<object>(8)
            {
                BuildSlot(component, "main", "主技能", component.MainActiveSkill),
                BuildSlot(component, "sub", "副技能", component.SubActiveSkill),
                BuildSlot(component, "passive", "被动", component.PassiveSkill),
                BuildSlot(component, "combatArt", "战技", component.CombatArtSkill)
            };

            for (int i = 0; i < component.SpellSlots.Length; i++)
                result.Add(BuildSlot(component, "spell" + i, "法术 " + (i + 1), component.SpellSlots[i]));

            return result;
        }

        private static object BuildSlot(
            AgentSkillComponent component,
            string key,
            string slotName,
            SkillBase skill)
        {
            return new
            {
                key,
                slot = slotName,
                skill = BuildSkill(component, skill)
            };
        }

        private static object BuildSkill(AgentSkillComponent component, SkillBase skill)
        {
            if (skill == null || string.Equals(skill.SkillID, "NullSkill", StringComparison.OrdinalIgnoreCase))
                return new
                {
                    equipped = false,
                    id = string.Empty,
                    name = "空",
                    type = string.Empty,
                    cost = 0f,
                    cooldown = 0f,
                    remaining = 0f,
                    description = string.Empty,
                    ready = false
                };

            component._cooldownTimers.TryGetValue(skill, out float remaining);
            remaining = Math.Max(0f, remaining);

            string name = skill.Text == null ? skill.SkillID : skill.Text.ToString();
            if (string.IsNullOrWhiteSpace(name))
                name = skill.SkillID;

            return new
            {
                equipped = true,
                id = skill.SkillID ?? string.Empty,
                name,
                type = skill.Type.ToString(),
                cost = Math.Max(0f, skill.ResourceCost),
                cooldown = Math.Max(0f, skill.Cooldown),
                remaining,
                description = skill.Description == null ? string.Empty : skill.Description.ToString(),
                ready = remaining <= 0.01f
            };
        }

        private static float ClampResource(float value)
        {
            if (value < 0f) return 0f;
            if (value > 100f) return 100f;
            return value;
        }
    }
}
