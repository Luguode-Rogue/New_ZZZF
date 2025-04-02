using New_ZZZF.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace New_ZZZF
{
    internal class lingmashaodi : SkillBase
    {
        public lingmashaodi()
        {
            SkillID = "lingmashaodi";      // 必须唯一
            Type = SkillType.Spell;    // 类型必须明确
            Cooldown = 3;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0009}lingmashaodi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
        }
        public override bool Activate(Agent agent)
        {
            if (CreatMount(agent))
            {
                SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var v);
                v._globalCooldownTimer += 3f;
                return true;
            }

            
            return false;
        }
        /// <summary>
        /// 【玩家坐骑交互系统】控制主角的骑乘状态切换
        /// 功能：当玩家未骑马时生成坐骑并上马，已骑马时执行下马动作
        /// </summary>
        /// <param name="Rider">理论上是玩家角色，但参数传入值会被覆盖（代码问题）</param>
        /// <returns>始终返回null（设计缺陷）</returns>
        public static bool CreatMount(Agent Rider)
        {
            // [!] 严重问题：覆盖传入参数，使参数失去意义
            // Rider = Mission.Current.MainAgent; // 强制绑定到当前任务主角

            if (Rider != null)
            {
                // 骑乘状态检测
                if (Rider.MountAgent == null) // 未骑马状态
                {
                    // 马匹装备校验系统
                    if (Rider.Character.Equipment[EquipmentIndex.Horse].Item == null)
                    { Script.SysOut("无马匹装备",Rider); return false; } // 无马匹装备直接返回

                    // 马匹对象加载
                    ItemObject @object = MBObjectManager.Instance.GetObject<ItemObject>(
                        Rider.Character.Equipment[EquipmentIndex.Horse].Item.StringId);
                    if (@object == null)
                    { Script.SysOut("无有效马匹装备", Rider); return false; } // 加载失败保护

                    // 马具配置系统
                    ItemRosterElement itemRosterElement = new ItemRosterElement(@object, 1, null);
                    ItemRosterElement itemRosterElement2;
                    if (Rider.Character.Equipment[EquipmentIndex.HorseHarness].Item == null)
                    { itemRosterElement2 = default(ItemRosterElement); } // 空马具处理
                    else
                    {
                        itemRosterElement2 = new ItemRosterElement(
                        MBObjectManager.Instance.GetObject<ItemObject>(
                            Rider.Character.Equipment[EquipmentIndex.HorseHarness].Item.StringId), 1, null);
                    }

                    // 马匹实体生成
                    if (@object.HasHorseComponent)
                    {
                        // 坐标计算系统
                        MatrixFrame globalFrame = Rider.GetWorldFrame().ToGroundMatrixFrame();

                        // 马匹生成逻辑
                        Mission mission = Mission.Current;
                        Vec2 asVec = globalFrame.rotation.f.AsVec2;
                        Agent agent = mission.SpawnMonster(
                            itemRosterElement,
                            itemRosterElement2,
                            globalFrame.origin,
                            asVec,
                            -1
                        );
                        agent.FadeIn(); // 渐显动画

                        // 骑乘动作系统
                        Rider.Mount(agent); // 绑定骑乘关系
                        //Rider.SetActionChannel(0,
                        //    ActionIndexCache.Create("act_mount_horse_from_left"), // 上马动作
                        //    false, 272UL, 0, 1, -0.2f, 0.4f, 0.25f
                        //);
                    }
                    return true;
                }
                else // 已骑马状态
                {
                    // 下马动作系统
                    Rider.MountAgent.SetActionChannel(0,
                        ActionIndexCache.Create("act_horse_rear")); // 马匹后仰动作
                    Agent RiderMountAgent = Rider.MountAgent; // 暂存坐骑引用

                    // [!] 危险操作：可能导致状态不一致
                    Rider.Mount(Rider.MountAgent); // 解除骑乘（API可能设计为传入null才是下马）

                    // 角色动作系统
                    //Roll.AgentRoll(Rider); // 执行翻滚动作（需确认是否合理）
                    RiderMountAgent.FadeOut(false, false); // 坐骑渐隐
                    return true;
                }
            }
            return false; // [!] 功能缺陷：应返回操作结果
        }
    }
}
