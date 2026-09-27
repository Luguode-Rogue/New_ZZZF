using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF
{
    /// <summary>
    /// 管理跟随 Agent 的场景特效。只在主角或视角模式变化时刷新，
    /// 第一人称隐藏主角自己的特效，第三人称恢复；其他 Agent 始终可见。
    /// </summary>
    internal static class AgentAttachedVisualVisibility
    {
        private static readonly Dictionary<GameEntity, Agent> Owners = new Dictionary<GameEntity, Agent>();
        private static readonly List<GameEntity> RemovalScratch = new List<GameEntity>();
        private static Mission _mission;
        private static Agent _lastMainAgent;
        private static bool _lastFirstPerson;

        public static void Register(Agent owner, GameEntity entity)
        {
            if (owner == null || entity == null || owner.Mission == null)
                return;
            if (_mission != owner.Mission)
                Clear();
            _mission = owner.Mission;
            Owners[entity] = owner;
            ApplyVisibility(entity, owner, _mission.MainAgent, _mission.CameraIsFirstPerson);
        }

        public static void Unregister(GameEntity entity)
        {
            if (entity != null)
                Owners.Remove(entity);
        }

        public static void ForgetOwner(Agent owner)
        {
            if (owner == null || Owners.Count == 0)
                return;
            RemovalScratch.Clear();
            foreach (KeyValuePair<GameEntity, Agent> entry in Owners)
                if (entry.Value == owner)
                    RemovalScratch.Add(entry.Key);
            foreach (GameEntity entity in RemovalScratch)
                Owners.Remove(entity);
            RemovalScratch.Clear();
        }

        public static void Tick(Mission mission)
        {
            if (mission == null)
                return;
            if (_mission != mission)
            {
                Clear();
                _mission = mission;
            }

            Agent mainAgent = mission.MainAgent;
            bool firstPerson = mission.CameraIsFirstPerson;
            if (_lastMainAgent == mainAgent && _lastFirstPerson == firstPerson)
                return;

            _lastMainAgent = mainAgent;
            _lastFirstPerson = firstPerson;
            foreach (KeyValuePair<GameEntity, Agent> entry in Owners)
                ApplyVisibility(entry.Key, entry.Value, mainAgent, firstPerson);
        }

        public static void Clear()
        {
            // 任务结束时只丢弃引用，不再访问可能已回收的原生场景实体。
            Owners.Clear();
            RemovalScratch.Clear();
            _mission = null;
            _lastMainAgent = null;
            _lastFirstPerson = false;
        }

        private static void ApplyVisibility(GameEntity entity, Agent owner,
            Agent mainAgent, bool firstPerson)
        {
            bool visible = !firstPerson || owner != mainAgent;
            if (entity.GetVisibilityExcludeParents() != visible)
                entity.SetVisibilityExcludeParents(visible);
        }
    }
}
