using New_ZZZF.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static New_ZZZF.AgentSkillComponent;
using static New_ZZZF.KongNueCiFu;
using static TaleWorlds.PlayerServices.Avatar.AvatarData;
using MathF = TaleWorlds.Library.MathF;

namespace New_ZZZF
{
    /// <summary>
    /// 负责全局管理技能系统的 Mission 行为..\..\..\
    /// 核心职责：监听Agent创建事件并挂载技能组件
    /// </summary>

    public class SkillSystemBehavior : MissionLogic
    {

        /// <summary>
        /// activeComponents绑定了agent的新属性
        /// </summary>
        private readonly List<AgentSkillComponent> _activeComponents = new List<AgentSkillComponent>();
        public static Dictionary<int, AgentSkillComponent> ActiveComponents = new Dictionary<int, AgentSkillComponent>();


        public static Dictionary<float, GameEntity> WoW_Line = new Dictionary<float, GameEntity>()
        {


        }
        ;

        //辉剑圆阵类制导追踪
        public static Dictionary<GameEntity, ProjectileData> WoW_ProjectileDB = new Dictionary<GameEntity, ProjectileData>();
        public static List<GameEntity> WoW_CustomGameEntity = new List<GameEntity>() { };
        // 定义一个计时器变量
        // 定义触发间隔时间（0.5秒）
        private float _tickTimer05 = 0.0f;
        private const float TickInterval_05 = 0.5f;
        private float _tickTimer05_2 = 0.0f;
        private const float TickInterval_05_2 = 0.5f;
        private float _tickTimer001 = 0.0f;
        private const float TickInterval_001 = 0.01f;
        /// <summary>
        /// 平滑移动的效果。字典用来存放施法agent和目标agent
        /// </summary>
        public static Dictionary<int, Agent> WoW_AgentRushAgent = new Dictionary<int, Agent>();
        /// <summary>
        /// 平滑移动的效果。字典用来存放施法agent和目标agent
        /// </summary>
        public static Dictionary<int, Vec3> WoW_AgentRushPos = new Dictionary<int, Vec3>();



        /// <summary>
        /// WoW_MissileIndex存放新添加的投射物index
        /// </summary>
        public static List<int> WoW_MissileIndex = new List<int>();
        /// <summary>
        /// WoW_WeaponMissile存放投射物index和射击武器伤害值的键值对，可以根据投射物index查找到当时射击时武器的伤害值
        /// </summary>
        public static Dictionary<int, int> WoW_WeaponMissile = new Dictionary<int, int>();
        /// <summary>
        /// 记录agent投射物真实速度的字典，避免有提升投射物速度的效果时，投射物速度获取异常
        /// </summary>
        public static Dictionary<int, List<AgentMissileSpeedData>> WoW_AgentMissileSpeedData = new Dictionary<int, List<AgentMissileSpeedData>>();

        /// <summary>
        /// 当Agent被创建时触发（玩家或AI）
        /// </summary>
        public override void OnAgentCreated(Agent agent)
        {
            try
            {
                // 仅处理人类Agent（排除马匹等）
                if (!agent.IsHuman || agent.IsMount)
                    return;

                // 绑定技能组件到Agent
                var skillComponent = new AgentSkillComponent(agent);
                agent.AddComponent(skillComponent);

                // 获取兵种ID并初始化技能配置
                string troopId = GetTroopId(agent);
                skillComponent.InitializeFromTroop(troopId);
                _activeComponents.Add(skillComponent);
                ActiveComponents.Add(agent.Index, skillComponent);


                InformationManager.DisplayMessage(new InformationMessage("[技能系统] Agent" + agent.Name + " 已绑定技能组件"));
            }
            catch (Exception ex)
            {
                HandleError($"技能组件初始化失败 - Agent: {agent.Name}", ex);
            }
        }

        /// <summary>
        /// 获取Agent的兵种标识符（需根据实际游戏数据调整）
        /// </summary>
        private string GetTroopId(Agent agent)
        {
            // 战役模式中Hero对象的处理

            if (Game.Current.GameType is Campaign && agent.IsHero)
            {
                Hero? hero = (agent.Character as CharacterObject)?.HeroObject;
                if (hero != null)
                {
                    return hero.StringId;
                }
            }

            // 普通士兵处理
            return agent.Character?.StringId ?? "unknown_troop";
        }

