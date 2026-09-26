using NetworkMessages.FromServer;
using SandBox.Missions.MissionLogics;
using SandBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.ObjectSystem;
using New_ZZZF.Skills;
using static TaleWorlds.MountAndBlade.Agent;
using System.Reflection;
using static TaleWorlds.Core.ItemObject;
using MathF = TaleWorlds.Library.MathF;
using TaleWorlds.MountAndBlade.ViewModelCollection;
using TaleWorlds.MountAndBlade.Launcher.Library;
using Newtonsoft.Json.Linq;

namespace New_ZZZF
{
    public class Script
    {
        private const int EggShellSides = 24;
        private const int EggShellLayers = 12;
        private const float EggShellRadius = 0.82f;
        private const float EggShellHalfHeight = 1.04f;
        private static readonly Dictionary<uint, Mesh> EggShellMeshes = new Dictionary<uint, Mesh>();
        private const int BellShieldLayers = 18;
        private static readonly Dictionary<uint, Mesh> BellShieldMeshes = new Dictionary<uint, Mesh>();

        /// <summary>
        /// 创建包围单位的半透明蛋壳。默认白色、34% 不透明度；Color.Alpha 可调整透明度。
        /// 调用者持有返回的实体，在效果结束时 Remove(0)。
        /// </summary>
        public static GameEntity CreateEggShellVisual(Agent agent, Color? color = null)
        {
            if (agent == null || !agent.IsActive() || agent.Mission?.Scene == null)
                return null;

            Color tint = color ?? new Color(1f, 1f, 1f, 0.34f);
            uint colorKey = tint.ToUnsignedInteger();
            if (!EggShellMeshes.TryGetValue(colorKey, out Mesh mesh) || mesh == null || !mesh.IsValid)
            {
                Material material = Material.GetFromResource("vertex_color_lighting")?.CreateCopy();
                if (material == null || !material.IsValid)
                    return null;
                material.SetAlphaBlendMode(Material.MBAlphaBlendMode.Modulate);

                MeshBuilder builder = new MeshBuilder();
                for (int lat = 0; lat < EggShellLayers; lat++)
                {
                    float top = (float)Math.PI * lat / EggShellLayers;
                    float bottom = (float)Math.PI * (lat + 1) / EggShellLayers;
                    for (int side = 0; side < EggShellSides; side++)
                    {
                        float left = (float)(Math.PI * 2.0 * side / EggShellSides);
                        float right = (float)(Math.PI * 2.0 * (side + 1) / EggShellSides);
                        AddEggShellQuad(builder, EggShellPoint(top, left), EggShellPoint(top, right),
                            EggShellPoint(bottom, right), EggShellPoint(bottom, left), colorKey);
                    }
                }

                mesh = builder.Finalize();
                if (mesh == null || !mesh.IsValid)
                    return null;
                mesh.SetMaterial(material);
                mesh.Color = new Color(tint.Red, tint.Green, tint.Blue).ToUnsignedInteger();
                mesh.CullingMode = MBMeshCullingMode.None;
                mesh.UpdateBoundingBox();
                EggShellMeshes[colorKey] = mesh;
            }

            GameEntity shell = GameEntity.CreateEmptyDynamic(agent.Mission.Scene, false);
            if (shell == null)
                return null;
            shell.AddMesh(mesh);
            shell.SetFactorColor(new Color(tint.Red, tint.Green, tint.Blue).ToUnsignedInteger());
            shell.SetVisibilityExcludeParents(true);
            shell.SetReadyToRender(true);
            UpdateEggShellVisual(shell, agent);
            return shell;
        }

        /// <summary>
        /// 创建钟形半透明护盾：圆顶、窄肩、向下渐宽并在底缘外扩，底部敞开。
        /// 保留蛋壳版本；颜色参数与蛋壳版本相同，默认半透明白色。
        /// 调用者持有返回实体，在效果结束时 Remove(0)。
        /// </summary>
        public static GameEntity CreateBellShieldVisual(Agent agent, Color? color = null)
        {
            if (agent == null || !agent.IsActive() || agent.Mission?.Scene == null)
                return null;

            Color tint = color ?? new Color(1f, 1f, 1f, 0.34f);
            uint colorKey = tint.ToUnsignedInteger();
            if (!BellShieldMeshes.TryGetValue(colorKey, out Mesh mesh) || mesh == null || !mesh.IsValid)
            {
                Material material = Material.GetFromResource("vertex_color_lighting")?.CreateCopy();
                if (material == null || !material.IsValid)
                    return null;
                material.SetAlphaBlendMode(Material.MBAlphaBlendMode.Modulate);

                MeshBuilder builder = new MeshBuilder();
                for (int layer = 0; layer < BellShieldLayers; layer++)
                {
                    float top = (float)layer / BellShieldLayers;
                    float bottom = (float)(layer + 1) / BellShieldLayers;
                    for (int side = 0; side < EggShellSides; side++)
                    {
                        float left = (float)(Math.PI * 2.0 * side / EggShellSides);
                        float right = (float)(Math.PI * 2.0 * (side + 1) / EggShellSides);
                        AddEggShellQuad(builder, BellShieldPoint(top, left), BellShieldPoint(top, right),
                            BellShieldPoint(bottom, right), BellShieldPoint(bottom, left), colorKey);
                    }
                }

                mesh = builder.Finalize();
                if (mesh == null || !mesh.IsValid)
                    return null;
                mesh.SetMaterial(material);
                mesh.Color = new Color(tint.Red, tint.Green, tint.Blue).ToUnsignedInteger();
                mesh.CullingMode = MBMeshCullingMode.None;
                mesh.UpdateBoundingBox();
                BellShieldMeshes[colorKey] = mesh;
            }

            GameEntity bell = GameEntity.CreateEmptyDynamic(agent.Mission.Scene, false);
            if (bell == null)
                return null;
            bell.AddMesh(mesh);
            bell.SetFactorColor(new Color(tint.Red, tint.Green, tint.Blue).ToUnsignedInteger());
            bell.SetVisibilityExcludeParents(true);
            bell.SetReadyToRender(true);
            UpdateEggShellVisual(bell, agent);
            return bell;
        }

        /// <summary>更新蛋壳位置；持续效果每帧调用即可跟随步行或骑乘单位。</summary>
        public static void UpdateEggShellVisual(GameEntity shell, Agent agent)
        {
            if (shell == null || agent == null || !agent.IsActive())
                return;
            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = agent.GetEyeGlobalPosition() - new Vec3(0f, 0f, 0.72f);
            shell.SetGlobalFrame(frame);
        }

        private static Vec3 EggShellPoint(float latitude, float longitude)
        {
            float sin = (float)Math.Sin(latitude);
            float cos = (float)Math.Cos(latitude);
            float width = EggShellRadius * sin * (1f - 0.14f * cos);
            return new Vec3(width * (float)Math.Cos(longitude),
                width * (float)Math.Sin(longitude), EggShellHalfHeight * cos);
        }

        private static Vec3 BellShieldPoint(float heightFraction, float longitude)
        {
            float radius;
            if (heightFraction <= 0.2f)
            {
                // 钟顶圆拱，至肩部逐渐转为近乎竖直的钟身。
                radius = 0.55f * (float)Math.Sin(heightFraction / 0.2f * Math.PI * 0.5);
            }
            else if (heightFraction <= 0.75f)
            {
                radius = 0.55f + 0.10f * (heightFraction - 0.2f) / 0.55f;
            }
            else if (heightFraction <= 0.94f)
            {
                float spread = (heightFraction - 0.75f) / 0.19f;
                radius = 0.65f + 0.18f * spread * spread;
            }
            else
            {
                // 外翻的钟口，不用不透明描边。
                radius = 0.83f + 0.12f * (heightFraction - 0.94f) / 0.06f;
            }

            return new Vec3(radius * (float)Math.Cos(longitude),
                radius * (float)Math.Sin(longitude),
                EggShellHalfHeight * (1f - 2f * heightFraction));
        }

        private static void AddEggShellQuad(MeshBuilder builder, Vec3 a, Vec3 b, Vec3 c,
            Vec3 d, uint color)
        {
            Vec3 normal = (a + b + c + d) * 0.25f;
            normal = new Vec3(normal.X, normal.Y, normal.Z / EggShellHalfHeight);
            normal.Normalize();
            int first = builder.AddFaceCorner(a, normal, new Vec2(0f, 0f), color);
            int second = builder.AddFaceCorner(b, normal, new Vec2(1f, 0f), color);
            int third = builder.AddFaceCorner(c, normal, new Vec2(1f, 1f), color);
            int fourth = builder.AddFaceCorner(d, normal, new Vec2(0f, 1f), color);
            builder.AddFace(first, second, third);
            builder.AddFace(first, third, fourth);
        }

