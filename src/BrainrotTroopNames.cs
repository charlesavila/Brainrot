using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Brainrot
{
    internal static class BrainrotTroopNames
    {
        private const string HarmonyId = "Brainrot.TroopNames";
        private static readonly Dictionary<string, TextObject> DisplayNames =
            new Dictionary<string, TextObject>(StringComparer.OrdinalIgnoreCase);
        private static Harmony harmony;

        internal static void Install()
        {
            if (harmony != null) return;

            DisplayNames.Clear();
            foreach (var entry in BrainrotSubModule.ReplacementConfig.DisplayNames)
                DisplayNames.Add(entry.Key, new TextObject("{=!}" + entry.Value));

            MethodInfo basicName = AccessTools.DeclaredPropertyGetter(typeof(BasicCharacterObject), nameof(BasicCharacterObject.Name))
                ?? throw new MissingMethodException("BasicCharacterObject.Name");
            MethodInfo campaignName = AccessTools.DeclaredPropertyGetter(typeof(CharacterObject), nameof(CharacterObject.Name))
                ?? throw new MissingMethodException("CharacterObject.Name");

            harmony = new Harmony(HarmonyId);
            try
            {
                var prefix = new HarmonyMethod(typeof(BrainrotTroopNames), nameof(BeforeGetName));
                harmony.Patch(basicName, prefix: prefix);
                harmony.Patch(campaignName, prefix: prefix);
                BrainrotPreviewLog.Write("Troop name hooks installed: " + DisplayNames.Count +
                    " display names from character_replacements.xml.");
            }
            catch
            {
                Uninstall();
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                harmony = null;
            }
            DisplayNames.Clear();
        }

        private static bool BeforeGetName(BasicCharacterObject __instance, ref TextObject __result)
        {
            if (__instance == null || __instance.IsHero || string.IsNullOrEmpty(__instance.StringId) ||
                !DisplayNames.TryGetValue(__instance.StringId, out TextObject name)) return true;

            // Override only the returned label. The native name, troop ID and save data stay intact.
            __result = name;
            return false;
        }
    }
}
