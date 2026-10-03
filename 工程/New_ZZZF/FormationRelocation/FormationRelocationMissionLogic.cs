using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace New_ZZZF.FormationRelocation
{
    public sealed class FormationRelocationMissionLogic : MissionLogic
    {
        private static FormationRelocationMissionLogic _active;

        internal static FormationRelocationMissionLogic Active => _active;

        public override void OnCreated()
        {
            base.OnCreated();
            _active = this;
        }

        internal bool Owns(OrderController controller)
        {
            return Mission != null && ReferenceEquals(Mission, Mission.Current) &&
                   Mission.Mode == MissionMode.Battle && !GameNetwork.IsSessionActive &&
                   ScreenManager.TopScreen is MissionScreen &&
                   ReferenceEquals(controller, Mission.PlayerTeam?.PlayerOrderController);
        }

        internal bool HasSelectedGroup(OrderController controller)
        {
            if (controller?.SelectedFormations == null) return false;
            int count = 0;
            foreach (Formation formation in controller.SelectedFormations)
            {
                if (formation == null || formation.CountOfUnitsWithoutDetachedOnes <= 0) continue;
                if (++count >= 2) return true;
            }
            return false;
        }

        internal void Issue(OrderController controller, WorldPosition lineStart, WorldPosition lineEnd)
        {
            if (Mission?.Scene == null || !lineStart.IsValid || !lineEnd.IsValid) return;

            Vec2 origin = Vec2.Zero;
            var formations = new List<Formation>();
            foreach (Formation formation in controller.SelectedFormations)
            {
                if (formation == null || formation.CountOfUnitsWithoutDetachedOnes <= 0) continue;
                Vec2 current = formation.CurrentPosition;
                if (!current.IsValid)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "保持阵型移动：选中的编队无法接受整体行军命令，未移动任何编队"));
                    return;
                }
                formations.Add(formation);
                origin += current;
            }
            if (formations.Count < 2) return;

            origin *= 1f / formations.Count;
            Vec2 destination = (lineStart.AsVec2 + lineEnd.AsVec2) * 0.5f;
            var slots = new List<FormationRelocationSlot>(formations.Count);
            foreach (Formation formation in formations)
                slots.Add(new FormationRelocationSlot(formation, formation.CurrentPosition, origin));

            // Prepare the complete order batch before changing any formation.
            // Each formation follows its own native path. A rigid group route
            // would reject detours and narrow passages that native movement supports.
            var targets = new WorldPosition[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                Vec2 point = destination + slots[i].Offset;
                if (!FormationRelocationPlacement.TryTarget(Mission, lineStart, point,
                        slots[i].Formation.Team, out targets[i]))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "保持阵型移动：至少一个编队的目标中心不在可下令区域，未移动任何编队"));
                    return;
                }
            }

            // The intercepted OrderController command does not call BeforeSetOrder.
            // Release the selected formations from AI control, then let the native
            // movement system take them directly to their offset destinations.
            for (int i = 0; i < slots.Count; i++)
            {
                Formation formation = slots[i].Formation;
                if (formation.IsAIControlled)
                    formation.SetControlledByAI(false, false);
                formation.SetMovementOrder(MovementOrder.MovementOrderMove(targets[i]));
            }
            InformationManager.DisplayMessage(new InformationMessage(
                $"保持阵型移动：{slots.Count} 个选中编队前往新位置"));
        }

        protected override void OnEndMission()
        {
            Release();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            Release();
            base.OnRemoveBehavior();
        }

        private void Release()
        {
            if (ReferenceEquals(_active, this)) _active = null;
        }
    }
}
