using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class HongShiZiHuoYan : SkillBase
    {
        private const float SampleRadius = 3f;
        private const float ConeLength = 10f;
        private const float HalfAngle = 30f;

        public override bool TryGetDamageArea(Agent caster, int index, out SkillDamageArea area)
        {
            // 实际伤害为七条射线、每米一个半径 3 米的球形采样；不是单个圆形 AOE。
            area = index == 0 ? new SkillDamageArea
            {
                Shape = SkillDamageAreaShape.SampledCone,
                Radius = SampleRadius,
                Length = ConeLength,
                HalfAngleDegrees = HalfAngle
            } : default;
            return index == 0;
        }

        public HongShiZiHuoYan()
        {
            SkillID = "HongShiZiHuoYan";
            Type = SPSkillType.CombatArt;
            Cooldown = 3;
            ResourceCost = 15;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0062}HongShiZiHuoYan");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0063}喷射一道火焰，焚烧前方锥形区域的敌人。消耗法力值：30。冷却时间：5秒。");


        }


        public override bool Activate(Agent agent)
        {
            List<Agent> target = FindTarget(agent);
            if (target != null && target.Count > 0)
            {
                foreach (var item in target)
                {
                    if (item == null || !item.IsActive()) continue;
                    Script.CalculateFinalMagicDamage(agent, item, 60, DamageType.FIRE_ENHANCEMENT_BLASTING);
                    item.SetActionChannel(0, ActionIndexCache.Create("act_jump_loop"));
                }
                return true;
            }
            else
                return false;


        }
        private List<Agent> FindTarget(Agent agent)
        {
            TryGetDamageArea(agent, 0, out SkillDamageArea damageArea);
            List<Agent> list = new List<Agent>();
            Vec3 vec3 = new Vec3();
            for (int i = -3; i <= 3; i++)
            {
                for (global::System.Int32 j = 0; j <= (int)damageArea.Length; j++)
                {
                    Vec3 lookD= agent.LookDirection;
                    lookD=lookD.AsVec2.ToVec3();
                    lookD.RotateAboutZ(i * (damageArea.HalfAngleDegrees / 3f) * (3.1415f / 180.0f));
                    vec3 = agent.Position + Script.MultiplyVectorByScalar(lookD, j);
                    list.AddRange(Script.FindAgentsWithinSpellRange(vec3, damageArea.Radius));
                    vec3.z += 1f;
                    GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                    projectile.SetLocalPosition(vec3);
                    projectile.AddParticleSystemComponent("psys_battleground_env_fire");
                    var projData = new ProjectileData
                    {
                        BaseSpeed = 0f,
                        Name = SkillID,
                        CasterAgent = agent,
                        TargetAgent = agent,
                        SpawnTime = Mission.Current.CurrentTime,
                        Lifetime = 1f, // 自定义存在时间
                    };
                    SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                    SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, projData);//制导gameEntity测试

                }
            }
            list = list.Distinct<Agent>().ToList();
            Script.AgentListIFF(agent, list, out var FriendAgent, out var FoeAgent);

            return FoeAgent;

        }

    }
}
