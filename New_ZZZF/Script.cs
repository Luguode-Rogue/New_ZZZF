using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    public class Script
    {
        /// <summary>
        /// 参数1：基于的agent
        /// 参数1：目标Pos
        /// 参数3：周围spellRange米
        /// 参数4：返回友军list或敌军list（默认敌军）
        /// 获取目标范围内敌人的list
        /// 无有效目标时，返回null
        /// </summary>
        public static List<Agent> GetTargetedInRange(Agent CasterAgent, Vec3 CasterPos, int spellRange,bool FriendList=false)
        {
            List<Agent> list = FindAgentsWithinSpellRange(CasterPos, spellRange);
            List<Agent> FriendAgent = null;
            List<Agent> FoeAgent = null;
            Script.AgentListIFF(CasterAgent, list, out FriendAgent, out FoeAgent);
            if (FriendList)
            { return FriendAgent; }
            else
            { return FoeAgent; }
            return null;
        }

        /// <summary>
        /// 参数1：基于的agent
        /// 参数2：需要判定的list
        /// 判定list里，距离目标agent最近的一个agent单位
        /// 无有效目标时，返回基于的agent
        /// </summary>
        public static Agent FindClosestAgentToCaster(Agent CasterAgent, List<Agent> agentList)
        {
            Agent OutAgent = CasterAgent;
            float Range = 9999f;
            foreach (Agent agent in agentList)
            {
                Vec2 v2 = CasterAgent.GetCurrentVelocity() - agent.GetCurrentVelocity();
                if (Range > v2.Length)
                {
                    Range = v2.Length;
                    OutAgent = agent;
                }
            }
            return OutAgent;
        }        /// <summary>
                 /// 参数1：基于的pos
                 /// 参数2：需要判定的list
                 /// 判定list里，距离目标agent最近的一个agent单位
                 /// 无有效目标时，返回null
                 /// </summary>
        public static Agent FindClosestAgentToPos(Vec3 vec3, List<Agent> agentList)
        {
            Agent OutAgent=null;
            float Range = 9999f;
            foreach (Agent agent in agentList)
            {
                Vec2 v2 = vec3.AsVec2 - agent.GetCurrentVelocity();
                if (Range > v2.Length)
                {
                    Range = v2.Length;
                    OutAgent = agent;
                }
            }
            return OutAgent;
        }
        /// <summary>
        ///参数1:目标地点vec3
        ///参数2:施法生效范围
        ///获取目标范围内所有的agent,存放在列表里.敌我判定只有拿列表里的agent再去判定,不在这里判定
        /// </summary>
        public static List<Agent> FindAgentsWithinSpellRange(Vec3 targetLocation, int spellRange)
        {
            List<Agent> agentsWithinRange = new List<Agent>();

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent.IsActive())
                {
                    float distanceToTarget = targetLocation.Distance(agent.GetEyeGlobalPosition());
                    if (distanceToTarget <= spellRange)
                    {
                        agentsWithinRange.Add(agent);
                    }
                }
            }
            return agentsWithinRange;
        }
        /// <summary>
        ///敌我识别脚本,不获取坐骑
        ///参数1：基于某agent进行敌我识别
        ///参数2：需要敌我识别的list
        ///参数3：输出友方list
        ///参数4：输出敌方list
        /// </summary>
        public static void AgentListIFF(Agent agent, List<Agent> InputList, out List<Agent> FriendAgent, out List<Agent> FoeAgent)
        {

            FriendAgent = new List<Agent>();
            FoeAgent = new List<Agent>();
            for (int i = 0; i < InputList.Count; i++)
            {
                if (InputList[i].IsFriendOf(agent)&& InputList[i].IsHuman)
                {
                    FriendAgent.Add(InputList[i]);
                }
                else if(!InputList[i].IsFriendOf(agent) && InputList[i].IsHuman)
                {
                    FoeAgent.Add(InputList[i]);
                }
            }
        }
        /// <summary>
        /// 创建一个新的Vec3向量，其每个分量都是原始向量分量与标量的乘积
        /// </summary>>
        public static Vec3 MultiplyVectorByScalar(Vec3 vector, float scalar)
        {
            // 创建一个新的Vec3向量，其每个分量都是原始向量分量与标量的乘积
            Vec3 result = new Vec3(vector.x * scalar, vector.y * scalar, vector.z * scalar);
            return result;
        }
        public static void CalculateFinalMagicDamage(Agent Caster,Agent Victim,float BaseDamage,String DamageType)
        {
            float DifHP= Victim.Health ;
            DifHP -= BaseDamage;
            Victim.Health = DifHP;
            InformationManager.DisplayMessage(new InformationMessage("造成了"+BaseDamage.ToString()+"点"+DamageType+"伤害"));

            if (Victim.Health <= 0)
            {
                Blow blow = new Blow(Caster.Index);
                blow.InflictedDamage = (int)BaseDamage;
                Victim.Die(blow);
                //Mission.Current.KillAgentCheat(Victim);
            }
        }
    }
}
