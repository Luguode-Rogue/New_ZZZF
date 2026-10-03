using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal sealed class JianRenLuanWu : SkillBase
    {
        public const string DescriptionText = "使用近战武器连续发动24次右横扫，挥空也计入次数。攻击命中不会中断，必定突破格挡并无限贯穿单位，使用原版武器物理伤害。纯戳刺武器使用戳刺伤害，动作速度180%，挥击时小幅扭身扩大扫击角度。切换武器或失去行动能力会结束。消耗耐力：60。冷却时间：40秒。";
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        public JianRenLuanWu() {
            SkillID = "JianRenLuanWu";
            Type = SPSkillType.MainActive;
            Cooldown = 40f;
            ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0068}剑刃乱舞");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0069}" + DescriptionText);
        }
        public override bool CanActivateWhilePerformingAction => true;
        public override bool Activate(Agent caster) {
            var logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null) return FailActivation("连续攻击管理器不可用。");
            return logic.TryStartBladeDance(caster, out string reason) || FailActivation(reason);
        }
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || caster.IsPlayerControlled ||
                !JiFengLianZhanMissionLogic.HasBladeDanceWeapon(caster)) return false;
            var logic = JiFengLianZhanMissionLogic.GetForCurrentMission();
            if (logic == null || logic.IsActive(caster) || JingXia.IsFrightened(caster)) return false;
            var usage = caster.WieldedWeapon.CurrentUsageItem;
            float reach = MathF.Max(1.5f, usage.WeaponLength * 0.01f + 0.8f);
            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, reach, _nearby);
            int count = 0;
            bool strong = false;
            foreach (Agent enemy in _nearby) {
                if (enemy == null || !enemy.IsActive() || !enemy.IsHuman || enemy.Health <= 0f ||
                    !caster.IsEnemyOf(enemy) || SkillTargetProtection.IsProtected(enemy) || !RushMovementMissionLogic.HasLineOfSight(caster, enemy) ||
                    (enemy.Position - caster.Position).LengthSquared > reach * reach) continue;
                count++;
                strong |= enemy.IsHero || WeiYa.GetTier(enemy) >= WeiYa.GetTier(caster);
            }
            return count >= 2 || (count == 1 && (strong ||
                caster.GetComponent<AgentSkillComponent>()?._currentStamina >= 80f));
        }
    }
}