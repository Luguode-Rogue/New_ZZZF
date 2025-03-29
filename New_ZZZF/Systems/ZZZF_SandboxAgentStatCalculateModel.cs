using SandBox.GameComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static New_ZZZF.ZhanYi;
using static New_ZZZF.JueXing;
using static New_ZZZF.TianQi;
using static New_ZZZF.GuWu;
using static New_ZZZF.FengBaoZhiLi;
using static New_ZZZF.ZhanHao;
using static New_ZZZF.WeiYa;
using static New_ZZZF.YingXiongZhuFu;
using static New_ZZZF.KongNueCiFu;

namespace New_ZZZF.Systems
{
    /// <summary>
    /// 修改属性加值，比如跑动加速
    /// </summary>
    public class ZZZF_SandboxAgentStatCalculateModel: SandboxAgentStatCalculateModel
    {
        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float native = base.GetWeaponDamageMultiplier(agent, weapon);
            native = StrikeMagnitudeScript.WOW_Script_AgentStatCalculateModel(agent, native);
            return native;
            //接下来的代码会乘等这个函数的输出值，所以这个函数的数值1==100%
            //float weaponDamageMultiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(attackInformation.AttackerAgent, currentUsageItem2);
            //baseMagnitude *= weaponDamageMultiplier;
        }
        public float _dt = 0f;
        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (agent.IsHuman)
            {
                this.UpdateHumanStats(agent, agentDrivenProperties, _dt);
                return;
            }
        }
        private void UpdateHumanStats(Agent agent, AgentDrivenProperties agentDrivenProperties , float dt)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var result);
            if (result != null)
            {
                if (result.StateContainer.HasState("ZhanYiBuff"))
                {
                    ZhanYiBuff buff = result.StateContainer.GetState("ZhanYiBuff") as ZhanYiBuff;
                    if (buff != null)
                    {
                        //原来代码不写这边，需要手动清，写在这边就不要手动清理了，每次走完base的设置后，会自动给你重置成原版应有的数值
                        //agent.AgentDrivenProperties.SwingSpeedMultiplier -= buff.enduranceRecord;
                        //agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier -= buff.enduranceRecord;
                        //agent.AgentDrivenProperties.MaxSpeedMultiplier -= buff.enduranceRecord;

                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agentSkillComponent.ChangeStamina(5);

                        agent.AgentDrivenProperties.SwingSpeedMultiplier += agentSkillComponent._currentStamina / 100;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += agentSkillComponent._currentStamina / 100;
                        agent.AgentDrivenProperties.MaxSpeedMultiplier += agentSkillComponent._currentStamina / 100;

                    }
                }
                if (result.StateContainer.HasState("JueXingBuff"))
                {
                    JueXingBuff buff = result.StateContainer.GetState("JueXingBuff") as JueXingBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agentSkillComponent.ChangeStamina(-5);

                        agent.AgentDrivenProperties.SwingSpeedMultiplier += agentSkillComponent._currentStamina / 100;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += agentSkillComponent._currentStamina / 100;
                        agent.AgentDrivenProperties.HandlingMultiplier += agentSkillComponent._currentStamina / 100;
                        agent.AgentDrivenProperties.MaxSpeedMultiplier += agentSkillComponent._currentStamina / 100 * 1.5f;

                    }
                }
                if (result.StateContainer.HasState("GuWuBuff"))
                {
                    GuWuBuff buff = result.StateContainer.GetState("GuWuBuff") as GuWuBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);

                        agent.AgentDrivenProperties.WeaponInaccuracy /= 2;

                    }
                }
                if (result.StateContainer.HasState("TianQiBuff"))
                {
                    TianQiBuff buff = result.StateContainer.GetState("TianQiBuff") as TianQiBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);

                    }
                }
                if (result.StateContainer.HasState("KongNueCiFuBuff"))
                {
                    KongNueCiFuBuff buff = result.StateContainer.GetState("KongNueCiFuBuff") as KongNueCiFuBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agent.AgentDrivenProperties.SwingSpeedMultiplier *= 2f;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= 2f;
                        agent.AgentDrivenProperties.HandlingMultiplier *= 2f;
                        agent.AgentDrivenProperties.MissileSpeedMultiplier /= 2f;
                        agent.AgentDrivenProperties.WeaponInaccuracy *= 2f;
                        agent.AgentDrivenProperties.WeaponMaxMovementAccuracyPenalty /= 2f;
                        agent.AgentDrivenProperties.WeaponMaxUnsteadyAccuracyPenalty /= 2f;
                        agent.AgentDrivenProperties.WeaponBestAccuracyWaitTime /= 2f;
                        agent.AgentDrivenProperties.ArmorEncumbrance /= 2f;
                        agent.AgentDrivenProperties.WeaponsEncumbrance /= 2f;
                        agent.AgentDrivenProperties.ArmorHead *= 2f;
                        agent.AgentDrivenProperties.ArmorTorso *= 2f;
                        agent.AgentDrivenProperties.ArmorLegs *= 2f;
                        agent.AgentDrivenProperties.ArmorArms *= 2f;
                        agent.AgentDrivenProperties.AttributeRiding *= 2f;
                        agent.AgentDrivenProperties.AttributeShield *= 2f;
                        agent.AgentDrivenProperties.AttributeShieldMissileCollisionBodySizeAdder *= 2f;
                        agent.AgentDrivenProperties.ShieldBashStunDurationMultiplier *= 2f;
                        agent.AgentDrivenProperties.KickStunDurationMultiplier *= 2f;
                        agent.AgentDrivenProperties.TopSpeedReachDuration /= 2f;
                        agent.AgentDrivenProperties.MaxSpeedMultiplier *= 2f;
                        agent.AgentDrivenProperties.CombatMaxSpeedMultiplier *= 2f;
                        agent.AgentDrivenProperties.AttributeHorseArchery *= 2f;
                        agent.AgentDrivenProperties.AttributeCourage *= 2f;
                    }
                }
                if (result.StateContainer.HasState("ZhanHaoBuff"))
                {
                    ZhanHaoBuff buff = result.StateContainer.GetState("ZhanHaoBuff") as ZhanHaoBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agent.AgentDrivenProperties.SwingSpeedMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.HandlingMultiplier *= 1.5f;
                        agent.AgentDrivenProperties.ReloadSpeed *= 1.2f;
                        agent.AgentDrivenProperties.MissileSpeedMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.WeaponInaccuracy /= 1.2f;
                        agent.AgentDrivenProperties.WeaponMaxMovementAccuracyPenalty /= 1.2f;
                        agent.AgentDrivenProperties.WeaponMaxUnsteadyAccuracyPenalty /= 1.2f;
                        agent.AgentDrivenProperties.WeaponBestAccuracyWaitTime *= 1.2f;
                        agent.AgentDrivenProperties.ArmorEncumbrance /= 1.2f;
                        agent.AgentDrivenProperties.WeaponsEncumbrance /= 1.2f;
                        agent.AgentDrivenProperties.ArmorHead *= 1.2f;
                        agent.AgentDrivenProperties.ArmorTorso *= 1.2f;
                        agent.AgentDrivenProperties.ArmorLegs *= 1.2f;
                        agent.AgentDrivenProperties.ArmorArms *= 1.2f;
                        agent.AgentDrivenProperties.AttributeRiding *= 1.2f;
                        agent.AgentDrivenProperties.AttributeShield *= 1.2f;
                        agent.AgentDrivenProperties.AttributeShieldMissileCollisionBodySizeAdder *= 1.2f;
                        agent.AgentDrivenProperties.ShieldBashStunDurationMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.KickStunDurationMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.ReloadMovementPenaltyFactor *= 1.2f;
                        agent.AgentDrivenProperties.TopSpeedReachDuration /= 1.2f;
                        agent.AgentDrivenProperties.MaxSpeedMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.CombatMaxSpeedMultiplier *= 1.2f;
                        agent.AgentDrivenProperties.AttributeHorseArchery *= 1.2f;
                        agent.AgentDrivenProperties.AttributeCourage *= 1.2f;
                    }
                }
                if (result.StateContainer.HasState("WeiYaBuffToEnemy"))
                {
                    WeiYaBuffToEnemy buff = result.StateContainer.GetState("WeiYaBuffToEnemy") as WeiYaBuffToEnemy;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agent.AgentDrivenProperties.HandlingMultiplier *= 0.5f;
                        agent.AgentDrivenProperties.WeaponInaccuracy *= 1.5f;
                        agent.AgentDrivenProperties.TopSpeedReachDuration *= 1.5f;
                        agent.AgentDrivenProperties.MaxSpeedMultiplier *= 0.5f;
                        agent.AgentDrivenProperties.MountSpeed *= 0.5f;
                        agent.AgentDrivenProperties.MountManeuver *= 0.5f;


                    }
                }
                if (result.StateContainer.HasState("FengBaoZhiLiBuff"))
                {
                    FengBaoZhiLiBuff buff = result.StateContainer.GetState("FengBaoZhiLiBuff") as FengBaoZhiLiBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agent.AgentDrivenProperties.WeaponMaxMovementAccuracyPenalty /= 5;
                        agent.AgentDrivenProperties.WeaponMaxUnsteadyAccuracyPenalty /= 5;
                        agent.AgentDrivenProperties.WeaponRotationalAccuracyPenaltyInRadians /= 5;
                        agent.AgentDrivenProperties.WeaponInaccuracy /= 5;
                        agent.AgentDrivenProperties.ReloadSpeed *=2;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *=2;

                    }
                }
                if (result.StateContainer.HasState("YingXiongZhuFuBuff"))
                {
                    YingXiongZhuFuBuff buff = result.StateContainer.GetState("YingXiongZhuFuBuff") as YingXiongZhuFuBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agent.AgentDrivenProperties.WeaponMaxMovementAccuracyPenalty /= 3;
                        agent.AgentDrivenProperties.WeaponMaxUnsteadyAccuracyPenalty /= 3;
                        agent.AgentDrivenProperties.WeaponRotationalAccuracyPenaltyInRadians /= 3;
                        agent.AgentDrivenProperties.WeaponInaccuracy /= 3;
                        agent.AgentDrivenProperties.ReloadSpeed *=1.4f;
                        agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *=1.4f;
                        agent.AgentDrivenProperties.HandlingMultiplier *= 1.3f;
                        agent.AgentDrivenProperties.WeaponInaccuracy /= 1.5f;
                        agent.AgentDrivenProperties.TopSpeedReachDuration *= 1.3f;
                        agent.AgentDrivenProperties.MountSpeed *= 1.3f;
                        agent.AgentDrivenProperties.MountManeuver *= 1.3f;

                    }
                }
            }
           
        }
    }
}
