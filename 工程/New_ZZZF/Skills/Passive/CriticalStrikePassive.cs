using New_ZZZF;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

public class CriticalStrikePassive : New_ZZZF.SkillBase
{
    public CriticalStrikePassive()
    {
        SkillID = "CriticalStrike";
        Type = SPSkillType.Passive;
        // 被动技能无需冷却和消耗
        Cooldown = 0;
        ResourceCost = 0;
    }

    public override bool Activate(Agent agent)
    {
        return false;
    }

    public override void OnEquip(Agent agent)
    {
        // 注册攻击事件
        //agent.OnDealDamage += OnDealDamageHandler;
    }

    private void OnDealDamageHandler(Agent victim, AttackInformation attackInfo)
    {
        if (MBRandom.RandomFloat < 0.15f) // 15%暴击率
        {
            //attackInfo.Damage *= 2;
            /* 此代码看不到log：Debug.Print 不会写入可查看的日志文件，已禁用。 */;
        }
    }
}