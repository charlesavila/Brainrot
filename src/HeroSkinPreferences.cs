using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Brainrot
{
    [DataContract]
    internal sealed class HeroSkinPreferences
    {
        [DataMember(Order = 0)] public int Version = 1;
        [DataMember(Order = 1)] public string CampaignId;
        [DataMember(Order = 2)] public Dictionary<string, string> Heroes = new Dictionary<string, string>();

        internal static string FilePath(string directory, string campaignId)
        {
            if (string.IsNullOrWhiteSpace(campaignId)) throw new ArgumentException("Campaign ID is required.");
            using (var hash = SHA256.Create())
                return Path.Combine(directory, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(campaignId)))
                    .Replace("-", "").ToLowerInvariant() + ".json");
        }

        internal static HeroSkinPreferences Load(string directory, string campaignId)
        {
            string path = FilePath(directory, campaignId);
            if (!File.Exists(path)) return new HeroSkinPreferences { CampaignId = campaignId };
            using (var stream = File.OpenRead(path))
            {
                var result = (HeroSkinPreferences)new DataContractJsonSerializer(typeof(HeroSkinPreferences)).ReadObject(stream);
                if (result == null || result.Version != 1 || result.CampaignId != campaignId || result.Heroes == null)
                    throw new InvalidDataException("Unrecognized or mismatched skin preferences.");
                return result;
            }
        }

        internal void Save(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = FilePath(directory, CampaignId);
            string temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                new DataContractJsonSerializer(typeof(HeroSkinPreferences)).WriteObject(stream, this);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
    }
}
