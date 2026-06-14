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
    /// 1. 在 Agent 创建时，将 Campaign 层的"Hero装备槽 → InstanceId"绑定复制到 Mission 层缓存
    /// 2. 为 NewDamageModel 提供按 Agent+槽位 查询 InstanceId 的接口
    /// 3. Agent 移除/战斗结束时清理缓存
    ///
    /// 设计要点：
    /// - 缓存是 Mission 层的"快照"，战斗中途换装需后续通过装备变化事件刷新
    /// - 非 Hero Agent（强盗/NPC等）无绑定，查询返回 null → 走模板回退
    /// - InstanceId 为 null 时，GetAffixDamageMultiplier 内置模板回退逻辑，不会崩溃
    /// </summary>
    public class AffixMissionBehavior : MissionLogic
    {
        /// <summary>
        /// Agent.Index → AgentAffixContext 缓存。
        /// static 是因为 NewDamageModel 需要通过静态方法访问。
        /// </summary>
        private static readonly Dictionary<int, AgentAffixContext> _agentAffixCache
            = new Dictionary<int, AgentAffixContext>();

        // ========== 公开查询接口（供 NewDamageModel 使用） ==========

        /// <summary>
        /// 根据 Agent 和装备槽获取词缀物品的 InstanceId。
        /// 未绑定时返回 null，调用方应回退到模板查找。
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

        // ========== MissionLogic 生命周期 ==========

        /// <summary>
        /// OnAgentCreated 在 InitializeSpawnEquipment 之前触发，
        /// 此时 agent.SpawnEquipment 为 null（详见 Mission.SpawnAgent 时序）。
        /// 因此只做基础过滤，将装备槽绑定延迟到 OnAgentBuild。
        /// </summary>
        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);

            if (!agent.IsHuman || agent.IsMount)
                return;

            // SpawnEquipment 此时尚未初始化，仅注册空上下文占位
            if (agent.SpawnEquipment == null)
            {
                var ctx = new AgentAffixContext();
                lock (_agentAffixCache)
                {
                    _agentAffixCache[agent.Index] = ctx;
                }
                return;
            }

            // 如果 SpawnEquipment 已可用（极少情况），走完整绑定
            BindAgentEquipmentSlots(agent);
        }

        /// <summary>
        /// OnAgentBuild 在 InitializeSpawnEquipment 之后触发，
        /// 此时 SpawnEquipment 已完整可用。
        /// 对已在 OnAgentCreated 中注册了空上下文的 agent 进行装备槽绑定。
        /// </summary>
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            if (!agent.IsHuman || agent.IsMount)
                return;

            BindAgentEquipmentSlots(agent);
        }

        /// <summary>
        /// 遍历 Agent 的 SpawnEquipment 全部槽位（武器+护甲+马匹），为词缀物品绑定 InstanceId。
        /// 空上下文或非 Hero 的绑定结果存储在 _agentAffixCache 中。
        /// </summary>
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

            if (hero != null && campaignBehavior != null)
            {
                // 遍历全部装备槽位：Weapon0~Weapon3 + ExtraWeaponSlot + Head/Body/Leg/Gloves/Cape + Horse/HorseHarness
                for (int i = 0; i <= (int)EquipmentIndex.HorseHarness; i++)
                {
                    var slot = (EquipmentIndex)i;
                    var element = agent.SpawnEquipment[slot];

                    if (element.IsEmpty || element.Item == null)
                        continue;

                    string? instanceId = ResolveInstanceId(campaignBehavior, hero, slot,
                        element.Item.StringId);

                    if (!string.IsNullOrEmpty(instanceId))
                        ctx.SlotToInstanceId[slot] = instanceId;
                }
            }

            lock (_agentAffixCache)
            {
                _agentAffixCache[agent.Index] = ctx;
            }
        }

        public override void OnAgentRemoved(
            Agent affectedAgent, Agent affectorAgent,
            AgentState agentState, KillingBlow blow)
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

        // ========== 内部工具 ==========

        /// <summary>
        /// 为指定装备槽解析 InstanceId。
        /// 优先级：BindingMap 精确匹配 → ItemRecordMap 模板匹配 → null
        /// </summary>
        private static string? ResolveInstanceId(
            AffixCampaignBehavior behavior,
            Hero hero,
            EquipmentIndex slot,
            string baseItemId)
        {
            // 1. 优先从 BindingMap 精确查找
            var instanceId = behavior.GetEquippedInstanceId(hero, slot);
            if (!string.IsNullOrEmpty(instanceId))
                return instanceId;

            // 2. 过渡方案：从 ItemRecordMap 按模板ID查找首个匹配
            //    BindingMap 目前无调用者（待后续装备变化事件接线），
            //    此回退确保 Hero 装备现在就能查到 InstanceId
#pragma warning disable CS0618 // 过渡回退
            var affix = behavior.GetAffixByBaseItemId(baseItemId);
#pragma warning restore CS0618
            return affix?.InstanceId;
        }
    }
}
