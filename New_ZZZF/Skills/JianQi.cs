using NetworkMessages.FromServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal class JianQi : SkillBase
    {
        public JianQi()
        {
            SkillID = "JianQi";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 1f;             // 冷却时间（秒）
            ResourceCost = 0f;        // 法力消耗
            Text = new TaleWorlds.Localization.TextObject("{=12345676}JianQi");
            Difficulty = null;// new List<SkillDifficulty> {new SkillDifficulty(60,"Strong"), new SkillDifficulty(120, "OneHand") };//技能装备的需求
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
                Vec3 TarPos = vec3 + Script.MultiplyVectorByScalar(vec31, 15);//做一下向量计算,获得一个延目视方向往前走n个单位长度的坐标

                // 初始化数据对象
                var projData = new ProjectileData
                {
                    Name = SkillID,
                    skillBase = this,
                    CasterAgent = casterAgent,
                    TargetPos = TarPos,
                    SpawnTime = Mission.Current.CurrentTime,
                    Lifetime = 7f, // 自定义存在时间
                    BaseColor = new Vec3(0, 1, 0), // 绿色拖尾
                    SpiralIntensity = 2.5f // 强螺旋效果
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
            float BaseDamage = 20;
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
                Script.CalculateFinalMagicDamage(data.CasterAgent, agent, BaseDamage, DamageType.None);
                AgentSkillComponent agentComponent= Script.GetActiveComponents(agent);
                agentComponent._beHitCount += 1;
                agentComponent._beHitTime += 0.3f;


            }


        }
    }
}