        public static bool FindTarAgents(Agent castAgent, int selectRannge, out List<Agent> target, Vec3 agentLookPos = default)
        {
            ///通用搜寻施法目标的方法
            ///对于玩家，没有按缩放键时，自动选择视线落点附近的目标，并且选择周围单位最多的一个目标。按下缩放时，获取视线落点处的目标。
            ///对于英雄，只有自动施法，且获得视线落点附近周围单位最多的目标。必要时调整为全屏获取
            ///对于非英雄，只有自动施法，只获得视线落点范围的单位。必要时调整为全屏获取
            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            target = new List<Agent>();
            if (castAgent.IsHero)
            {
                if (Agent.Main != null && castAgent.IsMainAgent && missionScreen != null && missionScreen.SceneLayer.Input.IsGameKeyDown(24))
                {
                    Vec3 lookP = Script.CameraLookPos();
                    Script.AgentListIFF(Agent.Main, Script.FindAgentsWithinSpellRange(lookP, selectRannge), out var friendAgent, out var foeAgent);
                    target = foeAgent;
                    return true;
                }
                else
                {
                    // 5. 分离友方和敌方代理（AgentListIFF）
                    Script.AgentListIFF(
                        Agent.Main,
                        Mission.Current.Agents,
                        out var friendAgent,
                        out var foeAgent
                    );
                    Vec3 targetPosition = Vec3.Invalid;

                    // 1. 获取当前角色的视线位置（AgentLookPos）
                    if (agentLookPos != null && agentLookPos != default)
                    {
                        agentLookPos = AgentLookPos(castAgent);
                    }
                    if (AgentLookPos == null) return false;
                    // 2. 计算最优冲突位置（FindOptimalConflictPos）
                    var optimalConflictPos = FindOptimalConflictPos(
                        castAgent,
                        agentLookPos,
                        selectRannge * 10
                    );
                    if (optimalConflictPos == null && FindClosestAgentToCaster(castAgent, foeAgent).Index != castAgent.Index)
                    {
                        targetPosition = FindClosestAgentToCaster(castAgent, foeAgent).Position;
                    }
                    else if (optimalConflictPos != null)
                    {
                        // 3. 提取冲突位置的坐标（Position）
                        targetPosition = optimalConflictPos.Position;
                    }
                    else
                    { return false; }

                    // 4. 获取指定范围内的所有代理（FindAgentsWithinSpellRange）
                    var agentsInRadius = Script.FindAgentsWithinSpellRange(
                        targetPosition,
                        selectRannge
                    );
                    // 5. 分离友方和敌方代理（AgentListIFF）
                    Script.AgentListIFF(
                        Agent.Main,
                        agentsInRadius,
                        out friendAgent,
                        out foeAgent
                    );

                    target = foeAgent;
                    return true;

                }
            }
            else
            {

                // 1. 获取当前角色的视线位置（AgentLookPos）
                if (agentLookPos != null && agentLookPos != default|| (agentLookPos.x == 0 && agentLookPos.y == 0 && agentLookPos.z == 0 ))
                {
                    agentLookPos = AgentLookPos(castAgent);
                    if (castAgent.GetTargetAgent()!=null)
                    {
                        agentLookPos = castAgent.GetTargetAgent().Position;
                    }
                }



                // 4. 获取指定范围内的所有代理（FindAgentsWithinSpellRange）
                var agentsInRadius = Script.FindAgentsWithinSpellRange(
                    agentLookPos,
                    selectRannge
                );

                // 5. 分离友方和敌方代理（AgentListIFF）
                Script.AgentListIFF(
                    Agent.Main,
                    agentsInRadius,
                    out var friendAgent,
                    out var foeAgent
                );
                target = foeAgent;
                return true;

            }



            return false;

        }


        /// <summary>
        /// onMissionTick里调用的，按下缩放键后的区域显示
        /// </summary>
        private static Dictionary<Agent, uint?> _contourCache = new Dictionary<Agent, uint?>(); // 新增缓存字典
        private static readonly MBList<Agent> _previewNearbyAgents = new MBList<Agent>();
        private static readonly HashSet<Agent> _previewAgentsInArea = new HashSet<Agent>();
        private static readonly List<Agent> _previewAgentsToUnmark = new List<Agent>();
        private static readonly uint _previewEnemyColor = new Color(1f, 0f, 0f, 1f).ToUnsignedInteger();
        private static float _nextAreaContourRefreshTime;
        private static bool _previewMarkersShown;
        private const float AreaContourRefreshInterval = 0.1f;

        /// <summary>Shift 指示只取当前法术的尺寸；不在每帧执行技能的选敌逻辑。</summary>
        private static bool TryGetSelectedDamageArea(Agent caster, out SkillDamageArea area)
        {
            area = default;
            AgentSkillComponent component = caster?.GetComponent<AgentSkillComponent>();
            if (component == null || component.SelectedSpellSlot < 0 ||
                component.SelectedSpellSlot >= component.SpellSlots.Length)
                return false;
            SkillBase skill = component.SpellSlots[component.SelectedSpellSlot];
            return skill != null && skill.TryGetDamageArea(caster, 0, out area) &&
                area.IsValid && area.Shape != SkillDamageAreaShape.SampledCone;
        }

        private static void ClearPreviewContours()
        {
            _nextAreaContourRefreshTime = 0f;
            _previewNearbyAgents.Clear();
            _previewAgentsInArea.Clear();
            _previewAgentsToUnmark.Clear();
            if (_contourCache.Count == 0)
                return;
            if (Mission.Current == null)
            {
                _contourCache.Clear();
                return;
            }
            // 只访问任务中仍然活跃的 Visuals；缓存里可能留有已被原生层回收的对象。
            foreach (Agent currentAgent in Mission.Current.Agents)
            {
                if (currentAgent == null || !currentAgent.IsActive() ||
                    !_contourCache.ContainsKey(currentAgent))
                    continue;
                MBAgentVisuals visuals = currentAgent.AgentVisuals;
                if (visuals != null)
                    visuals.SetContourColor(null, true);
            }
            _contourCache.Clear();
        }

        private static Vec3 GetPreviewCapsuleDirection(Vec3 point, Agent caster)
        {
            Vec3 forward = point - caster.Position;
            forward.z = 0f;
            if (forward.LengthSquared < 0.001f)
                forward = caster.LookDirection;
            forward.z = 0f;
            if (forward.LengthSquared < 0.001f)
                forward = Vec3.Forward;
            forward.Normalize();
            return new Vec3(-forward.y, forward.x, 0f);
        }

        /// <summary>每 0.1 秒只查询指示点附近的敌人；轮廓跟随真实伤害形状。</summary>
        private static void RefreshDamageAreaContours(Vec3 point, Agent caster, SkillDamageArea area)
        {
            Mission mission = Mission.Current;
            if (mission == null || mission.CurrentTime < _nextAreaContourRefreshTime)
                return;
            _nextAreaContourRefreshTime = mission.CurrentTime + AreaContourRefreshInterval;

            _previewNearbyAgents.Clear();
            _previewAgentsInArea.Clear();
            float queryRadius = area.Shape == SkillDamageAreaShape.Capsule
                ? area.Length * 0.5f + area.Radius : area.Radius;
            if (caster.Team != null)
                mission.GetNearbyEnemyAgents(point.AsVec2, queryRadius, caster.Team, _previewNearbyAgents);
            else
                mission.GetNearbyAgents(point.AsVec2, queryRadius, _previewNearbyAgents);

            Vec3 along = area.Shape == SkillDamageAreaShape.Capsule
                ? GetPreviewCapsuleDirection(point, caster) : Vec3.Zero;
            foreach (Agent target in _previewNearbyAgents)
            {
                if (target == null || !target.IsActive() || !target.IsHuman ||
                    !caster.IsEnemyOf(target))
                    continue;
                Vec3 targetPoint = target.Position + Vec3.Up;
                bool inside;
                if (area.Shape == SkillDamageAreaShape.Capsule)
                {
                    if (MathF.Abs(targetPoint.z - point.z) > area.HeightTolerance)
                        continue;
                    Vec2 start = point.AsVec2 - along.AsVec2 * (area.Length * 0.5f);
                    Vec2 segment = along.AsVec2 * area.Length;
                    Vec2 offset = targetPoint.AsVec2 - start;
                    float t = MathF.Clamp(Vec2.DotProduct(offset, segment) /
                        (area.Length * area.Length), 0f, 1f);
                    inside = (targetPoint.AsVec2 - (start + segment * t)).LengthSquared <=
                        area.Radius * area.Radius;
                }
                else
                {
                    inside = (targetPoint - point).LengthSquared <= area.Radius * area.Radius;
                }
                if (inside)
                    _previewAgentsInArea.Add(target);
            }

            // 只对当前 Mission.Agents 中仍活跃的实体写原生轮廓，规避移除后的 Visuals 指针。
            _previewAgentsToUnmark.Clear();
            if (_contourCache.Count > 0)
            {
                foreach (Agent currentAgent in mission.Agents)
                {
                    if (currentAgent == null || !currentAgent.IsActive() ||
                        !_contourCache.ContainsKey(currentAgent) ||
                        _previewAgentsInArea.Contains(currentAgent))
                        continue;
                    MBAgentVisuals visuals = currentAgent.AgentVisuals;
                    if (visuals != null)
                        visuals.SetContourColor(null, true);
                    _previewAgentsToUnmark.Add(currentAgent);
                }
                foreach (Agent agent in _previewAgentsToUnmark)
                    _contourCache.Remove(agent);
            }
            foreach (Agent agent in _previewAgentsInArea)
            {
                if (_contourCache.TryGetValue(agent, out uint? oldColor) &&
                    oldColor == _previewEnemyColor)
                    continue;
                MBAgentVisuals visuals = agent.AgentVisuals;
                if (visuals == null)
                    continue;
                visuals.SetContourColor(_previewEnemyColor, true);
                _contourCache[agent] = _previewEnemyColor;
            }
        }

        private static void PositionDamageAreaMarkers(Vec3 point, Agent caster, SkillDamageArea area)
        {
            // 与火墙结算一致：中轴线垂直于施法者至落点的水平朝向。
            Vec3 along = GetPreviewCapsuleDirection(point, caster);
            Vec3 across = new Vec3(-along.y, along.x, 0f);
            int count = SkillSystemBehavior.WoW_Ring.Count;
            foreach (var item in SkillSystemBehavior.WoW_Ring)
            {
                int index = (int)item.Key;
                Vec3 markerPosition;
                if (area.Shape == SkillDamageAreaShape.Capsule)
                {
                    // 16 个模型分别铺在两端圆弧和两侧直边，不把模型全挤在端点。
                    float halfLength = area.Length * 0.5f;
                    if (index < 4 || (index >= 8 && index < 12))
                    {
                        bool atStart = index < 4;
                        float t = (index % 4) / 3f;
                        float angle = atStart
                            ? (float)(System.Math.PI * (0.5 + t))
                            : (float)(System.Math.PI * (-0.5 + t));
                        Vec3 capCenter = point + along * (atStart ? -halfLength : halfLength);
                        markerPosition = capCenter + along * ((float)System.Math.Cos(angle) * area.Radius) +
                                         across * ((float)System.Math.Sin(angle) * area.Radius);
                    }
                    else
                    {
                        bool lowerEdge = index < 8;
                        float t = (index % 4 + 1) / 5f;
                        markerPosition = point + along * ((lowerEdge ? -1f + 2f * t : 1f - 2f * t) * halfLength) +
                                         across * (lowerEdge ? -area.Radius : area.Radius);
                    }
                }
                else
                {
                    float angle = (float)(2.0 * System.Math.PI * index / count);
                    markerPosition = point + new Vec3(
                        (float)System.Math.Cos(angle) * area.Radius,
                        (float)System.Math.Sin(angle) * area.Radius, 0f);
                }
                MatrixFrame frame = item.Value.GetFrame();
                frame.origin = markerPosition;
                frame.rotation = caster.LookRotation;
                frame.rotation.u = Vec3.Up;
                item.Value.SetFrame(ref frame);
            }
        }

