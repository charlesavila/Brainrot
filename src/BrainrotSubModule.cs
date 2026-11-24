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
            TryInstallTroopNames();
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
            TryInstallTroopNames();
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

        private static void TryInstallTroopNames()
        {
            // Names use only managed character data and can be patched before any UI caches labels.
            try
            {
                BrainrotTroopNames.Install();
            }
            catch (Exception exception)
            {
                BrainrotPreviewLog.Write("Troop name hooks could not be installed: " + exception);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            BrainrotTroopNames.Uninstall();
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
