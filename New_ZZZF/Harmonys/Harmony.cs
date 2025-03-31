using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.MountAndBlade;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System;
using TaleWorlds.Engine;
using MathF = TaleWorlds.Library.MathF;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using System.Reflection;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem;
namespace New_ZZZF.Harmonys
{

    [HarmonyPatch(typeof(ItemPreviewVM), "Open")]
    public class ItemPreviewVMPatch
    {
        // 原始方法的前缀补丁
        [HarmonyPrefix]
        
        public static bool Open_Prefix(ItemPreviewVM __instance, EquipmentElement item)
        {
            string s = "";
            // 自定义逻辑：在调用原始方法之前执行


            __instance.ItemTableau.FillFrom(item, BannerCode.CreateFrom(Clan.PlayerClan.Banner).Code);
            __instance.ItemName = item.Item.Name.ToString();
            __instance.IsSelected = true;

            if (SkillFactory._skillRegistry.TryGetValue(item.Item.StringId, out var skillBase))
            {
                if (skillBase.Description != null)
                {
                    s = item.Item.StringId;
                    __instance.ItemName = skillBase.Description.ToString();
                }
            }

            // 如果返回true，则继续执行原始方法；如果返回false，则跳过原始方法
            return false;
        }

    }
    //// 针对 MeleeHitCallback 的 Harmony 补丁
    //[HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    //public static class MeleeHitCallbackPatch
    //{
    //    public static void Call_OnEntityHit(
    //            Mission missionInstance,
    //            GameEntity entity,
    //            Agent attackerAgent,
    //            int inflictedDamage,
    //            DamageTypes damageType,
    //            Vec3 impactPosition,
    //            Vec3 impactDirection,
    //            in MissionWeapon weapon)
    //    {
    //        // 使用 AccessTools 获取目标方法的 MethodInfo
    //        MethodInfo onEntityHitMethod = AccessTools.Method(
    //            typeof(Mission),
    //            "OnEntityHit",
    //            new Type[] {
    //            typeof(GameEntity),
    //            typeof(Agent),
    //            typeof(int),
    //            typeof(DamageTypes),
    //            typeof(Vec3),
    //            typeof(Vec3),
    //            typeof(MissionWeapon).MakeByRefType() // 处理 in 参数
    //            }
    //        );

    //        // 调用获取到的方法
    //        onEntityHitMethod?.Invoke(
    //            missionInstance,
    //            new object[] {
    //            entity,
    //            attackerAgent,
    //            inflictedDamage,
    //            damageType,
    //            impactPosition,
    //            impactDirection,
    //            weapon // 结构体按值传递
    //            }
    //        );
    //    }
    //    public static void Call_AddCombatLogSafe(
    //        Mission missionInstance,
    //        Agent attackerAgent,
    //        Agent victimAgent,
    //        GameEntity hitEntity,
    //        CombatLogData combatLog)
    //    {
    //        // 使用 AccessTools 获取目标方法的 MethodInfo
    //        MethodInfo addCombatLogSafeMethod = AccessTools.Method(
    //            typeof(Mission),
    //            "AddCombatLogSafe",
    //            new Type[] { typeof(Agent), typeof(Agent), typeof(GameEntity), typeof(CombatLogData) }
    //        );

