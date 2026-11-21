using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using TaleWorlds.Library;

namespace Brainrot
{
    public sealed class TroopReplacementConfig
    {
        private readonly Dictionary<string, string> replacements =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Replacements
        {
            get
            {
                return replacements;
            }
        }

        public static TroopReplacementConfig Load()
        {
            TroopReplacementConfig config = new TroopReplacementConfig();
            string configPath = FindConfigPath();

            if (configPath == null)
            {
                Debug.Print("Brainrot: troop_replacements.xml was not found.");
                return config;
            }

            XmlDocument document = new XmlDocument();
            document.Load(configPath);

            XmlNodeList nodes = document.SelectNodes("/TroopReplacements/TroopReplacement");
            if (nodes == null)
            {
                Debug.Print("Brainrot: troop_replacements.xml has no TroopReplacement entries.");
                return config;
            }

            foreach (XmlElement element in nodes)
            {
                string troopId = element.GetAttribute("troop_id");
                string itemId = element.GetAttribute("visual_item_id");

                if (string.IsNullOrWhiteSpace(troopId) || string.IsNullOrWhiteSpace(itemId))
                {
                    Debug.Print("Brainrot: ignored a TroopReplacement with an empty troop_id or visual_item_id.");
                    continue;
                }

                if (config.replacements.ContainsKey(troopId))
                {
                    Debug.Print("Brainrot: duplicate troop_id in troop_replacements.xml: " + troopId);
                    continue;
                }

                config.replacements.Add(troopId, itemId);
                Debug.Print("Brainrot: loaded replacement " + troopId + " -> " + itemId);
            }

            return config;
        }

        public bool TryGetItemId(string troopId, out string itemId)
        {
            return replacements.TryGetValue(troopId, out itemId);
        }

        private static string FindConfigPath()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "Modules", "Brainrot", "ModuleData", "troop_replacements.xml")),
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "Modules", "Brainrot", "ModuleData", "troop_replacements.xml")),
                Path.GetFullPath(Path.Combine(baseDirectory, "Modules", "Brainrot", "ModuleData", "troop_replacements.xml")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Brainrot", "ModuleData", "troop_replacements.xml"))
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
