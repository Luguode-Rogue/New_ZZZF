using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using SandBox.GameComponents;

namespace New_ZZZF
{
    /// <summary>
    /// 新伤害结算的最后一层：接收原版伤害计算完成后的 baseDamage，
    /// 在原版 Reduction 阶段替换旧的护甲减伤规则。
    /// </summary>
    internal static class ZZZFDamageRules
    {
        /// <summary>
        /// 攻击方保底伤害比例。
        /// 英雄：等级 * 1%。
        /// 士兵：战斗阶数 * 5%。
        /// </summary>
        public static float GetMinimumDamageRatio(Agent attacker)
        {
            BasicCharacterObject character = attacker?.Character;
            if (character == null)
                return 0f;

            if (character.IsHero)
            {
                Hero hero = (character as CharacterObject)?.HeroObject;
                if (hero == null)
                    return 0f;

                return MathF.Max(0f, hero.Level * 0.01f);
            }

            if (character.IsSoldier)
            {
                return MathF.Max(0f, character.GetBattleTier() * 0.05f);
            }

            return 0f;
        }

        /// <summary>
        /// n = 原版伤害在进入 Reduction 阶段时的值。
        /// 新规则：max(n - armor, n * attackerMinimumRatio)。
        /// </summary>
        public static float ApplyArmorAndMinimumDamage(
            in AttackInformation attackInformation,
            float n)
        {
            if (n <= 0f)
                return 0f;

            float armor = MathF.Max(0f, attackInformation.ArmorAmountFloat);
            float armorDamage = n - armor;
            float minimumDamage = n * GetMinimumDamageRatio(attackInformation.AttackerAgent);

            return MathF.Max(0f, MathF.Max(armorDamage, minimumDamage));
        }
    }

    /// <summary>
    /// Campaign/Sandbox 伤害模型。
    /// 只覆盖 Reduction 阶段，避免重新实现 Bannerlord 前面的原版伤害计算。
    /// </summary>
    public sealed class ZZZFSandboxDamageRefactorModel : SandboxAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            return ZZZFDamageRules.ApplyArmorAndMinimumDamage(in attackInformation, baseDamage);
        }
    }

    /// <summary>
    /// CustomAgentApplyDamageModel 伤害模型。
    /// 只覆盖 Reduction 阶段，避免重新实现 Bannerlord 前面的原版伤害计算。
    /// </summary>
    public sealed class ZZZFCustomDamageRefactorModel : CustomAgentApplyDamageModel
    {
        public override float ApplyDamageReductions(
            in AttackInformation attackInformation,
            in AttackCollisionData collisionData,
            float baseDamage)
        {
            return ZZZFDamageRules.ApplyArmorAndMinimumDamage(in attackInformation, baseDamage);
        }
    }
}
