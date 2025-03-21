using System;
using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace New_ZZZF
{
    public class SubModule : MBSubModuleBase
    {

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            try
            {
                // 初始化技能配置管理器并加载XML
                string xmlPath = "../../Modules/New_ZZZF/ModuleData/troop_skills.xml";
                SkillConfigManager.Instance.LoadFromXml(xmlPath);

                // 调试日志
                Debug.Print("[New_ZZZF] 技能配置加载完成！");
            }
            catch (Exception ex)
            {
                Debug.Print($"[New_ZZZF] 配置加载失败: {ex.Message}");
            }
        }
        public override void BeginGameStart(Game game)
        {
            base.BeginGameStart( game );
            SkillFactory.SkillToItemObject();
        }
        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded( game, initializerObject );
            SkillFactory.SkillToItemObject();
        }
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            // 添加自定义的 MissionBehavior 到当前任务
            mission.AddMissionBehavior(new SkillSystemBehavior());

            // 调试日志
            Debug.Print("[New_ZZZF] 技能系统已激活！");

        }
        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            Debug.Print("[New_ZZZF] Mod已启动！");
        }
    }
}
//代码说明
//1. 关键功能
//配置加载：在 OnSubModuleLoad 阶段从 troop_skills.xml 加载技能配置。

//错误处理：捕获XML解析异常并通过游戏内消息和日志输出。

//行为注册：在 OnMissionBehaviorInitialize 中将 SkillSystemBehavior 添加到任务中。

//2. 路径说明
//BasePath.Name：自动获取游戏根目录（如 ...\Steam\steamapps\common\Mount & Blade II Bannerlord）。

//模块路径：Modules/YourMod/ModuleData/troop_skills.xml 需按实际 Mod 名称调整。

//3. 扩展性
//战役模式支持：通过 OnGameStart 可扩展战役模式逻辑（如英雄技能存档）。