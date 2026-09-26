using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class JueXing : SkillBase
    {
        public JueXing()
        {
            SkillID = "JueXing";      // 必须唯一
            Type = SPSkillType.MainActive;    // 类型必须明确
            Cooldown = 60f;           // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0017}JueXing");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0018}开启觉醒后，武器伤害随当前耐力提高，满耐力时最多提高50%，攻击、操控及移动速度也随耐力提高。状态持续至耐力耗尽；每秒消耗4耐力，每有30耐力再额外消耗1耐力，并使副主动技能与战技的冷却速度额外提高100%。觉醒期间击杀敌人额外恢复5耐力（合计10耐力），并恢复5%最大生命值。消耗耐力：0。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("JueXingBuff") ||
                component._currentStamina <= 0f)
                return FailActivation("觉醒已经生效、耐力不足或技能组件不可用。");
            component.StateContainer.AddState(new JueXingBuff(agent), agent);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster)) return false;
            AgentSkillComponent component = caster.GetComponent<AgentSkillComponent>();
            if (component == null || component._currentStamina < 50f ||
                component.StateContainer.HasState("JueXingBuff"))
                return false;

            Agent target = caster.GetTargetAgent();
            float distanceSquared = float.MaxValue;
            if (target != null && target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target))
                distanceSquared = (target.Position.AsVec2 - caster.Position.AsVec2).LengthSquared;
            Formation enemyFormation = caster.Formation?.CachedClosestEnemyFormation?.Formation;
            if (enemyFormation != null)
                distanceSquared = Math.Min(distanceSquared,
                    (enemyFormation.CachedMedianPosition.AsVec2 - caster.Position.AsVec2).LengthSquared);
            if (distanceSquared == float.MaxValue) return false;

            WeaponComponentData weapon = caster.WieldedWeapon.CurrentUsageItem;
            float readyDistance;
            if (weapon != null && weapon.IsRangedWeapon)
            {
                float missileRange = caster.GetMissileRange();
                if (missileRange <= 0f) return false;
                readyDistance = missileRange * 0.85f;
            }
            else
            {
                readyDistance = caster.MountAgent != null ? 30f : 15f;
            }
            return distanceSquared <= readyDistance * readyDistance;
        }

        public class JueXingBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public JueXingBuff(Agent source)
            {
                StateId = "JueXingBuff";
                Duration = 100f;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                agent.UpdateAgentProperties();
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
                if (component == null || component._currentStamina <= 0f)
                {
                    Duration = 0f;
                    return;
                }

                float stamina = component._currentStamina;
                float drainPerSecond = 4f + (int)(stamina / 30f);
                component.ChangeStamina(-drainPerSecond * dt);
                if (component._currentStamina <= 0f)
                {
                    Duration = 0f;
                    return;
                }

                // 状态由耐力维持，计时器只用于兼容通用状态容器的超时移除。
                Duration = 100f;
                _timeSinceLastTick += dt;
                if (_timeSinceLastTick >= 1f)
                {
                    _timeSinceLastTick -= (int)_timeSinceLastTick;
                    agent.UpdateAgentProperties();
                }
            }

            public override void OnRemove(Agent agent)
            {
                agent.UpdateAgentProperties();
            }
        }
    }
}
