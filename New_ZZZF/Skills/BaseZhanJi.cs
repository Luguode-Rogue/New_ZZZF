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
using static New_ZZZF.GuWu;

namespace New_ZZZF.Skills//（法术）
{
    // 示例：在火球术中附加燃烧状态
    public class BaseZhanJi : SkillBase
    {
        public BaseZhanJi()
        {
            SkillID = "BaseZhanJi";
            Type = SkillType.None;
            Cooldown = 3;
            ResourceCost = 15;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0062}BaseZhanJi");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0063}BaseZhanJi");


        }


        public override bool Activate(Agent casterAgent)
        {
            if (casterAgent != null)
            {
                //获取pos(骑砍1版)即位置和方向
                MatrixFrame Frame = casterAgent.LookFrame;//MatrixFrame,包含vec3格式的位置坐标,以及一个mat3格式的方向矩阵
                Frame.origin = casterAgent.GetEyeGlobalPosition();//因为LookFrame获取的位置信息是在agent脚下的那个位置,所以重新把坐标赋值为头的位置                    //以目视方向做偏移
                Vec3 vec3 = Frame.origin;//新建一个三维向量,记录玩家头的位置
                Vec3 vec31 = Frame.rotation.f;//新建一个三维方向向量,记录玩家头的旋转角度
                vec31 = vec31.NormalizedCopy();
                //测试一下这个坐标正不正常,新建一个游戏实体,并且附加一下模型,并且把这个东西set到刚才的坐标上
                GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                projectile.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "weapon_heap_sword_a", true));
                projectile.SetLocalPosition(vec3);
                Vec3 TarPos = vec3 + Script.MultiplyVectorByScalar(vec31, 1);//做一下向量计算,获得一个延目视方向往前走n个单位长度的坐标

                // 初始化数据对象
                var projData = new ProjectileData
                {
                    Name = SkillID,
                    skillBase = this,
                    CasterAgent = casterAgent,
                    TargetPos = TarPos,
                    SpawnTime = Mission.Current.CurrentTime,
                    Lifetime = 0.1f, // 自定义存在时间
                };
                // 获取当前 vec3 位置
                Vec3 currentPos = vec3; // 这里的 vec3 应该是你在循环中计算得到的

                // 创建一个 MatrixFrame，其原点是当前 vec3 位置，方向朝向 casterAgent
                MatrixFrame testtargetFrame = new MatrixFrame(Frame.rotation, currentPos);
                projectile.SetGlobalFrame(testtargetFrame);
                SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, projData);//制导gameEntity测试

            }

            return true;
        }
        public override void GameEntityDamage(GameEntity missileEntity)
        {
            if (!SkillSystemBehavior.WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                return;
            float BaseDamage = 40;
            // 获取Agent的CharacterObject

            int skill = 0;
            CharacterObject characterObject = ((data.CasterAgent != null) ? data.CasterAgent.Character : null) as CharacterObject;
            if (characterObject == null)
            {
                BasicCharacterObject character = (data.CasterAgent != null) ? data.CasterAgent.Character : null;
                skill = character.GetSkillValue(DefaultSkills.Bow);
            }
            else
            {
                CharacterObject character = characterObject;
                skill = character.GetSkillValue(DefaultSkills.Bow);
            }

            BaseDamage = BaseDamage * (1 + skill / 100);
            // 通过属性管理器获取智力值//小兵没有智力值，改用熟练度吧
            //int intelligenceValue = character.HeroObject.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
            Agent castAgent = data.CasterAgent;
            List<Agent> list = Script.FindAgentsWithinSpellRange(missileEntity.GlobalPosition, 2);
            List<Agent> FriendAgent = new List<Agent>();
            List<Agent> FoeAgent = new List<Agent>();
            Script.AgentListIFF(castAgent, list, out FriendAgent, out FoeAgent);
            foreach (Agent agent in FoeAgent)
            {
                AgentSkillComponent agentComponent = Script.GetActiveComponents(agent);
                if (agentComponent != null&& agentComponent._beHitCount==0)
                {
                    Script.CalculateFinalMagicDamage(data.CasterAgent, agent, BaseDamage, DamageType.None);
                    agentComponent._beHitCount += 1;
                    agentComponent._beHitTime += 0.3f;
                }


            }


        }

    }
}
