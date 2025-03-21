using NetworkMessages.FromServer;
using SandBox.Missions.MissionLogics;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.ObjectSystem;
using New_ZZZF.Skills;
using static TaleWorlds.MountAndBlade.Agent;

namespace New_ZZZF
{
    public class Script
    {
        /// <summary>
        /// 玩家报错信息
        /// </summary>
        /// <param name="s"></param>
        public static void SysOut(string s, Agent agent)
        {
            if (Mission.Current != null)
                if (Mission.Current.MainAgent == agent)
                    InformationManager.DisplayMessage(new InformationMessage(s));
        }
        /// <summary>
        /// 0无有效武器
        /// 1无弹道速度记录，需要进行一次射击
        /// 2该角色无装备
        /// 3"无有效目标"
        /// </summary>
        /// <param name="i"></param>
        /// <param name="agent"></param>
        public static void SysOut(int i, Agent agent)
        {
            if (Mission.Current != null)
                if (Mission.Current.MainAgent == agent)
                {
                    switch (i)
                    {
                        case 0:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        case 1:
                            InformationManager.DisplayMessage(new InformationMessage("无弹道速度记录，需要进行一次射击"));
                            break;
                        case 2:
                            InformationManager.DisplayMessage(new InformationMessage("该角色无装备"));
                            break;
                        case 3:
                            InformationManager.DisplayMessage(new InformationMessage("无有效目标"));
                            break;
                        case 4:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        case 5:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        default: break;
                    }
                }
        }
        /// <summary>
        /// 单位1能否看到单位2的位置
        /// </summary>
        /// <param name="mainAgent"></param>
        /// <param name="otherAgent"></param>
        /// <returns></returns>
        public static bool CanSeeAgent(Agent mainAgent, Agent otherAgent)
        {
            if ((mainAgent.Position - otherAgent.Position).Length < 500f)
            {
                Vec3 eyeGlobalPosition = otherAgent.GetEyeGlobalPosition();
                Vec3 eyeGlobalPosition2 = mainAgent.GetEyeGlobalPosition();
                if (TaleWorlds.Library.MathF.Abs(Vec3.AngleBetweenTwoVectors(otherAgent.Position - mainAgent.Position, mainAgent.LookDirection)) < 1.5f)
                {
                    float num;
                    return !Mission.Current.Scene.RayCastForClosestEntityOrTerrain(eyeGlobalPosition2, eyeGlobalPosition, out num, 0.01f, BodyFlags.CommonFocusRayCastExcludeFlags);
                }
            }
            return false;
        }
        /// <summary>
        /// 用于灵马哨笛
        /// 生成一个带有初始位置配置的游荡AI代理（NPC）
        /// </summary>
        /// <param name="locationCharacter">关联的场景角色数据</param>
        /// <param name="spawnPointFrame">生成点的空间坐标系（包含位置和朝向）</param>
        /// <param name="noHorses">是否禁止生成马匹（默认true）</param>
        /// <returns>生成的AI代理对象</returns>
        private static Agent SpawnWanderingAgentWithInitialFrame(LocationCharacter locationCharacter, MatrixFrame spawnPointFrame, bool noHorses = true)
        {
            // 1. 确定队伍归属
            Team team = Team.Invalid;
            switch (locationCharacter.CharacterRelation)
            {
                case LocationCharacter.CharacterRelations.Neutral:  // 中立角色保持无效队伍
                    team = Team.Invalid;
                    break;
                case LocationCharacter.CharacterRelations.Friendly: // 友方加入玩家盟友队伍
                    team = Mission.Current.PlayerAllyTeam;
                    break;
                case LocationCharacter.CharacterRelations.Enemy:    // 敌方加入玩家敌对队伍
                    team = Mission.Current.PlayerEnemyTeam;
                    break;
            }

            // 2. 调整生成点Z轴高度（确保生成在地面）
            spawnPointFrame.origin.z = Mission.Current.Scene.GetGroundHeightAtPosition(
                spawnPointFrame.origin,
                BodyFlags.CommonCollisionExcludeFlags
            );

            // 3. 获取定居点颜色配置（用于角色服装）
            ValueTuple<uint, uint> agentSettlementColors = MissionAgentHandler.GetAgentSettlementColors(locationCharacter);

            // 4. 构建代理基础数据
            AgentBuildData agentBuildData = locationCharacter.GetAgentBuildData()
                .Team(team)                         // 设置队伍
                .InitialPosition(spawnPointFrame.origin); // 设置初始位置

            // 5. 标准化朝向向量
            Vec2 vec = spawnPointFrame.rotation.f.AsVec2;
            vec = vec.Normalized();

            // 6. 扩展代理配置
            AgentBuildData agentBuildData2 = agentBuildData
                .InitialDirection(vec)                      // 设置初始朝向
                .ClothingColor1(agentSettlementColors.Item1) // 主服装颜色
                .ClothingColor2(agentSettlementColors.Item2) // 副服装颜色
                .CivilianEquipment(locationCharacter.UseCivilianEquipment) // 是否使用平民装备
                .NoHorses(noHorses);                        // 是否禁用马匹

            // 7. 获取氏族旗帜数据
            CharacterObject character = locationCharacter.Character;
            Banner banner = null;
            if (character?.HeroObject?.Clan != null) // 仅当角色有氏族关联时
            {
                banner = character.HeroObject.Clan.Banner; // 获取氏族旗帜
            }

            // 8. 最终代理配置
            AgentBuildData agentBuildData3 = agentBuildData2.Banner(banner); // 设置旗帜

            // 9. 生成代理实体
            Agent agent = Mission.Current.SpawnAgent(agentBuildData3, false);

            // 10. 配置动画系统（动作集合/步长）
            AnimationSystemData animationSystemData = agentBuildData3.AgentMonster.FillAnimationSystemData(
                MBGlobals.GetActionSet(locationCharacter.ActionSetCode), // 获取动作资源
                locationCharacter.Character.GetStepSize(),              // 设置移动步长
                false
            );
            agent.SetActionSet(ref animationSystemData); // 应用动作配置

            // 11. 添加战役代理组件
            agent.GetComponent<CampaignAgentComponent>().CreateAgentNavigator(locationCharacter);

            // 12. 绑定场景角色行为树
            locationCharacter.AddBehaviors(agent);

            return agent;
        }