    //        // 调用获取到的方法
    //        addCombatLogSafeMethod?.Invoke(missionInstance, new object[] { attackerAgent, victimAgent, hitEntity, combatLog });
    //    }
    //    private static void PrintAttackCollisionResults(Mission mission,Agent attackerAgent, Agent victimAgent, GameEntity hitEntity, ref AttackCollisionData attackCollisionData, ref CombatLogData combatLog)
    //    {
    //        if (attackCollisionData.IsColliderAgent && !attackCollisionData.AttackBlockedWithShield && attackerAgent != null && (attackerAgent.CanLogCombatFor || victimAgent.CanLogCombatFor) && victimAgent.State == AgentState.Active)
    //        {
    //            Call_AddCombatLogSafe(mission,attackerAgent, victimAgent, hitEntity, combatLog);
    //        }
    //    }
    //    private static void DecideWeaponCollisionReaction(Blow registeredBlow, in AttackCollisionData collisionData, Agent attacker, Agent defender, in MissionWeapon attackerWeapon, bool isFatalHit, bool isShruggedOff, out MeleeCollisionReaction colReaction)
    //    {
    //        AttackCollisionData attackCollisionData = collisionData;
    //        if (attackCollisionData.IsColliderAgent)
    //        {
    //            attackCollisionData = collisionData;
    //            if (attackCollisionData.StrikeType == 1)
    //            {
    //                attackCollisionData = collisionData;
    //                if (attackCollisionData.CollisionHitResultFlags.HasAnyFlag(CombatHitResultFlags.HitWithStartOfTheAnimation))
    //                {
    //                    colReaction = MeleeCollisionReaction.Staggered;
    //                    return;
    //                }
    //            }
    //        }
    //        attackCollisionData = collisionData;
    //        if (!attackCollisionData.IsColliderAgent)
    //        {
    //            attackCollisionData = collisionData;
    //            if (attackCollisionData.PhysicsMaterialIndex != -1)
    //            {
    //                attackCollisionData = collisionData;
    //                if (PhysicsMaterial.GetFromIndex(attackCollisionData.PhysicsMaterialIndex).GetFlags().HasAnyFlag(PhysicsMaterialFlags.AttacksCanPassThrough))
    //                {
    //                    colReaction = MeleeCollisionReaction.SlicedThrough;
    //                    return;
    //                }
    //            }
    //        }
    //        attackCollisionData = collisionData;
    //        if (!attackCollisionData.IsColliderAgent || registeredBlow.InflictedDamage <= 0)
    //        {
    //            colReaction = MeleeCollisionReaction.Bounced;
    //            return;
    //        }
    //        attackCollisionData = collisionData;
    //        if (attackCollisionData.StrikeType == 1 && attacker.IsDoingPassiveAttack)
    //        {
    //            colReaction = MissionGameModels.Current.AgentApplyDamageModel.DecidePassiveAttackCollisionReaction(attacker, defender, isFatalHit);
    //            return;
    //        }
    //        if (HitWithAnotherBone(collisionData, attacker, attackerWeapon))
    //        {
    //            colReaction = MeleeCollisionReaction.Bounced;
    //            return;
    //        }
    //        MissionWeapon missionWeapon = attackerWeapon;
    //        WeaponClass weaponClass;
    //        if (missionWeapon.IsEmpty)
    //        {
    //            weaponClass = WeaponClass.Undefined;
    //        }
    //        else
    //        {
    //            missionWeapon = attackerWeapon;
    //            weaponClass = missionWeapon.CurrentUsageItem.WeaponClass;
    //        }
    //        WeaponClass weaponClass2 = weaponClass;
    //        missionWeapon = attackerWeapon;
    //        if (missionWeapon.IsEmpty || isFatalHit || !isShruggedOff)
    //        {
    //            missionWeapon = attackerWeapon;
    //            if (missionWeapon.IsEmpty && defender != null && defender.IsHuman)
    //            {
    //                attackCollisionData = collisionData;
    //                if (!attackCollisionData.IsAlternativeAttack)
    //                {
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.VictimHitBodyPart == BoneBodyPartType.Chest)
    //                    {
    //                        goto IL_1B2;
    //                    }
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.VictimHitBodyPart == BoneBodyPartType.ShoulderLeft)
    //                    {
    //                        goto IL_1B2;
    //                    }
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.VictimHitBodyPart == BoneBodyPartType.ShoulderRight)
    //                    {
    //                        goto IL_1B2;
    //                    }
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.VictimHitBodyPart == BoneBodyPartType.Abdomen)
    //                    {
    //                        goto IL_1B2;
    //                    }
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.VictimHitBodyPart == BoneBodyPartType.Legs)
    //                    {
    //                        goto IL_1B2;
    //                    }
    //                }
    //            }
    //            if ((weaponClass2 != WeaponClass.OneHandedAxe && weaponClass2 != WeaponClass.TwoHandedAxe) || isFatalHit || (float)collisionData.InflictedDamage >= defender.HealthLimit * 0.5f)
    //            {
    //                missionWeapon = attackerWeapon;
    //                if (missionWeapon.IsEmpty)
    //                {
    //                    attackCollisionData = collisionData;
    //                    if (!attackCollisionData.IsAlternativeAttack)
    //                    {
    //                        attackCollisionData = collisionData;
    //                        if (attackCollisionData.AttackDirection == Agent.UsageDirection.AttackUp)
    //                        {
    //                            goto IL_257;
    //                        }
    //                    }
    //                }
    //                attackCollisionData = collisionData;
    //                if (attackCollisionData.ThrustTipHit)
    //                {
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.DamageType == 1)
    //                    {
    //                        missionWeapon = attackerWeapon;
    //                        if (!missionWeapon.IsEmpty)
    //                        {
    //                            attackCollisionData = collisionData;
    //                            if (defender.CanThrustAttackStickToBone(attackCollisionData.VictimHitBodyPart))
    //                            {
    //                                goto IL_257;
    //                            }
    //                        }
    //                    }
    //                }
    //                colReaction = MeleeCollisionReaction.SlicedThrough;
    //                goto IL_261;
    //            }
    //        IL_257:
    //            colReaction = MeleeCollisionReaction.Stuck;
    //            goto IL_261;
    //        }
    //    IL_1B2:
    //        colReaction = MeleeCollisionReaction.Bounced;
    //    IL_261:
    //        attackCollisionData = collisionData;
    //        if (!attackCollisionData.AttackBlockedWithShield)
    //        {
    //            attackCollisionData = collisionData;
    //            if (!attackCollisionData.CollidedWithShieldOnBack)
    //            {
    //                return;
    //            }
    //        }
    //        if (colReaction == MeleeCollisionReaction.SlicedThrough)
    //        {
    //            colReaction = MeleeCollisionReaction.Bounced;
    //        }
    //    }
    //    private static void DecideAgentHitParticles(Agent attacker, Agent victim, in Blow blow, in AttackCollisionData collisionData, ref HitParticleResultData hprd)
    //    {
    //        if (victim != null && (blow.InflictedDamage > 0 || victim.Health <= 0f))
    //        {
    //            BlowWeaponRecord weaponRecord = blow.WeaponRecord;
    //            bool flag;
    //            if (weaponRecord.HasWeapon() && !blow.WeaponRecord.WeaponFlags.HasAnyFlag(WeaponFlags.NoBlood))
    //            {
    //                AttackCollisionData attackCollisionData = collisionData;
    //                flag = attackCollisionData.IsAlternativeAttack;
    //            }
    //            else
    //            {
    //                flag = true;
    //            }
    //            if (flag)
    //            {
    //                MissionGameModels.Current.DamageParticleModel.GetMeleeAttackSweatParticles(attacker, victim, blow, collisionData, out hprd);
    //                return;
    //            }
    //            MissionGameModels.Current.DamageParticleModel.GetMeleeAttackBloodParticles(attacker, victim, blow, collisionData, out hprd);
    //        }
    //    }
    //    private static void RegisterBlow(Mission mission,Agent attacker, Agent victim, GameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
    //    {
    //        b.VictimBodyPart = collisionData.VictimHitBodyPart;
    //        if (!collisionData.AttackBlockedWithShield)
    //        {
    //            if (collisionData.IsColliderAgent)
    //            {
    //                if (b.SelfInflictedDamage > 0 && attacker != null && attacker.IsActive() && attacker.IsFriendOf(victim))
    //                {
    //                    Blow blow;
    //                    AttackCollisionData attackCollisionData;
    //                    attacker.CreateBlowFromBlowAsReflection(b, collisionData, out blow, out attackCollisionData);
    //                    if (victim.IsMount && attacker.MountAgent != null)
    //                    {
    //                        attacker.MountAgent.RegisterBlow(blow, attackCollisionData);
    //                    }
    //                    else
    //                    {
    //                        attacker.RegisterBlow(blow, attackCollisionData);
    //                    }
    //                }
    //                if (b.InflictedDamage > 0)
    //                {
    //                    combatLogData.IsFatalDamage = (victim != null && victim.Health - (float)b.InflictedDamage < 1f);
    //                    combatLogData.InflictedDamage = b.InflictedDamage - combatLogData.ModifiedDamage;
    //                    PrintAttackCollisionResults(mission,attacker, victim, realHitEntity, ref collisionData, ref combatLogData);
    //                }
    //                victim.RegisterBlow(b, collisionData);
    //            }
    //            else if (collisionData.EntityExists)
    //            {
    //                var missilesFieldInfo = AccessTools.Field(typeof(Mission), "_missiles");
    //                var missiles = missilesFieldInfo.GetValue(mission) as Dictionary<int, Mission.Missile>;
    //                MissionWeapon missionWeapon = b.IsMissile ? missiles[b.WeaponRecord.AffectorWeaponSlotOrMissileIndex].Weapon : ((attacker != null && b.WeaponRecord.HasWeapon()) ? attacker.Equipment[b.WeaponRecord.AffectorWeaponSlotOrMissileIndex] : MissionWeapon.Invalid);
    //                Call_OnEntityHit(mission,realHitEntity, attacker, b.InflictedDamage, (DamageTypes)collisionData.DamageType, b.GlobalPosition, b.SwingDirection, missionWeapon);
    //                if (attacker != null && b.SelfInflictedDamage > 0)
    //                {
    //                    Blow blow2;
    //                    AttackCollisionData attackCollisionData2;
    //                    attacker.CreateBlowFromBlowAsReflection(b, collisionData, out blow2, out attackCollisionData2);
    //                    attacker.RegisterBlow(blow2, attackCollisionData2);
    //                }
    //            }
    //        }
    //        foreach (MissionBehavior missionBehavior in mission.MissionBehaviors)
    //        {
    //            missionBehavior.OnRegisterBlow(attacker, victim, realHitEntity, b, ref collisionData, attackerWeapon);
    //        }
    //    }
    //    private static Blow CreateMeleeBlow(Mission mission, Agent attackerAgent, Agent victimAgent, in AttackCollisionData collisionData, in MissionWeapon attackerWeapon, CrushThroughState crushThroughState, Vec3 blowDirection, Vec3 swingDirection, bool cancelDamage)
    //    {
    //        Blow blow = new Blow(attackerAgent.Index);
    //        AttackCollisionData attackCollisionData = collisionData;
    //        blow.VictimBodyPart = attackCollisionData.VictimHitBodyPart;
    //        bool flag = HitWithAnotherBone(collisionData, attackerAgent, attackerWeapon);
    //        attackCollisionData = collisionData;
    //        MissionWeapon missionWeapon;
    //        if (attackCollisionData.IsAlternativeAttack)
    //        {
    //            missionWeapon = attackerWeapon;
    //            blow.AttackType = (missionWeapon.IsEmpty ? AgentAttackType.Kick : AgentAttackType.Bash);
    //        }
    //        else
    //        {
    //            blow.AttackType = AgentAttackType.Standard;
    //        }
    //        missionWeapon = attackerWeapon;
    //        sbyte b;
    //        if (!missionWeapon.IsEmpty)
    //        {
    //            Monster monster = attackerAgent.Monster;
    //            missionWeapon = attackerWeapon;
    //            b = monster.GetBoneToAttachForItemFlags(missionWeapon.Item.ItemFlags);
    //        }
    //        else
    //        {
    //            b = -1;
    //        }
    //        sbyte weaponAttachBoneIndex = b;
    //        missionWeapon = attackerWeapon;
    //        ItemObject item = missionWeapon.Item;
    //        missionWeapon = attackerWeapon;
    //        WeaponComponentData currentUsageItem = missionWeapon.CurrentUsageItem;
    //        attackCollisionData = collisionData;
    //        blow.WeaponRecord.FillAsMeleeBlow(item, currentUsageItem, attackCollisionData.AffectorWeaponSlotOrMissileIndex, weaponAttachBoneIndex);
    //        attackCollisionData = collisionData;
    //        blow.StrikeType = (StrikeType)attackCollisionData.StrikeType;
    //        missionWeapon = attackerWeapon;
    //        DamageTypes damageType;
    //        if (!missionWeapon.IsEmpty && !flag)
    //        {
    //            attackCollisionData = collisionData;
    //            if (!attackCollisionData.IsAlternativeAttack)
    //            {
    //                attackCollisionData = collisionData;
    //                damageType = (DamageTypes)attackCollisionData.DamageType;
    //                goto IL_122;
    //            }
    //        }
    //        damageType = DamageTypes.Blunt;
    //    IL_122:
    //        blow.DamageType = damageType;
    //        attackCollisionData = collisionData;
    //        blow.NoIgnore = attackCollisionData.IsAlternativeAttack;
    //        attackCollisionData = collisionData;
    //        blow.AttackerStunPeriod = attackCollisionData.AttackerStunPeriod;
    //        attackCollisionData = collisionData;
    //        blow.DefenderStunPeriod = attackCollisionData.DefenderStunPeriod;
    //        blow.BlowFlag = BlowFlags.None;
    //        attackCollisionData = collisionData;
    //        blow.GlobalPosition = attackCollisionData.CollisionGlobalPosition;
    //        attackCollisionData = collisionData;
    //        blow.BoneIndex = attackCollisionData.CollisionBoneIndex;
    //        blow.Direction = blowDirection;
    //        blow.SwingDirection = swingDirection;
    //        if (cancelDamage)
    //        {
    //            blow.BaseMagnitude = 0f;
    //            blow.MovementSpeedDamageModifier = 0f;
    //            blow.InflictedDamage = 0;
    //            blow.SelfInflictedDamage = 0;
    //            blow.AbsorbedByArmor = 0f;
    //        }
    //        else
    //        {
    //            blow.BaseMagnitude = collisionData.BaseMagnitude;
    //            blow.MovementSpeedDamageModifier = collisionData.MovementSpeedDamageModifier;
    //            blow.InflictedDamage = collisionData.InflictedDamage;
    //            blow.SelfInflictedDamage = collisionData.SelfInflictedDamage;
    //            blow.AbsorbedByArmor = (float)collisionData.AbsorbedByArmor;
    //        }
    //        blow.DamageCalculated = true;
    //        if (crushThroughState != CrushThroughState.None)
    //        {
    //            blow.BlowFlag |= BlowFlags.CrushThrough;
    //        }
    //        if (blow.StrikeType == StrikeType.Thrust)
    //        {
    //            attackCollisionData = collisionData;
    //            if (!attackCollisionData.ThrustTipHit)
    //            {
    //                blow.BlowFlag |= BlowFlags.NonTipThrust;
    //            }
    //        }
    //        attackCollisionData = collisionData;
    //        if (attackCollisionData.IsColliderAgent)
    //        {
    //            if (MissionGameModels.Current.AgentApplyDamageModel.DecideAgentShrugOffBlow(victimAgent, collisionData, blow))
    //            {
    //                blow.BlowFlag |= BlowFlags.ShrugOff;
    //            }
    //            if (victimAgent.IsHuman)
    //            {
    //                Agent mountAgent = victimAgent.MountAgent;
    //                if (mountAgent != null)
    //                {
    //                    if (mountAgent.RiderAgent == victimAgent)
    //                    {
    //                        AgentApplyDamageModel agentApplyDamageModel = MissionGameModels.Current.AgentApplyDamageModel;
    //                        missionWeapon = attackerWeapon;
    //                        if (agentApplyDamageModel.DecideAgentDismountedByBlow(attackerAgent, victimAgent, collisionData, missionWeapon.CurrentUsageItem, blow))
    //                        {
    //                            blow.BlowFlag |= BlowFlags.CanDismount;
    //                        }
    //                    }
    //                }
    //                else
    //                {
    //                    AgentApplyDamageModel agentApplyDamageModel2 = MissionGameModels.Current.AgentApplyDamageModel;
    //                    missionWeapon = attackerWeapon;
    //                    if (agentApplyDamageModel2.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, collisionData, missionWeapon.CurrentUsageItem, blow))
    //                    {
    //                        blow.BlowFlag |= BlowFlags.KnockBack;
    //                    }
    //                    AgentApplyDamageModel agentApplyDamageModel3 = MissionGameModels.Current.AgentApplyDamageModel;
    //                    missionWeapon = attackerWeapon;
    //                    if (agentApplyDamageModel3.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, collisionData, missionWeapon.CurrentUsageItem, blow))
    //                    {
    //                        blow.BlowFlag |= BlowFlags.KnockDown;
    //                    }
    //                }
    //            }
    //            else if (victimAgent.IsMount)
    //            {
    //                AgentApplyDamageModel agentApplyDamageModel4 = MissionGameModels.Current.AgentApplyDamageModel;
    //                missionWeapon = attackerWeapon;
    //                if (agentApplyDamageModel4.DecideMountRearedByBlow(attackerAgent, victimAgent, collisionData, missionWeapon.CurrentUsageItem, blow))
    //                {
    //                    blow.BlowFlag |= BlowFlags.MakesRear;
    //                }
    //            }
    //        }
    //        return blow;
    //    }
    //    private static bool CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase(Mission mission, Agent attacker, Agent victim)
    //    {
    //        if (victim == null || attacker == null)
    //        {
    //            return false;
    //        }
    //        bool flag = !GameNetwork.IsSessionActive || mission.ForceNoFriendlyFire || (MultiplayerOptions.OptionType.FriendlyFireDamageMeleeFriendPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions) <= 0 && MultiplayerOptions.OptionType.FriendlyFireDamageMeleeSelfPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions) <= 0) || mission.Mode == MissionMode.Duel || attacker.Controller == Agent.ControllerType.AI;
    //        bool flag2 = attacker.IsFriendOf(victim);
    //        return (flag && flag2) || (victim.IsHuman && !flag2 && !attacker.IsEnemyOf(victim));
    //    }
    //    private static void UpdateMomentumRemaining(ref float momentumRemaining, Blow b, in AttackCollisionData collisionData, Agent attacker, Agent victim, in MissionWeapon attackerWeapon, bool isCrushThrough)
    //    {//这个不关键，下面的用harmony改动的才是实际有效的
    //        float num = momentumRemaining;
    //        momentumRemaining = 0f;
    //        if (isCrushThrough)
    //        {
    //            momentumRemaining = num * 0.3f;
    //            return;
    //        }
    //        if (b.InflictedDamage > 0)
    //        {
    //            AttackCollisionData attackCollisionData = collisionData;
    //            if (!attackCollisionData.AttackBlockedWithShield)
    //            {
    //                attackCollisionData = collisionData;
    //                if (!attackCollisionData.CollidedWithShieldOnBack)
    //                {
    //                    attackCollisionData = collisionData;
    //                    if (attackCollisionData.IsColliderAgent)
    //                    {
    //                        attackCollisionData = collisionData;
    //                        if (!attackCollisionData.IsHorseCharge)
    //                        {
    //                            if (attacker != null && attacker.IsDoingPassiveAttack)
    //                            {
    //                                momentumRemaining = num * 0.5f;
    //                                return;
    //                            }

    //                            /* 项目“New_ZZZF (net6)”的未合并的更改
    //                            在此之前:
    //                                                            if (!this.HitWithAnotherBone(collisionData, attacker, attackerWeapon))
    //                            在此之后:
    //                                                            if (!HitWithAnotherBone(collisionData, attacker, attackerWeapon))
    //                            */
    //                            if (!HitWithAnotherBone(collisionData, attacker, attackerWeapon))
    //                            {
    //                                MissionWeapon missionWeapon = attackerWeapon;
    //                                if (!missionWeapon.IsEmpty && b.StrikeType != StrikeType.Thrust)
    //                                {
    //                                    missionWeapon = attackerWeapon;
    //                                    if (!missionWeapon.IsEmpty)
    //                                    {
    //                                        missionWeapon = attackerWeapon;
    //                                        if (missionWeapon.CurrentUsageItem.CanHitMultipleTargets)
    //                                        {
    //                                            momentumRemaining = num * (1f - b.AbsorbedByArmor / (float)b.InflictedDamage);
    //                                            momentumRemaining *= 0.5f;
    //                                            if (momentumRemaining < 0.25f)
    //                                            {
    //                                                momentumRemaining = 0f;
    //                                            }
    //                                        }
    //                                    }
    //                                }
    //                            }
    //                        }
    //                    }
    //                }
    //            }
    //        }
    //    }
    //    private static CombatLogData GetAttackCollisionResults(Agent attackerAgent, Agent victimAgent, GameEntity hitObject, float momentumRemaining, in MissionWeapon attackerWeapon, bool crushedThrough, bool cancelDamage, bool crushedThroughWithoutAgentCollision, ref AttackCollisionData attackCollisionData, out WeaponComponentData shieldOnBack, out CombatLogData combatLog)
    //    {
    //        AttackInformation attackInformation = new AttackInformation(attackerAgent, victimAgent, hitObject, attackCollisionData, attackerWeapon);
    //        shieldOnBack = attackInformation.ShieldOnBack;
    //        int num;
    //        MissionCombatMechanicsHelper.GetAttackCollisionResults(attackInformation, crushedThrough, momentumRemaining, attackerWeapon, cancelDamage, ref attackCollisionData, out combatLog, out num);
    //        float num2 = (float)attackCollisionData.InflictedDamage;
    //        if (num2 > 0f)
    //        {
    //            float num3 = MissionGameModels.Current.AgentApplyDamageModel.CalculateDamage(attackInformation, attackCollisionData, attackerWeapon, num2);
    //            combatLog.ModifiedDamage = MathF.Round(num3 - num2);
    //            attackCollisionData.InflictedDamage = MathF.Round(num3);
    //        }
    //        else
    //        {
    //            combatLog.ModifiedDamage = 0;
    //            attackCollisionData.InflictedDamage = 0;
    //        }
    //        if (!attackCollisionData.IsFallDamage && attackInformation.IsFriendlyFire)
    //        {
    //            if (!attackInformation.IsAttackerAIControlled && GameNetwork.IsSessionActive)
    //            {
    //                int num4 = attackCollisionData.IsMissile ? MultiplayerOptions.OptionType.FriendlyFireDamageRangedSelfPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions) : MultiplayerOptions.OptionType.FriendlyFireDamageMeleeSelfPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
    //                attackCollisionData.SelfInflictedDamage = MathF.Round((float)attackCollisionData.InflictedDamage * ((float)num4 * 0.01f));
    //                int num5 = attackCollisionData.IsMissile ? MultiplayerOptions.OptionType.FriendlyFireDamageRangedFriendPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions) : MultiplayerOptions.OptionType.FriendlyFireDamageMeleeFriendPercent.GetIntValue(MultiplayerOptions.MultiplayerOptionsAccessMode.CurrentMapOptions);
    //                attackCollisionData.InflictedDamage = MathF.Round((float)attackCollisionData.InflictedDamage * ((float)num5 * 0.01f));
    //                combatLog.InflictedDamage = attackCollisionData.InflictedDamage;
    //            }
    //            combatLog.IsFriendlyFire = true;
    //        }
    //        if (attackCollisionData.AttackBlockedWithShield && attackCollisionData.InflictedDamage > 0 && (int)attackInformation.VictimShield.HitPoints - attackCollisionData.InflictedDamage <= 0)
    //        {
    //            attackCollisionData.IsShieldBroken = true;
    //        }
    //        if (!crushedThroughWithoutAgentCollision)
    //        {
    //            combatLog.BodyPartHit = attackCollisionData.VictimHitBodyPart;
    //            combatLog.IsVictimEntity = (hitObject != null);
    //        }
    //        return combatLog;
    //    }
    //    private static bool HitWithAnotherBone(in AttackCollisionData collisionData, Agent attacker, in MissionWeapon attackerWeapon)
    //    {
    //        MissionWeapon missionWeapon = attackerWeapon;
    //        int num;
    //        if (missionWeapon.IsEmpty || attacker == null || !attacker.IsHuman)
    //        {
    //            num = -1;
    //        }
    //        else
    //        {
    //            Monster monster = attacker.Monster;
    //            missionWeapon = attackerWeapon;
    //            num = (int)monster.GetBoneToAttachForItemFlags(missionWeapon.Item.ItemFlags);
    //        }
    //        int weaponAttachBoneIndex = num;
    //        return MissionCombatMechanicsHelper.IsCollisionBoneDifferentThanWeaponAttachBone(collisionData, weaponAttachBoneIndex);
    //    }
    //    // 定义前缀/后缀补丁方法（根据需求选择）
    //    public static void Prefix(Mission __instance,
    //        ref AttackCollisionData collisionData,
    //        Agent attacker,
    //        Agent victim,
    //        GameEntity realHitEntity,
    //        ref float inOutMomentumRemaining,
    //        ref MeleeCollisionReaction colReaction,
    //        CrushThroughState crushThroughState,
    //        Vec3 blowDir,
    //        Vec3 swingDir,
    //        ref HitParticleResultData hitParticleResultData,
    //        bool crushedThroughWithoutAgentCollision
    //    )
    //    {
    //        // 前缀逻辑（可选）
    //        // TODO: 在此处添加拦截逻辑

    //        hitParticleResultData.Reset();
    //        bool flag = collisionData.CollisionResult == CombatCollisionResult.Parried || collisionData.CollisionResult == CombatCollisionResult.Blocked || collisionData.CollisionResult == CombatCollisionResult.ChamberBlocked;
    //        if (collisionData.IsAlternativeAttack && !flag && victim != null && victim.IsHuman && collisionData.CollisionBoneIndex != -1 && (collisionData.VictimHitBodyPart == BoneBodyPartType.ArmLeft || collisionData.VictimHitBodyPart == BoneBodyPartType.ArmRight) && victim.IsHuman)
    //        {
    //            colReaction = MeleeCollisionReaction.ContinueChecking;
    //        }
    //        if (colReaction != MeleeCollisionReaction.ContinueChecking)
    //        {
    //            bool flag2 = CancelsDamageAndBlocksAttackBecauseOfNonEnemyCase(__instance, attacker, victim);
    //            bool flag3 = victim != null && victim.CurrentMortalityState == Agent.MortalityState.Invulnerable;
    //            bool flag4;
    //            if (flag2)
    //            {
    //                collisionData.AttackerStunPeriod = ManagedParameters.Instance.GetManagedParameter(ManagedParametersEnum.StunPeriodAttackerFriendlyFire);
    //                flag4 = true;
    //            }
    //            else
    //            {
    //                flag4 = (flag3 || (flag && !collisionData.AttackBlockedWithShield));
    //            }
    //            int affectorWeaponSlotOrMissileIndex = collisionData.AffectorWeaponSlotOrMissileIndex;
    //            MissionWeapon missionWeapon = (affectorWeaponSlotOrMissileIndex >= 0) ? attacker.Equipment[affectorWeaponSlotOrMissileIndex] : MissionWeapon.Invalid;
    //            if (crushThroughState == CrushThroughState.CrushedThisFrame && !collisionData.IsAlternativeAttack)
    //            {
    //                UpdateMomentumRemaining(ref inOutMomentumRemaining, default(Blow), collisionData, attacker, victim, missionWeapon, true);
    //            }
    //            WeaponComponentData weaponComponentData = null;
    //            CombatLogData combatLogData = default(CombatLogData);
    //            if (!flag4)
    //            {
    //                GetAttackCollisionResults(attacker, victim, realHitEntity, inOutMomentumRemaining, missionWeapon, crushThroughState > CrushThroughState.None, flag4, crushedThroughWithoutAgentCollision, ref collisionData, out weaponComponentData, out combatLogData);
    //                if (!collisionData.IsAlternativeAttack && attacker.IsDoingPassiveAttack && !GameNetwork.IsSessionActive && ManagedOptions.GetConfig(ManagedOptions.ManagedOptionsType.ReportDamage) > 0f)
    //                {
    //                    if (attacker.HasMount)
    //                    {
    //                        if (attacker.IsMainAgent)
    //                        {
    //                            InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("ui_delivered_couched_lance_damage", null).ToString(), Color.ConvertStringToColor("#AE4AD9FF")));
    //                        }
    //                        else if (victim != null && victim.IsMainAgent)
    //                        {
    //                            InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("ui_received_couched_lance_damage", null).ToString(), Color.ConvertStringToColor("#D65252FF")));
    //                        }
    //                    }
    //                    else if (attacker.IsMainAgent)
    //                    {
    //                        InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("ui_delivered_braced_polearm_damage", null).ToString(), Color.ConvertStringToColor("#AE4AD9FF")));
    //                    }
    //                    else if (victim != null && victim.IsMainAgent)
    //                    {
    //                        InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("ui_received_braced_polearm_damage", null).ToString(), Color.ConvertStringToColor("#D65252FF")));
    //                    }
    //                }
    //                if (collisionData.CollidedWithShieldOnBack && weaponComponentData != null && victim != null && victim.IsMainAgent)
    //                {
    //                    InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("ui_hit_shield_on_back", null).ToString(), Color.ConvertStringToColor("#FFFFFFFF")));
    //                }
    //            }
    //            else
    //            {
    //                collisionData.InflictedDamage = 0;
    //                collisionData.BaseMagnitude = 0f;
    //                collisionData.AbsorbedByArmor = 0;
    //                collisionData.SelfInflictedDamage = 0;
    //            }
    //            if (!crushedThroughWithoutAgentCollision)
    //            {
    //                Blow blow = CreateMeleeBlow(__instance,attacker, victim, collisionData, missionWeapon, crushThroughState, blowDir, swingDir, flag4);
    //                if (!flag && ((victim != null && victim.IsActive()) || realHitEntity != null))
    //                {
    //                    RegisterBlow(__instance,attacker, victim, realHitEntity, blow, ref collisionData, missionWeapon, ref combatLogData);
    //                }
    //                UpdateMomentumRemaining(ref inOutMomentumRemaining, blow, collisionData, attacker, victim, missionWeapon, false);
    //                bool isFatalHit = victim != null && victim.Health <= 0f;
    //                bool isShruggedOff = (blow.BlowFlag & BlowFlags.ShrugOff) > BlowFlags.None;
    //                DecideAgentHitParticles(attacker, victim, blow, collisionData, ref hitParticleResultData);
    //                DecideWeaponCollisionReaction(blow, collisionData, attacker, victim, missionWeapon, isFatalHit, isShruggedOff, out colReaction);
    //            }
    //            else
    //            {
    //                colReaction = MeleeCollisionReaction.ContinueChecking;
    //            }
    //            foreach (MissionBehavior missionBehavior in Mission.Current.MissionBehaviors)
    //            {
    //                missionBehavior.OnMeleeHit(attacker, victim, flag4, collisionData);
    //            }
    //        }
    //    }


    //}

    //////// 针对 UpdateMomentumRemaining 的 Harmony 补丁
    ////[HarmonyPatch(typeof(Mission), "UpdateMomentumRemaining")]
    ////public static class UpdateMomentumRemainingPatch
    ////{
    ////    ///
    ////    /// Harmony Prefix补丁方法：在原始动量衰减计算前拦截并修改参数
    ////    /// 目标：修改攻击后的武器动量残留逻辑，实现自定义穿透效果
    ////    ///
    ////    /// <param name="__instance">当前Mission实例（通过Harmony自动注入）</param>
    ////    /// <param name="momentumRemaining">引用参数：攻击后的剩余动量（将被修改）</param>
    ////    /// <param name="b">当前打击的Blow对象，包含伤害、攻击类型等信息</param>
    ////    /// <param name="collisionData">攻击碰撞数据（只读引用）</param>
    ////    /// <param name="attacker">攻击者Agent对象</param>
    ////    /// <param name="victim">被攻击者Agent对象</param>
    ////    /// <param name="attackerWeapon">攻击者使用的武器（只读引用）</param>
    ////    /// <param name="isCrushThrough">标识是否触发击穿效果</param>
    ////    public static void Prefix(Mission __instance,
    ////        ref float momentumRemaining,
    ////        Blow b,
    ////        in AttackCollisionData collisionData,
    ////        Agent attacker,
    ////        Agent victim,
    ////        in MissionWeapon attackerWeapon,
    ////        bool isCrushThrough)
    ////    {
    ////        // 保存原始动量值用于后续计算
    ////        float originalMomentum = momentumRemaining;
    ////        momentumRemaining = 0f; // 重置动量，后续根据条件重新计算

    ////        // 击穿攻击处理（如盾牌被击破）
    ////        if (isCrushThrough)
    ////        {
    ////            // 保留30%动量用于后续穿透计算
    ////            momentumRemaining = originalMomentum * 0.3f;
    ////            return; // 跳过后续逻辑
    ////        }

    ////        // 仅当造成有效伤害时处理动量残留
    ////        if (b.InflictedDamage > 0)
    ////        {
    ////            // 多层防御状态检测（防止错误穿透）
    ////            // 1. 检测是否被盾牌主动格挡
    ////            // 2. 检测是否击中背部盾牌
    ////            // 3. 确认碰撞体是生物单位（非场景物体）
    ////            // 4. 排除骑兵冲锋攻击
    ////            if (!collisionData.AttackBlockedWithShield &&
    ////                !collisionData.CollidedWithShieldOnBack &&
    ////                collisionData.IsColliderAgent &&
    ////                !collisionData.IsHorseCharge)
    ////            {
    ////                // 被动攻击处理（如架矛攻击）
    ////                if (attacker != null && attacker.IsDoingPassiveAttack)
    ////                {
    ////                    // 保留50%动量用于特殊攻击穿透
    ////                    momentumRemaining = originalMomentum * 0.5f;
    ////                    return;
    ////                }

    ////                // 检测是否命中其他骨骼（通过反射调用私有方法）
    ////                if (!Call_HitWithAnotherBone(__instance, collisionData, attacker, attackerWeapon))
    ////                {
    ////                    // 武器有效性检测
    ////                    if (!attackerWeapon.IsEmpty &&
    ////                        b.StrikeType != StrikeType.Thrust) // 排除刺击类型
    ////                    {
    ////                        // 获取武器数据组件
    ////                        WeaponComponentData weaponData = attackerWeapon.CurrentUsageItem;

    ////                        // 多目标武器穿透逻辑（如长柄武器横扫）
    ////                        if (weaponData.CanHitMultipleTargets)
    ////                        {
    ////                            // 动量计算公式：
    ////                            // 剩余动量 = 原始动量 × (1 - 护甲吸收比例) × 衰减系数
    ////                            float armorAbsorptionRatio = b.AbsorbedByArmor / (float)b.InflictedDamage;
    ////                            momentumRemaining = originalMomentum * (1f - armorAbsorptionRatio);

    ////                            // 应用额外衰减（平衡性调整）
    ////                            momentumRemaining *= 0.5f;

    ////                            // 最小动量阈值处理（避免微量残留）
    ////                            if (momentumRemaining < 0.25f)
    ////                            {
    ////                                momentumRemaining = 0f; // 清除微量动量
    ////                            }
    ////                        }
    ////                    }
    ////                }
    ////            }
    ////        }
    ////    }

    ////    private static bool Call_HitWithAnotherBone(Mission instance, AttackCollisionData collisionData, Agent? attacker, MissionWeapon attackerWeapon)
    ////    {
    ////        MissionWeapon missionWeapon = attackerWeapon;
    ////        int num;
    ////        if (missionWeapon.IsEmpty || attacker == null || !attacker.IsHuman)
    ////        {
    ////            num = -1;
    ////        }
    ////        else
    ////        {
    ////            Monster monster = attacker.Monster;
    ////            missionWeapon = attackerWeapon;
    ////            num = (int)monster.GetBoneToAttachForItemFlags(missionWeapon.Item.ItemFlags);
    ////        }
    ////        int weaponAttachBoneIndex = num;
    ////        return MissionCombatMechanicsHelper.IsCollisionBoneDifferentThanWeaponAttachBone(collisionData, weaponAttachBoneIndex);
    ////        throw new NotImplementedException();
    ////    }

    ////    public static void Postfix(
    ////        ref float momentumRemaining,
    ////        in Blow b,
    ////        in AttackCollisionData collisionData,
    ////        Agent attacker,
    ////        Agent victim,
    ////        in MissionWeapon attackerWeapon,
    ////        bool isCrushThrough
    ////    )
    ////    {
    ////        // 后缀逻辑（可选）
    ////        // TODO: 在此处修改动量衰减后的结果
    ////    }
    ////}
    [HarmonyPatch(typeof(Agent), "Mount")]
    public class AgentPatches
    {
        [HarmonyPrefix]
        //__instance等价于this,后面的参数是修改的方法本来传入的参数
        //在这里，__instance是骑手agent，mountAgent是坐骑agent
        public static bool Prefix_Mount(Agent __instance, Agent mountAgent)
        {

            bool flag = mountAgent.GetCurrentActionType(0) == Agent.ActionCodeType.Rear;
            SkillSystemBehavior.ActiveComponents.TryGetValue(__instance.Index, out var v);
            if (__instance.MountAgent == null && mountAgent.RiderAgent == null)
            {
                if (__instance.CheckSkillForMounting(mountAgent) && (!flag || v._globalCooldownTimer <= 0f))// && __instance.GetCurrentActionValue(0) == ActionIndexValueCache.act_none)
                {
                    __instance.EventControlFlags |= Agent.EventControlFlag.Mount;
                    __instance.SetInteractionAgent(mountAgent);


                    if (v._globalCooldownTimer <= 0f)
                    {
                        var traverse = Traverse.Create(__instance);
                        traverse.Property("MountAgent").SetValue(mountAgent);
                        //v._globalCooldownTimer += 5;
                    }



                }
            }
            else if (__instance.MountAgent == mountAgent && !flag || v._globalCooldownTimer <= 0f)
            {
                __instance.EventControlFlags |= Agent.EventControlFlag.Dismount;

                if (v._globalCooldownTimer <= 0f)
                {
                    var traverse = Traverse.Create(__instance);
                    traverse.Property("MountAgent").SetValue(null);
                    //v._globalCooldownTimer +=5;
                }
            }
            return false;
            // 在这里添加你想要在Mount方法执行前执行的代码  
            // 例如，你可以记录日志，或者添加一些额外的检查  



            // 在这里添加你想要在Mount方法执行后执行的代码  
            // 例如，你可以根据originalResult的值执行一些操作  

            // 返回true表示继续执行原始方法，如果需要的话  
            return false;
        }
    }
}
