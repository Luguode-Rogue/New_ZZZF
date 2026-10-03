using System;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    internal class FengBaoZhiLi : SkillBase
    {
        private const float BuffDuration = 60f;
        private const float Range = 50f;
        private const float RangeSquared = Range * Range;
        private const float AiRefreshThreshold = 10f;
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        private static readonly string[] MaleYells =
        {
            "event:/voice/combat/male/01/yell",
            "event:/voice/combat/male/02/yell",
            "event:/voice/combat/male/03/yell"
        };
        private static readonly string[] FemaleYells =
        {
            "event:/voice/combat/female/01/yell",
            "event:/voice/combat/female/02/yell",
            "event:/voice/combat/female/03/yell"
        };
        private static readonly EquipmentIndex[] WeaponSlots =
        {
            EquipmentIndex.WeaponItemBeginSlot, EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2, EquipmentIndex.Weapon3
        };

        public FengBaoZhiLi()
        {
            SkillID = "FengBaoZhiLi";
            Type = SPSkillType.MainActive;
            Cooldown = 60f;
            ResourceCost = 60f;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0027}FengBaoZhiLi");
            Description = new TaleWorlds.Localization.TextObject(
                "{=ZZZF0028}使50米内装备远程武器的友军获得风暴之力：远程伤害提高150%，射击精度提高100%，远程准备和装填速度提高50%；远程命中有20%几率额外增加100基础伤害。消耗耐力：60。持续时间：60秒。冷却时间：60秒。");
        }

        public override bool CheckCondition(Agent caster)
        {
            if (!base.CheckCondition(caster) || caster.Mission == null ||
                !IsStartingCombat(caster))
                return false;

            if (NeedsBuff(caster, caster))
                return true;
            FillNearby(caster);
            foreach (Agent ally in _nearby)
                if (NeedsBuff(caster, ally))
                    return true;
            return false;
        }

        public override bool Activate(Agent caster)
        {
            if (caster == null || !caster.IsActive() || caster.Mission == null)
                return FailActivation("施法者不可用。");

            int affected = 0;
            if (ApplyToAlly(caster, caster))
                affected++;
            FillNearby(caster);
            foreach (Agent ally in _nearby)
                if (ally != caster && ApplyToAlly(caster, ally))
                    affected++;
            if (affected == 0)
                return FailActivation("50米内没有装备远程武器的友军。");
            string[] yells = caster.IsFemale ? FemaleYells : MaleYells;
            try
            {
                SoundManager.StartOneShotEvent(
                    yells[MBRandom.RandomInt(yells.Length)], caster.Position);
            }
            catch (Exception) { /* 声音资源故障不影响已施加的 Buff。 */ }
            return true;
        }

        private void FillNearby(Agent caster)
        {
            _nearby.Clear();
            caster.Mission.GetNearbyAgents(caster.Position.AsVec2, Range, _nearby);
        }

        private static bool CanBuff(Agent caster, Agent ally)
        {
            if (ally == null || !ally.IsActive() || !ally.IsHuman ||
                ally.GetComponent<AgentSkillComponent>() == null ||
                SkillTargetProtection.IsProtected(ally) ||
                (ally.Position - caster.Position).LengthSquared > RangeSquared ||
                (ally != caster && (!ally.IsFriendOf(caster) || ally.IsEnemyOf(caster))))
                return false;
            foreach (EquipmentIndex slot in WeaponSlots)
            {
                MissionWeapon equipped = ally.Equipment[slot];
                WeaponComponentData weapon = equipped.CurrentUsageItem;
                ItemObject.ItemTypeEnum? itemType = equipped.Item?.Type;
                if (weapon?.IsRangedWeapon == true ||
                    itemType == ItemObject.ItemTypeEnum.Bow ||
                    itemType == ItemObject.ItemTypeEnum.Crossbow ||
                    itemType == ItemObject.ItemTypeEnum.Thrown)
                    return true;
            }
            return false;
        }

        private static bool NeedsBuff(Agent caster, Agent ally)
        {
            if (!CanBuff(caster, ally))
                return false;
            FengBaoZhiLiBuff current = ally.GetComponent<AgentSkillComponent>()
                .StateContainer.GetState("FengBaoZhiLiBuff") as FengBaoZhiLiBuff;
            return current == null || current.Duration <= AiRefreshThreshold;
        }

        private static bool ApplyToAlly(Agent caster, Agent ally)
        {
            if (!CanBuff(caster, ally))
                return false;
            AgentBuffContainer states = ally.GetComponent<AgentSkillComponent>().StateContainer;
            FengBaoZhiLiBuff current = states.GetState("FengBaoZhiLiBuff") as FengBaoZhiLiBuff;
            if (current != null)
            {
                current.Refresh(ally, caster, BuffDuration);
                ally.GetComponent<AgentSkillComponent>().NotifySkillAvailabilityChanged();
            }
            else
                states.AddState(new FengBaoZhiLiBuff(BuffDuration, caster), ally);
            return true;
        }

        internal static bool IsCurrentlyRanged(Agent agent)
        {
            return agent != null &&
                agent.WieldedWeapon.CurrentUsageItem?.IsRangedWeapon == true;
        }

        internal static void ApplyRangedDrivenProperties(
            Agent agent, AgentDrivenProperties properties)
        {
            if (properties == null || !IsCurrentlyRanged(agent) ||
                agent.GetComponent<AgentSkillComponent>()?.StateContainer
                    .HasState("FengBaoZhiLiBuff") != true)
                return;
            properties.WeaponMaxMovementAccuracyPenalty /= 2f;
            properties.WeaponMaxUnsteadyAccuracyPenalty /= 2f;
            properties.WeaponRotationalAccuracyPenaltyInRadians /= 2f;
            properties.WeaponInaccuracy /= 2f;
            properties.ReloadSpeed *= 1.5f;
            properties.ThrustOrRangedReadySpeedMultiplier *= 1.5f;
        }

        private static bool IsStartingCombat(Agent agent)
        {
            Agent.ActionCodeType action = agent.GetCurrentActionType(1);
            return action == Agent.ActionCodeType.ReadyRanged ||
                action == Agent.ActionCodeType.ReleaseRanged ||
                action == Agent.ActionCodeType.ReleaseThrowing ||
                action == Agent.ActionCodeType.ReadyMelee ||
                action == Agent.ActionCodeType.ReleaseMelee;
        }

        public sealed class FengBaoZhiLiBuff : AgentBuff
        {
            private FengBaoZhiLiWhirlVisual _burst;
            private FengBaoZhiLiWhirlVisual _lingering;
            private float _weaponCheckTimer;
            private bool _wasRanged;

            public FengBaoZhiLiBuff(float duration, Agent source)
            {
                StateId = "FengBaoZhiLiBuff";
                Duration = duration;
                SourceAgent = source;
            }

            public void Refresh(Agent owner, Agent source, float duration)
            {
                Duration = MathF.Max(Duration, duration);
                SourceAgent = source;
                _burst?.Remove();
                _lingering?.Remove();
                _lingering = null;
                _burst = FengBaoZhiLiWhirlVisual.Create(owner, false);
            }

            public override void OnApply(Agent agent)
            {
                _wasRanged = IsCurrentlyRanged(agent);
                agent?.UpdateAgentProperties();
                _burst = FengBaoZhiLiWhirlVisual.Create(agent, false);
            }

            public override void OnUpdate(Agent agent, float dt)
            {
                if (agent == null || !agent.IsActive() || dt <= 0f)
                    return;
                if (_burst != null && !_burst.Update(agent, dt))
                {
                    _burst = null;
                    if (_lingering == null)
                        _lingering = FengBaoZhiLiWhirlVisual.Create(agent, true);
                }
                _lingering?.Update(agent, dt);
                _weaponCheckTimer += dt;
                if (_weaponCheckTimer >= 0.2f)
                {
                    _weaponCheckTimer = 0f;
                    bool ranged = IsCurrentlyRanged(agent);
                    if (ranged != _wasRanged)
                    {
                        _wasRanged = ranged;
                        agent.UpdateAgentProperties();
                    }
                }
            }

            public override void OnRemove(Agent agent)
            {
                _burst?.Remove();
                _burst = null;
                _lingering?.Remove();
                _lingering = null;
                if (agent != null && agent.IsActive())
                    agent.UpdateAgentProperties();
            }
        }
    }
}
