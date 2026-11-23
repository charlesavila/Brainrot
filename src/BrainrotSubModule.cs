using System;
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
            BrainrotPreviewLog.Start();
            BrainrotPreviewLog.Write("Stage 7: hero skin selection; installation deferred until game actions are loaded.");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            TryInstallPreview();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            TryInstallPreview();
        }

        private static void TryInstallPreview()
        {
            try
            {
                BrainrotPartyPreview.Install();
                BrainrotTroopPortraits.Install();
                BrainrotSkinSelector.Install();
                BrainrotAgentBodyRenderer.Install();
            }
            catch (Exception exception)
            {
                BrainrotPreviewLog.Write("Preview hooks could not be installed: " + exception);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            BrainrotAgentBodyRenderer.Uninstall();
            BrainrotSkinSelector.Uninstall();
            BrainrotTroopPortraits.Uninstall();
            BrainrotPartyPreview.Uninstall();
            base.OnSubModuleUnloaded();
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new BrainrotMissionBehavior());
            Debug.Print("Brainrot: mission behavior initialized.");
        }
    }
}
