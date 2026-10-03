using New_ZZZF.Systems;
using New_ZZZF.Skills;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;

namespace New_ZZZF
{
    /// <summary>每个目标独立掷骰延续；分帧调度限制瞬时工作量，不限制总落雷次数。</summary>
    internal sealed class StormLightningMissionLogic : MissionLogic
    {
        private const float StrikeInterval = 0.35f;
        private const int StrikesPerTick = 128;
        private const float StrikeRadius = 3f;
        private sealed class Chain
        {
            internal Agent Caster;
            internal Agent Target;
            internal float Power;
            internal float NextStrike;
            internal bool ShowVisual;
            internal bool FirstStrike = true;
            internal Vec3 InitialPosition;
        }
        private readonly Queue<Chain> _chains = new Queue<Chain>();
        private readonly List<Agent> _targets = new List<Agent>();
        private readonly MBList<Agent> _nearby = new MBList<Agent>();
        internal bool HasEligibleEnemy(Agent caster)
        {
            foreach (Agent target in Mission.Agents)
                if (IsEligible(caster, target)) return true;
            return false;
        }
        private static bool IsEligible(Agent caster, Agent target) => caster != null && caster.IsActive() &&
            target != null && target != caster && target.IsActive() && target.IsHuman && target.Health > 0f &&
            caster.IsEnemyOf(target) && !SkillTargetProtection.IsProtected(target);

        internal bool Begin(Agent caster)
        {
            _targets.Clear();
            foreach (Agent target in Mission.Agents)
                if (IsEligible(caster, target)) _targets.Add(target);
            if (_targets.Count == 0) return false;
            // 保持24个可见目标；优先玩家附近，所有目标仍参与完整伤害与追加判定。
            Vec3 reference = Mission.MainAgent?.Position ?? caster.Position;
            _targets.Sort((a, b) => (a.Position - reference).LengthSquared.CompareTo((b.Position - reference).LengthSquared));
            float power = MagicDamageSystem.GetSpellPowerCoefficient(caster);
            for (int i = 0; i < _targets.Count; i++)
                _chains.Enqueue(new Chain { Caster = caster, Target = _targets[i], Power = power,
                    NextStrike = Mission.CurrentTime, ShowVisual = i < 24, InitialPosition = _targets[i].Position });
            _targets.Clear();
            return true;
        }
        public override void OnMissionTick(float dt)
        {
            if (dt <= 0f) return;
            float now = Mission.CurrentTime;
            int pending = _chains.Count;
            int processed = 0;
            for (int i = 0; i < pending; i++)
            {
                Chain chain = _chains.Dequeue();
                if (chain.Caster == null || !chain.Caster.IsActive()) continue;
                if (!chain.FirstStrike && !IsEligible(chain.Caster, chain.Target)) continue;
                if (chain.NextStrike > now || processed >= StrikesPerTick)
                { _chains.Enqueue(chain); continue; }
                processed++;
                // 首轮所有已确定的落点都落雷，不因目标提前被相邻落雷击杀而撤掉。
                Vec3 position = chain.FirstStrike ? chain.InitialPosition : chain.Target.Position;
                chain.FirstStrike = false;
                StrikeArea(chain, position);
                if (MBRandom.RandomFloat >= 0.5f || !IsEligible(chain.Caster, chain.Target)) continue;
                chain.NextStrike = now + StrikeInterval;
                _chains.Enqueue(chain);
            }
        }
        private void StrikeArea(Chain chain, Vec3 position)
        {
            _nearby.Clear();
            Mission.GetNearbyAgents(position.AsVec2, StrikeRadius, _nearby);
            foreach (Agent victim in _nearby)
            {
                if (!IsEligible(chain.Caster, victim) || (victim.Position - position).LengthSquared > StrikeRadius * StrikeRadius) continue;
                // 同一道落雷每人一次，不同落点可叠加；复用雷击伤害与魔抗/反射入口。
                LeiJi.StrikeSingleTarget(chain.Caster, victim, chain.Power, false);
            }
            if (chain.ShowVisual) LeiJi.ShowSingleLightning(position + Vec3.Up);
        }

        public override void OnRemoveBehavior()
        { _chains.Clear(); _targets.Clear(); _nearby.Clear(); base.OnRemoveBehavior(); }
    }
}