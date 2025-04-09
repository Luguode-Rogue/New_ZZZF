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
    internal class LingHunDanMu : SkillBase
    {
        public LingHunDanMu()
        {
            SkillID = "LingHunDanMu";
            Type = SkillType.MainActive;
            Cooldown = 2;
            ResourceCost = 2;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0066}LingHunDanMu");
            this.Description = new TaleWorlds.Localization.TextObject("{=ZZZF0067}使用手持的远程武器以灵魂飞弹攻击敌人，瞬间对30米内的多名目标造成伤害。" +
                "只触发武器特效，不触发弹药效果。影响人数：5+武器专精/熟练取高。该技能每完成一个击杀，减少冷却3秒。消耗耐力值：20。冷却时间：20秒。");
            this.Difficulty = null;
        }

        public override bool Activate(Agent casterAgent)
        {
            if (casterAgent == null) return false;

            // 获取右手骨骼位置
            Vec3 spawnPosition = GetRightHandBonePosition(casterAgent);

            // 寻找有效目标
            const float searchRadius = 30f;
            var potentialTargets = Script.FindAgentsWithinSpellRange(spawnPosition, (int)searchRadius)
                .Distinct()
                .ToList();

            Script.AgentListIFF(casterAgent, potentialTargets,
                out List<Agent> _,
                out List<Agent> foeAgents);

            // 计算技能属性
            int specialization = 0;
            int proficiency = 0;
            if(casterAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand)==EquipmentIndex.None) { return false; }
            MissionWeapon mainHandWeapon = casterAgent.Equipment[casterAgent.GetWieldedItemIndex(Agent.HandIndex.MainHand)];
            SkillObject relevantSkill = mainHandWeapon.Item?.RelevantSkill;

            if (casterAgent.IsHero && casterAgent.Character is CharacterObject character)
            {
                specialization = character.HeroObject?.HeroDeveloper.GetFocus(relevantSkill) ?? 0;
            }
            else
            {
                proficiency = casterAgent.Character?.GetSkillValue(relevantSkill) ?? 0;
            }

            int totalProjectiles = 5 + specialization + proficiency;
            for (int i = totalProjectiles; i > 0; i--)
            {
                foreach (var targetAgent in foeAgents)
                {
                    if (targetAgent == null || targetAgent == casterAgent)
                    {
                        Script.SysOut("无有效目标", casterAgent);
                        return false;
                    }

                    if (totalProjectiles <= 0)
                        break;
                    // 计算基础视线方向
                    Vec3 lookDirection = (targetAgent.GetEyeGlobalPosition() - casterAgent.GetEyeGlobalPosition()).NormalizedCopy();

                    // 构建旋转矩阵
                    Mat3 rotation = Mat3.Identity;

                    // 创建投射物框架
                    MatrixFrame projectileFrame = new MatrixFrame(
                        rotation,
                        spawnPosition
                    );


                    projectileFrame.Rotate(-30, Vec3.Forward);
                    foreach (var item in SkillSystemBehavior.WoW_ProjectileDB)
                    {
                        if (item.Value.TargetAgent.Index == targetAgent.Index) 
                        {

                            projectileFrame.Rotate(-10*i, Vec3.Side);
                        }
                    }

                    // 创建投射物实体
                    GameEntity projectile = GameEntity.CreateEmpty(Mission.Current.Scene);
                    projectile.AddAllMeshesOfGameEntity(GameEntity.Instantiate(
                        Mission.Current.Scene,
                        "mangonel_mapicon_projectile",
                        true
                    ));

                    projectile.SetGlobalFrame(projectileFrame);

                    SkillSystemBehavior.WoW_CustomGameEntity.Add(projectile);
                    SkillSystemBehavior.WoW_ProjectileDB.Add(projectile, new ProjectileData
                    {
                        BaseSpeed = 90f,
                        MaxTurnRate = 500f,
                        Name = SkillID,
                        CasterAgent = casterAgent,
                        TargetAgent = targetAgent,
                        SpawnTime = Mission.Current.CurrentTime,
                        Lifetime = 0.3f
                    });

                    totalProjectiles--;
                    if(totalProjectiles<=0 ) break;
                }

            }

            return true;
        }

        // 新增方法：获取右手骨骼世界坐标
        private Vec3 GetRightHandBonePosition(Agent agent)
        {
            // 获取骨骼系统
            Skeleton skeleton = agent.AgentVisuals.GetSkeleton();

            // 获取右手骨骼局部坐标系
            MatrixFrame localBoneFrame = skeleton.GetBoneEntitialFrameWithName("r_hand");

            // 转换为世界坐标系
            MatrixFrame agentGlobalFrame = agent.AgentVisuals.GetGlobalFrame();
            return agentGlobalFrame.TransformToParent(localBoneFrame.origin);
        }






        public static void LingHunDanMuDamage(GameEntity missileEntity)
        {
            if (!SkillSystemBehavior.WoW_ProjectileDB.TryGetValue(missileEntity, out ProjectileData data))
                return;
            float BaseDamage = 20;


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


            Script.CalculateFinalMagicDamage(data.CasterAgent, data.TargetAgent, BaseDamage, DamageType.None);
            if (data.TargetAgent == null||(data.TargetAgent != null&& data.TargetAgent.Health<=1))
            { 
                SkillSystemBehavior.ActiveComponents.TryGetValue(data.CasterAgent.Index,out var agentSkill);
                if (agentSkill != null) 
                { agentSkill.UpdateCooldowns(2f,SkillType.MainActive); }
            }
        }
    }
}
