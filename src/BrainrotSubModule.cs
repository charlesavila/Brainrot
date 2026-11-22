using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Brainrot
{
    public sealed class BrainrotSubModule : MBSubModuleBase
    {
        private static TroopReplacementConfig replacementConfig;

        public static TroopReplacementConfig ReplacementConfig
        {
            get
            {
                if (replacementConfig == null)
                {
                    replacementConfig = TroopReplacementConfig.Load();
                }

                return replacementConfig;
            }
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Debug.Print("Brainrot: submodule loaded.");
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            CampaignGameStarter campaignStarter =
                gameStarterObject as CampaignGameStarter;
            if (campaignStarter != null)
            {
                campaignStarter.AddBehavior(new BrainrotPartyPreviewBehavior());
            }
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new BrainrotMissionBehavior());
            Debug.Print("Brainrot: mission behavior initialized.");
        }
    }
}
