using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Tableaus;
using TaleWorlds.ObjectSystem;
using TaleWorlds.ScreenSystem;

namespace Brainrot
{
    internal static class BrainrotPartyPreview
    {
        private const string HarmonyId = "Brainrot.PartyPreview";
        private static readonly ConditionalWeakTable<CharacterTableau, PreviewBuild> Pending =
            new ConditionalWeakTable<CharacterTableau, PreviewBuild>();
        private static readonly HashSet<string> ReportedProblems = new HashSet<string>();
        private static readonly Dictionary<string, string> MeshProbes = new Dictionary<string, string>();
        private static Harmony harmony;
        private static Type partyScreenType;
        private static FieldInfo partyDataSource;
        private static PropertyInfo selectedCharacter;
        private static PropertyInfo selectedCharacterId;
        private static FieldInfo equipment;
        private static FieldInfo characterId;
        private static FieldInfo visualsDirty;
        private static FieldInfo agentVisuals;
        private static FieldInfo oldAgentVisuals;
        private sealed class HeroRefresh { internal string Id; internal int Revision = -1; }
        private static readonly ConditionalWeakTable<CharacterTableau, HeroRefresh> HeroRefreshes =
            new ConditionalWeakTable<CharacterTableau, HeroRefresh>();

        internal static bool IsInstalled => harmony != null;

        internal static void Install()
        {
            if (harmony != null)
            {
                return;
            }

            // Preparing methods that use ActionIndexCache can initialize its static action IDs.
            // Never do that while Bannerlord's native action table is still empty.
            int actionCount = MBAnimation.GetNumActionCodes();
            if (actionCount <= 0)
            {
                BrainrotPreviewLog.Write("Preview hooks deferred: native action table is empty.");
                return;
            }
            int idleIndex = MBAnimation.GetActionCodeWithName("act_inventory_idle");
            if (idleIndex < 0)
            {
                BrainrotPreviewLog.Write("Preview hooks deferred: inventory idle action is not loaded.");
                return;
            }
            int cachedIdleIndex = GetCachedInventoryIdleIndex();
            BrainrotPreviewLog.Write("Installing preview hooks after native action loading: actions=" + actionCount +
                "; inventory-idle=" + idleIndex + "; cached-inventory-idle=" + cachedIdleIndex);
            if (cachedIdleIndex != idleIndex)
            {
                BrainrotPreviewLog.Write("Preview hooks skipped: animation cache was initialized before actions loaded. " +
                    "A fresh game process is required after correcting early mod initialization.");
                return;
            }

            Type tableauType = typeof(CharacterTableau);
            equipment = RequireField(tableauType, "_equipment");
            characterId = RequireField(tableauType, "_charStringId");
            visualsDirty = RequireField(tableauType, "_isVisualsDirty");
            agentVisuals = RequireField(tableauType, "_agentVisuals");
            oldAgentVisuals = RequireField(tableauType, "_oldAgentVisuals");
            partyScreenType = AccessTools.TypeByName("SandBox.GauntletUI.GauntletPartyScreen")
                ?? throw new TypeLoadException("GauntletPartyScreen was not found.");
            partyDataSource = RequireField(partyScreenType, "_dataSource");
            selectedCharacter = partyDataSource.FieldType.GetProperty("SelectedCharacter")
                ?? throw new MissingMemberException("PartyVM.SelectedCharacter");
            selectedCharacterId = selectedCharacter.PropertyType.GetProperty("CharStringId")
                ?? throw new MissingMemberException("SelectedCharacter.CharStringId");

            harmony = new Harmony(HarmonyId);
            try
            {
                PatchBuild("InitializeAgentVisuals", Type.EmptyTypes);
                PatchBuild("RefreshCharacterTableau", new[] { typeof(Equipment) });
                harmony.Patch(RequireMethod(tableauType, "SetCharStringID", new[] { typeof(string) }),
                    prefix: Patch(nameof(BeforeCharacterChange)),
                    postfix: Patch(nameof(AfterCharacterChange)));
                harmony.Patch(RequireMethod(tableauType, "OnTick", new[] { typeof(float) }),
                    prefix: Patch(nameof(BeforeTableauTick)),
                    postfix: Patch(nameof(AfterTableauTick)));
                BrainrotPreviewLog.Write("Hooks installed: InitializeAgentVisuals, " +
                    "RefreshCharacterTableau(Equipment oldEquipment), SetCharStringID, OnTick. " +
                    "Harmony=" + typeof(Harmony).Assembly.GetName().Version +
                    "; View=" + tableauType.Assembly.Location);
            }
            catch
            {
                Uninstall();
                throw;
            }
        }

