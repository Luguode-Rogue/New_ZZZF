using New_ZZZF.Systems;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    public class HuHuanFengBao : SkillBase
    {
        public HuHuanFengBao()
        {
            SkillID = "HuHuanFengBao"; Type = SPSkillType.MainActive; Cooldown = 90f; ResourceCost = 75f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0047}HuHuanFengBao");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0048}对全场敌人位置降下3米范围落雷，密集敌人会受到不同落点的交叠伤害；每次落雷后有50%概率继续追击存活目标，首次判定失败即停止。每次基础雷伤30，受法强与魔抗影响。施法者位置另有一次落雷表现。耐力75，冷却90秒。");
        }
        public override bool CheckCondition(Agent caster) => base.CheckCondition(caster) && caster.Mission != null &&
            caster.Mission.GetMissionBehavior<StormLightningMissionLogic>()?.HasEligibleEnemy(caster) == true;

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Mission == null) return FailActivation("施法者或任务不可用。");
            StormLightningMissionLogic logic = caster.Mission.GetMissionBehavior<StormLightningMissionLogic>();
            if (logic == null) return FailActivation("风暴管理器不可用。");
            if (!logic.Begin(caster)) return FailActivation("没有可被落雷命中的敌人。");
            LeiJi.ShowSingleLightning(caster.Position + TaleWorlds.Library.Vec3.Up);
            SpellProjectileMissionLogic.GetForCurrentMission()?.QueueOneShotSound("event:/mission/siege/ballista/fire", caster.Position);
            return true;
        }
    }
}