﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    internal class 暗影步未完成 : SkillBase
    {//
        public 暗影步未完成()
        {
            SkillID = "暗影步";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 0;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0011}暗影步");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool Activate(Agent agent)
        {
            if (暗影步实现(agent))
            { return true; }
            return false;
        }
        public bool 暗影步实现(Agent agent)
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
                    if (item.IsActive() && item.Health > 0)
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

            if ((vimagent.GetEyeGlobalPosition() - agent.GetEyeGlobalPosition()).Length > 50)
            {
                return true;
            }
            else
            {
                agent.TeleportToPosition(vimagent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(vimagent.LookDirection, -2f));
                //agent.SetLookToPointOfInterest(( (vimagent.Position + Script.MultiplyVectorByScalar(vimagent.LookDirection, -2f)) - vimagent.Position));
                MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;

                missionScreen.CombatCamera.Frame = new MatrixFrame(LookAt(vimagent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(vimagent.LookDirection, -2f), vimagent.GetEyeGlobalPosition()), (vimagent.GetEyeGlobalPosition() + Script.MultiplyVectorByScalar(vimagent.LookDirection, -2f)));

                missionScreen.SceneView.SetCamera(missionScreen.CombatCamera);
                bool flag = false;
                using (List<MissionBehavior>.Enumerator enumerator = missionScreen.Mission.MissionBehaviors.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        MissionView missionView;
                        if ((missionView = (enumerator.Current as MissionView)) != null)
                        {
                            flag = (flag || missionView.UpdateOverridenCamera(0.00005f));
                        }
                    }
                }
                vimagent.SetActionChannel(1, ActionIndexCache.Create("act_smithing_machine_anvil_part_1"));

                agent.SetTargetAgent(vimagent);
                //agent.SetActionChannel(1, ActionIndexCache.Create("act_smithing_machine_anvil_part_1"), false, 250UL, 0.5f);
                return true;
            }
            return false;
        }
        public static Mat3 LookAt(Vec3 eye, Vec3 target)
        {
            Vec3 forward = (target - eye).NormalizedCopy();

            Vec3 up = Vec3.Up; // 假设默认的上方向是Y轴正方向
            Vec3 right = Vec3.CrossProduct(forward, up);
            Vec3 newUp = Vec3.CrossProduct(right, forward);

            return new Mat3(right, -forward, newUp);
        }
    }
    public class 暗影步增伤 : AgentBuff
    {
        private float _damagePerSecond;
        private float _timeSinceLastTick;
        public 暗影步增伤(float duration, float dps, Agent source)
        {
            StateId = "暗影步增伤";
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
                InformationManager.DisplayMessage(new InformationMessage("暗影步增伤"));

                _timeSinceLastTick -= 1f; // 重置计时器
            }
        }

        public override void OnRemove(Agent agent)
        {
            // 移除特效
            agent.StopParticleEffect("暗影步增伤");
        }
    }
}
