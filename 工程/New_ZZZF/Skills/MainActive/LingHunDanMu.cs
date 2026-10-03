using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills
{
    internal class LingHunDanMu : SkillBase
    {
        internal const float Range = 60f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private readonly List<Agent> _targets = new List<Agent>();
        internal const string DescriptionText = "手持弓、弩或投掷武器，向60米内敌人发射追踪灵魂弹。数量为5＋专精与熟练度÷60取高，飞刀数量乘3。基础伤害等于武器面板，接受原版及Mod武器增伤。熟练度低于100命中胸部，100至199命中护甲最弱部位，200至299命中头部，300以上头部且触发偷袭。每击杀一人本技能冷却减少3秒。不消耗弹药。耐力20，冷却20秒。";
        public LingHunDanMu() {
            SkillID = "LingHunDanMu"; Type = SPSkillType.MainActive; ResourceCost = 20f; Cooldown = 20f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0066}灵魂弹幕");
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0067}" + DescriptionText);
        }
        private static bool TryWeapon(Agent caster, out EquipmentIndex slot, out MissionWeapon weapon, out int proficiency) {
            slot = caster?.GetPrimaryWieldedItemIndex() ?? EquipmentIndex.None;
            weapon = MissionWeapon.Invalid; proficiency = 0;
            if (caster == null || !caster.IsActive() || caster.Mission == null || slot == EquipmentIndex.None) return false;
            weapon = caster.Equipment[slot];
            var usage = weapon.CurrentUsageItem;
            if (weapon.IsEmpty || usage == null || !usage.IsRangedWeapon) return false;
            proficiency = usage.RelevantSkill == null ? 0 : MissionGameModels.Current.AgentStatCalculateModel.GetEffectiveSkillForWeapon(caster, usage);
            return usage.GetModifiedThrustDamage(weapon.ItemModifier) > 0 || usage.GetModifiedSwingDamage(weapon.ItemModifier) > 0;
        }
        private static MetaMesh GetFlyingMesh(ItemObject item) => item == null ? null :
            MetaMesh.GetCopy(!string.IsNullOrEmpty(item.FlyingMeshName) ? item.FlyingMeshName : item.MultiMeshName, false, true);

        private static MetaMesh GetWeaponProjectileMesh(MissionWeapon weapon) {
            if (weapon.IsEmpty) return null;
            // 原版视图回调包含复合模型、锻造部件及物品修饰，不能用 Mesh.GetFromResource 替代。
            WeaponData data = weapon.GetWeaponData(false);
            return data.FlyingMesh ?? data.WeaponMesh;
        }
        private static MetaMesh GetProjectileMesh(Agent caster, MissionWeapon weapon) {
            var usage = weapon.CurrentUsageItem;
            if (usage.WeaponClass != WeaponClass.Bow && usage.WeaponClass != WeaponClass.Crossbow)
                return GetWeaponProjectileMesh(weapon);
            // 读取当前关联弹药，不扣数量；弹药用尽时也允许从装备或物品定义提取外观。
            MissionWeapon ammo = weapon.AmmoWeapon;
            if (!ammo.IsEmpty && ammo.CurrentUsageItem?.WeaponClass == usage.AmmoClass) {
                MetaMesh mesh = GetWeaponProjectileMesh(ammo);
                if (mesh != null) return mesh;
            }
            for (int i = (int)EquipmentIndex.WeaponItemBeginSlot; i < (int)EquipmentIndex.NumAllWeaponSlots; i++) {
                MissionWeapon equipped = caster.Equipment[(EquipmentIndex)i];
                if (equipped.IsEmpty || equipped.CurrentUsageItem?.WeaponClass != usage.AmmoClass) continue;
                MetaMesh mesh = GetWeaponProjectileMesh(equipped);
                if (mesh != null) return mesh;
            }
            if (Game.Current != null)
                foreach (ItemObject item in Game.Current.ObjectManager.GetObjectTypeList<ItemObject>()) {
                    if (item.PrimaryWeapon?.WeaponClass != usage.AmmoClass) continue;
                    MetaMesh mesh = GetFlyingMesh(item);
                    if (mesh != null) return mesh;
                }
            return null;
        }
        protected virtual int GetProjectileCount(Agent caster, MissionWeapon weapon, int proficiency) {
            int focus = 0;
            var skill = weapon.CurrentUsageItem.RelevantSkill;
            if (skill != null && caster.Character is CharacterObject character && character.IsHero)
                focus = character.HeroObject?.HeroDeveloper.GetFocus(skill) ?? 0;
            int count = 5 + Math.Max(focus, Math.Max(0, proficiency) / 60);
            return weapon.CurrentUsageItem.WeaponClass == WeaponClass.ThrowingKnife ? count * 3 : count;
        }
        private static bool IsTarget(Agent caster, Agent enemy) => enemy != null && enemy.IsActive() && enemy.IsHuman &&
            enemy.Health > 0f && caster.IsEnemyOf(enemy) && !SkillTargetProtection.IsProtected(enemy) &&
            (enemy.Position - caster.Position).LengthSquared <= Range * Range && RushMovementMissionLogic.HasLineOfSight(caster, enemy);
        private void CollectTargets(Agent caster) {
            _nearby.Clear(); _targets.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);
            foreach (Agent enemy in _nearby) if (IsTarget(caster, enemy)) _targets.Add(enemy);
            Vec3 forward = caster.LookDirection;
            if (forward.LengthSquared > 0.001f) forward.Normalize();
            _targets.Sort((a, b) => {
                Vec3 da = a.GetChestGlobalPosition() - caster.GetEyeGlobalPosition();
                Vec3 db = b.GetChestGlobalPosition() - caster.GetEyeGlobalPosition();
                float la = da.Normalize(), lb = db.Normalize();
                int comparison = Vec3.DotProduct(forward, db).CompareTo(Vec3.DotProduct(forward, da));
                return comparison != 0 ? comparison : la.CompareTo(lb);
            });
        }
        public override bool CheckCondition(Agent caster) {
            if (!base.CheckCondition(caster) || !TryWeapon(caster, out _, out _, out _) ||
                SpellProjectileMissionLogic.GetForCurrentMission() == null) return false;
            CollectTargets(caster);
            if (_targets.Count >= 2) return true;
            if (_targets.Count == 0) return false;
            return _targets[0].IsHero || WeiYa.GetTier(_targets[0]) >= WeiYa.GetTier(caster) ||
                caster.GetComponent<AgentSkillComponent>()?._currentStamina >= 80f;
        }
        public override bool Activate(Agent caster) {
            if (!TryWeapon(caster, out EquipmentIndex slot, out MissionWeapon weapon, out int proficiency))
                return FailActivation("需要手持远程或投掷武器。");
            var manager = SpellProjectileMissionLogic.GetForCurrentMission();
            if (manager == null) return FailActivation("投射物管理器不可用。");
            CollectTargets(caster);
            if (_targets.Count == 0) return FailActivation("60米内没有有效目标。");
            MetaMesh projectileMesh = GetProjectileMesh(caster, weapon);
            if (projectileMesh == null) return FailActivation("无法获取武器弹药模型。");
            var cast = new SoulBarrageNativeHit.CastSnapshot {
                Caster = caster, Skill = this, Weapon = weapon, Slot = slot, Proficiency = proficiency,
                Start = caster.GetChestGlobalPosition(),
                BaseDamage = Math.Max(weapon.CurrentUsageItem.GetModifiedThrustDamage(weapon.ItemModifier),
                    weapon.CurrentUsageItem.GetModifiedSwingDamage(weapon.ItemModifier)) };
            int count = GetProjectileCount(caster, weapon, proficiency), spawned = 0;
            for (int i = 0; i < count; i++) {
                Agent target = _targets[i % _targets.Count];
                Vec3 direction = target.GetChestGlobalPosition() - cast.Start;
                if (direction.LengthSquared < 0.001f) direction = caster.LookDirection;
                float targetDistance = direction.Normalize();
                int targetShotIndex = i / _targets.Count;
                if (targetShotIndex > 0) {
                    // 同目标首弹直飞，后续沿不同方位散开，再使用流星飞弹的转向机制回收。
                    Mat3 launchBasis = Mat3.CreateMat3WithForward(direction);
                    float angle = (targetShotIndex - 1) * 2.3999632f;
                    float spread = Math.Min(0.7f, targetDistance * 0.06f);
                    direction = direction + launchBasis.s * (MathF.Cos(angle) * spread) +
                        launchBasis.u * (MathF.Sin(angle) * spread);
                    direction.Normalize();
                }
                var candidates = new Agent[] { target };
                var request = new SpellProjectileRequest {
                    Caster = caster, StartPosition = cast.Start, Direction = direction,
                    Speed = 90f, Lifetime = 1.5f, MaxTravelDistance = 90f,
                    HitRadius = 0.2f, WorldHitRadius = 0.05f, CollisionInterval = 0.05f,
                    VisualMesh = projectileMesh,
                    // 原版弹药模型沿本地 +Z 延伸，将箭尖轴映射到当前飞行方向。
                    VisualPitchOffset = -1.5707964f,
                    TargetAgent = target, HomingTurnRateDegrees = 500f,
                    HomingDelay = targetShotIndex == 0 ? 0f : Math.Min(0.08f, targetDistance / 90f * 0.15f),
                    CollisionCandidatesProvider = () => candidates,
                    HitHumanAgentsOnly = true, HitEnemiesOnly = true,
                    AdditionalAgentFilter = candidate => candidate == target && !SkillTargetProtection.IsProtected(candidate),
                    InvokeImpactWhenLifetimeExpires = false,
                    OnImpact = impact => {
                        if (impact.Reason == SpellProjectileImpactReason.Agent && impact.DirectTarget != null)
                            SoulBarrageNativeHit.Register(cast, impact.DirectTarget, impact.Position);
                    } };
                if (manager.TrySpawn(request, out _)) spawned++;
            }
            if (spawned == 0) return FailActivation("无法生成灵魂飞弹。");
            MagicShoot.PlayReleasePresentation(caster);
            return true;
        }
    }
}