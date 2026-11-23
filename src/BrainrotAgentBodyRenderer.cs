using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ObjectSystem;

namespace Brainrot
{
    internal static class BrainrotAgentBodyRenderer
    {
        private const string Owner = "Brainrot.MissionVisuals";
        private static Harmony harmony;
        private static readonly ConditionalWeakTable<Agent, RenderEquipment> EquipmentCache =
            new ConditionalWeakTable<Agent, RenderEquipment>();
        private static readonly HashSet<string> Errors = new HashSet<string>();
        private static readonly MethodInfo SpawnGetter = AccessTools.PropertyGetter(typeof(Agent), "SpawnEquipment");
        private static readonly MethodInfo VisualGetter = AccessTools.Method(typeof(BrainrotAgentBodyRenderer), nameof(GetVisualEquipment));

        private sealed class RenderEquipment
        {
            internal Equipment Native;
            internal Equipment Visual;
        }

        internal static void Install()
        {
            if (harmony != null || !BrainrotPartyPreview.IsInstalled) return;
            harmony = new Harmony(Owner);
            try
            {
                harmony.Patch(AccessTools.DeclaredMethod(typeof(Agent), "AddSkinMeshes",
                    new[] { typeof(bool), typeof(bool), typeof(int) }), transpiler: Patch(nameof(SkinInstructions)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(MissionScreen),
                    "TaleWorlds.MountAndBlade.IMissionListener.OnEquipItemsFromSpawnEquipment"),
                    transpiler: Patch(nameof(ArmorInstructions)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(Agent), "EquipItemsFromSpawnEquipment",
                    new[] { typeof(bool), typeof(bool), typeof(bool), typeof(int) }),
                    prefix: Patch(nameof(BeginEquipment)), postfix: Patch(nameof(EndEquipment)));
                BrainrotPreviewLog.Write("Mission visuals v2: native skin/armor construction hooks installed; combat equipment getter unchanged.");
            }
            catch { Uninstall(); throw; }
        }

        internal static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static HarmonyMethod Patch(string name) => new HarmonyMethod(typeof(BrainrotAgentBodyRenderer), name);

        // Only the two visual construction methods call this getter. The actual Agent getter,
        // combat updates, mission weapons, and equipment rosters always retain native data.
        internal static Equipment GetVisualEquipment(Agent agent)
        {
            Equipment source = agent.SpawnEquipment;
            if (!agent.IsHuman || agent.Character == null || source == null) return source;
            try
            {
                if (EquipmentCache.TryGetValue(agent, out RenderEquipment cached) &&
                    ReferenceEquals(cached.Native, source)) return cached.Visual;
                ItemObject item;
                if (agent.Character.IsHero)
                {
                    if (!BrainrotHeroSkins.TryGetItem(agent.Character, out item)) return source;
                }
                else
                {
                    if (!BrainrotPartyPreview.TryGetReplacement(agent.Character.StringId, out string id)) return source;
                    item = MBObjectManager.Instance.GetObject<ItemObject>(id);
                    if (item?.ArmorComponent == null || !BrainrotPartyPreview.TryProbeMesh(item, out _)) return source;
                }
                var result = new RenderEquipment {
                    Native = source, Visual = BrainrotPartyPreview.CreateVisualEquipment(source, item)
                };
                EquipmentCache.Remove(agent);
                EquipmentCache.Add(agent, result);
                return result.Visual;
            }
            catch (Exception error)
            {
                if (Errors.Add(error.ToString())) BrainrotPreviewLog.Write("Native mission visual lookup failed: " + error);
                return source;
            }
        }

        private static void BeginEquipment(Agent __instance)
        {
            EquipmentCache.Remove(__instance);
            // Native construction clears the previous body components. Drop the managed reference
            // to the previous cape simulator before that happens; native armor setup registers a new one.
            __instance.SetCapeClothSimulator(null);
        }

        private static void EndEquipment(Agent __instance)
        {
            if (EquipmentCache.TryGetValue(__instance, out RenderEquipment cached))
                BrainrotPreviewLog.Write("Native mission appearance built: character=" + __instance.Character.StringId +
                    "; body=" + cached.Visual[EquipmentIndex.Body].Item?.StringId +
                    "; source-unchanged=" + ReferenceEquals(cached.Native, __instance.SpawnEquipment));
        }

        private static IEnumerable<CodeInstruction> SkinInstructions(IEnumerable<CodeInstruction> instructions)
        {
            return RewriteEquipmentReads(instructions, false);
        }

        private static IEnumerable<CodeInstruction> ArmorInstructions(IEnumerable<CodeInstruction> instructions)
        {
            return RewriteEquipmentReads(instructions, true);
        }

        internal static IEnumerable<CodeInstruction> RewriteEquipmentReads(IEnumerable<CodeInstruction> instructions, bool preserveLast)
        {
            var result = instructions.Select(instruction => new CodeInstruction(instruction)).ToList();
            var reads = result.Select((instruction, index) => new { instruction, index })
                .Where(entry => entry.instruction.Calls(SpawnGetter)).Select(entry => entry.index).ToList();
            if (reads.Count < (preserveLast ? 2 : 1))
                throw new MissingMethodException("Native visual equipment read pattern changed.");
            // The last armor-method read controls body armor material (sounds/impacts), not geometry.
            // Leave that read native as well as every combat/property update outside these methods.
            int count = reads.Count - (preserveLast ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                result[reads[i]].opcode = OpCodes.Call;
                result[reads[i]].operand = VisualGetter;
            }
            return result;
        }
    }
}