using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace Brainrot
{
    public sealed class BrainrotMissionBehavior : MissionBehavior
    {
        private readonly HashSet<Agent> appliedAgents = new HashSet<Agent>();
        private bool diagnosticPrinted;
        private readonly Dictionary<string, ItemObject> replacementItems =
            new Dictionary<string, ItemObject>(StringComparer.OrdinalIgnoreCase);

        public override MissionBehaviorType BehaviorType
        {
            get
            {
                return MissionBehaviorType.Other;
            }
        }

        public override void OnAgentCreated(Agent agent)
        {
            ApplyReplacement(agent);
        }

        public override void OnFixedMissionTick(float fixedDt)
        {
            if (Mission == null)
            {
                return;
            }

            foreach (Agent agent in Mission.Agents)
            {
                if (agent != null)
                {
                    ApplyReplacement(agent);
                }
            }
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            ApplyReplacement(agent);
        }

        private void ApplyReplacement(Agent agent)
        {
            if (agent == null || !agent.IsHuman || appliedAgents.Contains(agent))
            {
                return;
            }

            BasicCharacterObject character = agent.Character as BasicCharacterObject;
            if (character == null)
            {
                return;
            }

            string troopId = character.StringId;
            TroopReplacementConfig config = BrainrotSubModule.ReplacementConfig;
            string configuredTroopId = troopId;
            if (!config.TryGetItemId(configuredTroopId, out string itemId) &&
                troopId != null &&
                troopId.Contains("."))
            {
                configuredTroopId = troopId.Substring(troopId.LastIndexOf('.') + 1);
                config.TryGetItemId(configuredTroopId, out itemId);
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            if (!diagnosticPrinted)
            {
                Debug.Print("Brainrot: replacement found for troop ID=" + troopId);
                diagnosticPrinted = true;
            }

            try
            {
                if (!replacementItems.TryGetValue(itemId, out ItemObject replacementItem))
                {
                    replacementItem = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    if (replacementItem != null)
                    {
                        replacementItems.Add(itemId, replacementItem);
                    }
                }

                if (replacementItem == null)
                {
                    Debug.Print("Brainrot: visual item was not found: " + itemId);
                    return;
                }

                Equipment visualEquipment = agent.SpawnEquipment.Clone();
                visualEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, new EquipmentElement(replacementItem));
                visualEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, EquipmentElement.Invalid);
                visualEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, EquipmentElement.Invalid);
                visualEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, EquipmentElement.Invalid);
                visualEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, EquipmentElement.Invalid);

                agent.UpdateSpawnEquipmentAndRefreshVisuals(visualEquipment);

                Debug.Print("Brainrot: applied visual item " + itemId + " to troop " + troopId);
                appliedAgents.Add(agent);
            }
            catch (Exception exception)
            {
                Debug.Print("Brainrot: failed to apply visual replacement: " + exception);
            }
        }

    }
}