        public static void UpdateProjectileTargets()
        {
            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            if (missionScreen != null && missionScreen.SceneLayer.Input.IsGameKeyDown(24) &&
                Agent.Main != null && Mission.Current?.Scene != null)
            {
                if (SkillSystemBehavior.WoW_Ring.Count == 0)
                {
                    for (global::System.Int32 i = 0; i < 16; i++)
                    {
                        GameEntity gameEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                        gameEntity.AddMesh(Mesh.GetFromResource("ballista_projectile_flying"));
                        //gameEntity.SetContourColor(new uint?(4294901760U), true);
                        SkillSystemBehavior.WoW_Ring.Add(i, gameEntity);
                    }
                }
                Vec3 lookP = Script.CameraLookPos();
                if (!lookP.IsValid)
                {
                    if (_previewMarkersShown)
                    {
                        foreach (var item in SkillSystemBehavior.WoW_Ring)
                        {
                            MatrixFrame hidden = MatrixFrame.Identity;
                            item.Value.SetFrame(ref hidden);
                        }
                        _previewMarkersShown = false;
                    }
                    ClearPreviewContours();
                    return;
                }
                if (TryGetSelectedDamageArea(Agent.Main, out SkillDamageArea damageArea))
                {
                    PositionDamageAreaMarkers(lookP, Agent.Main, damageArea);
                    _previewMarkersShown = true;
                    RefreshDamageAreaContours(lookP, Agent.Main, damageArea);
                }
                else
                {
                    Script.AgentListIFF(Agent.Main, Mission.Current.Agents, out var friendAgent, out var foeAgent);
                    foreach (var item in SkillSystemBehavior.WoW_Ring)
                    {
                        MatrixFrame matrixFrame = item.Value.GetFrame();
                        matrixFrame.origin = lookP;
                        Vec3 ro = Agent.Main.LookDirection;
                        ro.RotateAboutZ(22.5f * item.Key * 3.1415f / 180);
                        ro.RotateAboutZ(10f * 3.1415f / 180);
                        matrixFrame.origin += Script.MultiplyVectorByScalar(ro, 5);
                        matrixFrame.rotation = Agent.Main.LookRotation;
                        matrixFrame.rotation.u = Vec3.Up;
                        item.Value.SetFrame(ref matrixFrame);
                    }
                    _previewMarkersShown = true;

                    // 轮廓判定与16个指示器模型无关，每帧只执行一次。
                    foreach (var foe in foeAgent)
                    {
                        if (foe != null && foe.IsActive())
                        {
                            MBAgentVisuals visuals = foe.AgentVisuals;
                            if (visuals == null)
                                continue;
                            float distanceSq = lookP.DistanceSquared(foe.GetEyeGlobalPosition());
                            uint? targetColor = distanceSq <= 25 ?
                                new Color(1f, 0f, 0f, 1f).ToUnsignedInteger() :
                                null;

                            if (_contourCache.TryGetValue(foe, out var currentColor) &&
                                currentColor == targetColor)
                                continue;

                            visuals.SetContourColor(targetColor, true);
                            _contourCache[foe] = targetColor;
                        }
                    }
                }
            }
            else if (_previewMarkersShown || _contourCache.Count > 0)
            {
                if (_previewMarkersShown)
                {
                    foreach (var item in SkillSystemBehavior.WoW_Ring)
                    {
                        MatrixFrame matrixFrame = MatrixFrame.Identity;
                        item.Value.SetFrame(ref matrixFrame);
                    }
                    _previewMarkersShown = false;
                }
                ClearPreviewContours();
            }
        }

        /// <summary>任务切换时只丢弃托管缓存，不访问可能已失效的原生 AgentVisuals。</summary>
        public static void ClearProjectileTargetVisualCache()
        {
            _contourCache.Clear();
            _previewNearbyAgents.Clear();
            _previewAgentsInArea.Clear();
            _previewAgentsToUnmark.Clear();
            _nextAreaContourRefreshTime = 0f;
            _previewMarkersShown = false;
        }

        /// <summary>Agent 进入移除回调后只注销引用，禁止再访问其 AgentVisuals。</summary>
        public static void ForgetProjectileTarget(Agent agent)
        {
            if (agent != null)
            {
                _contourCache.Remove(agent);
                _previewAgentsInArea.Remove(agent);
            }
        }
        /// <summary>
        /// 玩家报错信息
        /// </summary>
        /// <param name="s"></param>
        public static void SysOut(string s, Agent agent)
        {
            if (Mission.Current != null)
                if (Mission.Current.MainAgent == agent)
                    InformationManager.DisplayMessage(new InformationMessage(s));
        }
        /// <summary>
        /// 0无有效武器
        /// 1无弹道速度记录，需要进行一次射击
        /// 2该角色无装备
        /// 3"无有效目标"
        /// </summary>
        /// <param name="i"></param>
        /// <param name="agent"></param>
        public static void SysOut(int i, Agent agent)
        {
            if (Mission.Current != null)
                if (Mission.Current.MainAgent == agent)
                {
                    switch (i)
                    {
                        case 0:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        case 1:
                            InformationManager.DisplayMessage(new InformationMessage("无弹道速度记录，需要进行一次射击"));
                            break;
                        case 2:
                            InformationManager.DisplayMessage(new InformationMessage("该角色无装备"));
                            break;
                        case 3:
                            InformationManager.DisplayMessage(new InformationMessage("无有效目标"));
                            break;
                        case 4:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        case 5:
                            InformationManager.DisplayMessage(new InformationMessage("无有效武器"));
                            break;
                        default: break;
                    }
                }
        }
        /// <summary>
        /// 单位1能否看到单位2的位置
        /// </summary>
        /// <param name="mainAgent"></param>
        /// <param name="otherAgent"></param>
        /// <returns></returns>
        public static bool CanSeeAgent(Agent mainAgent, Agent otherAgent)
        {
            if ((mainAgent.Position - otherAgent.Position).Length < 500f)
            {
                Vec3 eyeGlobalPosition = otherAgent.GetEyeGlobalPosition();
                Vec3 eyeGlobalPosition2 = mainAgent.GetEyeGlobalPosition();
                if (TaleWorlds.Library.MathF.Abs(Vec3.AngleBetweenTwoVectors(otherAgent.Position - mainAgent.Position, mainAgent.LookDirection)) < 1.5f)
                {
                    float num;
                    return !Mission.Current.Scene.RayCastForClosestEntityOrTerrain(eyeGlobalPosition2, eyeGlobalPosition, out num, 0.01f, BodyFlags.CommonFocusRayCastExcludeFlags);
                }
            }
            return false;
        }
        /// <summary>
        /// 用于灵马哨笛
        /// 生成一个带有初始位置配置的游荡AI代理（NPC）
        /// </summary>
        /// <param name="locationCharacter">关联的场景角色数据</param>
        /// <param name="spawnPointFrame">生成点的空间坐标系（包含位置和朝向）</param>
        /// <param name="noHorses">是否禁止生成马匹（默认true）</param>
        /// <returns>生成的AI代理对象</returns>
        private static Agent SpawnWanderingAgentWithInitialFrame(LocationCharacter locationCharacter, MatrixFrame spawnPointFrame, bool noHorses = true)
        {
            // 1. 确定队伍归属
            Team team = Team.Invalid;
            switch (locationCharacter.CharacterRelation)
            {
                case LocationCharacter.CharacterRelations.Neutral:  // 中立角色保持无效队伍
                    team = Team.Invalid;
                    break;
                case LocationCharacter.CharacterRelations.Friendly: // 友方加入玩家盟友队伍
                    team = Mission.Current.PlayerAllyTeam;
                    break;
                case LocationCharacter.CharacterRelations.Enemy:    // 敌方加入玩家敌对队伍
                    team = Mission.Current.PlayerEnemyTeam;
                    break;
            }

            // 2. 调整生成点Z轴高度（确保生成在地面）
            spawnPointFrame.origin.z = Mission.Current.Scene.GetGroundHeightAtPosition(
                spawnPointFrame.origin,
                BodyFlags.CommonCollisionExcludeFlags
            );

            // 3. 获取定居点颜色配置（用于角色服装）
            ValueTuple<uint, uint> agentSettlementColors = MissionAgentHandler.GetAgentSettlementColors(locationCharacter);

            // 4. 构建代理基础数据
            AgentBuildData agentBuildData = locationCharacter.GetAgentBuildData()
                .Team(team)                         // 设置队伍
                .InitialPosition(spawnPointFrame.origin); // 设置初始位置

            // 5. 标准化朝向向量
            Vec2 vec = spawnPointFrame.rotation.f.AsVec2;
            vec = vec.Normalized();

            // 6. 扩展代理配置
            AgentBuildData agentBuildData2 = agentBuildData
                .InitialDirection(vec)                      // 设置初始朝向
                .ClothingColor1(agentSettlementColors.Item1) // 主服装颜色
                .ClothingColor2(agentSettlementColors.Item2) // 副服装颜色
                .CivilianEquipment(locationCharacter.UseCivilianEquipment) // 是否使用平民装备
                .NoHorses(noHorses);                        // 是否禁用马匹

            // 7. 获取氏族旗帜数据
            CharacterObject character = locationCharacter.Character;
            Banner banner = null;
            if (character?.HeroObject?.Clan != null) // 仅当角色有氏族关联时
            {
                banner = character.HeroObject.Clan.Banner; // 获取氏族旗帜
            }

            // 8. 最终代理配置
            AgentBuildData agentBuildData3 = agentBuildData2.Banner(banner); // 设置旗帜

            // 9. 生成代理实体
            Agent agent = Mission.Current.SpawnAgent(agentBuildData3, false);

            // 10. 配置动画系统（动作集合/步长）
            AnimationSystemData animationSystemData = agentBuildData3.AgentMonster.FillAnimationSystemData(
                MBGlobals.GetActionSet(locationCharacter.ActionSetCode), // 获取动作资源
                locationCharacter.Character.GetStepSize(),              // 设置移动步长
                false
            );
            agent.SetActionSet(ref animationSystemData); // 应用动作配置

            // 11. 添加战役代理组件
            agent.GetComponent<CampaignAgentComponent>().CreateAgentNavigator(locationCharacter);

            // 12. 绑定场景角色行为树
            locationCharacter.AddBehaviors(agent);

            return agent;
        }

