using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.BattlefieldControl
{
    internal static class BattlefieldControlBootstrap
    {
        private const string Id = "New_ZZZF.BattlefieldControl";
        private static Harmony _harmony;
        internal static bool IsInstalled => _harmony != null;

        internal static void Install()
        {
            if (_harmony != null) return;
            // Detouring SetMovementOrder can initialize MovementOrder's static fields.
            // Their constructors read Mission.Current.CurrentTime; module load is too early.
            if (Mission.Current == null)
                throw new InvalidOperationException("战场控制补丁必须在任务创建后安装。");
            var harmony = new Harmony(Id);
            try
            {
                Patch(harmony, typeof(Formation), nameof(Formation.SetMovementOrder),
                    new[] { typeof(MovementOrder) }, nameof(BeforeMovement));
                Patch(harmony, typeof(Agent), nameof(Agent.SetTargetAgent),
                    new[] { typeof(Agent) }, nameof(BeforeTarget));
                Patch(harmony, typeof(Agent), nameof(Agent.SetAutomaticTargetSelection),
                    new[] { typeof(bool) }, nameof(BeforeAutomaticSelection));
                MethodInfo aiInput = AccessTools.Method(typeof(Agent), "OnAIInputSet", new[]
                {
                    typeof(Agent.EventControlFlag).MakeByRefType(),
                    typeof(Agent.MovementControlFlag).MakeByRefType(),
                    typeof(TaleWorlds.Library.Vec2).MakeByRefType()
                });
                if (aiInput == null) throw new MissingMethodException(typeof(Agent).FullName, "OnAIInputSet");
                // Run after all AgentComponents so they cannot reintroduce weapon input.
                harmony.Patch(aiInput, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(BattlefieldControlBootstrap), nameof(AfterAIInput)))
                    { priority = Priority.Last });
                _harmony = harmony;
            }
            catch
            {
                harmony.UnpatchAll(Id);
                throw;
            }
        }

        internal static void Uninstall()
        {
            BattlefieldControlMissionLogic.Active?.Release();
            _harmony?.UnpatchAll(Id);
            _harmony = null;
        }

        private static void Patch(Harmony harmony, Type type, string method, Type[] args, string prefix)
        {
            MethodInfo original = AccessTools.Method(type, method, args);
            if (original == null) throw new MissingMethodException(type.FullName, method);
            harmony.Patch(original, prefix: new HarmonyMethod(
                AccessTools.Method(typeof(BattlefieldControlBootstrap), prefix)));
        }

        private static void BeforeMovement(Formation __instance)
        {
            // Also catches direct TacticalMap and FormationRelocation orders, which do not
            // emit OrderController.OnOrderIssued. A new movement command replaces the lock.
            BattlefieldControlMissionLogic.Active?.OnMovementChanged(__instance);
        }

        private static void BeforeTarget(Agent __instance, ref Agent agent)
        {
            Formation target = BattlefieldControlMissionLogic.Active?.RestrictedTarget(__instance);
            if (target != null && agent != null &&
                (agent.Formation != target || !agent.IsActive() || !__instance.IsEnemyOf(agent)))
                agent = null;
        }

        private static void BeforeAutomaticSelection(Agent __instance, ref bool enable)
        {
            if (enable && BattlefieldControlMissionLogic.Active?.RestrictedTarget(__instance) != null)
                enable = false;
        }

        private static void AfterAIInput(Agent __instance, ref Agent.EventControlFlag eventFlag)
        {
            if (BattlefieldControlMissionLogic.Active?.OwnsWeaponSelection(__instance) != true) return;
            // Stop native switching BEFORE it is executed, rather than switching back
            // in OnAgentWieldedItemChange. Attack/defend/movement input is untouched.
            eventFlag &= ~(Agent.EventControlFlag.Wield0 | Agent.EventControlFlag.Wield1 |
                Agent.EventControlFlag.Wield2 | Agent.EventControlFlag.Wield3 |
                Agent.EventControlFlag.Sheath0 | Agent.EventControlFlag.Sheath1 |
                Agent.EventControlFlag.ToggleAlternativeWeapon);
        }
    }
}
