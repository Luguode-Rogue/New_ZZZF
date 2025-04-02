using New_ZZZF.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF
{
    //传入一个施法者agent，施法者寻找一个目标，将目标强制击倒并瞬移至目标背后。短时间内大幅强化攻击伤害，并减少来自目标的伤害。
    //动画表现：选取目标后，如果目标较远，则进入一段时间的跑动动作，使用rush类似的方法来完成。
    //
    internal class HuoYanTuXi : SkillBase
    {//
        public HuoYanTuXi()
        {
            SkillID = "HuoYanTuXi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 0;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0051}HuoYanTuXi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            this.Description =new TaleWorlds.Localization.TextObject("{=ZZZF0052}向前突进一段距离，并持续造成火焰伤害。如果目标过远，则改为闪现到目标面前，并造成一次火焰新星。消耗耐力：35。冷却时间：30秒。");
        }
        public override bool Activate(Agent agent)
        {
            if (UseHuoYanTuXi(agent))
            { return true; }
            return false;
        }
        public bool UseHuoYanTuXi(Agent agent)
        {
            Agent vimagent = null;
            //目标选取：先判定视线落点附近是否有合适目标，如果没有，就选择一个随机的目标。
            //不可对骑兵生效，随机目标优先选择射手（按手持远程武器判定）
            List<Agent> agents = Script.FindAgentsWithinSpellRange(Script.AgentLookPos(agent), 10);
            Script.AgentListIFF(agent, agents, out var friendAgent, out var foeAgent);
            if (foeAgent.Count >= 1)
            {
                vimagent = foeAgent[0];
            }
            else
            {
                Script.AgentListIFF(agent, Mission.Current.Agents, out friendAgent, out foeAgent);
                foreach (Agent item in foeAgent)
                {
                    if (item.IsActive() && item.Health > 0&&Script.CanSeeAgent(agent,item))
                    {
                        if (item.GetWieldedItemIndex(Agent.HandIndex.MainHand) != EquipmentIndex.None
                        && item.Equipment[item.GetWieldedItemIndex(Agent.HandIndex.MainHand)].CurrentUsageItem.IsRangedWeapon) //手上的物品是远程武器
                        {
                            vimagent = item;
                            break;
                        }
                        else
                        { vimagent = item; }
                    }

                }
            }
            if (vimagent == null) { Script.SysOut("无有效目标", agent); return false; }
            //分两个情况，如果目标较远（比如超出50m），则先进入跑动状态，状态结束时，瞬移至目标附近。
            //如果目标较近，则直接在这里传送到目标身后，并附加强制动作

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { };

            if ((vimagent.GetEyeGlobalPosition() - agent.GetEyeGlobalPosition()).Length > 10)
            {
                MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                MissionMainAgentController missionMainAgentController = missionScreen.Mission.GetMissionBehavior<MissionMainAgentController>();

                // 获取 LockedAgent 属性的信息
                PropertyInfo lockedAgentProperty = typeof(MissionMainAgentController).GetProperty("LockedAgent", BindingFlags.Public | BindingFlags.Instance);
                if (lockedAgentProperty == null)
                {
                    throw new Exception("LockedAgent 属性未找到");
                }

                // 获取 LockedAgent 属性的私有 setter 方法
                MethodInfo setMethod = lockedAgentProperty.GetSetMethod(nonPublic: true);
                if (setMethod == null)
                {
                    throw new Exception("LockedAgent 的私有 setter 方法未找到");
                }

                // 调用私有 setter 方法
                setMethod.Invoke(missionMainAgentController, new object[] { vimagent });

                agent.TeleportToPosition(vimagent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(vimagent.LookDirection, 1f));
                List<Agent>vimList= Script.GetTargetedInRange(agent, vimagent.Position, 5);
                foreach (var item in vimList)
                {
                    item.SetActionChannel(0, ActionIndexCache.Create("act_jump_end"));
                    Script.CalculateFinalMagicDamage(agent, item, 30, DamageType.FIRE_DAMAGE);
                }
                return true;
            }
            else
            {
                if (!SkillSystemBehavior.WoW_AgentRushPos.ContainsKey(agent.Index))
                {
                    SkillSystemBehavior.WoW_AgentRushPos.Add(agent.Index, agent.GetEyeGlobalPosition()+Script.MultiplyVectorByScalar(agent.LookDirection.AsVec2.ToVec3(),10));
                    // 每次创建新的状态实例
                    newStates = new List<AgentBuff> { new RushToPosBuff(1.75f, 0f, agent)};
                    foreach (var state in newStates)
                    {
                        state.TargetAgent = agent;
                        agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                    }
                }
                if (agent != null)
                {

                    GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                    projectile.SetLocalPosition(agent.GetEyeGlobalPosition());
                    Vec3 TarPos = agent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(agent.LookDirection.AsVec2.ToVec3(), 10);

                    // 初始化数据对象
                    var projData = new ProjectileData
                    {
                        Name = SkillID,
                        skillBase = this,
                        CasterAgent = agent,
                        TargetPos = TarPos,
                        SpawnTime = Mission.Current.CurrentTime,
                        Lifetime = 7f, // 自定义存在时间
                    };
                    SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                    SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, projData);//制导gameEntity测试

                }

                return true;
            }
            return false;
        }
        public override void GameEntityDamage(GameEntity missileEntity)
        {
            if (!SkillSystemBehavior.WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                return;
            float BaseDamage = 20;
            // 获取Agent的CharacterObject

            int skill = 0;
            CharacterObject characterObject = ((data.CasterAgent != null) ? data.CasterAgent.Character : null) as CharacterObject;
            if (characterObject == null)
            {
                BasicCharacterObject character = (data.CasterAgent != null) ? data.CasterAgent.Character : null;
                skill = character.GetSkillValue(DefaultSkills.Bow);
            }
            else
            {
                CharacterObject character = characterObject;
                skill = character.GetSkillValue(DefaultSkills.Bow);
            }

            BaseDamage = BaseDamage * (1 + skill / 100);
            // 通过属性管理器获取智力值//小兵没有智力值，改用熟练度吧
            //int intelligenceValue = character.HeroObject.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
            Agent castAgent = data.CasterAgent;
            List<Agent> list = Script.FindAgentsWithinSpellRange(missileEntity.GlobalPosition, 2);
            List<Agent> FriendAgent = new List<Agent>();
            List<Agent> FoeAgent = new List<Agent>();
            Script.AgentListIFF(castAgent, list, out FriendAgent, out FoeAgent);
            foreach (Agent agent in FoeAgent)
            {
                AgentSkillComponent agentComponent = Script.GetActiveComponents(agent);
                if (agentComponent._beHitCount <= 4)
                {

                    agentComponent._beHitCount += 1;
                    Script.CalculateFinalMagicDamage(data.CasterAgent, agent, BaseDamage, DamageType.None);
                } 
                else if (agentComponent._beHitCount == 4)
                {
                    agentComponent._beHitTime += 0.3f;
                }

            }


        }
    }
    
}