        /// <summary>
        /// 参数1：基于的agent
        /// 参数1：目标Pos
        /// 参数3：周围spellRange米
        /// 参数4：返回友军list或敌军list（默认敌军）
        /// 获取目标范围内敌人的list
        /// 无有效目标时，返回null
        /// </summary>
        public static List<Agent> GetTargetedInRange(Agent CasterAgent, Vec3 CasterPos, int spellRange, bool FriendList = false)
        {
            List<Agent> list = FindAgentsWithinSpellRange(CasterPos, spellRange);
            List<Agent> FriendAgent = null;
            List<Agent> FoeAgent = null;
            Script.AgentListIFF(CasterAgent, list, out FriendAgent, out FoeAgent);
            if (FriendList)
            { return FriendAgent; }
            else
            { return FoeAgent; }
            return null;
        }

        /// <summary>
        /// 参数1：基于的agent
        /// 参数2：需要判定的list
        /// 判定list里，距离目标agent最近的一个agent单位(非自身，非坐骑）
        /// 无有效目标时，返回基于的agent
        /// </summary>
        public static Agent FindClosestAgentToCaster(Agent CasterAgent, List<Agent> agentList)
        {
            Agent OutAgent = CasterAgent;
            float Range = 9999f;
            foreach (Agent agent in agentList)
            {
                if (CasterAgent.Index == agent.Index) continue;
                if (!agent.IsHuman) continue;
                Vec2 v2 = CasterAgent.GetCurrentVelocity() - agent.GetCurrentVelocity();
                if (Range > v2.Length)
                {
                    Range = v2.Length;
                    OutAgent = agent;
                }
            }
            return OutAgent;
        }        /// <summary>
                 /// 参数1：基于的pos
                 /// 参数2：需要判定的list
                 /// 判定list里，距离目标agent最近的一个agent单位
                 /// 无有效目标时，返回null
                 /// </summary>
        public static Agent FindClosestAgentToPos(Vec3 vec3, List<Agent> agentList)
        {
            Agent OutAgent = null;
            float Range = 9999f;
            foreach (Agent agent in agentList)
            {
                Vec2 v2 = vec3.AsVec2 - agent.GetCurrentVelocity();
                if (Range > v2.Length)
                {
                    Range = v2.Length;
                    OutAgent = agent;
                }
            }
            return OutAgent;
        }
        /// <summary>
        ///参数1:目标地点vec3
        ///参数2:施法生效范围
        ///获取目标范围内所有的agent,存放在列表里.敌我判定只有拿列表里的agent再去判定,不在这里判定
        /// </summary>
        public static List<Agent> FindAgentsWithinSpellRange(Vec3 targetLocation, int spellRange)
        {
            return FindAgentsWithinSpellRange(targetLocation, (float)spellRange);
        }

