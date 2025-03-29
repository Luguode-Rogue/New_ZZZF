using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class FireballSkill : SkillBase
    {
        public FireballSkill()
        {
            SkillID = "Fireball";
            Type = SkillType.Spell;
            Cooldown = 100f;
            ResourceCost = 100f;
            Text = new TaleWorlds.Localization.TextObject("{=12345678}Fireball");

        }


        public override bool Activate(Agent agent)
        {
            Agent target = FindTarget(agent);
            if (target == null || !target.IsActive()) return false;

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff>
                {
                    new BurningState(5f, 1f, agent), // 新实例
                    new du(5f, 1f, agent)            // 新实例
                };

            foreach (var state in newStates)
            {
                state.TargetAgent = target;
                target.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
            }
            return true;
        }
        private Agent FindTarget(Agent agent)
        {
           // Script.GetTargetedInRange( agent,);
            Agent castAgent = agent;
            List<Agent> list = Script.FindAgentsWithinSpellRange(agent.GetEyeGlobalPosition(), 15);
            List<Agent> FriendAgent = new List<Agent>();
            List<Agent> FoeAgent = new List<Agent>();
            Script.AgentListIFF(castAgent, list, out FriendAgent, out FoeAgent);
            Agent outAgent = Script.FindClosestAgentToCaster(agent, FoeAgent);
            if (outAgent != agent)
            {
                return outAgent;
            }
            return null;
        }
    }
}