        // Keep this field access out of Install's JIT compilation before the readiness checks.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetCachedInventoryIdleIndex()
        {
            return ActionIndexCache.act_inventory_idle.Index;
        }

        internal static void Uninstall()
        {
            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                harmony = null;
            }
        }

        private static void PatchBuild(string name, Type[] parameters)
        {
            harmony.Patch(RequireMethod(typeof(CharacterTableau), name, parameters),
                prefix: Patch(nameof(BeforeBuild)),
                postfix: Patch(nameof(AfterBuild)),
                finalizer: Patch(nameof(FinishBuild)));
        }

        private static HarmonyMethod Patch(string method)
        {
            return new HarmonyMethod(typeof(BrainrotPartyPreview), method);
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }

        private static MethodInfo RequireMethod(Type type, string name, Type[] parameters)
        {
            return AccessTools.DeclaredMethod(type, name, parameters)
                ?? throw new MissingMethodException(type.FullName, name);
        }

        private static bool IsPartyScreen()
        {
            return ScreenManager.TopScreen != null && partyScreenType.IsInstanceOfType(ScreenManager.TopScreen);
        }

        private static string SelectedPartyTroopId()
        {
            object dataSource = partyDataSource.GetValue(ScreenManager.TopScreen);
            object selected = dataSource == null ? null : selectedCharacter.GetValue(dataSource);
            return selected == null ? null : selectedCharacterId.GetValue(selected) as string;
        }

