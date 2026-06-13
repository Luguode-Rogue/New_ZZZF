using NetworkMessages.FromServer;
using SandBox.Missions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace New_ZZZF.Skills//（法术）
{
    public class ZhaoHuan半成品 : SkillBase
    {
        public ZhaoHuan半成品()
        {
            SkillID = "ZhaoHuan";
            Type = SPSkillType.MainActive;
            Cooldown = 0;
            ResourceCost = 0;
            Text = new TaleWorlds.Localization.TextObject("{=ZZZF0049}ZhaoHuan");
            Difficulty = null;// new List<SkillDifficulty> { new SkillDifficulty(50, "跑动"), new SkillDifficulty(5, "耐力") };//技能装备的需求
            Description = new TaleWorlds.Localization.TextObject("{=ZZZF0050}生成一个6级士兵。消耗耐力：35。冷却时间：30秒。");


        }


        public override bool Activate(Agent agent)
        {
            CharacterObject @object = Game.Current.ObjectManager.GetObject<CharacterObject>("imperial_veteran_infantryman");

            CharacterObject basicCharacter = Game.Current.ObjectManager.GetObject<CharacterObject>("main_hero");
            AgentBuildData agentBuildData = new AgentBuildData(basicCharacter).Team(Mission.Current.PlayerTeam);
            //Agent newAgent = Mission.Current.SpawnAgent(agentBuildData, false);
            SimpleAgentOrigin troopOrigin = new SimpleAgentOrigin(@object, -1, null, default(UniqueTroopDescriptor));
            Agent newAgent = Mission.Current.SpawnAgent(agentBuildData, false);
            newAgent.SetInitialFrame(Script.AgentLookPos(agent), agent.LookDirection.AsVec2, false);

            return true;


        }

    }
}
