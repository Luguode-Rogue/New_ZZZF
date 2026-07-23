using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.LegacyWorld.Bannerlord
{
    /// <summary>
    /// Legacy World 子系统的独立 SubModule 入口。
    /// 
    /// 使用方式：
    /// - 作为独立 Mod：在 SubModule.xml 中配置本类的全名作为入口
    /// - 集成到 New_ZZZF：在 New_ZZZF.SubModule 的 InitializeGameStarter 中
    ///   调用 campaignGameStarter.AddBehavior(new LegacyBehavior()) 即可
    /// 
    /// 建议采用集成方式，与本项目的现有结构保持一致。
    /// </summary>
    public class LegacySubModule : MBSubModuleBase
    {
        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            if (!(game.GameType is Campaign))
                return;

            if (gameStarter is CampaignGameStarter starter)
            {
                starter.AddBehavior(new LegacyBehavior());
            }
        }
    }
}
