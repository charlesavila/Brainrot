using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Brainrot
{
    internal static class BrainrotHeroSkins
    {
        internal sealed class Skin
        {
            internal string Id;
            internal string Name;
            internal ItemObject Item;
        }

        private static readonly string DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Brainrot", "HeroSkins");
        private static Campaign campaign;
        private static HeroSkinPreferences preferences;
        private static Dictionary<string, string> pending;
        internal static int Revision { get; private set; }

        private static bool EnsureCampaign()
        {
            if (Campaign.Current == null || string.IsNullOrEmpty(Campaign.Current.UniqueGameId)) return false;
            if (ReferenceEquals(campaign, Campaign.Current)) return true;
            campaign = Campaign.Current;
            pending = null;
            try { preferences = HeroSkinPreferences.Load(DirectoryPath, campaign.UniqueGameId); }
            catch (Exception error)
            {
                BrainrotPreviewLog.Write("Hero skin preferences unavailable; using defaults: " + error.Message);
                preferences = new HeroSkinPreferences { CampaignId = campaign.UniqueGameId };
            }
            Revision++;
            return true;
        }

        internal static bool Eligible(Hero hero)
        {
            return hero != null && Campaign.Current != null &&
                (hero == Hero.MainHero || (hero.CompanionOf != null && hero.CompanionOf == Clan.PlayerClan));
        }

        internal static CharacterObject Character(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || Campaign.Current == null) return null;
            return MBObjectManager.Instance.GetObject<CharacterObject>(characterId);
        }

        internal static List<Skin> Available()
        {
            var skins = new List<Skin>();
            string path = TroopReplacementConfig.FindConfigPath("character_replacements.xml");
            if (path == null) return skins;
            var xml = new XmlDocument();
            xml.Load(path);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlElement entry in xml.SelectNodes("/BrainrotCharacters/Character"))
            {
                string id = entry.GetAttribute("id").Trim();
                if (id.Length == 0 || !ids.Add(id)) continue;
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(id + "_body_armor");
                if (item?.ArmorComponent == null || item.MultiMeshName != entry.GetAttribute("mesh") ||
                    !BrainrotPartyPreview.TryProbeMesh(item, out _)) continue;
                string name = entry.GetAttribute("display_name").Trim();
                if (name.Length == 0) name = item.Name.ToString();
                if (name.EndsWith(" Costume", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 8);
                skins.Add(new Skin { Id = id, Name = name, Item = item });
            }
            return skins.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        internal static string Selection(Hero hero)
        {
            if (!Eligible(hero) || !EnsureCampaign()) return null;
            (pending ?? preferences.Heroes).TryGetValue(hero.StringId, out string key);
            return key;
        }

        internal static bool TryGetItem(BasicCharacterObject character, out ItemObject item)
        {
            item = null;
            var hero = (character as CharacterObject)?.HeroObject;
            string selection = Selection(hero);
            if (string.IsNullOrEmpty(selection)) return false;
            if (!BrainrotSubModule.ReplacementConfig.CharacterIds.Contains(selection)) return false;
            var resolved = MBObjectManager.Instance.GetObject<ItemObject>(selection + "_body_armor");
            if (resolved?.ArmorComponent == null || string.IsNullOrEmpty(resolved.MultiMeshName) ||
                !BrainrotPartyPreview.TryProbeMesh(resolved, out _)) return false;
            item = resolved;
            return true;
        }

        internal static void Begin()
        {
            if (EnsureCampaign()) pending = new Dictionary<string, string>(preferences.Heroes);
            Revision++;
        }

        internal static void Choose(Hero hero, string skin)
        {
            if (!Eligible(hero) || !EnsureCampaign() || pending == null) return;
            if (string.IsNullOrEmpty(skin)) pending.Remove(hero.StringId);
            else pending[hero.StringId] = skin;
            Revision++;
        }

        internal static void Reset()
        {
            if (preferences != null && pending != null) pending = new Dictionary<string, string>(preferences.Heroes);
            Revision++;
        }

        internal static void Commit()
        {
            if (!EnsureCampaign() || pending == null) return;
            var next = new HeroSkinPreferences { CampaignId = preferences.CampaignId, Heroes = new Dictionary<string, string>(pending) };
            next.Save(DirectoryPath);
            preferences = next;
            Revision++;
            BrainrotPreviewLog.Write("Hero skin choices saved for campaign " + preferences.CampaignId);
        }

        internal static void End()
        {
            pending = null;
            Revision++;
        }
    }
}
