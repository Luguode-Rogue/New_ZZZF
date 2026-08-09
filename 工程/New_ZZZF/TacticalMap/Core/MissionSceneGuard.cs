using System;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.TacticalMap.Core
{
    /// <summary>
    /// 场景准入判定：只允许在“有真实地形的野外战斗场景”里启用战术地图。
    ///
    /// 背景：酒馆 / 城镇街道 / 竞技场 / 藏身处等室内或城镇场景，Scene 内部并不存在
    /// TerrainComponent。此时调用 Scene.GetTerrainData / GetTerrainMinMaxHeight 等原生
    /// 互操作方法，引擎会在非托管侧解引用空指针，抛出 System.AccessViolationException。
    ///
    /// 该异常属于 CorruptedStateException，自 .NET 4.0 起默认**不会**被 catch(Exception)
    /// 捕获，因此无法靠 try/catch 兜底，只能提前判定并跳过。
    /// </summary>
    public static class MissionSceneGuard
    {
        /// <summary>当前 Mission 是否适合启用战术地图。</summary>
        public static bool IsTacticalMapSupported(Mission mission)
        {
            if (mission == null || mission.Scene == null) return false;
            if (!IsBattleLikeMission(mission)) return false;
            return IsSceneTerrainReady(mission.Scene);
        }

        /// <summary>
        /// 单独判定某个 Scene 是否具备可用地形。供 TerrainCache 在烘焙前再次自检，
        /// 保证即使绕过 Bootstrap 直接调用也不会崩溃。
        /// </summary>
        public static bool IsSceneTerrainReady(Scene scene)
        {
            if (scene == null) return false;
            return HasTerrain(scene);
        }

        /// <summary>
        /// 按 Mission 模式过滤掉明确不是战斗的场景（对话、酒馆闲逛、编辑器等）。
        /// </summary>
        private static bool IsBattleLikeMission(Mission mission)
        {
            switch (mission.Mode)
            {
                case MissionMode.Conversation:
                case MissionMode.CutScene:
                case MissionMode.Barter:
                case MissionMode.Duel:
                    return false;
            }

            // 城镇/村庄/酒馆等“闲逛”场景通常不含任何 Formation 与战斗逻辑。
            // MissionCombatantsLogic 只在真正的战斗任务里注册。
            if (mission.GetMissionBehavior<MissionCombatantsLogic>() == null) return false;

            return true;
        }

        /// <summary>
        /// 通过场景名 + 地形高度查询双重确认地形是否真实存在。
        /// 用 MethodImplOptions.NoInlining 保证调用点稳定，便于崩溃时定位。
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasTerrain(Scene scene)
        {
            try
            {
                // GetTerrainHeight 在无地形场景中返回 0 且不会访问地形指针，
                // 相比 GetTerrainData 更安全，可作为前置探测。
                bool hasTerrain = scene.GetTerrainMinMaxHeight(out float minH, out float maxH);
                if (!hasTerrain) return false;
                if (float.IsNaN(minH) || float.IsNaN(maxH)) return false;
                // 完全平坦（min==max）几乎可以确定是城镇/室内的占位地形。
                if (Math.Abs(maxH - minH) < 0.001f) return false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
