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

namespace New_ZZZF.Systems
{
    public class ZZZF_SandboxAgentStatCalculateModel: SandboxAgentStatCalculateModel
    {
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

                        agent.AgentDrivenProperties.WeaponInaccuracy *= 1.2f;

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
            }
           
        }
    }
}
