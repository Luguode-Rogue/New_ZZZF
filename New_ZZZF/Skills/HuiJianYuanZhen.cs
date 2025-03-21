using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace New_ZZZF.Skills
{
    internal class HuiJianYuanZhen : SkillBase
    {
        public HuiJianYuanZhen()
        {
            SkillID = "HuiJianYuanZhen";      // 必须唯一
            Type = SkillType.Spell;    // 类型必须明确
            Cooldown = 0f;             // 冷却时间（秒）
            ResourceCost = 0f;        // 法力消耗
            Text = new TaleWorlds.Localization.TextObject("{=12345677}HuiJianYuanZhen");
        }

        public override bool Activate(Agent casterAgent)
        {
            Random random = new Random();
            if (casterAgent != null)
            {
                //获取pos(骑砍1版)即位置和方向
                MatrixFrame Frame = casterAgent.LookFrame;//MatrixFrame,包含vec3格式的位置坐标,以及一个mat3格式的方向矩阵
                Frame.origin = casterAgent.GetEyeGlobalPosition();//因为LookFrame获取的位置信息是在agent脚下的那个位置,所以重新把坐标赋值为头的位置
                List<Agent> TarAgents = new List<Agent>();//建一个list用来存放一会需要造成伤害的agent
                List<Agent> FriendAgent = new List<Agent>();
                List<Agent> FoeAgent = new List<Agent>();
                TarAgents.AddRange(Script.FindAgentsWithinSpellRange(Frame.origin, 50));
                TarAgents = TarAgents.Distinct().ToList();
                Script.AgentListIFF(casterAgent, TarAgents, out FriendAgent, out FoeAgent);//获取敌方agent
                Agent TarAgent=Script.FindClosestAgentToCaster(casterAgent, FoeAgent);
                if (casterAgent == TarAgent)
                {
                    InformationManager.DisplayMessage(new InformationMessage("无有效目标"));
                    return false;
                }
                for (int i = 0; i <= 20; i++)
                {
                    //以目视方向做偏移
                    Vec3 vec3 = Frame.origin;//新建一个三维向量,记录玩家头的位置
                    Vec3 vec31 = Frame.rotation.f;//新建一个三维方向向量,记录玩家头的旋转角度
                    vec3 = vec3 + Script.MultiplyVectorByScalar(vec31, 0);//做一下向量计算,获得一个延目视方向往前走n个单位长度的坐标

                    //测试一下这个坐标正不正常,新建一个游戏实体,并且附加一下模型,并且把这个东西set到刚才的坐标上
                    GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                    projectile.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "mangonel_mapicon_projectile", true));
                    projectile.SetLocalPosition(vec3);
                    // 初始化数据对象
                    var projData = new ProjectileData
                    {
                        BaseSpeed = 30f,
                        MaxTurnRate = 270f,
                        Name = SkillID,
                        CasterAgent= casterAgent,
                        TargetAgent = TarAgent,
                        SpawnTime = Mission.Current.CurrentTime,
                        Lifetime = 7f, // 自定义存在时间
                        BaseColor = new Vec3(0, 1, 0), // 绿色拖尾
                        SpiralIntensity = 2.5f // 强螺旋效果
                    };
                    // 获取 casterAgent 的眼睛位置
                    Vec3 casterEyePosition = casterAgent.GetEyeGlobalPosition();
                    // 设定随机偏移的最大值
                    float randomOffsetMax = 100000.0f; // 你可以根据需要调整这个值
                    Vec3 randomOffset = new Vec3(
                        (float)(random.NextDouble() * 2 - 1) * randomOffsetMax,
                        (float)(random.NextDouble() * 2 - 1) * randomOffsetMax,
                        (float)(random.NextDouble() * 2 - 1) * randomOffsetMax
                    );
                    casterEyePosition = casterEyePosition + randomOffset;
                    // 获取当前 vec3 位置
                    Vec3 currentPos = vec3; // 这里的 vec3 应该是你在循环中计算得到的

                    // 计算从当前位置到 casterAgent 眼睛位置的向量
                    Vec3 forward = casterEyePosition - currentPos;
                    forward.Normalize(); // 确保这是一个单位向量

                    // 获取一个合适的上方向向量，这里我们使用世界空间中的 Vec3.Up
                    Vec3 up = Vec3.Up;

                    // 计算右方向向量，这里我们使用前方向量和上方向量来计算
                    Vec3 right = Vec3.CrossProduct(up, forward);
                    right.Normalize(); // 确保这是一个单位向量

                    // 由于前方向量和上方向量可能不是完全正交的，我们需要重新计算上方向向量
                    up = Vec3.CrossProduct(forward, right);
                    up.Normalize(); // 确保这是一个单位向量

                    // 创建一个 Mat3 旋转矩阵
                    Mat3 rotationMatrix = new Mat3(
                        right.x, right.y, right.z,
                        up.x, up.y, up.z,
                        forward.x, forward.y, forward.z
                    );

                    // 创建一个 MatrixFrame，其原点是当前 vec3 位置，方向朝向 casterAgent
                    MatrixFrame testtargetFrame = new MatrixFrame(rotationMatrix, currentPos);
                    projectile.SetGlobalFrame(testtargetFrame);
                    SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                    SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, projData);//制导gameEntity测试

                }

            }
            return true;

        }

        public static void HuiJianYuanZhenDamage(GameEntity missileEntity)
        {
            if (!SkillSystemBehavior.WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                return;
            float BaseDamage=20;
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

            BaseDamage= BaseDamage * (1+ skill / 100);
            // 通过属性管理器获取智力值//小兵没有智力值，改用熟练度吧
            //int intelligenceValue = character.HeroObject.GetAttributeValue(DefaultCharacterAttributes.Intelligence);
            Script.CalculateFinalMagicDamage(data.CasterAgent,data.TargetAgent,BaseDamage,"mofa");
        }
    }
}
