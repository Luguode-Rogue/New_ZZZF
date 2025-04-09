using System;
using System.Collections.Generic; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class DaDiJianTa : SkillBase
    {
        public DaDiJianTa()
        {
            SkillID = "DaDiJianTa";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 3f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0012}DaDiJianTa");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            this.Description =new TaleWorlds.Localization.TextObject("{=ZZZF0055}" +
                "猛击地面，使附近10米的敌方单位聚拢在一起，受到一次伤害，并硬直一段时间。每影响一个单位，回复5点耐力值。消耗耐力：20。持续时间：5秒。冷却时间：10秒。");
        }
        public override bool Activate(Agent casterAgent)
        {
            if (DaDiJianTaUse(casterAgent))
            { return true; }
             return false;
        }
        public override bool CheckCondition(Agent caster)
        {
            List<Agent> agents = Script.GetTargetedInRange(caster, caster.Position, 15);
            if (agents.Count>=10)
            { return true; }
            if (caster.Health / caster.HealthLimit <= 0.5 && agents.Count >= 1)
            { return true; }

            return false;
        }
        public bool DaDiJianTaUse(Agent casterAgent)
        {
            Script.AgentListIFF(casterAgent,Script.FindAgentsWithinSpellRange(casterAgent.Position,15),out var friendAgent,out var foeAgent);
            Vec3 tarVec3 = casterAgent.Position;
            Vec2 tarVec2 = casterAgent.LookDirection.AsVec2;
            tarVec3 += Script.MultiplyVectorByScalar(casterAgent.LookDirection, 2);
            foreach (var vimAgent in foeAgent)
            {                       
                // 每次创建新的状态实例
                List<AgentBuff> newStates = new List<AgentBuff> { new DaDiJianTaBuffToEnemy(5, casterAgent), }; // 新实例
                foreach (var state in newStates)
                {
                    state.TargetAgent = vimAgent;
                    vimAgent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                }
                vimAgent.SetInitialFrame(tarVec3, tarVec2);
                vimAgent.SetActionChannel(0, ActionIndexCache.Create("act_horse_command_follow"), false, 272UL, 0, 0.1f, -0.2f, 0.4f, 0.0f);
                vimAgent.SetActionChannel(1, ActionIndexCache.Create("act_horse_command_follow"), false, 272UL, 0, 0.1f, -0.2f, 0.4f, 0.0f);
                Script.CalculateFinalMagicDamage(casterAgent, vimAgent, 20, DamageType.None);
            }
            
            if (SkillSystemBehavior.ActiveComponents.TryGetValue(casterAgent.Index, out var activeComponent) && activeComponent != null)
            { 
                activeComponent.ChangeStamina(foeAgent.Count*5);
            }
            return false;
        }
    }

    public class DaDiJianTaBuffToEnemy : AgentBuff
    {
        private float _timeSinceLastTick;
        public DaDiJianTaBuffToEnemy(float duration, Agent source)
        {
            StateId = "DaDiJianTaBuffToEnemy";
            Duration = duration;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(this.SourceAgent.Index, out var agentSkillComponent);
            if (agentSkillComponent == null) { return; }
            // 累积时间
            _timeSinceLastTick += dt;

            //每秒刷一次状态
            if (_timeSinceLastTick >= 1f)
            {

                agent.SetTargetPosition(agent.Position.AsVec2);
                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            agent.ClearTargetFrame();
            agent.SetActionChannel(0, ActionIndexCache.Create("act_none"), false, 272UL, 0, 5f, -0.2f, 0.4f, 1.0f);
            agent.SetActionChannel(1, ActionIndexCache.Create("act_none"), false, 272UL, 0, 5f, -0.2f, 0.4f, 1.0f);
        }
    }

}
