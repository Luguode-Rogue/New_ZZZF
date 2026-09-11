using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.FeatureTests.NativeMeleeCollisionTest
{
    /// <summary>
    /// Functional probe for the engine's native melee collision pipeline.
    /// Press Q while wielding a melee weapon to request a fixed right-side attack.
    /// The attack is not manually raycast and no Blow is constructed here:
    /// native Agent attack handling performs the weapon sweep/collision and normal damage.
    /// </summary>
    public sealed class NativeMeleeCollisionTestBehavior : MissionBehavior, IPlayerInputEffector
    {
        private bool _pendingTestHit;
        private float _pendingUntil;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

        public Agent.EventControlFlag OnCollectPlayerEventControlFlags()
        {
            Agent agent = Mission?.MainAgent;
            if (agent == null ||
                agent.State != AgentState.Active ||
                !agent.CombatActionsEnabled ||
                !Input.IsKeyPressed(InputKey.Q))
            {
                return Agent.EventControlFlag.None;
            }

            MissionWeapon weapon = agent.WieldedWeapon;
            if (weapon.IsEmpty ||
                weapon.CurrentUsageItem == null ||
                !weapon.CurrentUsageItem.IsMeleeWeapon)
            {
                return Agent.EventControlFlag.None;
            }

            // MissionMainAgentController has already cleared MovementFlags before
            // collecting IPlayerInputEffector flags. Injecting AttackRight here
            // therefore enters the same native melee attack path as normal input.
            agent.MovementFlags |= Agent.MovementControlFlag.AttackRight;

            _pendingTestHit = true;
            _pendingUntil = Mission.CurrentTime + 2f;

            return Agent.EventControlFlag.None;
        }

        public override void OnMeleeHit(
            Agent attacker,
            Agent victim,
            bool isCanceled,
            AttackCollisionData collisionData)
        {
            if (!_pendingTestHit || Mission == null || Mission.CurrentTime > _pendingUntil)
            {
                _pendingTestHit = false;
                return;
            }

            if (attacker != Mission.MainAgent)
                return;

            _pendingTestHit = false;
        }
    }
}
