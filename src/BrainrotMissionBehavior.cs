using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Brainrot
{
    public sealed class BrainrotMissionBehavior : MissionBehavior
    {
        private readonly Dictionary<Agent, string> heroAppearances = new Dictionary<Agent, string>();
        private readonly HashSet<string> errors = new HashSet<string>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            if (agent?.Character?.IsHero == true) heroAppearances[agent] = Appearance(agent);
        }

        private static string Appearance(Agent agent)
        {
            return BrainrotHeroSkins.TryGetItem(agent.Character, out ItemObject item) ? item.StringId : "default";
        }

        public override void OnFixedMissionTick(float fixedDt)
        {
            if (Mission == null) return;
            foreach (Agent agent in Mission.Agents)
            {
                if (agent == null || !agent.IsHuman || agent.Character?.IsHero != true || !agent.IsActive()) continue;
                try
                {
                    string current = Appearance(agent);
                    if (!heroAppearances.TryGetValue(agent, out string previous))
                    {
                        heroAppearances[agent] = current;
                        continue;
                    }
                    if (previous == current) continue;
                    // A confirmed inventory choice changed on an already-built agent. Re-enter the
                    // engine's own equipment lifecycle using its unchanged real equipment.
                    Equipment original = agent.SpawnEquipment;
                    agent.EquipItemsFromSpawnEquipment(true, true, false, 0);
                    heroAppearances[agent] = current;
                    BrainrotPreviewLog.Write("Live hero appearance refreshed: character=" + agent.Character.StringId +
                        "; appearance=" + current + "; native-equipment-preserved=" +
                        ReferenceEquals(original, agent.SpawnEquipment));
                }
                catch (Exception error)
                {
                    if (errors.Add(error.ToString())) BrainrotPreviewLog.Write("Hero appearance refresh failed: " + error);
                }
            }
        }
    }
}