using New_ZZZF.Skills;
using New_ZZZF.Systems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class ZhenYinZhan : SkillBase
    {
        public bool CombatArtFlag = false;
        public ZhenYinZhan()
        {
            SkillID = "ZhenYinZhan";      // 必须唯一
            Type = SkillType.MainActive;    // 类型必须明确
            Cooldown = 2;             // 冷却时间（秒）
            ResourceCost = 0f;        // 消耗
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0072}ZhenYinZhan");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0073}每次挥动武器时，触发弧形剑气，伤害范围内所有的敌人。消耗耐力值：60。持续时间：45秒。冷却时间：90秒。");
        }
        public override bool Activate(Agent agent)
        {

            // 每次创建新的状态实例
            List<AgentBuff> newStates = new List<AgentBuff> { new ZhenYinZhanBuff(45f, agent), }; // 新实例
            foreach (var state in newStates)
            {
                state.TargetAgent = agent;
                agent.GetComponent<AgentSkillComponent>().StateContainer.AddState(state);
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
                if (agentComponent == null || agentComponent._beHitCount>=1) { return; }

                Script.CalculateFinalMagicDamage(data.CasterAgent, agent, BaseDamage, DamageType.None);
                agentComponent._beHitCount += 1;
                agentComponent._beHitTime = TaleWorlds.Library.MathF.Clamp(agentComponent._beHitTime + 0.3f, 0, 0.3f);


            }


        }
        public void CanUse(Agent agent)
        {


            //获取pos(骑砍1版)即位置和方向
            MatrixFrame FrameL = agent.LookFrame;//MatrixFrame,包含vec3格式的位置坐标,以及一个mat3格式的方向矩阵
            MatrixFrame FrameM = agent.LookFrame;//MatrixFrame,包含vec3格式的位置坐标,以及一个mat3格式的方向矩阵
            MatrixFrame FrameR = agent.LookFrame;//MatrixFrame,包含vec3格式的位置坐标,以及一个mat3格式的方向矩阵
            FrameL.origin = agent.GetEyeGlobalPosition();
            FrameM.origin = agent.GetEyeGlobalPosition();
            FrameR.origin = agent.GetEyeGlobalPosition();
            FrameL.rotation.RotateAboutAnArbitraryVector(Vec3.Up, -45);
            FrameR.rotation.RotateAboutAnArbitraryVector(Vec3.Up, 45);
            Vec3 vec3 = FrameM.origin;//新建一个三维向量,记录玩家头的位置
            Vec3 vec31 = FrameL.rotation.f;//新建一个三维方向向量,记录玩家头的旋转角度
            Vec3 vec32 = FrameM.rotation.f;//新建一个三维方向向量,记录玩家头的旋转角度
            Vec3 vec33 = FrameR.rotation.f;//新建一个三维方向向量,记录玩家头的旋转角度
            vec31 = vec31.NormalizedCopy().AsVec2.ToVec3();
            vec32 = vec32.NormalizedCopy().AsVec2.ToVec3();
            vec33 = vec33.NormalizedCopy().AsVec2.ToVec3();
            //测试一下这个坐标正不正常,新建一个游戏实体,并且附加一下模型,并且把这个东西set到刚才的坐标上
            GameEntity projectileL = GameEntity.CreateEmpty(Mission.Current.Scene);
            GameEntity projectileM = GameEntity.CreateEmpty(Mission.Current.Scene);
            GameEntity projectileR = GameEntity.CreateEmpty(Mission.Current.Scene);
            projectileL.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "weapon_heap_sword_a", true));
            projectileM.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "weapon_heap_sword_a", true));
            projectileR.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "weapon_heap_sword_a", true));


            // 对于左侧的projectileL
            Vec3 TarPosL = vec3 + Script.MultiplyVectorByScalar(vec31, 10); // 左侧目标位置
            MatrixFrame leftFrame = new MatrixFrame(FrameL.rotation, vec3);
            projectileL.SetGlobalFrame(leftFrame);
            var projDataL = new ProjectileData
            {
                Name = this.SkillID,
                skillBase = this,
                CasterAgent = agent,
                TargetPos = TarPosL,
                SpawnTime = Mission.Current.CurrentTime,
                Lifetime = 0.3f, // 自定义存在时间
            };
            SkillSystemBehavior.WoW_CustomGameEntity.Add(projectileL);
            SkillSystemBehavior.WoW_ProjectileDB.Add(projectileL, projDataL);

            // 对于中间的projectileM (已有的代码)
            Vec3 TarPosM = vec3 + Script.MultiplyVectorByScalar(vec32, 10); // 中间目标位置
            MatrixFrame middleFrame = new MatrixFrame(FrameM.rotation, vec3);
            projectileM.SetGlobalFrame(middleFrame);
            var projDataM = new ProjectileData
            {
                Name = this.SkillID,
                skillBase = this,
                CasterAgent = agent,
                TargetPos = TarPosM,
                SpawnTime = Mission.Current.CurrentTime,
                Lifetime = 0.3f, // 自定义存在时间
            };
            SkillSystemBehavior.WoW_CustomGameEntity.Add(projectileM);
            SkillSystemBehavior.WoW_ProjectileDB.Add(projectileM, projDataM);

            // 对于右侧的projectileR
            Vec3 TarPosR = vec3 + Script.MultiplyVectorByScalar(vec33, 10); // 右侧目标位置
            MatrixFrame rightFrame = new MatrixFrame(FrameR.rotation, vec3);
            projectileR.SetGlobalFrame(rightFrame);
            var projDataR = new ProjectileData
            {
                Name = this.SkillID,
                skillBase = this,
                CasterAgent = agent,
                TargetPos = TarPosR,
                SpawnTime = Mission.Current.CurrentTime,
                Lifetime = 0.3f, // 自定义存在时间
            };
            SkillSystemBehavior.WoW_CustomGameEntity.Add(projectileR);
            SkillSystemBehavior.WoW_ProjectileDB.Add(projectileR, projDataR);

        }
        public class ZhenYinZhanBuff : AgentBuff
        {
            public ZhenYinZhanBuff(float duration, Agent source)
            {
                StateId = "ZhenYinZhanBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public override void OnApply(Agent agent)
            {
                agent.UpdateAgentProperties();
            }

            public override void OnUpdate(Agent agent, float dt)
            {
            }

            public override void OnRemove(Agent agent)
            {
                agent.UpdateAgentProperties();
            }
        }
    }
}
