using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 词缀系统 Mission 层行为。
    ///
    /// 职责：
    /// 1. 在 Agent 创建时，将当前 Agent 装备上的 ItemModifier 精确映射到 InstanceId
    /// 2. 为 NewDamageModel 提供按 Agent+槽位查询 InstanceId 的接口
    /// 3. Agent 移除/战斗结束时清理缓存
    ///
    /// 关键规则：
    /// - 战斗实例识别只允许走 ItemModifier → InstanceId 或 Hero BindingMap。
    /// - 不再使用 BaseItemId 回退，以避免同模板不同实例串词缀。
    /// - Agent 装备槽重新绑定时会清理已经失效的槽位缓存。
    /// </summary>
    public class AffixMissionBehavior : MissionLogic
    {
        /// <summary>
        /// Agent.Index → AgentAffixContext 缓存。
        /// static 是因为 NewDamageModel 需要通过静态方法访问。
        /// </summary>
        private static readonly Dictionary<int, AgentAffixContext> _agentAffixCache
            = new Dictionary<int, AgentAffixContext>();

        /// <summary>
        /// 根据 Agent 和装备槽获取词缀物品的 InstanceId。
        /// 未绑定时返回 null。
        /// </summary>
        public static string? GetAgentWeaponInstanceId(Agent? agent, EquipmentIndex slot)
        {
            if (agent == null) return null;

            lock (_agentAffixCache)
            {
                if (_agentAffixCache.TryGetValue(agent.Index, out var ctx))
                    return ctx.GetInstanceId(slot);
            }
            return null;
        }

        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);

            if (!agent.IsHuman || agent.IsMount)
                return;

            if (agent.SpawnEquipment == null)
            {
                lock (_agentAffixCache)
                {
                    _agentAffixCache[agent.Index] = new AgentAffixContext();
                }
                return;
            }

            BindAgentEquipmentSlots(agent);
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (!agent.IsHuman || agent.IsMount)
                return;

            BindAgentEquipmentSlots(agent);
        }

        private void BindAgentEquipmentSlots(Agent agent)
        {
            var campaignBehavior = AffixCampaignBehavior.Current;
            var characterObject = agent.Character as CharacterObject;
            var hero = characterObject?.HeroObject;

            AgentAffixContext ctx;
            lock (_agentAffixCache)
            {
                if (!_agentAffixCache.TryGetValue(agent.Index, out ctx))
                    ctx = new AgentAffixContext();
            }

            for (int i = 0; i <= (int)EquipmentIndex.HorseHarness; i++)
            {
                var slot = (EquipmentIndex)i;
                var element = agent.SpawnEquipment[slot];

                // 每次重新扫描都先清理该槽位，避免换装后继续使用旧 InstanceId。
                ctx.SlotToInstanceId.Remove(slot);

                if (element.IsEmpty || element.Item == null)
                    continue;

                string? instanceId = ResolveInstanceId(
                    campaignBehavior,
                    hero,
                    slot,
                    element);

                if (!string.IsNullOrEmpty(instanceId))
                    ctx.SlotToInstanceId[slot] = instanceId;
            }

            lock (_agentAffixCache)
            {
                _agentAffixCache[agent.Index] = ctx;
            }
        }

        public override void OnAgentRemoved(
            Agent affectedAgent,
            Agent affectorAgent,
            AgentState agentState,
            KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);

            if (affectedAgent != null)
            {
                lock (_agentAffixCache)
                {
                    _agentAffixCache.Remove(affectedAgent.Index);
                }
            }
        }

        protected override void OnEndMission()
        {
            base.OnEndMission();

            lock (_agentAffixCache)
            {
                _agentAffixCache.Clear();
            }
        }

        /// <summary>
        /// 精确解析当前 Agent 装备槽对应的 InstanceId。
        /// 优先级：Agent 当前 ItemModifier → Hero BindingMap → null。
        /// 不再按 BaseItemId 回退。
        /// </summary>
        private static string? ResolveInstanceId(
            AffixCampaignBehavior behavior,
            Hero hero,
            EquipmentIndex slot,
            EquipmentElement element)
        {
            if (element.ItemModifier != null)
            {
                string modifierId = element.ItemModifier.StringId;
                if (!string.IsNullOrEmpty(modifierId) &&
                    behavior != null &&
                    behavior.ModifierToInstanceMap.TryGetValue(modifierId, out string? modifierInstanceId) &&
                    !string.IsNullOrEmpty(modifierInstanceId))
                {
                    return modifierInstanceId;
                }
            }

            if (hero != null && behavior != null)
            {
                string? boundInstanceId = behavior.GetEquippedInstanceId(hero, slot);
                if (!string.IsNullOrEmpty(boundInstanceId))
                    return boundInstanceId;
            }

            return null;
        }
    }
}
