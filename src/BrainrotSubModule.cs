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
            BrainrotPreviewLog.Write("Preview v3: large tableau and troop portraits; installation deferred until game actions are loaded.");
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
            }
            catch (Exception exception)
            {
                BrainrotPreviewLog.Write("Preview hooks could not be installed: " + exception);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
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