        /// <summary>
        /// 统一错误处理
        /// </summary>
        private void HandleError(string context, Exception ex)
        {
            string errorMsg = $"[技能系统错误] {context}\n{ex.Message}";

            // 游戏内提示（仅限调试）
            InformationManager.DisplayMessage(new InformationMessage(errorMsg, Colors.Red));

            // 日志输出
            Debug.Print(errorMsg);
            Debug.Print(ex.StackTrace);
        }
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            //代码测试区
            if (Input.IsKeyPressed(InputKey.K))
            {
                if (base.Mission.IsInventoryAccessAllowed)
                {
                    //InventoryManager.OpenScreenAsInventory(new InventoryManager.DoneLogicExtrasDelegate(this.OnInventoryScreenDone));
                    SkillInventoryManager.OpenScreenAsInventory(new SkillInventoryManager.DoneLogicExtrasDelegate(this.OnInventoryScreenDone));
                    return;
                }
                InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText((base.Mission.Mode == MissionMode.Battle || base.Mission.Mode == MissionMode.Duel) ? "str_cannot_reach_inventory_during_battle" : "str_cannot_reach_inventory", null).ToString()));
                return;
            }
            if (Mission.Current != null && Mission.MainAgent != null && Input.IsKeyPressed(InputKey.L))
            {
                //Agent agent =Script.FindClosestAgentToCaster(Agent.Main, Mission.Current.Agents);
                for (int i = 0; i < 20; i++)
                {
                    Script.SysOut(Agent.Main.GetCurrentAction(i).Name, Agent.Main);
                }
            }
            if (Mission.Current != null && Mission.MainAgent != null && Input.IsKeyPressed(InputKey.O))
            {
                Script.AgentListIFF(Agent.Main, Mission.Current.Agents, out var friendAgent, out var foeAgent);

                foreach (var agent in Mission.Current.Agents)
                {
                    if (agent.HasMount)
                    { agent.MountAgent.SetTargetPosition(foeAgent[0].GetEyeGlobalPosition().AsVec2); }
                    if (foeAgent.Count > 0 && agent.Index != Agent.Main.Index)
                    {
                        agent.SetTargetPositionAndDirection(Agent.Main.GetEyeGlobalPosition().AsVec2, Agent.Main.LookDirection);
                        agent.ClearTargetFrame();
                        //WorldPosition worldPosition = agent.GetRetreatPos();
                        //worldPosition = agent.GetWorldPosition();
                        //agent.Retreat(agent.GetWorldPosition());
                        Vec3 vec3 = Agent.Main.Position;
                        vec3.y += 10;
                        Vec2 vec2 = -Agent.Main.LookDirection.AsVec2;
                        agent.SetInitialFrame(vec3, vec2);
                        agent.SetActionChannel(1, ActionIndexCache.Create("act_reload_crossbow"), false, 172UL);
                    }
                }
            }

            //测试区↑
            // 更新计时器
            _tickTimer05 += dt;
            _tickTimer05_2 += dt;
            _tickTimer001 += dt;

            // 创建副本以避免遍历时集合被修改
            List<AgentSkillComponent> componentsToProcess = _activeComponents.ToList();
            foreach (Agent agent in Mission.Agents)//刷新位置和速度信息
            {
                if (ActiveComponents.TryGetValue(agent.Index, out var data))
                {
                    data.Speed.Tick(dt);
                }
            }
            foreach (var comp in componentsToProcess)
            {
                // 检查组件是否有效
                if (comp.AgentInstance == null || !comp.AgentInstance.IsActive())
                {
                    continue; // 跳过无效组件
                }

                // 原有的处理逻辑
                if (comp.AgentInstance.IsHero)
                {
                    comp.Tick(dt);
                }

                if (_tickTimer05 >= TickInterval_05_2)
                {
                    if (!comp.AgentInstance.IsHero)
                    {
                        comp.Tick(TickInterval_05_2);
                    }
                    comp.CoolDownTick(TickInterval_05_2);
                }
            }

            // 如果计时器超过了间隔时间，则减去间隔时间
            // 这样可以避免因为累积误差导致的调用频率不稳定
            if (_tickTimer05 >= TickInterval_05)
            {
                _tickTimer05 -= TickInterval_05;
                foreach (GameEntity missileEntity in WoW_CustomGameEntity.ToList())
                {
                    if (!WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                        continue;
                    //每0.5秒，刷新一下目标agent，方便打多个人
                    if (data.TargetAgent != null)
                    {
                        Vec3 currentPos = missileEntity.GlobalPosition;
                        Agent castAgent = data.CasterAgent;
                        List<Agent> list = Script.FindAgentsWithinSpellRange(currentPos, 5);
                        List<Agent> FriendAgent = new List<Agent>();
                        List<Agent> FoeAgent = new List<Agent>();
                        Script.AgentListIFF(castAgent, list, out FriendAgent, out FoeAgent);
                        Agent tarAgent = Script.FindClosestAgentToPos(currentPos, FoeAgent);
                        if (tarAgent != null)
                        {
                            data.TargetAgent = tarAgent;
                        }
                    }
                    else if (data.TargetPos != null)
                    {

                    }
                    else
                    { break; }
                }
            }
            if (_tickTimer001 >= TickInterval_001)
            {
                _tickTimer001 -= TickInterval_001;
                foreach (GameEntity missileEntity in WoW_CustomGameEntity.ToList())
                {
                    if (!WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                        continue;
                    // 存在时间检测
                    if (data.Age > data.Lifetime)
                    {
                        DestroyProjectile(missileEntity);
                        continue;
                    }
                    // 获取当前导弹的位置和方向
                    Vec3 currentPos = missileEntity.GlobalPosition;
                    Mat3 currentRotation = missileEntity.GetGlobalFrame().rotation;
                    Vec3 currentDirection = currentRotation.f;
                    Vec3 targetPos = new Vec3();
                    // 计算目标方向
                    if (data.TargetAgent != null)
                    {
                        targetPos = data.TargetAgent.GetEyeGlobalPosition();
                    }
                    else if (data.TargetPos != null)
                    {
                        targetPos = data.TargetPos;
                    }
                    else
                    { break; }
                    Vec3 targetDirection = (targetPos - currentPos).NormalizedCopy();

                    // 计算当前方向与目标方向的夹角（弧度）
                    float dot = Vec3.DotProduct(currentDirection, targetDirection);
                    float angleRad = (float)Math.Acos(TaleWorlds.Library.MathF.Clamp(dot, -1f, 1f));

                    // 每帧最大允许转向弧度
                    float maxTurnRadPerFrame = (float)(data.MaxTurnRate * (System.Math.PI / 180f) * TickInterval_001);

                    Vec3 newDirection;
                    if (angleRad <= maxTurnRadPerFrame || angleRad <= float.Epsilon)
                    {
                        newDirection = targetDirection;
                    }
                    else
                    {
                        // 计算旋转轴（修正叉乘顺序）
                        Vec3 rotationAxis = Vec3.CrossProduct(currentDirection, targetDirection);
                        if (rotationAxis.LengthSquared < 0.001f)
                        {
                            // 如果旋转轴接近零向量，可以选择不旋转或者使用一个稳定的轴
                            newDirection = currentDirection;
                        }
                        else
                        {
                            rotationAxis.Normalize();

                            // 构造旋转矩阵（使用实际存在的API）
                            Mat3 rotationMat = Mat3.Identity;
                            rotationMat.RotateAboutAnArbitraryVector(rotationAxis, maxTurnRadPerFrame);
                            // 确保使用正确的方法来应用旋转矩阵到向量上
                            newDirection = rotationMat.TransformToParent(currentDirection).NormalizedCopy();
                        }
                    }

                    // 更新位置和朝向
                    //Vec3 newPosition = currentPos + newDirection * MoveSpeed * dt;
                    Vec3 newPosition = currentPos + newDirection * data.BaseSpeed * TickInterval_001;

                    // 动态速度变化（示例：随时间加速）
                    float currentSpeed = 10f * data.SpeedMultiplier * (1 + data.Age * 0.2f);
                    //// 螺旋轨迹计算
                    //if (data.SpiralIntensity > 0)
                    //{
                    //    Mat3 spiralMat = Mat3.Identity;
                    //    spiralMat.RotateAboutUp(data.Age * 5f); // Y轴旋转
                    //    Vec3 spiralOffset = spiralMat.TransformToParent(Vec3.Forward) * data.SpiralIntensity;
                    //    newPosition += spiralOffset;
                    //}
                    // 构造新旋转矩阵
                    Mat3 newRotation = Mat3.CreateMat3WithForward(newDirection); // 使用文档中存在的CreateMat3WithForward
                    missileEntity.SetGlobalFrame(new MatrixFrame(newRotation, newPosition));

                    if (data.Name == "JianQi")
                    {
                        JianQi.JianQiDamage(missileEntity);
                    }
                    // 距离判定（使用文档中的Distance方法）
                    if (currentPos.Distance(targetPos) < 0.5f)
                    {
                        if (data.Name == "HuiJianYuanZhen") { HuiJianYuanZhen.HuiJianYuanZhenDamage(missileEntity); }
                        missileEntity.Remove(1);
                        WoW_ProjectileDB.Remove(missileEntity);
                        WoW_CustomGameEntity.Remove(missileEntity);

                    }




                    newPosition = currentPos;
                    newPosition.z = -1;

                    if (Mission.Current.Scene.RayCastForClosestEntityOrTerrain(currentPos, newPosition, out var collisionDistance1, out var closestPoint1, out var gameE1, 1f))
                    {
                        if (collisionDistance1 < 0.5f)
                        {
                            missileEntity.Remove(1);
                            WoW_ProjectileDB.Remove(missileEntity);
                            WoW_CustomGameEntity.Remove(missileEntity);
                        }
                    }
                    newPosition.z = 1;
                    if (Mission.Current.Scene.RayCastForClosestEntityOrTerrain(currentPos, newPosition, out var collisionDistance12, out _, out _, 1f))
                    {
                        if (collisionDistance12 < 0.5f)
                        {
                            missileEntity.Remove(1);
                            WoW_ProjectileDB.Remove(missileEntity);
                            WoW_CustomGameEntity.Remove(missileEntity);
                            InformationManager.DisplayMessage(new InformationMessage("撞击地面"));
                        }
                    }
                }//ai

            }
            foreach (AgentSkillComponent agent in _activeComponents)
            {

                Agent TAgent;
                //平滑移动的实现部分
                if (WoW_AgentRushAgent.TryGetValue(agent.AgentInstance.Index, out TAgent))
                {
                    Vec3 directionToTarget = TAgent.Position - agent.AgentInstance.Position;

                    float _dashSpeed = 30.2f; // 速度，单位为米/秒
                    float distanceToMove = _dashSpeed * dt;
                    if (directionToTarget.Length < 1.5f || !agent.StateContainer.HasState("RushToAgentBuff"))
                    {

                        WoW_AgentRushAgent.Remove(agent.AgentInstance.Index); // 停止冲刺
                        agent.StateContainer.RemoveState("RushToAgentBuff", agent.BaseAgent);
                    }
                    else
                    {
                        // 否则，向目标位置移动指定的距离

                        Vec3 newPosition = agent.AgentInstance.Position + Script.MultiplyVectorByScalar(directionToTarget.NormalizedCopy(), distanceToMove);
                        agent.AgentInstance.TeleportToPosition(newPosition);

                    }
                }
                Vec3 vec3;
                if (WoW_AgentRushPos.TryGetValue(agent.AgentInstance.Index, out vec3))
                {
                    Vec3 directionToTarget = vec3 - agent.AgentInstance.Position;

                    float _dashSpeed = 15f; // 速度，单位为米/秒
                    float distanceToMove = _dashSpeed * dt;
                    if (directionToTarget.AsVec2.Length < 1f || !agent.StateContainer.HasState("RushToPosBuff"))
                    {

                        WoW_AgentRushPos.Remove(agent.AgentInstance.Index); // 停止冲刺
                        agent.StateContainer.RemoveState("RushToPosBuff", agent.AgentInstance);
                        if (agent.AgentInstance.GetCurrentAction(0).Name == "act_horse_fall_roll")
                        {
                            agent.AgentInstance.GetCurrentActionProgress(0);
                            agent.AgentInstance.SetActionChannel(0, ActionIndexCache.Create("act_none"));
                        }


                    }
                    else
                    {
                        // 否则，向目标位置移动指定的距离

                        Vec3 newPosition = agent.AgentInstance.Position + Script.MultiplyVectorByScalar(directionToTarget.NormalizedCopy(), distanceToMove);
                        agent.AgentInstance.TeleportToPosition(newPosition);

                    }
                }
            }
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
            if (affectedAgent != null && !affectedAgent.IsMount)
            {
                // 清理失效的组件引用
                var comp = affectedAgent.GetComponent<AgentSkillComponent>();
                if (comp != null)
                {
                    _activeComponents.Remove(comp);
                }

                ActiveComponents.Remove(affectedAgent.Index);
            }
        }
        public override void OnAfterMissionCreated()
        {
            base.OnAfterMissionCreated();
            //WoW_Agents.Clear();
            WoW_MissileIndex.Clear();
            //WoW_WeaponMissile.Clear();
            //WoW_SmartMisslie.Clear();
            WoW_ProjectileDB.Clear();
            WoW_Line.Clear();
            //WoW_gameEntityOwnedByAgent.Clear();
            WoW_CustomGameEntity.Clear();
            WoW_AgentMissileSpeedData.Clear();
            ActiveComponents.Clear();

        }
        protected override void OnEndMission()
        {
            base.OnEndMission();
            //WoW_Agents.Clear();
            WoW_MissileIndex.Clear();
            //WoW_WeaponMissile.Clear();
            //WoW_SmartMisslie.Clear();
            WoW_ProjectileDB.Clear();
            WoW_Line.Clear();
            //WoW_gameEntityOwnedByAgent.Clear();
            WoW_CustomGameEntity.Clear();
            WoW_AgentMissileSpeedData.Clear();
            ActiveComponents.Clear();
        }
        private void ExecuteHitEvents(Agent attacker, Agent victim, MissionWeapon affectorWeapon, Blow blow, AttackCollisionData collisionData)
        {
            Random random = new Random();
            if (attacker != null)
            {
                ActiveComponents.TryGetValue(attacker.Index, out var attackerSkillComponent);
                ActiveComponents.TryGetValue(attacker.Index, out var victimSkillComponent);
                //非击杀处理
                //不能先处理击杀事件，不然会先消耗复活次数再消耗护盾
                ActiveComponents.TryGetValue(victim.Index, out victimSkillComponent);
                if (victimSkillComponent != null)
                {
                    if (victimSkillComponent._shieldStrength > 0)
                    {
                        if (victimSkillComponent._shieldStrength >= blow.InflictedDamage)
                        {
                            Script.SysOut("损失" + blow.InflictedDamage.ToString() + "点护盾，并抵消同等伤害", victim);
                            victimSkillComponent._shieldStrength -= blow.InflictedDamage;
                            victim.Health = MathF.Clamp(victim.Health + blow.InflictedDamage,0,victimSkillComponent.MaxHP); 
                        }
                        else
                        {
                            Script.SysOut("损失" + victimSkillComponent._shieldStrength.ToString() + "点护盾，并抵消同等伤害", victim);
                            victim.Health = MathF.Clamp(victimSkillComponent._shieldStrength , 0, victimSkillComponent.MaxHP);
                            victimSkillComponent._shieldStrength = 0;

                        }
                        return;
                    }
                    if (victimSkillComponent.StateContainer.HasState("NaGouCiFuBuff"))
                    {
                        if (random.NextFloat() <= 0.5f)
                        {
                            victim.Health += Math.Max(blow.InflictedDamage, victimSkillComponent.MaxHP);
                        }
                    }
                    if (victimSkillComponent.StateContainer.HasState("TianQiBuff"))
                    {
                        victim.Health += Math.Max(blow.InflictedDamage, victimSkillComponent.MaxHP);
                    }

                }
                //击杀事件处理
                if (attackerSkillComponent != null)
                {
                    if (victim == null || victim.Health <= 0)
                    {
                        attackerSkillComponent.ChangeStamina(5);
                        if (attackerSkillComponent.StateContainer.HasState("JueXingBuff"))
                        {
                            attackerSkillComponent.StateContainer.UpdateStates(attacker, 0f);
                            attackerSkillComponent.ChangeStamina(5);
                        }
                        if (attackerSkillComponent.StateContainer.HasState("ZhanYiBuff"))
                        {
                            attacker.Health += (attackerSkillComponent.MaxHP - attacker.Health) * 0.5f;
                        }
                        if (attackerSkillComponent.StateContainer.HasState("KongNueCiFuBuff"))
                        {
                            KongNueCiFuBuff buff = attackerSkillComponent.StateContainer.GetState("KongNueCiFuBuff") as KongNueCiFuBuff;
                            buff.carnageRankCounter += victim.Character.Level;
                            if (buff.carnageRankCounter > 888)
                            {
                                buff.carnageRankCounter -= 888;
                                Script.SysOut("受赐获得复活次数", attacker);
                                attackerSkillComponent._lifeResurgenceCount += 1;
                            }
                            attackerSkillComponent.StateContainer.UpdateStates(attacker, 0f);
                            attackerSkillComponent.ChangeStamina(5);
                            attacker.Health += (attackerSkillComponent.MaxHP - attacker.Health) * 0.5f;
                        }
                        if (victimSkillComponent != null && victimSkillComponent._lifeResurgenceCount >= 1)
                        {
                            Script.SysOut("损失" + victimSkillComponent._lifeResurgenceCount.ToString() + "复活次数", victim);
                            victimSkillComponent._lifeResurgenceCount -= 1;
                            victim.Health += victimSkillComponent.MaxHP;
                        }
                    }


                }
            }


        }
        public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            base.OnMeleeHit(attacker, victim, isCanceled, collisionData);
        }

        public override void OnMissileHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData collisionData)
        {
            base.OnMissileHit(attacker, victim, isCanceled, collisionData);
            if (WoW_MissileIndex.Contains(collisionData.AffectorWeaponSlotOrMissileIndex))
            {
                WoW_MissileIndex.Remove(collisionData.AffectorWeaponSlotOrMissileIndex);
                WoW_WeaponMissile.Remove(collisionData.AffectorWeaponSlotOrMissileIndex);

            }

        }
        /// <summary>
        /// 完成了agent的伤害扣血流程后，进入这里
        /// </summary>
        /// <param name="受击"></param>
        /// <param name="攻击"></param>
        /// <param name="affectorWeapon"></param>
        /// <param name="blow"></param>
        /// <param name="attackCollisionData"></param>
        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);
            ExecuteHitEvents(affectorAgent, affectedAgent, affectorWeapon, blow, attackCollisionData);

        }
        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            base.OnAgentShootMissile(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);
            MissionWeapon missionWeapon = shooterAgent.Equipment[weaponIndex];
            float speed = velocity.Length;
            AgentMissileSpeedData agentMissileSpeedData = new AgentMissileSpeedData(missionWeapon, speed, shooterAgent);
            if (!WoW_AgentMissileSpeedData.ContainsKey(shooterAgent.Index))
            {
                List<AgentMissileSpeedData> list = new List<AgentMissileSpeedData>();
                list.Add(agentMissileSpeedData);
                WoW_AgentMissileSpeedData.Add(shooterAgent.Index, list);
            }
            else
            {
                WoW_AgentMissileSpeedData.TryGetValue(shooterAgent.Index, out var list);
                bool flag = false;
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == missionWeapon.Item.Id)
                    {
                        continue;
                    }

                    flag = true;
                    break;


                }
                if (flag)
                {
                    list.Add(agentMissileSpeedData);
                }

            }




        }
        private static void DestroyProjectile(GameEntity proj)
        {
            proj.Remove(1);
            WoW_CustomGameEntity.Remove(proj);
            WoW_ProjectileDB.Remove(proj);
            // 可添加粒子爆炸效果
        }
        private void OnInventoryScreenDone()
        {
            Mission mission = Mission.Current;
            if (((mission != null) ? mission.Agents : null) != null)
            {
                foreach (Agent agent in Mission.Current.Agents)
                {
                    if (agent != null)
                    {
                        CharacterObject characterObject = (CharacterObject)agent.Character;
                        Campaign campaign = Campaign.Current;
                        bool flag;
                        if (campaign == null || campaign.GameMode != CampaignGameMode.Tutorial)
                        {
                            if (agent.IsHuman && characterObject != null && characterObject.IsHero)
                            {
                                Hero heroObject = characterObject.HeroObject;
                                flag = (((heroObject != null) ? heroObject.PartyBelongedTo : null) == MobileParty.MainParty);
                            }
                            else
                            {
                                flag = false;
                            }
                        }
                        else
                        {
                            flag = (agent.IsMainAgent && characterObject != null);
                        }
                        if (flag)
                        {
                            agent.UpdateSpawnEquipmentAndRefreshVisuals(Mission.Current.DoesMissionRequireCivilianEquipment ? characterObject.FirstCivilianEquipment : characterObject.FirstBattleEquipment);
                        }
                    }
                }
            }
        }



    }
}
//代码说明
//1. 核心逻辑
//Agent过滤：仅对人类Agent（IsHuman && !IsMount）绑定技能组件。

//组件挂载：通过 agent.AddComponent 添加 AgentSkillComponent。

//配置初始化：根据兵种ID加载对应的 SkillSet。

//2. 健壮性设计
//异常捕获：通过 try-catch 包裹关键逻辑，避免崩溃。

//错误反馈：

//游戏内显示红色警告消息（调试模式）。

//控制台输出详细错误堆栈。

//兵种ID容错：未知兵种时返回 "unknown_troop"，可由 SkillConfigManager 提供默认配置。

//3. 扩展性提示
//GetTroopId 方法：
//当前实现假设兵种ID存储在 agent.Character.StringId。如果实际数据源不同（如战役模式中的英雄），需在此方法内调整逻辑。

//调试信息：
//可通过 Debug.Print 输出技能加载详情，发布时注释掉即可。