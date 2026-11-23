using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Brainrot
{
    internal static class BrainrotTroopPortraits
    {
        private const string HarmonyId = "Brainrot.TroopPortraits";
        private static readonly HashSet<string> Reported = new HashSet<string>();
        private static Harmony harmony;

        internal static void Install()
        {
            // The existing preview integration owns the native action-table readiness check.
            if (harmony != null || !BrainrotPartyPreview.IsInstalled) return;

            MethodInfo characterFactory = AccessTools.DeclaredMethod(typeof(CharacterCode), "CreateFrom",
                new[] { typeof(BasicCharacterObject), typeof(Equipment) })
                ?? throw new MissingMethodException("CharacterCode.CreateFrom(BasicCharacterObject, Equipment)");
            Type partyVm = AccessTools.TypeByName("TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM")
                ?? throw new TypeLoadException("PartyCharacterVM was not found.");
            MethodInfo partyFactory = AccessTools.DeclaredMethod(partyVm, "GetCharacterCode")
                ?? throw new MissingMethodException(partyVm.FullName, "GetCharacterCode");
            if (partyFactory.ReturnType != typeof(CharacterCode) ||
                partyFactory.GetParameters().Length != 3 ||
                !typeof(BasicCharacterObject).IsAssignableFrom(partyFactory.GetParameters()[0].ParameterType))
            {
                throw new MissingMethodException("The installed PartyCharacterVM.GetCharacterCode signature changed.");
            }

            harmony = new Harmony(HarmonyId);
            try
            {
                var postfix = new HarmonyMethod(typeof(BrainrotTroopPortraits), nameof(AfterCharacterCode));
                harmony.Patch(characterFactory, postfix: postfix);
                harmony.Patch(partyFactory, postfix: postfix);
                BrainrotPreviewLog.Write("Portrait hooks installed: CharacterCode.CreateFrom(character, equipment) " +
                    "for troop-tree images and PartyCharacterVM.GetCharacterCode for party/upgrade icons.");
            }
            catch
            {
                Uninstall();
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (harmony == null) return;
            harmony.UnpatchAll(HarmonyId);
            harmony = null;
        }

        private static void AfterCharacterCode([HarmonyArgument(0)] BasicCharacterObject character,
            MethodBase __originalMethod, ref CharacterCode __result)
        {
            // Replace only the transient image description. Heroes require an explicit personal
            // choice; NPC troops use their catalog mapping. Hidden placeholders stay empty.
            if (character == null || __result == null || __result.IsEmpty) return;

            try
            {
                ItemObject item;
                string itemId;
                if (character.IsHero)
                {
                    if (!BrainrotHeroSkins.TryGetItem(character, out item)) return;
                    itemId = item.StringId;
                }
                else
                {
                    if (!BrainrotPartyPreview.TryGetReplacement(character.StringId, out itemId)) return;
                    item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                }
                if (item == null || item.ArmorComponent == null || string.IsNullOrWhiteSpace(item.MultiMeshName))
                {
                    ReportOnce("Portrait item unavailable: troop=" + character.StringId + "; item=" + itemId);
                    return;
                }
                if (!BrainrotPartyPreview.TryProbeMesh(item, out string meshDiagnostic))
                {
                    ReportOnce("Leaving native portrait intact: troop=" + character.StringId + "; " + meshDiagnostic);
                    return;
                }

                CharacterCode nativeCode = __result;
                Equipment source = nativeCode.CalculateEquipment();
                if (source == null) return;
                Equipment visual = BrainrotPartyPreview.CreateVisualEquipment(source, item);
                CharacterCode replacement = CreatePortraitCode(nativeCode, visual);
                __result = replacement;

                string origin = __originalMethod.DeclaringType.Name + "." + __originalMethod.Name;
                string diagnosticKey = origin + ":" + character.StringId;
                if (Reported.Add(diagnosticKey))
                {
                    BrainrotPreviewLog.Write("Portrait data prepared: source=" + origin +
                        "; troop=" + character.StringId + "; body=" + itemId + "; " + meshDiagnostic +
                        "; cache-key-changed=" + (nativeCode.Code != replacement.Code));
                }
            }
            catch (Exception exception)
            {
                ReportOnce("Portrait replacement failed for " + character.StringId + ": " + exception);
            }
        }

        internal static CharacterCode CreatePortraitCode(CharacterCode nativeCode, Equipment visual)
        {
            if (nativeCode == null) throw new ArgumentNullException(nameof(nativeCode));
            if (visual == null) throw new ArgumentNullException(nameof(visual));

            // A new code changes both ImageIdentifier.Id and the thumbnail RenderId before
            // the native cache is queried. No cached vanilla image needs to be overwritten.
            return CharacterCode.CreateFrom(visual.CalculateEquipmentCode(), nativeCode.BodyProperties,
                nativeCode.IsFemale, nativeCode.IsHero, nativeCode.Color1, nativeCode.Color2,
                nativeCode.FormationClass, nativeCode.Race);
        }

        private static void ReportOnce(string message)
        {
            if (Reported.Add(message)) BrainrotPreviewLog.Write(message);
        }
    }
}
