using SandBox.GameComponents;
using Helpers;
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
using static New_ZZZF.FengBaoZhiLi;
using static New_ZZZF.ZhanHao;
using static New_ZZZF.WeiYa;
using static New_ZZZF.YingXiongZhuFu;
using static New_ZZZF.KongNueCiFu;
using static New_ZZZF.NaGouCiFu;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using static New_ZZZF.BKB;

namespace New_ZZZF.Systems
{
    /// <summary>
    /// 修改属性加值，比如跑动加速
    /// </summary>
    public class ZZZF_SandboxAgentStatCalculateModel: SandboxAgentStatCalculateModel
    {
        public override float GetSneakAttackMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float original = base.GetSneakAttackMultiplier(agent, weapon);
            if (agent?.Character == null || weapon == null) return original;
            var value = new ExplainedNumber(1f);
            int skill = Math.Max(50, GetEffectiveSkill(agent, DefaultSkills.Roguery));
            SkillHelper.AddSkillBonusForSkillLevel(DefaultSkillEffects.SneakDamage, ref value, skill);
            if (weapon.WeaponClass == WeaponClass.Dagger || weapon.WeaponClass == WeaponClass.ThrowingKnife)
                value.AddFactor(2f);
            return Math.Max(original, value.ResultNumber);
        }
        public override float GetWeaponDamageMultiplier(Agent agent, WeaponComponentData weapon)
        {
            float native = base.GetWeaponDamageMultiplier(agent, weapon);
            native = StrikeMagnitudeScript.WOW_Script_AgentStatCalculateModel(agent, native, weapon);
            return native;
            //接下来的代码会乘等这个函数的输出值，所以这个函数的数值1==100%
            //float weaponDamageMultiplier = MissionGameModels.Current.AgentStatCalculateModel.GetWeaponDamageMultiplier(attackInformation.AttackerAgent, currentUsageItem2);
            //baseMagnitude *= weaponDamageMultiplier;
        }
        public float _dt = 0f;
        public override void UpdateAgentStats(Agent agent, AgentDrivenProperties agentDrivenProperties)
        {
            base.UpdateAgentStats(agent, agentDrivenProperties);
            if (agent.IsMount)
            {
                WeiYa.ApplyMountDrivenProperties(agent, agentDrivenProperties);
                if (agent.RiderAgent?.GetComponent<AgentSkillComponent>()?.StateContainer.HasState("YingXiongZhuFuBuff") == true)
                {
                    agentDrivenProperties.MountSpeed *= 1.3f;
                    agentDrivenProperties.MountManeuver *= 1.3f;
                    agentDrivenProperties.MountDashAccelerationMultiplier *= 1.3f;
                    agentDrivenProperties.TopSpeedReachDuration *= 1.3f;
                }
            }
            if (agent.IsHuman)
            {
                this.UpdateHumanStats(agent, agentDrivenProperties, _dt);
                WeaponCombatRules.Stats(agent, agentDrivenProperties);
                AggressiveAi.AiDefenseThreatAdjustment.Apply(agent, agentDrivenProperties);

                // 沿用模组中“按住空格加速跑”的已验证实现：base 每次重算后直接
                // 设置本次最终速度倍率。仅处理徒步冲刺斩，不触碰任何坐骑属性。
#if false
                // 仅提高 Max 不会推动人物达到目标速度；由安全连续位移实现替代。
                if (RushMovementMissionLogic.IsOnFootChongCiZhanRush(agent))
                {
                    agentDrivenProperties.MaxSpeedMultiplier = 5f;
                    agentDrivenProperties.CombatMaxSpeedMultiplier = 5f;
                }
#endif
                if (RushMovementMissionLogic.IsMountedChongCiZhanCharge(agent))
                {
                    // 坐骑实际速度由骑手的 MountSpeed 决定。仅修改这两个原生骑乘
                    // 字段，避免此前 5 倍操控/碰撞属性组合造成物理负载失控。
                    agentDrivenProperties.MountSpeed *= 2f;
                    agentDrivenProperties.MountDashAccelerationMultiplier *= 2f;
                }
                return;
            }
        }
        private void UpdateHumanStats(Agent agent, AgentDrivenProperties agentDrivenProperties , float dt)
        {
            SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var result);
            if (result != null)
            {
                MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
                if (missionScreen != null && missionScreen.SceneLayer!=null)
                {
                    // 修复：原实现每个 agent 的回调都会把 Agent.Main 速度 ×2（N 个 agent 每帧累乘
                    // → Infinity 污染 native AgentDrivenProperties → 引擎 Tick 未定义行为），
                    // 且 resultMain 可能为 null。改为仅主角自己的回调内设置固定倍率
                    // （base.UpdateAgentStats 每帧已重置数值，松开自然恢复，无需释放分支）。
                    if (missionScreen.SceneLayer.Input.IsGameKeyDown(14)
                        && Agent.Main != null && agent == Agent.Main)
                    {
                        agentDrivenProperties.MaxSpeedMultiplier = 2f;
                        agentDrivenProperties.CombatMaxSpeedMultiplier = 2f;
                        if (SkillSystemBehavior.ActiveComponents.TryGetValue(Agent.Main.Index, out var resultMain)
                            && resultMain != null)
                        {
                            resultMain.ChangeStamina(-5);
                        }
                    }
                }
                
                if (result.StateContainer.HasState("ZhanYiBuff"))
                {
                    ZhanYiBuff buff = result.StateContainer.GetState("ZhanYiBuff") as ZhanYiBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        if (agentSkillComponent != null)
                        {
                            // 属性重算只读耐力；每秒回复由 ZhanYiBuff 的状态计时负责。
                            agent.AgentDrivenProperties.SwingSpeedMultiplier += agentSkillComponent._currentStamina / 100;
                            agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += agentSkillComponent._currentStamina / 100;
                            agent.AgentDrivenProperties.MaxSpeedMultiplier += agentSkillComponent._currentStamina / 100;
                        }
                    }
                }
                if (result.StateContainer.HasState("JueXingBuff"))
                {
                    JueXingBuff buff = result.StateContainer.GetState("JueXingBuff") as JueXingBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        if (agentSkillComponent != null)
                        {
                            agent.AgentDrivenProperties.SwingSpeedMultiplier += agentSkillComponent._currentStamina / 100;
                            agent.AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier += agentSkillComponent._currentStamina / 100;
                            agent.AgentDrivenProperties.HandlingMultiplier += agentSkillComponent._currentStamina / 100;
                            agent.AgentDrivenProperties.MaxSpeedMultiplier += agentSkillComponent._currentStamina / 100 * 1.5f;
                        }
                    }
                }
                if (result.StateContainer.HasState("GuWuBuff"))
                {
                    agentDrivenProperties.WeaponInaccuracy *= 0.8f;
                }
                if (result.StateContainer.HasState("KongNueCiFuBuff"))
                {
                    agentDrivenProperties.SwingSpeedMultiplier *= KongNueCiFu.StrengthMultiplier;
                    if (KongNueCiFu.HasMeleeWeapon(agent))
                        agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= KongNueCiFu.StrengthMultiplier;
                    else
                        agentDrivenProperties.ThrustOrRangedReadySpeedMultiplier *= 0.5f;
                    agentDrivenProperties.MaxSpeedMultiplier *= KongNueCiFu.StrengthMultiplier;
                    agentDrivenProperties.CombatMaxSpeedMultiplier *= KongNueCiFu.StrengthMultiplier;
                    agentDrivenProperties.ArmorHead *= 1.5f;
                    agentDrivenProperties.ArmorTorso *= 1.5f;
                    agentDrivenProperties.ArmorLegs *= 1.5f;
                    agentDrivenProperties.ArmorArms *= 1.5f;
                    agentDrivenProperties.MissileSpeedMultiplier *= 0.5f;
                    agentDrivenProperties.WeaponInaccuracy *= 3f;
                    agentDrivenProperties.ReloadSpeed *= 0.5f;
                }
                if (result.StateContainer.HasState("BKBBuff"))
                {
                    BKBBuff buff = result.StateContainer.GetState("BKBBuff") as BKBBuff;
                    if (buff != null)
                    {
                        SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out var agentSkillComponent);
                        agentDrivenProperties.TopSpeedReachDuration *= 2f;
                        agentDrivenProperties.MaxSpeedMultiplier /= 2f;
                        agentDrivenProperties.CombatMaxSpeedMultiplier /= 2f;
                    }
                }
                if (result.StateContainer.HasState("NaGouCiFuBuff"))
                {
                    agentDrivenProperties.MaxSpeedMultiplier *= 0.7f;
                    agentDrivenProperties.CombatMaxSpeedMultiplier *= 0.7f;
                }
                ZhanHao.ApplyDrivenProperties(agent, agentDrivenProperties);
                WeiYa.ApplyDrivenProperties(agent, agentDrivenProperties);
                FengBaoZhiLi.ApplyRangedDrivenProperties(agent, agentDrivenProperties);
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
                        agent.AgentDrivenProperties.TopSpeedReachDuration /= 1.3f;

                    }
                }
            }
           
        }
    }
}
