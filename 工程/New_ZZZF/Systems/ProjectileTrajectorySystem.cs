using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 弹道显示系统
    /// </summary>
    public static class ProjectileTrajectorySystem
    {
        private static bool _hasCollided = false;
        private static Vec3 _collisionPoint = Vec3.Invalid;

        /// <summary>
        /// 每帧更新弹道显示
        /// </summary>
        public static void UpdateTrajectory(Agent agent, RangedSiegeWeapon rangedSiege = null)
        {
            if (rangedSiege == null)
            {
                // 清理所有特殊标记实体
                var keysToRemove = SkillSystemBehavior.WoW_Line
                    .Where(pair => pair.Key >= 0)
                    .Select(pair => pair.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    if (SkillSystemBehavior.WoW_Line.TryGetValue(key, out var entity))
                    {
                        entity.SetLocalPosition(Vec3.Zero);
                    }
                }
                return;
            }

            // 重置状态（如果武器发生变化）
            if (_hasCollided)
            {
                _hasCollided = false;
                _collisionPoint = Vec3.Invalid;
            }

            // 获取武器参数（保持你原有的反射代码）
            float shootingSpeed = GetShootingSpeed(rangedSiege);
            Vec3 shootingDir = GetShootingDirection(rangedSiege);
            Vec3 startPos = GetStartPosition(rangedSiege);

            firstFlag = false;
            // 计算弹道点
            for (float t = 0.3f; t <= 4.8f; t += 0.15f)
            {
                if (_hasCollided)
                {
                    // 碰撞后不再计算新点
                    break;
                }
                airFriction = 0.003f;
                CalculatePositionAtTime(startPos, shootingDir, shootingSpeed, t);
            }
        }
        private static Vec3 _lastPosition = Vec3.Zero;
        public static void RenderDebugLine(Vec3 position, Vec3 direction, uint color, bool depthCheck, float time)
        {
            try
            {
                // 1. 获取内部类的类型（需要完整的程序集限定名）
                Type engineAppType = Type.GetType("TaleWorlds.Engine.EngineApplicationInterface, TaleWorlds.Engine");
                if (engineAppType == null)
                {
                    throw new TypeLoadException("EngineApplicationInterface type not found");
                }

                // 2. 获取静态IDebug字段
                FieldInfo debugField = engineAppType.GetField("IDebug",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (debugField == null)
                {
                    throw new MissingFieldException("IDebug field not found");
                }

                // 3. 获取IDebug接口实例
                object debugInterface = debugField.GetValue(null);
                if (debugInterface == null)
                {
                    throw new NullReferenceException("IDebug interface instance is null");
                }

                // 4. 获取接口方法信息
                MethodInfo renderMethod = debugInterface.GetType().GetMethod("RenderDebugLine",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                    typeof(Vec3),
                    typeof(Vec3),
                    typeof(uint),
                    typeof(bool),
                    typeof(float)
                    },
                    null);

                if (renderMethod == null)
                {
                    throw new MissingMethodException("RenderDebugLine method not found");
                }

                // 5. 创建参数数组
                object[] parameters = new object[]
                {
                position,
                direction,
                color,
                depthCheck,
                time
                };

                // 6. 调用方法
                renderMethod.Invoke(debugInterface, parameters);
            }
            catch (Exception ex)
            {
                // 处理反射异常
                Console.WriteLine($"反射调用失败: {ex}");
                //throw;
            }
        }
        public static void UpdateTrajectoryRangeWeapon(Agent agent)
        {
            // 获取Agent主手中武器的Index索引
            EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
            if (mainHandIndex == EquipmentIndex.None)
            {
                return;
            }
            // EquipmentIndex转MissionWeapon
            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
            float baseSpeed = -1;
            float MissileSpeedMul = agent.AgentDrivenProperties.MissileSpeedMultiplier;
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
            baseSpeed *= MissileSpeedMul;
            if (mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
            {
                firstFlag = false;
                for (float t = 0.0f; t <= 3f; t += 0.15f)
                {
                    t = TaleWorlds.Library.MathF.Round(t, 2);
                    airFriction = ProjectileTrajectorySystem.getMissionWeaponAirFriction(agent, out var equipmentIndex);
                    CalculatePositionAtTime(Agent.Main.GetEyeGlobalPosition(), Agent.Main.LookDirection, baseSpeed, t);
                }

            }
            return;

        }
        private static bool firstFlag = false;
        private static float airFriction;
        private static Vec3 CalculatePositionAtTime(Vec3 initialPosition, Vec3 direction, float initialSpeed, float time)
        {
            // 从配置获取空气阻力系数（示例使用箭的0.003）
            const float simulationDt = 0.01f; // 匹配原生的MISSILE_SIMULATION_DT

            direction.Normalize();
            Vec3 velocity = direction * initialSpeed;
            Vec3 position = initialPosition;

            // 内部时间步进积分
            for (float t = 0; t < time; t += simulationDt)
            {
                // 计算空气阻力（严格遵循原生公式）
                float speed = velocity.Length;
                if (speed > 0.001f)
                {
                    Vec3 drag = velocity.NormalizedCopy() * (airFriction * speed * speed * simulationDt);
                    velocity -= drag;
                }

                // 计算重力
                velocity.z -= 9.81f * simulationDt;

                // 更新位置
                position += velocity * simulationDt;
            }

            Vec3 currentPos = position;
            if (firstFlag)
            {
                Vec3 lastDir = (currentPos - _lastPosition);
                RenderDebugLine(_lastPosition, lastDir, 4294967295U, true, 0);
            }
            else
            {
                firstFlag = !firstFlag;
            }
            _lastPosition = currentPos;
            // 碰撞检测
            if (!_hasCollided && Mission.Current.Scene.RayCastForClosestEntityOrTerrain(
                currentPos, currentPos + Vec3.Up * 5000f,
                out var collisionDistance, out var closestPoint, out _, 1f))
            {
                if (collisionDistance < 5f)
                {
                    _hasCollided = true;
                    _collisionPoint = closestPoint;
                    GenerateImpactCircle(_collisionPoint);

                    // 新功能：移动碰撞点之后的所有弹道实体（11-99）到 Vec3.Zero
                    ClearTrajectoryAfterCollision(time);
                    return _collisionPoint;
                }
            }

            // 常规弹道点处理（未碰撞或碰撞前）
            if (!_hasCollided)
            {
                if (SkillSystemBehavior.WoW_Line.Count < 30)
                {
                    CreateProjectileMarker(currentPos, time);
                }
                else if (SkillSystemBehavior.WoW_Line.TryGetValue(time, out var entity))
                {
                    entity.SetLocalPosition(currentPos);
                }
            }

            return currentPos;
        }

        /// <summary>
        /// 碰撞后清除后续弹道实体（移动11-99到原点，保留0-10和100+）
        /// </summary>
        private static void ClearTrajectoryAfterCollision(float collisionTimeKey)
        {
            foreach (var pair in SkillSystemBehavior.WoW_Line.ToList()) // 遍历副本避免修改异常
            {
                float timeKey = pair.Key;
                // 仅处理普通弹道点（11-99），跳过碰撞标记点（100+）和碰撞点之前的点（0-10）
                if (timeKey > collisionTimeKey && timeKey < 100)
                {
                    pair.Value.SetLocalPosition(Vec3.Zero);
                }
            }
        }
        private static void GenerateImpactCircle(Vec3 center)
        {
            const int numPoints = 8;
            const float radius = 2.0f;

            for (int i = 0; i < numPoints; i++)
            {
                float timeKey = i + 100; // 特殊时间键范围 100-107
                float angle = i * (360f / numPoints);
                float rad = angle * (float)Math.PI / 180f;

                Vec3 pos = new Vec3(
                    center.x + radius * (float)Math.Cos(rad),
                    center.y + radius * (float)Math.Sin(rad),
                    center.z
                );

                // 直接复用原有字典
                if (SkillSystemBehavior.WoW_Line.TryGetValue(timeKey, out GameEntity entity))
                {
                    // 存在则更新位置
                    entity.SetLocalPosition(pos);
                }
                else
                {
                    // 不存在则创建新实体
                    entity = GameEntity.CreateEmpty(Mission.Current.Scene);
                    entity.SetContourColor(0xFF00FFFFu, true);
                    entity.AddAllMeshesOfGameEntity(GameEntity.Instantiate(
                        Mission.Current.Scene, "mangonel_mapicon_projectile", true));
                    entity.SetLocalPosition(pos);

                    // 添加到原有系统
                    SkillSystemBehavior.WoW_Line[timeKey] = entity;
                    if (!SkillSystemBehavior.WoW_CustomGameEntity.Contains(entity))
                    {
                        SkillSystemBehavior.WoW_CustomGameEntity.Add(entity);
                    }
                }
            }
        }


        private static void CreateProjectileMarker(Vec3 position, float timeKey)
        {
            GameEntity entity = GameEntity.CreateEmpty(Mission.Current.Scene);
            //entity.SetContourColor(0xFF00FFFFu, true);
            //entity.AddMesh(Mesh.GetFromResource("ballista_projectile_flying"));
            //entity.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "mangonel_mapicon_projectile", true));
            entity.SetLocalPosition(position);

            if (!SkillSystemBehavior.WoW_CustomGameEntity.Contains(entity))
            {
                SkillSystemBehavior.WoW_CustomGameEntity.Add(entity);
            }

            SkillSystemBehavior.WoW_Line[timeKey] = entity;
        }

        // 以下是你的原有辅助方法（保持原样）
        private static float GetShootingSpeed(RangedSiegeWeapon weapon)
        {
            PropertyInfo prop = weapon.GetType().GetProperty("ShootingSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return prop != null ? (float)prop.GetValue(weapon) : 0f;
        }

        private static Vec3 GetShootingDirection(RangedSiegeWeapon weapon)
        {
            PropertyInfo prop = weapon.GetType().GetProperty("ShootingDirection",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return prop != null ? (Vec3)prop.GetValue(weapon) : Vec3.Invalid;
        }

        private static Vec3 GetStartPosition(RangedSiegeWeapon weapon)
        {
            WeakGameEntity? launcher = weapon.GameEntity.GetChildren().FirstOrDefault(x => x.Name == "clean");
            WeakGameEntity? startPos = launcher?.GetChildren().FirstOrDefault(x => x.Name == "projectile_leaving_position");
            return startPos?.GlobalPosition ?? Vec3.Invalid;
        }
        public static float getMissionWeaponAirFriction(Agent agent, out EquipmentIndex EquipmentIndex)
        {
            //因为missionweapon.ammoweapon会在武器装填时输出空值，所以还是重写一个弹药获取的代码
            //现在是获取手上的武器后，依次遍历agent的武器栏位。如果遍历到的物品是手上武器的弹药，并且弹药数量大于0，则设置为一会addmissile的弹药。
            //手上武器为近战时还要做个缺省值
            EquipmentIndex = EquipmentIndex.None;
            EquipmentIndex 备用index = EquipmentIndex.None;//如果弹药数量为空的话，弹药所在的index记录在这个位置，如果本身有弹药但是因为用完了的时候，输出这个位置
            if (!agent.IsHuman || agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None)
                return 0;
            MissionWeapon missionWeapon = agent.Equipment[agent.GetPrimaryWieldedItemIndex()];
            if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].CurrentUsageItem.IsRangedWeapon)//手上的物品是远程武器
            {
                for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)//循环遍历武器栏位
                {

                    if (!agent.Equipment[agent.GetPrimaryWieldedItemIndex()].IsEmpty && !agent.Equipment[equipmentIndex].IsEmpty)//如果手上的武器和遍历的物品非空
                    {
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.Crossbow)//如果手上的武器是弩
                        {
                            return 0.005f;
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bow)
                        {
                            return 0.003f;
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.Javelin)
                        {
                            return 0.002f;
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.ThrowingAxe)
                        {
                            return 0.007f;
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.ThrowingKnife)
                        {
                            return 0.007f;
                        }
                    }

                }
            }
            return 0f;
        }
    }
}
