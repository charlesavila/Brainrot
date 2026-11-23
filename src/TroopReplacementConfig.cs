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

        internal readonly HashSet<string> CharacterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, string> Replacements
        {
            get
            {
                return replacements;
            }
        }

        public static TroopReplacementConfig Load()
        {
            string configPath = FindConfigPath("character_replacements.xml");
            if (configPath == null)
            {
                Debug.Print("Brainrot: character_replacements.xml was not found; native appearances are retained.");
                return new TroopReplacementConfig();
            }
            return LoadCatalog(configPath);
        }

        internal static TroopReplacementConfig LoadCatalog(string configPath)
        {
            TroopReplacementConfig config = new TroopReplacementConfig();
            XmlDocument document = new XmlDocument();
            document.Load(configPath);
            XmlNodeList characters = document.SelectNodes("/BrainrotCharacters/Character");
            if (characters == null || characters.Count == 0)
            {
                Debug.Print("Brainrot: character_replacements.xml has no Character entries.");
                return config;
            }

            foreach (XmlElement element in characters)
            {
                string characterId = element.GetAttribute("id").Trim();
                string meshName = element.GetAttribute("mesh").Trim();
                string troopId = element.GetAttribute("troop_id").Trim();
                if (!string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(meshName))
                    config.CharacterIds.Add(characterId);
                if (string.IsNullOrWhiteSpace(characterId) ||
                    string.IsNullOrWhiteSpace(meshName) || string.IsNullOrWhiteSpace(troopId))
                {
                    Debug.Print("Brainrot: ignored a Character with missing id, mesh, or troop_id.");
                    continue;
                }
                if (config.replacements.ContainsKey(troopId))
                {
                    Debug.Print("Brainrot: duplicate troop_id in character_replacements.xml: " + troopId);
                    continue;
                }

                string itemId = characterId + "_body_armor";
                config.replacements.Add(troopId, itemId);
                Debug.Print("Brainrot: loaded character " + characterId + " with mesh " + meshName +
                    " for troop " + troopId + " -> " + itemId);
            }

            return config;
        }

        public bool TryGetItemId(string troopId, out string itemId)
        {
            return replacements.TryGetValue(troopId, out itemId);
        }

        internal static string FindConfigPath(string fileName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(TroopReplacementConfig).Assembly.Location), "..", "..", "ModuleData", fileName)),
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "Modules", "Brainrot", "ModuleData", fileName)),
                Path.GetFullPath(Path.Combine(baseDirectory, "..", "Modules", "Brainrot", "ModuleData", fileName)),
                Path.GetFullPath(Path.Combine(baseDirectory, "Modules", "Brainrot", "ModuleData", fileName)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Brainrot", "ModuleData", fileName))
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