        /// <summary>
        /// 参数1：基于的agent
        /// 参数1：目标Pos
        /// 参数3：周围spellRange米
        /// 参数4：返回友军list或敌军list（默认敌军）
        /// 获取目标范围内敌人的list
        /// 无有效目标时，返回null
        /// </summary>
        public static List<Agent> GetTargetedInRange(Agent CasterAgent, Vec3 CasterPos, int spellRange, bool FriendList = false)
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
        /// 判定list里，距离目标agent最近的一个agent单位(非自身，非坐骑）
        /// 无有效目标时，返回基于的agent
        /// </summary>
        public static Agent FindClosestAgentToCaster(Agent CasterAgent, List<Agent> agentList)
        {
            Agent OutAgent = CasterAgent;
            float Range = 9999f;
            foreach (Agent agent in agentList)
            {
                if (CasterAgent.Index== agent.Index) continue;
                if(!agent.IsHuman)continue;
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
            Agent OutAgent = null;
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
                if (InputList[i].IsFriendOf(agent) && InputList[i].IsHuman&&!InputList[i].IsEnemyOf(agent))
                {
                    FriendAgent.Add(InputList[i]);
                }
                else if (!InputList[i].IsFriendOf(agent) && InputList[i].IsHuman&& InputList[i].IsEnemyOf(agent))
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
        /// <summary>
        /// //根据相机视野，获取准星附近的一个非友军agent
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static Agent FindTargetedLockableAgent(Agent player)
        {
            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = missionScreen.CombatCamera.Direction;
            Vec3 vec = direction;
            Vec3 position = missionScreen.CombatCamera.Position;
            Vec3 visualPosition = player.VisualPosition;
            float num = new Vec3(position.x, position.y, 0f, -1f).Distance(new Vec3(visualPosition.x, visualPosition.y, 0f, -1f));
            Vec3 v = position * (1f - num) + (position + direction) * num;
            float num2 = 0f;
            Agent agent = null;
            foreach (Agent agent2 in Mission.Current.Agents)
            {
                if ((agent2.IsMount && agent2.RiderAgent != null && !agent2.RiderAgent.IsFriendOf(player)&&agent2.RiderAgent.IsEnemyOf(player)) || (!agent2.IsMount && !agent2.IsFriendOf(player) && agent2.IsEnemyOf(player)))
                {
                    Vec3 vec2 = agent2.GetChestGlobalPosition() - v;
                    float num3 = vec2.Normalize();
                    if (num3 < 100f)//这个应该是锁定的距离
                    {
                        float num4 = Vec2.DotProduct(vec.AsVec2.Normalized(), vec2.AsVec2.Normalized());
                        float num5 = Vec2.DotProduct(new Vec2(vec.AsVec2.Length, vec.z), new Vec2(vec2.AsVec2.Length, vec2.z));
                        if (num4 > 0.95f && num5 > 0.95f)//这个数也可以放宽点，0.95降低
                        {
                            float num6 = num4 * num4 * num4 / TaleWorlds.Library.MathF.Pow(num3, 0.15f);
                            if (num6 > num2)
                            {
                                num2 = num6;
                                agent = agent2;
                            }
                        }
                    }
                }
            }
            if (agent != null && agent.IsMount && agent.RiderAgent != null)
            {
                return agent.RiderAgent;
            }
            return agent;
        }/// <summary>
         /// 获取目视地点
         /// </summary>
         /// <param name="agent"></param>
         /// <returns></returns>
        public static Vec3 AgentLookPos(Agent agent) //获取目视地点,效果凑合了
        {
            Vec3 vec3 = Vec3.Invalid;
            float f = 0f;
            Mission.Current.Scene.RayCastForClosestEntityOrTerrain(agent.GetEyeGlobalPosition(), agent.GetEyeGlobalPosition() + MultiplyVectorByScalar(agent.LookDirection, 5000f), out f, out vec3);

            return vec3;
        }
        /// <summary>
        /// agent朝目视方向射击，勉强可用，比实际落点要低要近,需要删掉游戏配置里的空气阻力，删除后正常
        /// 重载一个射击脚本，添加一个精度属性，需要输入记录的连射时间，然后根据时间降低精度。
        /// </summary>
        public static bool AgentShootTowardsLookDirection(Agent agent, float Full_autoTime)
        {//抽空加一下基础的命中率,调用一下agent还是哪里的获取武器当前精准度,可以获得一个基于准星的散布值
            Random random = new Random();

            if (agent.Equipment != null)
            {
                //每次循环的时候，重新取一遍本身射击的角度，去做角度转换
                //角度的获取和改变，以后都用 Mat3 mat3 = agent.LookRotation;需要输出vec3时，用mat3.f获取vec3的值
                Mat3 mat3 = agent.LookRotation;

                float M = random.NextFloat() * Full_autoTime;
                // 随机决定正负
                int sign = random.Next(2) * 2 - 1; // 将产生1或-1
                float radians = (M * sign) * (3.1415f / 180.0f); // 角度转换为弧度//纵向后坐力不用取负数
                mat3.RotateAboutUp(radians);
                M = random.NextFloat() * Full_autoTime / 3 + 0.3f;
                radians = (M * sign) * (3.1415f / 180.0f); // 角度转换为弧度
                mat3.RotateAboutSide(radians);

                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut("无有效武器", agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = -1;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list == null)
                {
                    SysOut("无弹道速度记录，需要进行一次射击", agent);
                    return false;
                }
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }
                //if(baseSpeed == -1)
                //{
                //    SysOut("有弹道速度记录，是要使用此武器进行一次射击", agent);
                //    return false;
                //}   
                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);


                Vec3 headPosition = agent.GetEyeGlobalPosition();

                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, headPosition, mat3.f, baseSpeed);
                return true;
            }
            else
            {
                SysOut("该角色无装备", agent);
                return false;
            }

        }
        /// <summary>
        /// 参考agent当前手持的武器,从agent当前的武器栏中,获取合适的投射物
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="ThrewMeleeWeapon"></param>
        /// <param name="EquipmentIndex"></param>
        /// <returns></returns>
        public static MissionWeapon getMissionWeaponFromAgentInventory(Agent agent, out bool ThrewMeleeWeapon, out EquipmentIndex EquipmentIndex)
        {
            //因为missionweapon.ammoweapon会在武器装填时输出空值，所以还是重写一个弹药获取的代码
            //现在是获取手上的武器后，依次遍历agent的武器栏位。如果遍历到的物品是手上武器的弹药，并且弹药数量大于0，则设置为一会addmissile的弹药。
            //手上武器为近战时还要做个缺省值
            ThrewMeleeWeapon = false;
            EquipmentIndex = EquipmentIndex.None;
            EquipmentIndex 备用index = EquipmentIndex.None;//如果弹药数量为空的话，弹药所在的index记录在这个位置，如果本身有弹药但是因为用完了的时候，输出这个位置
            if (!agent.IsHuman || agent.GetWieldedItemIndex(Agent.HandIndex.MainHand) == EquipmentIndex.None)
                return MissionWeapon.Invalid;
            MissionWeapon missionWeapon = agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)];
            if (agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)].CurrentUsageItem.IsRangedWeapon)//手上的物品是远程武器
            {
                for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)//循环遍历武器栏位
                {

                    if (!agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)].IsEmpty && !agent.Equipment[equipmentIndex].IsEmpty)//如果手上的武器和遍历的物品非空
                    {
                        if (agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)].Item.PrimaryWeapon.WeaponClass == WeaponClass.Crossbow)//如果手上的武器是弩
                        {
                            //先进行一次把射出的武器定位默认值
                            // 获取ItemObject引用，这里使用了一个字符串ID来查找特定的武器。字符串是xml里item的id值。
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("bolt_a");
                            // 创建MissionWeapon实例
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt && agent.Equipment[equipmentIndex].Amount > 0)//如果遍历的物品是弩箭
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                EquipmentIndex = equipmentIndex;
                                break;//退出遍历循环
                            }
                            else if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                        if (agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bow)
                        {
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("arrow_emp_1_a");
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow && agent.Equipment[equipmentIndex].Amount > 0)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];
                                EquipmentIndex = equipmentIndex;
                                break;
                            }
                            else if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                        if (agent.Equipment[agent.GetWieldedItemIndex(Agent.HandIndex.MainHand)].CurrentUsageItem.IsConsumable && agent.Equipment[equipmentIndex].CurrentUsageItem.IsRangedWeapon && agent.Equipment[equipmentIndex].CurrentUsageItem.IsConsumable)
                        {
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("western_javelin_1_t2");
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Amount > 0)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];
                                EquipmentIndex = equipmentIndex;
                                break;
                            }
                            else
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                    }

                }
            }
            else//如果手上不是远程武器，也写了适配,默认射一个投矛。伤害的问题在伤害计算那边处理
            {
                ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("western_javelin_1_t2");
                missionWeapon = new MissionWeapon(weaponItem, null, null);
                ThrewMeleeWeapon = true;
            }
            if (备用index != EquipmentIndex.None && EquipmentIndex == EquipmentIndex.None)
            {
                EquipmentIndex = 备用index;
            }
            return missionWeapon;
        }
        public bool AimShoot(Agent agent)//自瞄步骤1，选择射击目标agent
        {
            EquipmentIndex mainHandIndex1 = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            if (mainHandIndex1 == EquipmentIndex.None)
            {
                return false;
            }
            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex1];
            if (!mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
            {
                return false;
            }
            Agent ShootAgent = agent;
            Agent vAgent = null;
            List<Agent> list1 = new List<Agent>();
            List<Agent> list2 = new List<Agent>();
            foreach (Agent ChooseAgent in agent.Mission.Agents)
            {
                if (!ChooseAgent.IsFriendOf(ShootAgent) && ChooseAgent.IsHuman && ChooseAgent.CurrentMortalityState != MortalityState.Invulnerable)
                {
                    list1.Add(ChooseAgent);
                }


            }

            foreach (Agent ChooseAgent in list1)
            {
                vAgent = ChooseAgent;
                EquipmentIndex mainHandIndex = ChooseAgent.GetWieldedItemIndex(Agent.HandIndex.OffHand);
                if (mainHandIndex == EquipmentIndex.None || vAgent.GetCurrentActionType(1) != Agent.ActionCodeType.DefendShield || isBehindTarget(ChooseAgent, ShootAgent))//如果左手为空或者没有顶盾或者处于身后
                {
                    list2.Add(ChooseAgent);
                }
            }
            list1.Clear();
            foreach (Agent ChooseAgent in list2)
            {

                vAgent = ChooseAgent;
                if (CanSeeAgent(ShootAgent, ChooseAgent))//射线检测，能看到这个agent
                {
                    list1.Add(ChooseAgent);
                }
            }
            list2.Clear();

            list1.Sort((x, y) => (x.Position - ShootAgent.Position).Length.CompareTo((y.Position - ShootAgent.Position).Length));
            float len = float.MaxValue;
            foreach (Agent ChooseAgent in list1)
            {
                //if ((ChooseAgent.Position - AgentLookPos(ShootAgent)).Length < len)
                //{
                //    len = (ChooseAgent.Position - AgentLookPos(ShootAgent)).Length;
                //    vAgent = ChooseAgent;
                //}
                // InformationManager.DisplayMessage(new InformationMessage($"{(ChooseAgent.Position - ShootAgent.Position).Length}"));
                vAgent = ChooseAgent; break;
            }

            if (vAgent != null && CanSeeAgent(ShootAgent, vAgent) && AgentShotAgent(ShootAgent, vAgent) != 0)
            {
                //vAgent.SetTargetPosition(vAgent.Position.AsVec2);
                //vAgent.SetTargetPosition(ShootAgent.Position.AsVec2);

                ShootAgent.SetAttackState((int)Agent.ActionStage.AttackRelease);
                //ShootAgent.SetAttackState((int)Agent.ActionStage.AttackReady);
                return true;
            }

            return false;
        }
        /// <summary>
        /// 2是否在1身后
        /// </summary>
        /// <param name="agent1"></param>
        /// <param name="agent2"></param>
        /// <returns></returns>
        public static bool isBehindTarget(Agent agent1, Agent agent2)//
        {
            Vec2 rel_pos = agent1.Position.AsVec2 - agent2.Position.AsVec2;
            Vec2 dir_1 = agent1.LookDirection.AsVec2;
            dir_1 = -dir_1;
            if (dir_1.x * rel_pos.x + dir_1.y * rel_pos.y < 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 自瞄步骤2，决定实际射击的位置，添加预瞄功能
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="vagent"></param>
        /// <returns></returns>
        public static int AgentShotAgent(Agent agent, Agent vagent)
        {
            if (agent.Equipment != null)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut(0, agent);
                    return 0;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = (float)mainHandEquipmentElement.GetModifiedMissileSpeedForCurrentUsage();

                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);
                if (ThrewMeleeWeapon)
                { baseSpeed = -1; }

                Vec3 headPosition = agent.GetEyeGlobalPosition();
                Vec3 VheadPosition = vagent.GetEyeGlobalPosition();
                Vec3 VAgenSpeed = Vec3.Invalid;
                if (SkillSystemBehavior.ActiveComponents.TryGetValue(vagent.Index, out var data))
                {
                    VAgenSpeed = data.Speed.speed;
                }
                Vec3 shotDir = CalculateProjectileFiringSolution(headPosition, VheadPosition, baseSpeed, 9.81f);
                float timeOld = (headPosition - VheadPosition).AsVec2.Length / (baseSpeed * shotDir.AsVec2.Length);
                float timeNew = float.MaxValue;
                //VheadPosition.z -= 15 / 100f;
                //VheadPosition.y += 15 / 100f;

                while (TaleWorlds.Library.MathF.Abs(timeOld - timeNew) > 0.001f)
                {
                    timeNew = timeOld;
                    Vec3 s = MultiplyVectorByScalar(VAgenSpeed, timeOld);
                    //s = vagent.LookDirection.AsVec2 * VAgenSpeed.Length * timeOld;
                    VheadPosition = vagent.GetEyeGlobalPosition() + s;
                    shotDir = CalculateProjectileFiringSolution(headPosition, VheadPosition, baseSpeed, 9.81f);
                    timeOld = (headPosition - VheadPosition).AsVec2.Length / (baseSpeed * shotDir.AsVec2.Length);

                }


                //GameEntity gameEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                //gameEntity.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "mangonel_mapicon_projectile", true));
                //gameEntity.SetLocalPosition(VheadPosition);


                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, headPosition, VheadPosition, baseSpeed);
                mainHandEquipmentElement.Amount = (short)(mainHandEquipmentElement.Amount - 1);
                return index;

            }
            SysOut(2, agent);
            return 0;
        }
        /// <summary>
        /// 自瞄弹道计算，任意位置射击某目标vagent
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="agent"></param>
        /// <param name="vagent"></param>
        /// <returns></returns>
        public bool PosShotAgent(Vec3 StartPos, Agent agent, Agent vagent)
        {
            if (agent.Equipment != null)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut(0, agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = -1;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list == null)
                {
                    SysOut("无弹道速度记录，需要进行一次射击", agent);
                    return false;
                }
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }

                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);
                mainHandEquipmentElement.Amount = (short)(mainHandEquipmentElement.Amount - 1);
                if (ThrewMeleeWeapon)
                { baseSpeed = -1; }

                Vec3 VheadPosition = vagent.GetEyeGlobalPosition();

                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, StartPos, VheadPosition, baseSpeed);
                if (index == 0)
                { SysOut("无有效目标", agent); return false; }


            }

            SysOut(2, agent);
            return false;
        }
        /// <summary>
        ///         函数需要传进来的参数和一代的addmissile基本一致，分别是：
        ///         谁，用什么武器，射击出什么投射物，在什么位置，以什么角度，什么速度射击。
        ///         其中“用什么武器（ShotWeapon）”可以随便填MissionWeapon ，近战远程投掷都无所谓，但是“射击出什么投射物（AmmoWeapon）”必须是一个投射物，否则报错。。
        ///         射击角度这里可以填入一个目标pos，如果传进来的武器能射击到这个pos，会自动计算一个射击角度，从StartPos射击到EndPos。如果不能射击到，一会的返回值会输出一个0，方便后续代码处理。
        /// </summary>
        /// <param name="shotAgent"></param>
        /// <param name="ShotWeapon"></param>
        /// <param name="AmmoWeapon"></param>
        /// <param name="StartPos"></param>
        /// <param name="StartDirOrEndPos"></param>
        /// <param name="MissileRealSpeed"></param>
        /// <returns></returns>
        public static int FireProjectileFromAgentWithWeaponAtPosition(Agent shotAgent, MissionWeapon ShotWeapon, MissionWeapon AmmoWeapon, Vec3 StartPos, Vec3 StartDirOrEndPos, float MissileRealSpeed = -1)
        {

            //需要设定一个缺省值，避免传入的物品是近战武器，从而无法获取弹药速度
            //首先是根据传递进来的ShotWeapon获取AddCustomMissile需要的missile的speed属性，如果传递进来一个近战武器，则固定使用30的速度，差不多是投矛的弹速。
            ////更新：两个speed知道怎么回事了，投射物真实速度是在OnAgentShootMissile里 进行获取，然后进行记录，再在这里使用
            if (ShotWeapon.CurrentUsageItem.IsRangedWeapon && MissileRealSpeed == -1)
            {
                if (SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(shotAgent.Index, out var list))
                {
                    foreach (AgentMissileSpeedData item in list)
                    {
                        if (item.Weapon.Item.Id == ShotWeapon.Item.Id)
                        {
                            MissileRealSpeed = item.MissileSpeed;
                        }
                    }
                }
                MissileRealSpeed = (float)ShotWeapon.GetModifiedMissileSpeedForCurrentUsage();
            }
            else if (!ShotWeapon.CurrentUsageItem.IsRangedWeapon && MissileRealSpeed == -1)
            {
                MissileRealSpeed = 30;
            }
            float MissilePanelSpeed = ShotWeapon.GetModifiedMissileSpeedForCurrentUsage();
            //接着初始化一个index值，然后遍历一遍已有的Missiles，确保index不会重复。确认后，把index添加到WoW_MissileIndex里。
            //同时把传递进来的武器的伤害值，以键值对的形式添加在WoW_WeaponMissile里，以后可以通过对应的index来获取到当时对应的射击武器伤害值。
            int index = 100;

            foreach (var missile in shotAgent.Mission.Missiles)
            {
                //if (missile.Index == index || SkillSystemBehavior.WoW_MissileIndex.Contains(index) || SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(index))
                //{
                //    //index++;//这边写的有点问题，应该至少再判定一下index+1后，是否在链表/字典里。因为不一定先射出的投射物先消失，所以这里直接这样写会index冲突。
                //}
                index = Math.Max(missile.Index, index);//干脆直接这样，index只增不减，反正只是一个数，不影响计算开销
            }
            index++;

            while (SkillSystemBehavior.WoW_MissileIndex.Contains(index))
            {
                index++;
            }
            SkillSystemBehavior.WoW_MissileIndex.Add(index);
            SkillSystemBehavior.WoW_WeaponMissile.Add(index, ShotWeapon.GetModifiedMissileDamageForCurrentUsage());
            //需要处理StartDirOrEndPos是dir还是pos。dir的长度会是1
            //这里利用一下pos和rot的特性，判定传递进来的StartDirOrEndPos是一个角度还是地点，如果是角度，直接去生成投射物；如果是地点，走一下弹道计算的代码后，再生成投射物。
            //最后返回一下投射物的index，如果返回了0，则说明自动瞄准无法计算出有效的弹道。
            if (ShotWeapon.Item.ToString() != "composite_steppe_bow" && 1 == 2)//限制一下，自动步枪武器不触发动作
            {
                if (ShotWeapon.CurrentUsageItem.IsConsumable)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_javelin_with_shield"));
                else if (ShotWeapon.CurrentUsageItem.WeaponClass == WeaponClass.Bow)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_bow"));
                else if (ShotWeapon.CurrentUsageItem.WeaponClass == WeaponClass.Crossbow)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_crossbow"));
            }


            if (Math.Round(StartDirOrEndPos.Length) == 1)
            {
                Mission.Current.AddCustomMissile(shotAgent, AmmoWeapon, StartPos, StartDirOrEndPos, shotAgent.LookRotation, MissilePanelSpeed, MissileRealSpeed, true, null, index);
            }
            else
            {

                if (ShotWeapon.CurrentUsageItem.IsConsumable)
                { StartDirOrEndPos.z -= 0.5f; }
                Vec3 shotDir = CalculateProjectileFiringSolution(StartPos, StartDirOrEndPos, MissileRealSpeed, 9.81f);
                if (shotDir == Vec3.Invalid || shotDir.x.Equals(float.NaN) || shotDir.y.Equals(float.NaN) || shotDir.z.Equals(float.NaN))
                {
                    SkillSystemBehavior.WoW_MissileIndex.Remove(index);
                    SkillSystemBehavior.WoW_WeaponMissile.Remove(index);
                    return 0;
                }
                Mission.Current.AddCustomMissile(shotAgent, AmmoWeapon, StartPos, shotDir, shotAgent.LookRotation, MissilePanelSpeed, MissileRealSpeed, true, null, index);
            }

            return index;
        }
        /// <summary>
        /// pos射击pos，弹道计算，输出射击角度。凑合
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="speed"></param>
        /// <param name="gravity"></param>
        /// <returns></returns>
        public static Vec3 CalculateProjectileFiringSolution(Vec3 start, Vec3 end, float speed, float gravity)
        {
            // 计算水平距离
            Vec2 horizontalDistance = new Vec2(end.x - start.x, end.y - start.y);
            float horizontalRange = horizontalDistance.Length;

            // 计算垂直距离，即高度差
            float verticalDistance = end.z - start.z;

            // 计算发射角度的可能解
            float speedSquared = speed * speed;
            float sqrtTerm = speedSquared * speedSquared - gravity * (gravity * horizontalRange * horizontalRange + 2 * verticalDistance * speedSquared);

            // 如果这个术语小于0，则没有实际的解决方案，因为速度不够以克服重力
            if (sqrtTerm < 0.0f)
            {
                // throw new InvalidOperationException("No valid firing solution for given parameters.");
                return Vec3.Invalid;
            }

            float sqrtValue = (float)Math.Sqrt(sqrtTerm);

            // 取两个可能的解中较小的一个（较高的一个将是较大的发射角）
            float angle = (float)Math.Atan2(speedSquared - sqrtValue, gravity * horizontalRange);

            // 将发射角转换为方向向量
            Vec3 firingSolution = new Vec3(horizontalDistance.x, horizontalDistance.y, 0);
            firingSolution.Normalize();
            firingSolution *= (float)Math.Cos(angle) * speed;   // 水平速度分量
            firingSolution.z = (float)Math.Sin(angle) * speed;  // 垂直速度分量
            firingSolution.Normalize();
            return firingSolution;
        }
        /// <summary>
        /// 废弃，弹道显示的效果太差了
        /// </summary>
        /// <param name="agent"></param>
        public static void dandaoxianshi(Agent agent)
        {
            // 获取Agent主手中武器的Index索引
            EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
            if (mainHandIndex == EquipmentIndex.None)
            {
                return;
            }
            // EquipmentIndex转MissionWeapon
            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
            float baseSpeed = -1;
            SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
            if (list == null)
            {
                baseSpeed = mainHandEquipmentElement.GetModifiedMissileSpeedForCurrentUsage();
            }
            else
            {
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }
            }
            if (mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
            {
                for (float i = 0.5f; i <= 5; i = i + 0.15f)
                {
                    Vec3 v3 = Script.CalculatePositionAtTime(Agent.Main.GetEyeGlobalPosition(), Agent.Main.LookDirection, baseSpeed, i);
                }

            }

            return;
        }
        /// <summary>
        /// 废弃，弹道显示的效果太差了
        /// </summary>
        public static Vec3 CalculatePositionAtTime(Vec3 initialPosition, Vec3 direction, float initialSpeed, float time)//弹道显示
        {
            // 确保方向向量是单位向量
            direction.Normalize();

            // 计算x, y, z坐标
            float x = initialPosition.x + direction.x * initialSpeed * time;
            float y = initialPosition.y + direction.y * initialSpeed * time;
            // 对于z坐标，考虑重力影响
            float z = initialPosition.z + direction.z * initialSpeed * time - 0.5f * 9.81f * time * time;

            if (SkillSystemBehavior.WoW_Line.Count < 30)
            {
                GameEntity gameEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                //gameEntity.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "mangonel_mapicon_projectile", true));
                gameEntity.AddMesh(Mesh.GetFromResource("ballista_projectile_flying"));
                MatrixFrame matrixFrame = new MatrixFrame(new Mat3(new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1)), new Vec3((float)x, (float)y, (float)z));
                gameEntity.SetGlobalFrame(matrixFrame);
                SkillSystemBehavior.WoW_CustomGameEntity.Add(gameEntity);
                SkillSystemBehavior.WoW_Line.Add(time, gameEntity);
                gameEntity.SetLocalPosition(new Vec3((float)x, (float)y, (float)z));
            }
            else
            {
                foreach (var dict in SkillSystemBehavior.WoW_Line)
                {
                    if (dict.Key == time)
                    {
                        dict.Value.SetGlobalFrame(new MatrixFrame(new Mat3(new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1)), new Vec3((float)x, (float)y, (float)z)));
                        dict.Value.SetLocalPosition(new Vec3((float)x, (float)y, (float)z));
                    }
                }
            }


            return new Vec3(x, y, z);
        }
        /// <summary>
        /// 魔法伤害脚本
        /// </summary>
        /// <param name="Caster"></param>
        /// <param name="Victim"></param>
        /// <param name="BaseDamage"></param>
        /// <param name="DamageType"></param>
        public static void CalculateFinalMagicDamage(Agent Caster, Agent Victim, float BaseDamage, String DamageType)
        {
            float DifHP = Victim.Health;
            DifHP -= BaseDamage;
            Victim.Health = DifHP;
            InformationManager.DisplayMessage(new InformationMessage("造成了" + BaseDamage.ToString() + "点" + DamageType + "伤害"));

            if (Victim.Health <= 0)
            {
                Blow blow = new Blow(Caster.Index);
                blow.InflictedDamage = (int)BaseDamage;
                Victim.Die(blow);
                //Mission.Current.KillAgentCheat(Victim);
            }
        }

        public static bool AgentShootConeOfArrows(Agent casterAgent, int v)
        {
            if (ConeOfArrows(casterAgent, v))
            {
                return true;
            }
            else return false;
        }

        private static bool ConeOfArrows(Agent agent, int num)
        {
            if (agent.Equipment != null)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetWieldedItemIndex(Agent.HandIndex.MainHand);
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut("无有效武器", agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = -1;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list == null)
                { SysOut("无弹道速度记录，需要进行一次射击", agent); return false; }
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }

                Vec3 headPosition = agent.GetEyeGlobalPosition();
                Random random = new Random();
                for (int i = num; i > 0; i--)
                {

                    float randomValue = -1.2f + random.NextFloat() * 2.4f;

                    //每次循环的时候，重新取一遍本身射击的角度，去做角度转换
                    //角度的获取和改变，以后都用 Mat3 mat3 = agent.LookRotation;需要输出vec3时，用mat3.f获取vec3的值
                    Mat3 mat3 = agent.LookRotation;
                    float radians = (randomValue) * (3.1415f / 180.0f); // 角度转换为弧度

                    mat3.RotateAboutUp(radians);
                    randomValue = 1.2f + random.NextFloat() * 2.4f;
                    radians = (randomValue) * (3.1415f / 180.0f); // 角度转换为弧度

                    mat3.RotateAboutSide(radians);
                    int index = FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, headPosition, mat3.f, baseSpeed);


                }
                return true;
            }
            else
            {
                SysOut("该角色无装备", agent);
                return false;
            }
        }
    }
}
