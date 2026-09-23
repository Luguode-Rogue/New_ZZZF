using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Systems.BattleEquipment
{
    public sealed class BattleEquipmentMissionLogic : MissionLogic
    {
        private Agent _focusedTarget;
        private bool _holdArmed;
        private float _holdTime;

        public override void AfterStart()
        {
            base.AfterStart();
            BattleEquipmentHtmlUi.Instance.OnMissionStarted();
        }

        public override bool IsThereAgentAction(Agent userAgent, Agent otherAgent)
        {
            return IsValidPair(userAgent, otherAgent);
        }

        public override void OnFocusGained(Agent agent, IFocusable focusableObject, bool isInteractable)
        {
            base.OnFocusGained(agent, focusableObject, isInteractable);
            Agent target = focusableObject as Agent;
            _focusedTarget = IsValidPair(agent, target) ? target : null;
            ResetHold();
            BattleEquipmentHtmlUi.Instance.UpdateHint(_focusedTarget, 0f, false);
        }

        public override void OnFocusLost(Agent agent, IFocusable focusableObject)
        {
            base.OnFocusLost(agent, focusableObject);
            if (ReferenceEquals(_focusedTarget, focusableObject))
            {
                _focusedTarget = null;
                ResetHold();
                BattleEquipmentHtmlUi.Instance.UpdateHint(null, 0f, false);
            }
        }

        public override void OnAgentInteraction(Agent userAgent, Agent agent, sbyte agentBoneIndex)
        {
            base.OnAgentInteraction(userAgent, agent, agentBoneIndex);
            if (!BattleEquipmentHtmlUi.Instance.IsOpen && ReferenceEquals(agent, _focusedTarget) && IsValidPair(userAgent, agent))
            {
                _holdArmed = true;
                _holdTime = 0f;
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (BattleEquipmentHtmlUi.Instance.IsOpen) return;
            if (_focusedTarget == null) return;
            if (!_focusedTarget.IsActive() || !IsValidPair(Agent.Main, _focusedTarget))
            {
                _focusedTarget = null;
                ResetHold();
                BattleEquipmentHtmlUi.Instance.UpdateHint(null, 0f, false);
                return;
            }

            if (!_holdArmed) return;

            if (!Mission.InputManager.IsGameKeyDown(13))
            {
                ResetHold();
                BattleEquipmentHtmlUi.Instance.UpdateHint(_focusedTarget, 0f, false);
                return;
            }

            _holdTime += dt;
            float progress = _holdTime / BattleEquipmentHtmlUi.HoldDurationSeconds;
            BattleEquipmentHtmlUi.Instance.UpdateHint(_focusedTarget, progress, true);
            if (_holdTime >= BattleEquipmentHtmlUi.HoldDurationSeconds)
            {
                Agent target = _focusedTarget;
                ResetHold();
                BattleEquipmentHtmlUi.Instance.Open(Agent.Main, target);
            }
        }

        protected override void OnEndMission()
        {
            BattleEquipmentHtmlUi.Instance.OnMissionEnded();
            _focusedTarget = null;
            ResetHold();
            base.OnEndMission();
        }

        private static bool IsValidPair(Agent userAgent, Agent otherAgent)
        {
            if (userAgent == null || otherAgent == null || ReferenceEquals(userAgent, otherAgent)) return false;
            if (!userAgent.IsActive() || !otherAgent.IsActive() || !otherAgent.IsHuman || otherAgent.IsMount) return false;
            if (userAgent.Team == null || otherAgent.Team == null || userAgent.Team != otherAgent.Team) return false;
            MissionMode mode = Mission.Current?.Mode ?? MissionMode.StartUp;
            return mode == MissionMode.Battle || mode == MissionMode.Duel || mode == MissionMode.Stealth;
        }

        private void ResetHold()
        {
            _holdArmed = false;
            _holdTime = 0f;
        }
    }
}
