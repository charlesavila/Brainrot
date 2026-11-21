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
            string configPath = FindConfigPath("character_replacements.xml");

            if (configPath != null)
            {
                XmlDocument catalogDocument = new XmlDocument();
                catalogDocument.Load(configPath);
                XmlNodeList characters = catalogDocument.SelectNodes("/BrainrotCharacters/Character");

                if (characters != null)
                {
                    foreach (XmlElement element in characters)
                    {
                        string characterId = element.GetAttribute("id").Trim();
                        string meshName = element.GetAttribute("mesh").Trim();
                        string troopId = element.GetAttribute("troop_id").Trim();

                        if (string.IsNullOrWhiteSpace(characterId) ||
                            string.IsNullOrWhiteSpace(meshName) ||
                            string.IsNullOrWhiteSpace(troopId))
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
                        Debug.Print("Brainrot: loaded character " + characterId +
                                    " with mesh " + meshName +
                                    " for troop " + troopId +
                                    " -> " + itemId);
                    }
                }
                else
                {
                    Debug.Print("Brainrot: character_replacements.xml has no Character entries.");
                }
            }
            else
            {
                Debug.Print("Brainrot: character_replacements.xml was not found; loading legacy replacements.");
            }

            configPath = FindConfigPath("troop_replacements.xml");

            if (configPath == null)
            {
                return config;
            }

            XmlDocument legacyDocument = new XmlDocument();
            legacyDocument.Load(configPath);

            XmlNodeList nodes = legacyDocument.SelectNodes("/TroopReplacements/TroopReplacement");
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

        private static string FindConfigPath(string fileName)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates =
            {
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
