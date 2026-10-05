using TaleWorlds.Localization;

namespace New_ZZZF
{
    /// <summary>复用主动翻滚的动作、方向、落点与威胁判断；独立个人冷却。</summary>
    internal sealed class PassiveRoll : Roll
    {
        protected override bool HasActiveEffects => false;
        public PassiveRoll()
        {
            SkillID = "PassiveRoll";
            Type = SPSkillType.Passive;
            Cooldown = 2f;
            ResourceCost = 0f;
            Text = new TextObject("{=ZZZF_PASSIVE_ROLL_NAME}被动翻滚");
            Description = new TextObject("{=ZZZF_PASSIVE_ROLL_DESC}徒步按空格朝移动方向翻滚，无输入时向前。无消耗，冷却2秒。仅翻滚位移，不免伤、不清除持续伤害。NPC根据近战威胁自动规避。");
        }
    }
}