        private static bool IsSelectedPartyTableau(string troopId)
        {
            if (!IsPartyScreen() || string.IsNullOrEmpty(troopId))
            {
                return false;
            }

            return string.Equals(NormalizeId(SelectedPartyTroopId()), NormalizeId(troopId), StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrEmpty(id) ? id : id.Substring(id.LastIndexOf('.') + 1);
        }

        internal static bool TryGetReplacement(string troopId, out string itemId)
        {
            itemId = null;
            return !string.IsNullOrEmpty(troopId) &&
                (BrainrotSubModule.ReplacementConfig.TryGetItemId(troopId, out itemId) ||
                 BrainrotSubModule.ReplacementConfig.TryGetItemId(NormalizeId(troopId), out itemId));
        }

        private static void BeforeBuild(CharacterTableau __instance, MethodBase __originalMethod,
            out PreviewBuild __state)
        {
            __state = null;
            try
            {
                string troopId = characterId.GetValue(__instance) as string;
                var heroCharacter = BrainrotHeroSkins.Character(troopId);
                ItemObject item;
                string itemId;
                if (BrainrotHeroSkins.TryGetItem(heroCharacter, out item)) itemId = item.StringId;
                else
                {
                    if (heroCharacter?.IsHero == true || !IsSelectedPartyTableau(troopId) ||
                        !TryGetReplacement(troopId, out itemId)) return;
                    item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                }
                if (item == null || item.ArmorComponent == null || string.IsNullOrWhiteSpace(item.MultiMeshName))
                {
                    ReportOnce("Invalid preview item for " + troopId + ": " + itemId);
                    return;
                }

                Equipment source = equipment.GetValue(__instance) as Equipment;
                if (source == null)
                {
                    ReportOnce("No tableau equipment available for " + troopId);
                    return;
                }

                if (!TryProbeMesh(item, out string meshDiagnostic))
                {
                    ReportOnce("Leaving native preview intact for " + troopId + ": " + meshDiagnostic);
                    return;
                }

                Equipment preview = CreateVisualEquipment(source, item);
                var build = new PreviewBuild
                {
                    OriginalEquipment = source,
                    PreviewEquipment = preview,
                    OriginalEquipmentCode = source.CalculateEquipmentCode(),
                    TroopId = troopId,
                    Item = item,
                    MeshDiagnostic = meshDiagnostic,
                    Phase = __originalMethod.Name
                };
                equipment.SetValue(__instance, preview);
                __state = build;
            }
            catch (Exception exception)
            {
                ReportOnce("Preview preparation failed: " + exception);
            }
        }

        internal static Equipment CreateVisualEquipment(Equipment source, ItemObject item)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (item == null) throw new ArgumentNullException(nameof(item));

            Equipment preview = source.Clone(false);
            preview.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, new EquipmentElement(item));
            preview.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Gloves, EquipmentElement.Invalid);
            preview.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, EquipmentElement.Invalid);
            return preview;
        }

        private static void AfterBuild(CharacterTableau __instance, PreviewBuild __state)
        {
            if (__state == null) return;
            try
            {
                __state.PreparedVisuals = agentVisuals.GetValue(__instance) as AgentVisuals;
                Pending.Remove(__instance);
                Pending.Add(__instance, __state);
                BrainrotPreviewLog.Write(__state.Phase + ": troop=" + __state.TroopId +
                    "; expected=" + __state.Item.StringId + "; " + __state.MeshDiagnostic +
                    "; new-buffer-body=" + BodyItemId(__state.PreparedVisuals) +
                    "; old-buffer-body=" + BodyItemId(oldAgentVisuals.GetValue(__instance) as AgentVisuals) +
                    "; source-unchanged=" +
                    (__state.OriginalEquipmentCode == __state.OriginalEquipment.CalculateEquipmentCode()));
            }
            catch (Exception exception)
            {
                ReportOnce("Preview build diagnostics failed: " + exception);
            }
        }

        private static Exception FinishBuild(CharacterTableau __instance, PreviewBuild __state, Exception __exception)
        {
            if (__state != null)
            {
                try
                {
                    // The renderer retains the clone. Restore only our temporary field substitution.
                    if (ReferenceEquals(equipment.GetValue(__instance), __state.PreviewEquipment))
                    {
                        equipment.SetValue(__instance, __state.OriginalEquipment);
                    }
                    if (__exception != null)
                    {
                        Pending.Remove(__instance);
                        BrainrotPreviewLog.Write("Native preview build failed for " + __state.TroopId + ": " + __exception);
                    }
                }
                catch (Exception exception)
                {
                    ReportOnce("Preview equipment restoration failed: " + exception);
                }
            }
            return __exception;
        }

        private static void BeforeCharacterChange(CharacterTableau __instance,
            [HarmonyArgument(0)] string troopId, out bool __state)
        {
            __state = false;
            try
            {
                string previousId = characterId.GetValue(__instance) as string;
                if (IsPartyScreen() && !string.Equals(previousId, troopId, StringComparison.Ordinal))
                {
                    bool mapped = TryGetReplacement(troopId, out string itemId);
                    BrainrotPreviewLog.Write("Party selection input: tableau=" + troopId +
                        "; selected=" + SelectedPartyTroopId() + "; mapping=" + (mapped ? itemId : "native"));
                    __state = mapped || TryGetReplacement(previousId, out _);
                }
            }
            catch (Exception exception)
            {
                ReportOnce("Preview selection tracking failed: " + exception);
            }
        }

        private static void AfterCharacterChange(CharacterTableau __instance, bool __state)
        {
            if (!__state) return;
            try
            {
                Pending.Remove(__instance);
                visualsDirty.SetValue(__instance, true);
            }
            catch (Exception exception)
            {
                ReportOnce("Preview selection refresh failed: " + exception);
            }
        }

        private static void BeforeTableauTick(CharacterTableau __instance)
        {
            try
            {
                string id = characterId.GetValue(__instance) as string;
                if (!BrainrotHeroSkins.Eligible(BrainrotHeroSkins.Character(id)?.HeroObject)) return;
                var state = HeroRefreshes.GetValue(__instance, _ => new HeroRefresh());
                if (state.Id == id && state.Revision == BrainrotHeroSkins.Revision) return;
                state.Id = id;
                state.Revision = BrainrotHeroSkins.Revision;
                visualsDirty.SetValue(__instance, true);
            }
            catch (Exception error) { ReportOnce("Hero preview refresh failed: " + error); }
        }

        private static void AfterTableauTick(CharacterTableau __instance)
        {
            if (!Pending.TryGetValue(__instance, out PreviewBuild build)) return;
            try
            {
                if (!IsSelectedPartyTableau(build.TroopId) &&
                    !BrainrotHeroSkins.Eligible(BrainrotHeroSkins.Character(build.TroopId)?.HeroObject))
                {
                    Pending.Remove(__instance);
                    return;
                }

                GameEntity entity = build.PreparedVisuals == null ? null : build.PreparedVisuals.GetEntity();
                if (entity != null && entity.GetVisibilityExcludeParents())
                {
                    BrainrotPreviewLog.Write("Buffer visible: troop=" + build.TroopId +
                        "; body=" + BodyItemId(build.PreparedVisuals) +
                        "; resources-ready=" + entity.CheckResources(false, false));
                    Pending.Remove(__instance);
                }
                else if (++build.WaitTicks >= 300)
                {
                    BrainrotPreviewLog.Write("Buffer still hidden after 300 UI ticks: troop=" + build.TroopId);
                    Pending.Remove(__instance);
                }
            }
            catch (Exception exception)
            {
                Pending.Remove(__instance);
                ReportOnce("Preview visibility diagnostics failed: " + exception);
            }
        }

        private static string BodyItemId(AgentVisuals visuals)
        {
            Equipment rendered = visuals == null ? null : visuals.GetEquipment();
            if (rendered == null) return "none";
            EquipmentElement body = rendered.GetEquipmentFromSlot(EquipmentIndex.Body);
            return (body.CosmeticItem ?? body.Item)?.StringId ?? "empty";
        }

        internal static bool TryProbeMesh(ItemObject item, out string result)
        {
            if (MeshProbes.TryGetValue(item.MultiMeshName, out result)) return true;
            MetaMesh mesh = null;
            try
            {
                // CreateMetaMesh creates an empty container; GetCopy loads the named resource.
                mesh = MetaMesh.GetCopy(item.MultiMeshName, false, true);
                result = "mesh=" + item.MultiMeshName + "; probe-mesh-count=" + (mesh == null ? 0 : mesh.MeshCount);
                bool valid = mesh != null && mesh.MeshCount > 0;
                if (valid) MeshProbes.Add(item.MultiMeshName, result);
                return valid;
            }
            finally
            {
                if (mesh != null) mesh.ManualInvalidate();
            }
        }

        private static void ReportOnce(string message)
        {
            if (ReportedProblems.Add(message)) BrainrotPreviewLog.Write(message);
        }

        private sealed class PreviewBuild
        {
            internal Equipment OriginalEquipment;
            internal Equipment PreviewEquipment;
            internal string OriginalEquipmentCode;
            internal string TroopId;
            internal ItemObject Item;
            internal string MeshDiagnostic;
            internal string Phase;
            internal AgentVisuals PreparedVisuals;
            internal int WaitTicks;
        }
    }
}
