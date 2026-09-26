using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class ZhanYi : SkillBase
    {
        public ZhanYi()
        {
            SkillID = "ZhanYi";      // 必须唯一
            Type = SPSkillType.MainActive;    // 类型必须明确
            Cooldown = 60;            // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0015}ZhanYi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0016}开启后获得战意状态，每秒回复5耐力。伤害提高25%，并随当前耐力最多再提高75%；每次伤害另有50%概率增加50点固定伤害。攻击与移动速度随当前耐力提高。击杀敌人恢复50%已损失生命并延长战意。基础持续8秒。消耗耐力：0。冷却时间：60秒。");
        }
        public override bool Activate(Agent agent)
        {
            AgentSkillComponent component = agent?.GetComponent<AgentSkillComponent>();
            if (component == null || component.StateContainer.HasState("ZhanYiBuff"))
                return FailActivation("战意已经生效或技能组件不可用。");
            component.StateContainer.AddState(new ZhanYiBuff(8f, agent), agent);
            return true;
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) ||
                caster.GetComponent<AgentSkillComponent>()?.StateContainer.HasState("ZhanYiBuff") != false)
                return false;

            Agent target = caster.GetTargetAgent();
            float distanceSquared = float.MaxValue;
            if (target != null && target.IsActive() && target.Health > 0f && caster.IsEnemyOf(target))
                distanceSquared = (target.Position.AsVec2 - caster.Position.AsVec2).LengthSquared;
            Formation enemyFormation = caster.Formation?.CachedClosestEnemyFormation?.Formation;
            if (enemyFormation != null)
                distanceSquared = Math.Min(distanceSquared,
                    (enemyFormation.CachedMedianPosition.AsVec2 - caster.Position.AsVec2).LengthSquared);
            if (distanceSquared == float.MaxValue)
                return false;

            WeaponComponentData weapon = caster.WieldedWeapon.CurrentUsageItem;
            float readyDistance;
            if (weapon != null && weapon.IsRangedWeapon)
            {
                float missileRange = caster.GetMissileRange();
                if (missileRange <= 0f)
                    return false;
                readyDistance = missileRange * 0.85f;
            }
            else
            {
                readyDistance = caster.MountAgent != null ? 30f : 15f;
            }
            return distanceSquared <= readyDistance * readyDistance;
        }

        public class ZhanYiBuff : AgentBuff
        {
            private float _timeSinceLastTick;
            public ZhanYiBuff(float duration, Agent source)
            {
                StateId = "ZhanYiBuff";
                Duration = duration;
                SourceAgent = source;
                _timeSinceLastTick = 0; // 新增初始化
            }

            public override void OnApply(Agent agent)
            {
                agent.UpdateAgentProperties();
            }

            public void ExtendAfterKill()
            {
                Duration = Duration >= 7f ? Math.Min(Duration + 1f, 100f) : 8f;
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (dt <= 0f)
                    return;
                _timeSinceLastTick += dt;
                if (_timeSinceLastTick < 1f)
                    return;

                int wholeSeconds = (int)_timeSinceLastTick;
                _timeSinceLastTick -= wholeSeconds;
                AgentSkillComponent component = agent.GetComponent<AgentSkillComponent>();
                if (component == null || component._currentStamina >= 100f)
                    return;
                float previousStamina = component._currentStamina;
                component.ChangeStamina(5f * wholeSeconds);
                if (component._currentStamina > previousStamina)
                    agent.UpdateAgentProperties();
            }

            public override void OnRemove(Agent agent)
            {
                agent.UpdateAgentProperties();
            }
        }
    }
}
