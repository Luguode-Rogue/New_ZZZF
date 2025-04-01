using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal class Roll : SkillBase
    {
        public Roll()
        {
            SkillID = "Roll";      // 必须唯一
            Type = SkillType.Passive_Spell;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 3f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0006}Roll");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool Activate(Agent agent)
        {
            if (agent.MountAgent == null && !SkillSystemBehavior.WoW_AgentRushPos.ContainsKey(agent.Index))
            {
                Vec3 vec3 = Vec3.Invalid;
                float f = 0f;
                Vec3 lookD = agent.LookDirection;
                Mat3 lookR = agent.LookRotation;
                lookR.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                if (agent == Agent.Main)
                {
                    //主角的8向翻滚
                    if (Input.IsKeyDown(InputKey.W) && Input.IsKeyDown(InputKey.A))
                    {
                        lookD.RotateAboutZ(45 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(45 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.A) && Input.IsKeyDown(InputKey.S))
                    {
                        lookD.RotateAboutZ(135 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(135 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.W) && Input.IsKeyDown(InputKey.D))
                    {
                        lookD.RotateAboutZ(-45 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(-45 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.D) && Input.IsKeyDown(InputKey.S))
                    {
                        lookD.RotateAboutZ(-135 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(-135 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.W))
                    {

                    }
                    else if (Input.IsKeyDown(InputKey.S))
                    {
                        lookD.RotateAboutZ(180 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(180 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.A))
                    {
                        lookD.RotateAboutZ(90 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(90 * (3.1415f / 180.0f));
                    }
                    else if (Input.IsKeyDown(InputKey.D))
                    {
                        lookD.RotateAboutZ(-90 * (3.1415f / 180.0f));
                        lookR.RotateAboutUp(-90 * (3.1415f / 180.0f));
                    }
                }
                else
                {
                    //非玩家的ai
                    EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                    if (mainHandIndex == EquipmentIndex.None)
                    {
                        //手上没武器就不额外操作
                    }
                    else
                    {
                        // EquipmentIndex转MissionWeapon
                        MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                        if (mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
                        {
                            //对于手上有远程武器的兵,自动触发为向后滚
                            lookD.RotateAboutZ(180 * (3.1415f / 180.0f));
                        }
                        else
                        {
                            //近战兵不额外操作,默认向前
                        }
                    }
                }
                //固定翻滚一个距离
                //射线检测阻挡物
                Mission.Current.Scene.RayCastForClosestEntityOrTerrain(agent.GetEyeGlobalPosition(), agent.Position + Script.MultiplyVectorByScalar(lookR.f, 7), out f, out vec3);
                //如果没有阻挡物但是距离过远或者射线检测没有碰撞
                if (f < 7)
                {

                    SkillSystemBehavior.WoW_AgentRushPos.Add(agent.Index, vec3);
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff>
                            {
                                new RushToPosBuff(1.25f, 0f, agent), // 新实例
                            };
                    foreach (var state in newStates)
                    {
                        state.TargetAgent = agent;
                        agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                    }
                }
                else
                {
                    SkillSystemBehavior.WoW_AgentRushPos.Add(agent.Index, agent.Position + Script.MultiplyVectorByScalar(lookR.f, 7));
                    // 每次创建新的状态实例
                    List<AgentBuff> newStates = new List<AgentBuff>
                            {
                                new RushToPosBuff(1.25f, 0f, agent), // 新实例
                            };
                    foreach (var state in newStates)
                    {
                        state.TargetAgent = agent;
                        agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
                    }
                }
                agent.SetActionChannel(0, ActionIndexCache.Create("act_horse_fall_roll"));
                agent.SetCurrentActionProgress(0, 0.3f);
                agent.SetCurrentActionSpeed(0, 2f);
                return true;
            }
            else return false;
        }
        public static bool AgentRoll(Agent agent)
        {
            SkillFactory._skillRegistry.TryGetValue("Roll",out  SkillBase skillBase);
           return skillBase.Activate(agent);
        }
    }
    public class RushToPosBuff : AgentBuff
    {
        private float _damagePerSecond;
        private float _timeSinceLastTick;
        public RushToPosBuff(float duration, float dps, Agent source)
        {
            StateId = "RushToAgentBuff";
            Duration = duration;
            _damagePerSecond = 0;
            SourceAgent = source;
            _timeSinceLastTick = 0; // 新增初始化
        }

        public override void OnApply(Agent agent)
        {
        }

        public override void OnUpdate(Agent agent, float dt)
        {
            // 累积伤害时间
            _timeSinceLastTick += dt;

            // 每秒触发一次伤害
            if (_timeSinceLastTick >= 1f)
            {
                InformationManager.DisplayMessage(new InformationMessage("RushToPosBuff"));

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            
        }
    }
}