        /// <summary>允许伤害范围按属性得到非整数半径；与旧 int 入口使用同一三维距离判定。</summary>
        public static List<Agent> FindAgentsWithinSpellRange(Vec3 targetLocation, float spellRange)
        {
            List<Agent> agentsWithinRange = new List<Agent>();

            foreach (Agent agent in Mission.Current.Agents)
            {
                if (agent.IsActive())
                {
                    float distanceToTarget = targetLocation.Distance(agent.GetEyeGlobalPosition());
                    if (distanceToTarget <= spellRange)
                    {
                        agentsWithinRange.Add(agent);
                    }
                }
            }
            return agentsWithinRange;
        }
        /// <summary>
        ///参数1:目标地点vec3
        ///参数2:弧度
        ///参数3:距离
        ///获取目标面前锥形区域内所有agent,存放在列表里.敌我判定只有拿列表里的agent再去判定,不在这里判定
        /// </summary>
        /// <param name="castAgent"></param>
        /// <param name="spellRange"></param>
        /// <returns></returns>
        public static List<Agent> FindAgentsInFrontArc(Agent castAgent, int frontArc, int spellRange)
        {
            List<Agent> list = new List<Agent>();
            Vec3 vec3 = new Vec3();
            for (int i = -frontArc; i <= frontArc; i++)
            {
                for (global::System.Int32 j = 0; j <= spellRange; j++)
                {
                    vec3 = castAgent.Position + Script.MultiplyVectorByScalar(castAgent.LookDirection, j);
                    vec3.RotateAboutZ(i * 30 * (float)Math.PI / 180f);
                    list.AddRange(Script.FindAgentsWithinSpellRange(vec3, 3));
                }
            }
            list = list.Distinct<Agent>().ToList();
            return list;

        }
        /// <summary>
        ///敌我识别脚本,不获取坐骑
        ///参数1：基于某agent进行敌我识别
        ///参数2：需要敌我识别的list
        ///参数3：输出友方list
        ///参数4：输出敌方list
        /// </summary>
        public static void AgentListIFF(Agent agent, List<Agent> InputList, out List<Agent> FriendAgent, out List<Agent> FoeAgent)
        {
           
            FriendAgent = new List<Agent>();
            FoeAgent = new List<Agent>();
            if (agent == null) { return; }
            for (int i = 0; i < InputList.Count; i++)
            {
                AgentSkillComponent agentSkill = Script.GetActiveComponents(InputList[i]);
                if (agentSkill != null && agentSkill.StateContainer.HasState("BKBBuff"))
                {
                    continue;
                }
                if (InputList[i].IsFriendOf(agent) && InputList[i].IsHuman && !InputList[i].IsEnemyOf(agent))
                {
                    FriendAgent.Add(InputList[i]);
                }
                else if (!InputList[i].IsFriendOf(agent) && InputList[i].IsHuman && InputList[i].IsEnemyOf(agent))
                {
                    FoeAgent.Add(InputList[i]);
                }
            }
        }
        /// <summary>
        /// 创建一个新的Vec3向量，其每个分量都是原始向量分量与标量的乘积
        /// </summary>>
        public static Vec3 MultiplyVectorByScalar(Vec3 vector, float scalar)
        {
            // 创建一个新的Vec3向量，其每个分量都是原始向量分量与标量的乘积
            Vec3 result = new Vec3(vector.x * scalar, vector.y * scalar, vector.z * scalar);
            return result;
        }
        /// <summary>
        /// //根据相机视野，获取准星附近的一个非友军agent
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static Agent FindTargetedLockableAgent(Agent player)
        {
            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = missionScreen.CombatCamera.Direction;
            Vec3 vec = direction;
            Vec3 position = missionScreen.CombatCamera.Position;
            Vec3 visualPosition = player.VisualPosition;
            float num = new Vec3(position.x, position.y, 0f, -1f).Distance(new Vec3(visualPosition.x, visualPosition.y, 0f, -1f));
            Vec3 v = position * (1f - num) + (position + direction) * num;
            float num2 = 0f;
            Agent agent = null;
            foreach (Agent agent2 in Mission.Current.Agents)
            {
                if ((agent2.IsMount && agent2.RiderAgent != null && !agent2.RiderAgent.IsFriendOf(player) && agent2.RiderAgent.IsEnemyOf(player)) || (!agent2.IsMount && !agent2.IsFriendOf(player) && agent2.IsEnemyOf(player)))
                {
                    Vec3 vec2 = agent2.GetChestGlobalPosition() - v;
                    float num3 = vec2.Normalize();
                    if (num3 < 100f)//这个应该是锁定的距离
                    {
                        float num4 = Vec2.DotProduct(vec.AsVec2.Normalized(), vec2.AsVec2.Normalized());
                        float num5 = Vec2.DotProduct(new Vec2(vec.AsVec2.Length, vec.z), new Vec2(vec2.AsVec2.Length, vec2.z));
                        if (num4 > 0.95f && num5 > 0.95f)//这个数也可以放宽点，0.95降低
                        {
                            float num6 = num4 * num4 * num4 / TaleWorlds.Library.MathF.Pow(num3, 0.15f);
                            if (num6 > num2)
                            {
                                num2 = num6;
                                agent = agent2;
                            }
                        }
                    }
                }
            }
            if (agent != null && agent.IsMount && agent.RiderAgent != null)
            {
                return agent.RiderAgent;
            }
            return agent;
        }
        /// <summary>
        /// 获取目视地点
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public static Vec3 AgentLookPos(Agent agent) //获取目视地点,效果凑合了
        {
            Vec3 vec3 = Vec3.Invalid;
            float f = 0f;
            Mission.Current.Scene.RayCastForClosestEntityOrTerrain(agent.GetEyeGlobalPosition(), agent.GetEyeGlobalPosition() + MultiplyVectorByScalar(agent.LookDirection, 5000f), out f, out vec3);

            return vec3;
        }
        /// <summary>
        /// 获取相机目视地点
        /// </summary>
        /// <returns></returns>
        public static Vec3 CameraLookPos()
        {

            Vec3 vec3 = Vec3.Invalid;
            float f = 0f;
            MissionScreen missionScreen = ScreenManager.TopScreen as MissionScreen;
            Vec3 direction = missionScreen.CombatCamera.Direction;
            Vec3 position = missionScreen.CombatCamera.Position;
            Mission.Current.Scene.RayCastForClosestEntityOrTerrain(position, position + MultiplyVectorByScalar(direction, 5000f), out f, out vec3);

            return vec3;
        }
        public static bool IsRangeWeapon(ItemObject item)
        {
            if (item == null) { return false; }
            return !(item.Type == ItemTypeEnum.Horse || item.Type == ItemTypeEnum.Polearm || item.Type == ItemTypeEnum.Shield || item.Type == ItemTypeEnum.OneHandedWeapon || item.Type == ItemTypeEnum.TwoHandedWeapon);
        }
        /// <summary>
        /// 获得输入agent当前手持武器的MissionWeapon版
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="missionWeapon"></param>
        /// <returns></returns>
        public static bool AgentGetCurrentWeapon(Agent agent, out MissionWeapon missionWeapon)
        {                 // 获取Agent主手中武器的Index索引
            EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
            if (mainHandIndex == EquipmentIndex.None)
            {
                SysOut("无有效武器", agent);
                missionWeapon = MissionWeapon.Invalid;
                return false;
            }

            // EquipmentIndex转MissionWeapon
            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
            missionWeapon = mainHandEquipmentElement;
            return true;
        }
        /// <summary>
        /// agent朝目视方向射击，勉强可用，比实际落点要低要近,需要删掉游戏配置里的空气阻力，删除后正常
        /// 重载一个射击脚本，添加一个精度属性，需要输入记录的连射时间，然后根据时间降低精度。
        /// </summary>
        public static bool AgentShootTowardsLookDirection(Agent agent, float Full_autoTime)
        {//抽空加一下基础的命中率,调用一下agent还是哪里的获取武器当前精准度,可以获得一个基于准星的散布值
            Random random = new Random();

            if (agent.Equipment != null)
            {
                //每次循环的时候，重新取一遍本身射击的角度，去做角度转换
                //角度的获取和改变，以后都用 Mat3 mat3 = agent.LookRotation;需要输出vec3时，用mat3.f获取vec3的值
                Mat3 mat3 = agent.LookRotation;

                float M = random.NextFloat() * Full_autoTime;
                // 随机决定正负
                int sign = random.Next(2) * 2 - 1; // 将产生1或-1
                float radians = (M * sign) * (3.1415f / 180.0f); // 角度转换为弧度//纵向后坐力不用取负数
                mat3.RotateAboutUp(radians);
                M = random.NextFloat() * Full_autoTime / 3 + 0.3f;
                radians = (M * sign) * (3.1415f / 180.0f); // 角度转换为弧度
                mat3.RotateAboutSide(radians);

                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut("无有效武器", agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                // 优先采用原生射击回调记录到的实际弹速；角色尚未射过第一箭时，
                // 直接使用当前武器面板弹速，不能让魔法射击依赖一次预热射击。
                float baseSpeed = mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon
                    ? mainHandEquipmentElement.GetModifiedMissileSpeedForCurrentUsage()
                    : -1f;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list != null)
                {
                    foreach (AgentMissileSpeedData item in list)
                    {
                        if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                        {
                            baseSpeed = item.MissileSpeed;
                            break;
                        }
                    }
                }
                //if(baseSpeed == -1)
                //{
                //    SysOut("有弹道速度记录，是要使用此武器进行一次射击", agent);
                //    return false;
                //}   
                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);


                Vec3 headPosition = agent.GetEyeGlobalPosition();

                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, headPosition, mat3.f, baseSpeed);
                if (index <= 0)
                {
                    SysOut("投射物创建失败", agent);
                    return false;
                }
                return true;
            }
            else
            {
                SysOut("该角色无装备", agent);
                return false;
            }

        }
        /// <summary>
        /// 参考agent当前手持的武器,从agent当前的武器栏中,获取合适的投射物
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="ThrewMeleeWeapon"></param>
        /// <param name="EquipmentIndex"></param>
        /// <returns></returns>
        public static MissionWeapon getMissionWeaponFromAgentInventory(Agent agent, out bool ThrewMeleeWeapon, out EquipmentIndex EquipmentIndex)
        {
            //因为missionweapon.ammoweapon会在武器装填时输出空值，所以还是重写一个弹药获取的代码
            //现在是获取手上的武器后，依次遍历agent的武器栏位。如果遍历到的物品是手上武器的弹药，并且弹药数量大于0，则设置为一会addmissile的弹药。
            //手上武器为近战时还要做个缺省值
            ThrewMeleeWeapon = false;
            EquipmentIndex = EquipmentIndex.None;
            EquipmentIndex 备用index = EquipmentIndex.None;//如果弹药数量为空的话，弹药所在的index记录在这个位置，如果本身有弹药但是因为用完了的时候，输出这个位置
            if (!agent.IsHuman || agent.GetPrimaryWieldedItemIndex() == EquipmentIndex.None)
                return MissionWeapon.Invalid;
            MissionWeapon missionWeapon = agent.Equipment[agent.GetPrimaryWieldedItemIndex()];
            if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].CurrentUsageItem.IsRangedWeapon)//手上的物品是远程武器
            {
                for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.NumAllWeaponSlots; equipmentIndex++)//循环遍历武器栏位
                {

                    if (!agent.Equipment[agent.GetPrimaryWieldedItemIndex()].IsEmpty && !agent.Equipment[equipmentIndex].IsEmpty)//如果手上的武器和遍历的物品非空
                    {
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.Crossbow)//如果手上的武器是弩
                        {
                            //先进行一次把射出的武器定位默认值
                            // 获取ItemObject引用，这里使用了一个字符串ID来查找特定的武器。字符串是xml里item的id值。
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("bolt_a");
                            // 创建MissionWeapon实例
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt && agent.Equipment[equipmentIndex].Amount > 0)//如果遍历的物品是弩箭
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                EquipmentIndex = equipmentIndex;
                                break;//退出遍历循环
                            }
                            else if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bolt)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].Item.PrimaryWeapon.WeaponClass == WeaponClass.Bow)
                        {
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("arrow_emp_1_a");
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow && agent.Equipment[equipmentIndex].Amount > 0)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];
                                EquipmentIndex = equipmentIndex;
                                break;
                            }
                            else if (agent.Equipment[equipmentIndex].Item.PrimaryWeapon.WeaponClass == WeaponClass.Arrow)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                        if (agent.Equipment[agent.GetPrimaryWieldedItemIndex()].CurrentUsageItem.IsConsumable && agent.Equipment[equipmentIndex].CurrentUsageItem.IsRangedWeapon && agent.Equipment[equipmentIndex].CurrentUsageItem.IsConsumable)
                        {
                            ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("western_javelin_1_t2");
                            missionWeapon = new MissionWeapon(weaponItem, null, null);
                            if (agent.Equipment[equipmentIndex].Amount > 0)
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];
                                EquipmentIndex = equipmentIndex;
                                break;
                            }
                            else
                            {
                                missionWeapon = agent.Equipment[equipmentIndex];//设定射出的投射物是这个弩箭
                                备用index = equipmentIndex;
                            }
                        }
                    }

                }
            }
            else//如果手上不是远程武器，也写了适配,默认射一个投矛。伤害的问题在伤害计算那边处理
            {
                ItemObject weaponItem = Game.Current.ObjectManager.GetObject<ItemObject>("western_javelin_1_t2");
                missionWeapon = new MissionWeapon(weaponItem, null, null);
                ThrewMeleeWeapon = true;
            }
            if (备用index != EquipmentIndex.None && EquipmentIndex == EquipmentIndex.None)
            {
                EquipmentIndex = 备用index;
            }
            return missionWeapon;
        }
        public static bool AimShoot(Agent agent)//自瞄步骤1，选择射击目标agent
        {
            EquipmentIndex mainHandIndex1 = agent.GetPrimaryWieldedItemIndex();
            if (mainHandIndex1 == EquipmentIndex.None)
            {
                return false;
            }
            MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex1];
            if (!mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
            {
                return false;
            }
            Agent ShootAgent = agent;
            Agent vAgent = null;
            List<Agent> list1 = new List<Agent>();
            List<Agent> list2 = new List<Agent>();
            foreach (Agent ChooseAgent in agent.Mission.Agents)
            {
                if (!ChooseAgent.IsFriendOf(ShootAgent) && ChooseAgent.IsHuman && ChooseAgent.CurrentMortalityState != MortalityState.Invulnerable)
                {
                    list1.Add(ChooseAgent);
                }


            }

            foreach (Agent ChooseAgent in list1)
            {
                vAgent = ChooseAgent;
                EquipmentIndex mainHandIndex = ChooseAgent.GetOffhandWieldedItemIndex();
                if (mainHandIndex == EquipmentIndex.None || vAgent.GetCurrentActionType(1) != Agent.ActionCodeType.DefendShield || isBehindTarget(ChooseAgent, ShootAgent))//如果左手为空或者没有顶盾或者处于身后
                {
                    list2.Add(ChooseAgent);
                }
            }
            list1.Clear();
            foreach (Agent ChooseAgent in list2)
            {

                vAgent = ChooseAgent;
                if (CanSeeAgent(ShootAgent, ChooseAgent))//射线检测，能看到这个agent
                {
                    list1.Add(ChooseAgent);
                }
            }
            list2.Clear();

            list1.Sort((x, y) => (x.Position - ShootAgent.Position).Length.CompareTo((y.Position - ShootAgent.Position).Length));
            float len = float.MaxValue;
            foreach (Agent ChooseAgent in list1)
            {
                //if ((ChooseAgent.Position - AgentLookPos(ShootAgent)).Length < len)
                //{
                //    len = (ChooseAgent.Position - AgentLookPos(ShootAgent)).Length;
                //    vAgent = ChooseAgent;
                //}
                // InformationManager.DisplayMessage(new InformationMessage($"{(ChooseAgent.Position - ShootAgent.Position).Length}"));
                vAgent = ChooseAgent; break;
            }

            if (vAgent != null && CanSeeAgent(ShootAgent, vAgent) && AgentShotAgent(ShootAgent, vAgent) != 0)
            {
                //vAgent.SetTargetPosition(vAgent.Position.AsVec2);
                //vAgent.SetTargetPosition(ShootAgent.Position.AsVec2);

                ShootAgent.SetAttackState((int)Agent.ActionStage.AttackRelease);
                //ShootAgent.SetAttackState((int)Agent.ActionStage.AttackReady);
                return true;
            }

            return false;
        }
        /// <summary>
        /// 2是否在1身后
        /// </summary>
        /// <param name="agent1"></param>
        /// <param name="agent2"></param>
        /// <returns></returns>
        public static bool isBehindTarget(Agent agent1, Agent agent2)//
        {
            Vec2 rel_pos = agent1.Position.AsVec2 - agent2.Position.AsVec2;
            Vec2 dir_1 = agent1.LookDirection.AsVec2;
            dir_1 = -dir_1;
            if (dir_1.x * rel_pos.x + dir_1.y * rel_pos.y < 0)
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// 自瞄步骤2，决定实际射击的位置，添加预瞄功能
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="vagent"></param>
        /// <returns></returns>
        public static int AgentShotAgent(Agent agent, Agent vagent)
        {
            if (agent.Equipment != null)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut(0, agent);
                    return 0;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = (float)mainHandEquipmentElement.GetModifiedMissileSpeedForCurrentUsage();

                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);
                if (ThrewMeleeWeapon)
                { baseSpeed = -1; }

                Vec3 headPosition = agent.GetEyeGlobalPosition();
                Vec3 VheadPosition = vagent.GetEyeGlobalPosition();
                Vec3 VAgenSpeed = Vec3.Zero;
                if (SkillSystemBehavior.ActiveComponents.TryGetValue(vagent.Index, out var data))
                {
                    VAgenSpeed = data.Speed.speed;
                }
                Vec3 shotDir = CalculateProjectileFiringSolution(headPosition, VheadPosition, baseSpeed, 9.81f);
                float denominator = baseSpeed * shotDir.AsVec2.Length;
                if (!IsFinitePositive(denominator))
                    return 0;
                float timeOld = (headPosition - VheadPosition).AsVec2.Length / denominator;
                if (!IsFiniteNonNegative(timeOld))
                    return 0;
                float timeNew = float.MaxValue;
                //VheadPosition.z -= 15 / 100f;
                //VheadPosition.y += 15 / 100f;

                for (int iteration = 0;
                     iteration < 4 && TaleWorlds.Library.MathF.Abs(timeOld - timeNew) > 0.001f;
                     iteration++)
                {
                    timeNew = timeOld;
                    Vec3 s = MultiplyVectorByScalar(VAgenSpeed, timeOld);
                    //s = vagent.LookDirection.AsVec2 * VAgenSpeed.Length * timeOld;
                    VheadPosition = vagent.GetEyeGlobalPosition() + s;
                    shotDir = CalculateProjectileFiringSolution(headPosition, VheadPosition, baseSpeed, 9.81f);
                    denominator = baseSpeed * shotDir.AsVec2.Length;
                    if (!IsFinitePositive(denominator))
                        return 0;
                    timeOld = (headPosition - VheadPosition).AsVec2.Length / denominator;
                    if (!IsFiniteNonNegative(timeOld))
                        return 0;
                }


                //GameEntity gameEntity = GameEntity.CreateEmpty(Mission.Current.Scene);
                //gameEntity.AddAllMeshesOfGameEntity(GameEntity.Instantiate(Mission.Current.Scene, "mangonel_mapicon_projectile", true));
                //gameEntity.SetLocalPosition(VheadPosition);


                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, headPosition, VheadPosition, baseSpeed);
                mainHandEquipmentElement.Amount = (short)(mainHandEquipmentElement.Amount - 1);
                return index;

            }
            SysOut(2, agent);
            return 0;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 对一个已经选定的目标进行低开销预判射击。不会扫描或排序任务中的 Agent；
        /// 只读取当前武器、当前目标速度，并进行固定两次提前量修正。
        /// </summary>
        public static bool TryShootAtAgentLowCost(Agent shooter, Agent target, bool useHighArc)
        {
            if (!TryPrepareLowCostShot(shooter, target, useHighArc,
                    out MissionWeapon shotWeapon, out MissionWeapon ammoWeapon,
                    out Vec3 start, out Vec3 shotDirection, out float missileSpeed))
                return false;

            return FireProjectileFromAgentWithWeaponAtPosition(
                shooter, shotWeapon, ammoWeapon, start,
                shotDirection.NormalizedCopy(), missileSpeed) > 0;
        }

        /// <summary>
        /// AI 施法前的纯检查：必须确实持有弹药，并能以当前武器弹速解出到当前
        /// 目标的弹道。不会创建投射物、遍历战场或修改 Agent 状态。
        /// </summary>
        public static bool CanShootAtAgentLowCost(Agent shooter, Agent target, bool useHighArc)
        {
            return TryPrepareLowCostShot(shooter, target, useHighArc,
                out _, out _, out _, out _, out _);
        }

        private static bool TryPrepareLowCostShot(
            Agent shooter,
            Agent target,
            bool useHighArc,
            out MissionWeapon shotWeapon,
            out MissionWeapon ammoWeapon,
            out Vec3 start,
            out Vec3 shotDirection,
            out float missileSpeed)
        {
            shotWeapon = MissionWeapon.Invalid;
            ammoWeapon = MissionWeapon.Invalid;
            start = Vec3.Invalid;
            shotDirection = Vec3.Invalid;
            missileSpeed = -1f;
            if (shooter == null || target == null || shooter.Equipment == null ||
                !shooter.IsActive() || !target.IsActive() || target.Health <= 0f ||
                shooter == target || !shooter.IsEnemyOf(target))
                return false;

            EquipmentIndex weaponIndex = shooter.GetPrimaryWieldedItemIndex();
            if (weaponIndex == EquipmentIndex.None)
                return false;
            shotWeapon = shooter.Equipment[weaponIndex];
            if (shotWeapon.IsEmpty || shotWeapon.CurrentUsageItem == null ||
                !shotWeapon.CurrentUsageItem.IsRangedWeapon)
                return false;

            missileSpeed = shotWeapon.GetModifiedMissileSpeedForCurrentUsage();
            if (SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(shooter.Index, out var speeds) &&
                speeds != null)
            {
                for (int i = 0; i < speeds.Count; i++)
                {
                    AgentMissileSpeedData speedData = speeds[i];
                    if (speedData.Weapon.Item.Id == shotWeapon.Item.Id)
                    {
                        missileSpeed = speedData.MissileSpeed;
                        break;
                    }
                }
            }
            if (missileSpeed <= 0.01f)
                return false;

            if (!TryGetActualAmmoWeapon(shooter, shotWeapon, out ammoWeapon))
                return false;

            start = shooter.GetEyeGlobalPosition();
            Vec3 targetVelocity = target.MovementVelocity.ToVec3();
            if (SkillSystemBehavior.ActiveComponents.TryGetValue(
                    target.Index, out AgentSkillComponent component) && component?.Speed != null &&
                component.Speed.speed.IsValid && component.Speed.speed.LengthSquared <= 2500f)
                targetVelocity = component.Speed.speed;

            Vec3 targetEye = target.GetEyeGlobalPosition();
            Vec3 predicted = targetEye;
            for (int i = 0; i < 2; i++)
            {
                shotDirection = CalculateProjectileFiringSolution(
                    start, predicted, missileSpeed, 9.81f, useHighArc);
                if (!shotDirection.IsValid || shotDirection.LengthSquared < 0.01f)
                    return false;
                float horizontalSpeed = missileSpeed * shotDirection.AsVec2.Length;
                if (horizontalSpeed <= 0.01f)
                    return false;
                float flightTime = (predicted - start).AsVec2.Length / horizontalSpeed;
                if (float.IsNaN(flightTime) || float.IsInfinity(flightTime) || flightTime > 15f)
                    return false;
                predicted = targetEye + targetVelocity * flightTime;
            }
            return true;
        }

        internal static bool TryGetActualAmmoWeapon(
            Agent shooter, MissionWeapon shotWeapon, out MissionWeapon ammoWeapon)
        {
            ammoWeapon = MissionWeapon.Invalid;
            WeaponComponentData usage = shotWeapon.CurrentUsageItem;
            if (usage == null)
                return false;

            // 投矛、飞斧等消耗品本身就是投射物。
            if (usage.IsConsumable)
            {
                if (shotWeapon.Amount <= 0)
                    return false;
                ammoWeapon = shotWeapon;
                return true;
            }

            WeaponClass ammoClass = usage.AmmoClass;
            if (ammoClass == WeaponClass.Undefined)
                return false;
            for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot;
                 index < EquipmentIndex.NumAllWeaponSlots; index++)
            {
                MissionWeapon candidate = shooter.Equipment[index];
                if (candidate.IsEmpty || candidate.Amount <= 0 ||
                    candidate.CurrentUsageItem == null)
                    continue;
                if (candidate.CurrentUsageItem.WeaponClass == ammoClass)
                {
                    ammoWeapon = candidate;
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 自瞄弹道计算，任意位置射击某目标vagent
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="agent"></param>
        /// <param name="vagent"></param>
        /// <returns></returns>
        public bool PosShotAgent(Vec3 StartPos, Agent agent, Agent vagent)
        {
            if (agent.Equipment != null)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut(0, agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = -1;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list == null)
                {
                    SysOut("无弹道速度记录，需要进行一次射击", agent);
                    return false;
                }
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }

                bool ThrewMeleeWeapon;
                EquipmentIndex equipmentIndex;
                mainHandEquipmentElement = getMissionWeaponFromAgentInventory(agent, out ThrewMeleeWeapon, out equipmentIndex);
                mainHandEquipmentElement.Amount = (short)(mainHandEquipmentElement.Amount - 1);
                if (ThrewMeleeWeapon)
                { baseSpeed = -1; }

                Vec3 VheadPosition = vagent.GetEyeGlobalPosition();

                int index = Script.FireProjectileFromAgentWithWeaponAtPosition(agent, agent.Equipment[mainHandIndex], mainHandEquipmentElement, StartPos, VheadPosition, baseSpeed);
                if (index == 0)
                { SysOut("无有效目标", agent); return false; }


            }

            SysOut(2, agent);
            return false;
        }
        /// <summary>
        ///         函数需要传进来的参数和一代的addmissile基本一致，分别是：
        ///         谁，用什么武器，射击出什么投射物，在什么位置，以什么角度，什么速度射击。
        ///         其中“用什么武器（ShotWeapon）”可以随便填MissionWeapon ，近战远程投掷都无所谓，但是“射击出什么投射物（AmmoWeapon）”必须是一个投射物，否则报错。。
        ///         射击角度这里可以填入一个目标pos，如果传进来的武器能射击到这个pos，会自动计算一个射击角度，从StartPos射击到EndPos。如果不能射击到，一会的返回值会输出一个0，方便后续代码处理。
        /// </summary>
        /// <param name="shotAgent"></param>
        /// <param name="ShotWeapon"></param>
        /// <param name="AmmoWeapon"></param>
        /// <param name="StartPos"></param>
        /// <param name="StartDirOrEndPos"></param>
        /// <param name="MissileRealSpeed"></param>
        /// <param name="forceTargetPosition">明确按世界坐标目标点求弹道，并保留传入的高度。</param>
        /// <returns></returns>
        public static int FireProjectileFromAgentWithWeaponAtPosition(Agent shotAgent, MissionWeapon ShotWeapon, MissionWeapon AmmoWeapon, Vec3 StartPos, Vec3 StartDirOrEndPos, float MissileRealSpeed = -1, bool forceTargetPosition = false)
        {
            if (shotAgent == null || shotAgent.Mission == null || ShotWeapon.IsEmpty ||
                ShotWeapon.CurrentUsageItem == null || AmmoWeapon.IsEmpty ||
                AmmoWeapon.CurrentUsageItem == null || !StartPos.IsValid || !StartDirOrEndPos.IsValid)
                return 0;

            WeaponComponentData ammoUsage = AmmoWeapon.CurrentUsageItem;
            if (!ammoUsage.IsAmmo && !ammoUsage.IsConsumable)
                return 0;

            //需要设定一个缺省值，避免传入的物品是近战武器，从而无法获取弹药速度
            //首先是根据传递进来的ShotWeapon获取AddCustomMissile需要的missile的speed属性，如果传递进来一个近战武器，则固定使用30的速度，差不多是投矛的弹速。
            ////更新：两个speed知道怎么回事了，投射物真实速度是在OnAgentShootMissile里 进行获取，然后进行记录，再在这里使用
            if (ShotWeapon.CurrentUsageItem.IsRangedWeapon && MissileRealSpeed == -1)
            {
                if (SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(shotAgent.Index, out var list))
                {
                    foreach (AgentMissileSpeedData item in list)
                    {
                        if (item.Weapon.Item.Id == ShotWeapon.Item.Id)
                        {
                            MissileRealSpeed = item.MissileSpeed;
                        }
                    }
                }
                MissileRealSpeed = (float)ShotWeapon.GetModifiedMissileSpeedForCurrentUsage();
            }
            else if (!ShotWeapon.CurrentUsageItem.IsRangedWeapon && MissileRealSpeed == -1)
            {
                MissileRealSpeed = 30;
            }
            float MissilePanelSpeed = ShotWeapon.GetModifiedMissileSpeedForCurrentUsage();
            //接着初始化一个index值，然后遍历一遍已有的Missiles，确保index不会重复。确认后，把index添加到WoW_MissileIndex里。
            //同时把传递进来的武器的伤害值，以键值对的形式添加在WoW_WeaponMissile里，以后可以通过对应的index来获取到当时对应的射击武器伤害值。
            int index = 100;

            foreach (var missile in shotAgent.Mission.MissilesList)
            {
                //if (missile.Index == index || SkillSystemBehavior.WoW_MissileIndex.Contains(index) || SkillSystemBehavior.WoW_WeaponMissile.ContainsKey(index))
                //{
                //    //index++;//这边写的有点问题，应该至少再判定一下index+1后，是否在链表/字典里。因为不一定先射出的投射物先消失，所以这里直接这样写会index冲突。
                //}
                index = Math.Max(missile.Index, index);//干脆直接这样，index只增不减，反正只是一个数，不影响计算开销
            }
            index++;

            while (SkillSystemBehavior.WoW_MissileIndex.Contains(index))
            {
                index++;
            }
            SkillSystemBehavior.WoW_MissileIndex.Add(index);
            SkillSystemBehavior.WoW_WeaponMissile.Add(index, ShotWeapon.GetModifiedMissileDamageForCurrentUsage());
            //需要处理StartDirOrEndPos是dir还是pos。dir的长度会是1
            //这里利用一下pos和rot的特性，判定传递进来的StartDirOrEndPos是一个角度还是地点，如果是角度，直接去生成投射物；如果是地点，走一下弹道计算的代码后，再生成投射物。
            //最后返回一下投射物的index，如果返回了0，则说明自动瞄准无法计算出有效的弹道。
            if (ShotWeapon.Item.ToString() != "composite_steppe_bow" && 1 == 2)//限制一下，自动步枪武器不触发动作
            {
                if (ShotWeapon.CurrentUsageItem.IsConsumable)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_javelin_with_shield"));
                else if (ShotWeapon.CurrentUsageItem.WeaponClass == WeaponClass.Bow)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_bow"));
                else if (ShotWeapon.CurrentUsageItem.WeaponClass == WeaponClass.Crossbow)
                    shotAgent.SetActionChannel(1, ActionIndexCache.Create("act_release_crossbow"));
            }


            if (!forceTargetPosition && Math.Round(StartDirOrEndPos.Length) == 1)
            {
                Mission.Current.AddCustomMissile(shotAgent, AmmoWeapon, StartPos, StartDirOrEndPos, shotAgent.LookRotation, MissilePanelSpeed, MissileRealSpeed, true, null, index);
            }
            else
            {

                if (ShotWeapon.CurrentUsageItem.IsConsumable && !forceTargetPosition)
                { StartDirOrEndPos.z -= 0.5f; }
                Vec3 shotDir = CalculateProjectileFiringSolution(StartPos, StartDirOrEndPos, MissileRealSpeed, 9.81f);
                if (shotDir == Vec3.Invalid || shotDir.x.Equals(float.NaN) || shotDir.y.Equals(float.NaN) || shotDir.z.Equals(float.NaN))
                {
                    SkillSystemBehavior.WoW_MissileIndex.Remove(index);
                    SkillSystemBehavior.WoW_WeaponMissile.Remove(index);
                    return 0;
                }
                Mission.Current.AddCustomMissile(shotAgent, AmmoWeapon, StartPos, shotDir, shotAgent.LookRotation, MissilePanelSpeed, MissileRealSpeed, true, null, index);
            }

            return index;
        }
        /// <summary>
        /// pos射击pos，弹道计算，输出射击角度。凑合
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="speed"></param>
        /// <param name="gravity"></param>
        /// <returns></returns>
        public static Vec3 CalculateProjectileFiringSolution(Vec3 start, Vec3 end, float speed, float gravity)
        {
            return CalculateProjectileFiringSolution(start, end, speed, gravity, false);
        }

        public static Vec3 CalculateProjectileFiringSolution(
            Vec3 start, Vec3 end, float speed, float gravity, bool useHighArc)
        {
            // 计算水平距离
            Vec2 horizontalDistance = new Vec2(end.x - start.x, end.y - start.y);
            float horizontalRange = horizontalDistance.Length;

            // 计算垂直距离，即高度差
            float verticalDistance = end.z - start.z;

            // 计算发射角度的可能解
            float speedSquared = speed * speed;
            float sqrtTerm = speedSquared * speedSquared - gravity * (gravity * horizontalRange * horizontalRange + 2 * verticalDistance * speedSquared);

            // 如果这个术语小于0，则没有实际的解决方案，因为速度不够以克服重力
            if (sqrtTerm < 0.0f)
            {
                // throw new InvalidOperationException("No valid firing solution for given parameters.");
                return Vec3.Invalid;
            }

            float sqrtValue = (float)Math.Sqrt(sqrtTerm);

            // 取两个可能的解中较小的一个（较高的一个将是较大的发射角）
            float angle = (float)Math.Atan2(
                speedSquared + (useHighArc ? sqrtValue : -sqrtValue),
                gravity * horizontalRange);

            // 将发射角转换为方向向量
            Vec3 firingSolution = new Vec3(horizontalDistance.x, horizontalDistance.y, 0);
            firingSolution.Normalize();
            firingSolution *= (float)Math.Cos(angle) * speed;   // 水平速度分量
            firingSolution.z = (float)Math.Sin(angle) * speed;  // 垂直速度分量
            firingSolution.Normalize();
            return firingSolution;
        }


        
        /// <summary>
        /// 查找目标地点，周围敌人数量最多的一个agent
        /// </summary>
        /// <param name="caster"></param>
        /// <param name="tarPos"></param>
        /// <param name="range"></param>
        /// <returns></returns>
        public static Agent FindOptimalConflictPos(Agent caster, Vec3 tarPos, int range)
        {
            List<Agent> l = GetTargetedInRange(caster, tarPos, (int)range);
            int conut = 0;
            Agent tarAgent = null;
            foreach (Agent agent in l)
            {
                int c = GetTargetedInRange(caster, agent.Position, (int)3).Count;
                if (conut < c)
                {
                    tarAgent = agent;
                    conut = c;
                }
            }
            if (tarAgent != null) return tarAgent;
            return null;
        }
        /// <summary>
        /// 魔法伤害脚本
        /// </summary>
        /// <param name="Caster"></param>
        /// <param name="Victim"></param>
        /// <param name="BaseDamage"></param>
        /// <param name="DamageType"></param>
        public static void CalculateFinalMagicDamage(Agent Caster, Agent Victim, float BaseDamage, DamageType DamageType)
        {
            Systems.MagicDamageSystem.Apply(
                Caster,
                Victim,
                BaseDamage,
                Systems.MagicDamageSystem.GetSpellPowerCoefficient(Caster),
                DamageType);
        }

        public static Systems.MagicDamageResult CalculateFinalMagicDamage(
            Agent caster,
            Agent victim,
            float baseDamage,
            float spellPowerCoefficient,
            DamageType damageType,
            Systems.MagicDamageFlags flags = Systems.MagicDamageFlags.None,
            Vec3? impactPosition = null)
        {
            return Systems.MagicDamageSystem.Apply(
                caster, victim, baseDamage, spellPowerCoefficient,
                damageType, flags, impactPosition);
        }
        /// <summary>
        /// 注意判定非空，输入agent，获取对应的扩展信息
        /// </summary>
        /// <param name="agent"></param>
        /// <returns></returns>
        public static AgentSkillComponent GetActiveComponents(Agent agent)
        {
            AgentSkillComponent agentSkillComponent;
            SkillSystemBehavior.ActiveComponents.TryGetValue(agent.Index, out agentSkillComponent);
            return agentSkillComponent;
        }
        public static bool AgentShootConeOfArrows(Agent casterAgent, int v)
        {
            if (ConeOfArrows(casterAgent, v))
            {
                return true;
            }
            else return false;
        }

        private static bool ConeOfArrows(Agent agent, int num)
        {
            if (agent != null && agent.IsActive() && agent.Mission != null &&
                agent.Equipment != null && num > 0)
            {


                // 获取Agent主手中武器的Index索引
                EquipmentIndex mainHandIndex = agent.GetPrimaryWieldedItemIndex();
                if (mainHandIndex == EquipmentIndex.None)
                {
                    SysOut("无有效武器", agent);
                    return false;
                }

                // EquipmentIndex转MissionWeapon
                MissionWeapon mainHandEquipmentElement = agent.Equipment[mainHandIndex];
                if (mainHandEquipmentElement.IsEmpty ||
                    mainHandEquipmentElement.CurrentUsageItem == null ||
                    !mainHandEquipmentElement.CurrentUsageItem.IsRangedWeapon)
                {
                    SysOut("主手武器不是有效的远程武器", agent);
                    return false;
                }

                if (!TryGetActualAmmoWeapon(agent, mainHandEquipmentElement, out MissionWeapon ammoWeapon))
                {
                    SysOut("无有效弹药", agent);
                    return false;
                }

                // 获取主手装备元素的修正后的导弹速度
                float baseSpeed = -1;
                SkillSystemBehavior.WoW_AgentMissileSpeedData.TryGetValue(agent.Index, out var list);
                if (list == null)
                { SysOut("无弹道速度记录，需要进行一次射击", agent); return false; }
                foreach (AgentMissileSpeedData item in list)
                {
                    if (item.Weapon.Item.Id == mainHandEquipmentElement.Item.Id)
                    {
                        baseSpeed = item.MissileSpeed;
                    }
                }

                Vec3 headPosition = agent.GetEyeGlobalPosition();
                Random random = new Random();
                for (int i = num; i > 0; i--)
                {

                    float randomValue = -1.2f + random.NextFloat() * 2.4f;

                    //每次循环的时候，重新取一遍本身射击的角度，去做角度转换
                    //角度的获取和改变，以后都用 Mat3 mat3 = agent.LookRotation;需要输出vec3时，用mat3.f获取vec3的值
                    Mat3 mat3 = agent.LookRotation;
                    float radians = (randomValue) * (3.1415f / 180.0f); // 角度转换为弧度

                    mat3.RotateAboutUp(radians);
                    randomValue = 1.2f + random.NextFloat() * 2.4f;
                    radians = (randomValue) * (3.1415f / 180.0f); // 角度转换为弧度

                    mat3.RotateAboutSide(radians);
                    int index = FireProjectileFromAgentWithWeaponAtPosition(
                        agent, mainHandEquipmentElement, ammoWeapon, headPosition, mat3.f, baseSpeed);


                }
                return true;
            }
            else
            {
                SysOut("该角色无装备", agent);
                return false;
            }
        }
        public static void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex)
        {
            //获取自己当前武器的剩余弹药数量
            int OwnCurrentAmmo = 0;
            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.ExtraWeaponSlot; equipmentIndex++)
            {
                if (!shooterAgent.Equipment[equipmentIndex].IsEmpty && shooterAgent.Equipment[equipmentIndex].CurrentUsageItem.IsRangedWeapon)
                {
                    OwnCurrentAmmo = shooterAgent.Equipment.GetAmmoAmount(equipmentIndex);
                }
            }
            //如果当前武器弹药数量过少
            if (OwnCurrentAmmo <= 3 )
            {
                //遍历自己队伍内的agent，抢过来一些弹药
                if (shooterAgent.Formation != null)//获取射击者的当前编队
                {
                    int maxAmmoAmount = 0;
                    Agent TAgent = null;
                    EquipmentIndex TAgentEquipmentIndex = EquipmentIndex.None;
                    WeaponClass shooterAgentWeaponClass = shooterAgent.Equipment[weaponIndex].CurrentUsageItem.AmmoClass;
                    //先遍历整个编队，找到弹药最多的agent。然后获取这个agent的弹药进行转移
                    int jishu = 0;
                    List<Agent> list = new List<Agent>();
                    shooterAgent.Formation.ApplyActionOnEachUnit(delegate (Agent agent) //遍历编队agent的函数，非常类似于1代的try_for_agents给感觉，甚至不能中途停下来（也可能是我不知道怎么让他中途停止）
                    {

                        if (!agent.IsMainAgent && agent != shooterAgent)
                        {

                            for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.ExtraWeaponSlot; equipmentIndex++)//遍历 物品栏
                            {
                                //筛选出当前遍历的agent有没有和射击者agent相同类型的武器，避免弓手拿到弩箭之类的。同时筛选出弹药最多的目标。
                                if (!agent.Equipment[equipmentIndex].IsEmpty && shooterAgent.Equipment[weaponIndex].CurrentUsageItem.WeaponClass == agent.Equipment[equipmentIndex].CurrentUsageItem.WeaponClass && maxAmmoAmount < agent.Equipment.GetAmmoAmount(equipmentIndex))
                                {
                                    maxAmmoAmount = agent.Equipment.GetAmmoAmount(equipmentIndex);
                                    TAgent = agent;
                                }
                            }
                        }

                        jishu++;
                        list.Add(agent);
                    }, null);
                    int itemAmmoAmount = 0;
                    //第二轮筛选，拿之前那个弹药最多的目标，找到他身上具体的哪个物品上弹药最多，一会从这个物品上扣弹药
                    if (maxAmmoAmount > 0 && TAgent != null)
                    {

                        itemAmmoAmount = 0;
                        for (EquipmentIndex equipmentIndex = EquipmentIndex.WeaponItemBeginSlot; equipmentIndex < EquipmentIndex.ExtraWeaponSlot; equipmentIndex++)
                        {
                            if (TAgent.Equipment[equipmentIndex].CurrentUsageItem != null && shooterAgentWeaponClass == TAgent.Equipment[equipmentIndex].CurrentUsageItem.WeaponClass)
                            {
                                if (itemAmmoAmount < TAgent.Equipment[equipmentIndex].Amount)
                                {
                                    itemAmmoAmount = TAgent.Equipment[equipmentIndex].Amount;
                                    TAgentEquipmentIndex = equipmentIndex;
                                }
                            }
                        }
                        getMissionWeaponFromAgentInventory(shooterAgent, out var threwMeleeWeapon, out var equipmentIndex1);//自己写的一个函数，根据手持的武器，获取一个合适的弹药物品
                        if (equipmentIndex1 == EquipmentIndex.None)
                        { return; }
                        //如果射击者有合适的弹药栏位可以填充，开始用狗屎代码填充那个物品的 数量
                        int shootAgentMaxAmmo = shooterAgent.Equipment[equipmentIndex1].MaxAmmo;
                        int agentAmmo = itemAmmoAmount;
                        if ((agentAmmo - 3) > 0)
                        {
                            if (shootAgentMaxAmmo - OwnCurrentAmmo - (agentAmmo - 3) > 0)
                            {
                                //射击者把目标的弹药全拿完了(留几根）
                                //shooterAgent.Equipment.SetAmountOfSlot(equipmentIndex1, (short)(OwnCurrentAmmo + agentAmmo - 3));
                                //TAgemt.Equipment.SetAmountOfSlot(TAgentEquipmentIndex, 3);

                                if (shooterAgent.Equipment[equipmentIndex1].Amount != OwnCurrentAmmo + agentAmmo - 3 && shooterAgent.Equipment[equipmentIndex1].Amount != 0 && OwnCurrentAmmo + agentAmmo - 3 != 0 && TAgent.Equipment[equipmentIndex1].Amount != 3 && TAgent.Equipment[equipmentIndex1].Amount != 0 && 3 != 0)//别问这是干啥的，问就是为了避免bug
                                {
                                    shooterAgent.SetWeaponAmountInSlot(equipmentIndex1, (short)(OwnCurrentAmmo + agentAmmo - 3), false);
                                    TAgent.SetWeaponAmountInSlot(TAgentEquipmentIndex, (short)(3), false);
                                    if (shooterAgent.IsFriendOf(Agent.Main))
                                    {
                                        InformationManager.DisplayMessage(new InformationMessage($"{shooterAgent.Name}{shooterAgent.Index}抢走了{TAgent.Name}{TAgent.Index}的{OwnCurrentAmmo + agentAmmo - 3}发弹药"));
                                    }
                                    shooterAgent.UpdateAgentProperties();
                                    TAgent.UpdateAgentProperties();
                                    return;
                                }
                            }
                            else
                            {
                                //没拿完的话
                                //shooterAgent.Equipment.SetAmountOfSlot(equipmentIndex1, (short)(shootAgentMaxAmmo));
                                //TAgemt.Equipment.SetAmountOfSlot(TAgentEquipmentIndex, (short)(agentAmmo - shootAgentMaxAmmo));
                                if (shooterAgent.Equipment[equipmentIndex1].Amount != shootAgentMaxAmmo && shooterAgent.Equipment[equipmentIndex1].Amount != 0 && shootAgentMaxAmmo != 0 && TAgent.Equipment[equipmentIndex1].Amount != agentAmmo - shootAgentMaxAmmo && TAgent.Equipment[equipmentIndex1].Amount != 0 && agentAmmo - shootAgentMaxAmmo != 0)//别问这是干啥的，问就是为了避免bug
                                {
                                    shooterAgent.SetWeaponAmountInSlot(equipmentIndex1, (short)(shootAgentMaxAmmo), false);
                                    TAgent.SetWeaponAmountInSlot(TAgentEquipmentIndex, (short)(agentAmmo - shootAgentMaxAmmo), false);
                                    if (shooterAgent.IsFriendOf(Agent.Main))
                                    {
                                        InformationManager.DisplayMessage(new InformationMessage($"{shooterAgent.Name}{shooterAgent.Index}抢走了{TAgent.Name}{TAgent.Index}的{shootAgentMaxAmmo}发弹药"));
                                    }
                                    shooterAgent.UpdateAgentProperties();
                                    TAgent.UpdateAgentProperties();
                                    return;
                                }
                            }
                        }
                    }

                }

            }

        }
    }
}
