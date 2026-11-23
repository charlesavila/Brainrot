param(
    [string]$ModuleRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$HarmonyDirectory
)

$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath (Join-Path $ModuleRoot '../..')).Path
if (!$HarmonyDirectory) {
    $localHarmony = Join-Path $gameRoot 'Modules/Bannerlord.Harmony/bin/Win64_Shipping_Client'
    $workshopHarmony = Join-Path $gameRoot '../../workshop/content/261550/2859188632/bin/Win64_Shipping_Client'
    $HarmonyDirectory = if (Test-Path -LiteralPath $localHarmony) { $localHarmony } else { $workshopHarmony }
}
$assemblyDirectories = @(
    (Join-Path $gameRoot 'bin/Win64_Shipping_Client'),
    (Join-Path $gameRoot 'Modules/Native/bin/Win64_Shipping_Client'),
    (Join-Path $gameRoot 'Modules/SandBox/bin/Win64_Shipping_Client'),
    $HarmonyDirectory,
    (Join-Path $ModuleRoot 'bin/Release')
)
$resolver = [System.ResolveEventHandler]{
    param($sender, $eventArgs)
    $assemblyFile = ([System.Reflection.AssemblyName]$eventArgs.Name).Name + '.dll'
    foreach ($directory in $assemblyDirectories) {
        $candidate = Join-Path $directory $assemblyFile
        if (Test-Path -LiteralPath $candidate) {
            return [System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $candidate).Path)
        }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

try {
    $references = @(
        'System.Core.dll',
        'System.Xml.dll',
        'System.Runtime.Serialization.dll',
        ([System.Reflection.Assembly]::Load('netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51').Location)
    )
    foreach ($name in @('TaleWorlds.Library', 'TaleWorlds.ObjectSystem', 'TaleWorlds.SaveSystem', 'TaleWorlds.Core',
            'TaleWorlds.CampaignSystem', 'TaleWorlds.Core.ViewModelCollection', 'TaleWorlds.Localization',
            'TaleWorlds.Engine.GauntletUI', 'TaleWorlds.GauntletUI', 'TaleWorlds.ScreenSystem',
            'TaleWorlds.DotNet', 'TaleWorlds.Engine', 'TaleWorlds.MountAndBlade',
            'TaleWorlds.MountAndBlade.View', 'SandBox.GauntletUI', '0Harmony', 'Brainrot')) {
        foreach ($directory in $assemblyDirectories) {
            $candidate = Join-Path $directory ($name + '.dll')
            if (Test-Path -LiteralPath $candidate) {
                $loaded = [System.Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $candidate).Path)
                $references += $loaded.Location
                break
            }
        }
    }

    # Uses the installed managed assemblies; no game or native renderer is started.
    Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Tableaus;
using TaleWorlds.MountAndBlade.View.Tableaus.Thumbnails;
using Path = System.IO.Path;

public static class BrainrotPreviewChecks
{
    private static readonly Type Preview = Assembly.Load("Brainrot").GetType("Brainrot.BrainrotPartyPreview", true);
    private static readonly Type Portraits = Assembly.Load("Brainrot").GetType("Brainrot.BrainrotTroopPortraits", true);
    private static readonly Type Selector = Assembly.Load("Brainrot").GetType("Brainrot.BrainrotSkinSelector", true);
    private static readonly Type Renderer = Assembly.Load("Brainrot").GetType("Brainrot.BrainrotAgentBodyRenderer", true);
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
    private static int SimulatedActionCount;
    private static int EarlyActionLookups;

    private static bool SuppressTestLog()
    {
        return false;
    }

    private static bool ActionCountPrefix(ref int __result)
    {
        __result = SimulatedActionCount;
        return false;
    }

    private static bool ActionLookupPrefix(ref int __result)
    {
        if (SimulatedActionCount == 0) EarlyActionLookups++;
        __result = SimulatedActionCount == 0 ? -1 : 7;
        return false;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }

    private static bool AcceptSkinFixture(ref bool __result) { __result = true; return false; }
    private static bool FailSkinSave;
    private static bool FakeSkinSave()
    {
        if (FailSkinSave) throw new IOException("Simulated preferences write failure");
        return false;
    }

    private static void RunSkinChecks()
    {
        Assembly module = Preview.Assembly;
        Type store = module.GetType("Brainrot.HeroSkinPreferences");
        Type skins = module.GetType("Brainrot.BrainrotHeroSkins");
        var heroesField = store.GetField("Heroes");
        var campaignField = store.GetField("CampaignId");
        var save = store.GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance);
        var load = store.GetMethod("Load", Hidden);
        var pathMethod = store.GetMethod("FilePath", Hidden);
        string directory = Path.Combine(Path.GetTempPath(), "BrainrotSkinChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            object data = Activator.CreateInstance(store, true);
            campaignField.SetValue(data, "campaign/a");
            var choices = (Dictionary<string, string>)heroesField.GetValue(data);
            choices["player"] = "shrek";
            choices["companion"] = "mike";
            string path = (string)pathMethod.Invoke(null, new object[] { directory, "campaign/a" });
            string otherPath = (string)pathMethod.Invoke(null, new object[] { directory, "campaign/b" });
            Check(path != otherPath && Path.GetDirectoryName(path) == directory,
                "Campaign preference paths are isolated and cannot escape the preferences directory");
            save.Invoke(data, new object[] { directory });
            object restored = load.Invoke(null, new object[] { directory, "campaign/a" });
            var restoredChoices = (Dictionary<string, string>)heroesField.GetValue(restored);
            Check(restoredChoices["player"] == "shrek" && restoredChoices["companion"] == "mike",
                "Different hero skin selections survive a JSON save and reload");
            Check(!File.ReadAllText(path).Contains("TaleWorlds") && File.ReadAllText(path).Contains("shrek"),
                "Preferences contain only campaign, hero and skin identifiers, without saved game objects");
            choices.Remove("player");
            save.Invoke(data, new object[] { directory });
            restored = load.Invoke(null, new object[] { directory, "campaign/a" });
            Check(!((Dictionary<string, string>)heroesField.GetValue(restored)).ContainsKey("player") && File.Exists(path + ".bak"),
                "Default restoration persists and atomic replacement preserves a previous preference file");
            string validJson = File.ReadAllText(path);
            File.WriteAllText(path, "invalid-json");
            bool rejected = false;
            try { load.Invoke(null, new object[] { directory, "campaign/a" }); }
            catch (TargetInvocationException) { rejected = true; }
            Check(rejected, "Corrupt preference files are rejected for the runtime default fallback");
            File.WriteAllText(path, validJson);

            var prefsField = AccessTools.Field(skins, "preferences");
            var pendingField = AccessTools.Field(skins, "pending");
            object previousPrefs = prefsField.GetValue(null), previousPending = pendingField.GetValue(null);
            var mocks = new Harmony("Brainrot.PreviewChecks.SkinState");
            mocks.Patch(AccessTools.Method(skins, "EnsureCampaign"), prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "AcceptSkinFixture"));
            mocks.Patch(AccessTools.Method(skins, "Eligible"), prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "AcceptSkinFixture"));
            mocks.Patch(save, prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "FakeSkinSave"));
            try
            {
                choices["player"] = "shrek";
                prefsField.SetValue(null, data);
                pendingField.SetValue(null, null);
                var player = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero)); player.StringId = "player";
                var companion = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero)); companion.StringId = "companion";
                skins.GetMethod("Begin", Hidden).Invoke(null, null);
                skins.GetMethod("Choose", Hidden).Invoke(null, new object[] { player, "luigi" });
                var select = skins.GetMethod("Selection", Hidden);
                Check((string)select.Invoke(null, new object[] { player }) == "luigi" &&
                    (string)select.Invoke(null, new object[] { companion }) == "mike" && choices["player"] == "shrek",
                    "Inventory preview choices are isolated per hero and leave committed choices unchanged");
                skins.GetMethod("Reset", Hidden).Invoke(null, null);
                Check((string)select.Invoke(null, new object[] { player }) == "shrek", "Inventory Reset restores the original skin selection");
                skins.GetMethod("Choose", Hidden).Invoke(null, new object[] { player, "luigi" });
                skins.GetMethod("End", Hidden).Invoke(null, null);
                Check((string)select.Invoke(null, new object[] { player }) == "shrek", "Closing without a successful Done discards pending skin changes");
                skins.GetMethod("Begin", Hidden).Invoke(null, null);
                skins.GetMethod("Choose", Hidden).Invoke(null, new object[] { player, "luigi" });
                skins.GetMethod("Commit", Hidden).Invoke(null, null);
                skins.GetMethod("End", Hidden).Invoke(null, null);
                Check((string)select.Invoke(null, new object[] { player }) == "luigi", "Successful inventory completion commits the selected skin");
                skins.GetMethod("Begin", Hidden).Invoke(null, null);
                skins.GetMethod("Choose", Hidden).Invoke(null, new object[] { player, "shrek" });
                FailSkinSave = true;
                try { skins.GetMethod("Commit", Hidden).Invoke(null, null); }
                catch (TargetInvocationException) { }
                skins.GetMethod("End", Hidden).Invoke(null, null);
                Check((string)select.Invoke(null, new object[] { player }) == "luigi", "A failed preference write retains the last committed skin");
                FailSkinSave = false;
                skins.GetMethod("Begin", Hidden).Invoke(null, null);
                Type skinType = skins.GetNestedType("Skin", BindingFlags.NonPublic);
                var options = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(skinType));
                foreach (string key in new[] { "shrek", "mike", "luigi" })
                {
                    object skin = Activator.CreateInstance(skinType, true);
                    AccessTools.Field(skinType, "Id").SetValue(skin, key);
                    AccessTools.Field(skinType, "Name").SetValue(skin, key);
                    options.Add(skin);
                }
                Type vmType = module.GetType("Brainrot.BrainrotSkinSelectorVM");
                object viewModel = Activator.CreateInstance(vmType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { options }, null);
                var sync = vmType.GetMethod("Sync", BindingFlags.NonPublic | BindingFlags.Instance);
                sync.Invoke(viewModel, new object[] { player });
                var selectorProperty = vmType.GetProperty("SkinSelector");
                object playerSelector = selectorProperty.GetValue(viewModel, null);
                var index = playerSelector.GetType().GetProperty("SelectedIndex");
                Check((int)index.GetValue(playerSelector, null) == 3, "Equipment dropdown initializes from the selected hero's saved skin");
                index.SetValue(playerSelector, 1, null);
                sync.Invoke(viewModel, new object[] { companion });
                Check((int)index.GetValue(selectorProperty.GetValue(viewModel, null), null) == 2 &&
                    (string)select.Invoke(null, new object[] { player }) == "shrek" &&
                    (string)select.Invoke(null, new object[] { companion }) == "mike",
                    "Switching equipment-screen characters cannot copy one hero's dropdown choice to another");
                index.SetValue(selectorProperty.GetValue(viewModel, null), 0, null);
                Check(select.Invoke(null, new object[] { companion }) == null,
                    "Selecting Default appearance removes the current companion's cosmetic override");
                skins.GetMethod("Reset", Hidden).Invoke(null, null);
                sync.Invoke(viewModel, new object[] { companion });
                Check((int)index.GetValue(selectorProperty.GetValue(viewModel, null), null) == 2,
                    "The actual selector view model reflects inventory Reset without changing heroes");
                ((TaleWorlds.Library.ViewModel)viewModel).OnFinalize();
            }
            finally
            {
                FailSkinSave = false;
                mocks.UnpatchAll(mocks.Id);
                prefsField.SetValue(null, previousPrefs);
                pendingField.SetValue(null, previousPending);
            }

            var visualGetter = AccessTools.Method(Renderer, "GetVisualEquipment");
            var bodyCalls = PatchProcessor.GetOriginalInstructions(visualGetter);
            Check(!bodyCalls.Any(code => code.operand is MethodInfo && new[] { "set_SpawnEquipment", "UpdateSpawnEquipmentAndRefreshVisuals", "UpdateAgentProperties", "SetSkeleton", "ClearAllWeaponMeshes", "ClearVisualComponents", "GetUninitializedObject" }.Contains(((MethodInfo)code.operand).Name)),
                "Visual equipment resolution never clears live components or writes combat equipment");
            var skinBuilder = AccessTools.DeclaredMethod(typeof(Agent), "AddSkinMeshes", new[] { typeof(bool), typeof(bool), typeof(int) });
            var missionScreen = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.Screens.MissionScreen");
            var armorBuilder = AccessTools.DeclaredMethod(missionScreen, "TaleWorlds.MountAndBlade.IMissionListener.OnEquipItemsFromSpawnEquipment");
            var spawnGetter = AccessTools.PropertyGetter(typeof(Agent), "SpawnEquipment");
            var rewrite = Renderer.GetMethod("RewriteEquipmentReads", Hidden);
            var skinOriginal = PatchProcessor.GetOriginalInstructions(skinBuilder);
            var armorOriginal = PatchProcessor.GetOriginalInstructions(armorBuilder);
            var skinRewritten = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { skinOriginal, false })).ToList();
            var armorRewritten = ((IEnumerable<CodeInstruction>)rewrite.Invoke(null, new object[] { armorOriginal, true })).ToList();
            Check(skinRewritten.Count(code => code.Calls(visualGetter)) == skinOriginal.Count(code => code.Calls(spawnGetter)) &&
                !skinRewritten.Any(code => code.Calls(spawnGetter)),
                "Native skin generation reads the visual clone for coverage without changing its lifecycle");
            Check(armorRewritten.Count(code => code.Calls(visualGetter)) == armorOriginal.Count(code => code.Calls(spawnGetter)) - 1 &&
                armorRewritten.Count(code => code.Calls(spawnGetter)) == 1 &&
                armorRewritten.Last(code => code.Calls(spawnGetter) || code.Calls(visualGetter)).Calls(spawnGetter),
                "Native armor rendering uses the costume while its final armor-material read stays native");
            Check(armorOriginal.Count == armorRewritten.Count && skinOriginal.Count == skinRewritten.Count,
                "Rendering patches preserve all native instructions except the intended equipment calls");
            bool unknownPatternRejected = false;
            try { rewrite.Invoke(null, new object[] { new[] { new CodeInstruction(System.Reflection.Emit.OpCodes.Ret) }, true }); }
            catch (TargetInvocationException error) { unknownPatternRejected = error.InnerException is MissingMethodException; }
            Check(unknownPatternRejected, "An unrecognized native rendering pattern is rejected instead of silently patched");
            var getterPatches = Harmony.GetPatchInfo(spawnGetter);
            Check(getterPatches == null || !getterPatches.Owners.Contains("Brainrot.MissionVisuals"),
                "The real Agent.SpawnEquipment getter is never patched globally");
            var nativeEquip = AccessTools.DeclaredMethod(typeof(Agent), "EquipItemsFromSpawnEquipment",
                new[] { typeof(bool), typeof(bool), typeof(bool), typeof(int) });
            var equipPatches = Harmony.GetPatchInfo(nativeEquip);
            Check(!equipPatches.Postfixes.Any(patch => patch.owner == "Brainrot.HeroSkinSelector"),
                "The crashing post-equipment body rebuild callback is absent");
            var beginCalls = PatchProcessor.GetOriginalInstructions(AccessTools.Method(Renderer, "BeginEquipment"));
            Check(beginCalls.Any(code => code.Calls(AccessTools.Method(typeof(Agent), "SetCapeClothSimulator"))),
                "Native equipment reconstruction drops the prior managed cape simulator reference");
            var movie = new XmlDocument(); movie.Load(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(module.Location))), "GUI", "Prefabs", "BrainrotSkinSelector.xml"));
            Check(movie.SelectSingleNode("//AnimatedDropdownWidget[@DataSource='{SkinSelector}']") != null,
                "Selector movie binds the native dropdown to the available skin list");
            Check(movie.SelectSingleNode("//Widget[@IsVisible='@IsVisible' and @MarginTop='230']") != null,
                "Skin selector is positioned below the inventory mode controls");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    public static void Run()
    {
        var configType = Preview.Assembly.GetType("Brainrot.TroopReplacementConfig");
        var loadCatalog = configType.GetMethod("LoadCatalog", Hidden);
        var mappingsProperty = configType.GetProperty("Replacements");
        string moduleRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Preview.Assembly.Location), "..", ".."));
        var catalog = new XmlDocument();
        catalog.Load(Path.Combine(moduleRoot, "ModuleData", "character_replacements.xml"));
        var loadedCatalog = configType.GetMethod("Load").Invoke(null, null);
        var liveMappings = (IReadOnlyDictionary<string, string>)mappingsProperty.GetValue(loadedCatalog, null);
        var entries = catalog.SelectNodes("/BrainrotCharacters/Character").Cast<XmlElement>().ToArray();
        Check(liveMappings.Count == entries.Length && entries.All(entry =>
            liveMappings[entry.GetAttribute("troop_id")] == entry.GetAttribute("id") + "_body_armor"),
            "All current mappings load unchanged from the catalog beside the module assembly");

        string fixtureDirectory = Path.Combine(Path.GetTempPath(), "BrainrotConfigChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        string fixturePath = Path.Combine(fixtureDirectory, "character_replacements.xml");
        string obsoletePath = Path.Combine(fixtureDirectory, "troop_replacements.xml");
        try
        {
            File.WriteAllText(obsoletePath, "<TroopReplacements><TroopReplacement troop_id='looter' visual_item_id='shrek_body_armor' /></TroopReplacements>");
            File.WriteAllText(fixturePath, "<BrainrotCharacters><Character id='mike' mesh='Longmicheal' troop_id='battanian_volunteer' /></BrainrotCharacters>");
            var reduced = (IReadOnlyDictionary<string, string>)mappingsProperty.GetValue(loadCatalog.Invoke(null, new object[] { fixturePath }), null);
            Check(reduced.Count == 1 && !reduced.ContainsKey("looter") && reduced["battanian_volunteer"] == "mike_body_armor",
                "Removing a catalog entry cannot resurrect its obsolete fallback mapping");
            File.WriteAllText(fixturePath, "<BrainrotCharacters />");
            var empty = (IReadOnlyDictionary<string, string>)mappingsProperty.GetValue(loadCatalog.Invoke(null, new object[] { fixturePath }), null);
            Check(empty.Count == 0, "An empty character catalog disables every replacement");
        }
        finally
        {
            File.Delete(fixturePath);
            File.Delete(obsoletePath);
            Directory.Delete(fixtureDirectory);
        }

        var source = new Equipment();
        for (int slot = 0; slot < 12; slot++)
        {
            source.AddEquipmentToSlotWithoutAgent((EquipmentIndex)slot,
                new EquipmentElement(new ItemObject("native_" + slot), new ItemModifier(),
                    new ItemObject("cosmetic_" + slot), slot == 0));
        }
        Equipment snapshot = source.Clone(false);
        var costume = new ItemObject("test_costume");
        var result = (Equipment)Preview.GetMethod("CreateVisualEquipment", Hidden).Invoke(null, new object[] { source, costume });
        Check(!ReferenceEquals(result, source), "Preview uses a separate equipment object");
        Check(ReferenceEquals(result[EquipmentIndex.Body].Item, costume), "Costume is used for the preview body");
        Check(result[EquipmentIndex.Body].CosmeticItem == null, "Native body cosmetic cannot override the costume");
        var cleared = new[] { EquipmentIndex.Head, EquipmentIndex.Cape, EquipmentIndex.Gloves, EquipmentIndex.Leg };
        Check(cleared.All(slot => result[slot].Item == null && result[slot].CosmeticItem == null),
            "Native head, cape, glove and leg visual layers are cleared");
        for (int slot = 0; slot < 12; slot++)
        {
            EquipmentElement before = snapshot[(EquipmentIndex)slot];
            EquipmentElement after = source[(EquipmentIndex)slot];
            if (!ReferenceEquals(before.Item, after.Item) || !ReferenceEquals(before.ItemModifier, after.ItemModifier) ||
                !ReferenceEquals(before.CosmeticItem, after.CosmeticItem) || before.IsQuestItem != after.IsQuestItem)
                throw new Exception("Native equipment changed in slot " + slot);
            if ((EquipmentIndex)slot != EquipmentIndex.Body && !cleared.Contains((EquipmentIndex)slot))
            {
                EquipmentElement visual = result[(EquipmentIndex)slot];
                if (!ReferenceEquals(before.Item, visual.Item) || !ReferenceEquals(before.ItemModifier, visual.ItemModifier) ||
                    !ReferenceEquals(before.CosmeticItem, visual.CosmeticItem) || before.IsQuestItem != visual.IsQuestItem)
                    throw new Exception("Preserved equipment changed in slot " + slot);
            }
        }
        Console.WriteLine("PASS: All native equipment slots remain unchanged");
        Console.WriteLine("PASS: Weapons, shields, mount equipment, modifiers, cosmetics and quest flags are preserved");

        var body = new BodyProperties(new DynamicBodyProperties(30, 0.5f, 0.4f), default(StaticBodyProperties));
        CharacterCode nativeCode = CharacterCode.CreateFrom(source.CalculateEquipmentCode(), body,
            true, false, 0xFFEEDDCCu, 0xFF112233u, FormationClass.Ranged, 1);
        string originalCode = nativeCode.Code;
        CharacterCode portraitCode = (CharacterCode)Portraits.GetMethod("CreatePortraitCode", Hidden)
            .Invoke(null, new object[] { nativeCode, result });
        Check(!ReferenceEquals(nativeCode, portraitCode) && nativeCode.Code == originalCode &&
            nativeCode.EquipmentCode == source.CalculateEquipmentCode(),
            "Portrait substitution creates a new code without changing the native image data");
        Check(portraitCode.EquipmentCode == result.CalculateEquipmentCode(),
            "Portrait equipment code contains the costume and the preserved native weapon slots");
        Check(portraitCode.BodyProperties.Equals(nativeCode.BodyProperties) &&
            portraitCode.IsFemale == nativeCode.IsFemale && portraitCode.IsHero == nativeCode.IsHero &&
            portraitCode.Color1 == nativeCode.Color1 && portraitCode.Color2 == nativeCode.Color2 &&
            portraitCode.FormationClass == nativeCode.FormationClass && portraitCode.Race == nativeCode.Race,
            "Portrait code preserves body properties, gender, hero flag, faction colors, formation and race");
        Check(new CharacterImageIdentifier(nativeCode).Id != new CharacterImageIdentifier(portraitCode).Id,
            "Meme portrait requests use a different image ID from cached vanilla portraits");
        var nativeThumbnail = new CharacterThumbnailCreationData(nativeCode, null, null, true, 128, 128);
        var memeThumbnail = new CharacterThumbnailCreationData(portraitCode, null, null, true, 128, 128);
        Check(nativeThumbnail.RenderId != memeThumbnail.RenderId && memeThumbnail.CharacterCode.EquipmentCode == result.CalculateEquipmentCode(),
            "Native thumbnail cache keys include the costume equipment before texture generation");

        var probe = Preview.GetMethod("TryProbeMesh", Hidden);
        var probeCalls = PatchProcessor.GetOriginalInstructions(probe);
        Check(probeCalls.Any(code => code.Calls(AccessTools.Method(typeof(MetaMesh), "GetCopy"))) &&
            !probeCalls.Any(code => code.Calls(AccessTools.Method(typeof(MetaMesh), "CreateMetaMesh"))),
            "Mesh validation loads an existing asset instead of creating an empty MetaMesh");

        var mocks = new Harmony("Brainrot.PreviewChecks.MockEngine");
        mocks.Patch(AccessTools.Method(Preview.Assembly.GetType("Brainrot.BrainrotPreviewLog"), "Write"),
            prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "SuppressTestLog"));
        mocks.Patch(AccessTools.Method(typeof(MBAnimation), "GetNumActionCodes"),
            prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "ActionCountPrefix"));
        mocks.Patch(AccessTools.Method(typeof(MBAnimation), "GetActionCodeWithName"),
            prefix: new HarmonyMethod(typeof(BrainrotPreviewChecks), "ActionLookupPrefix"));
        try
        {
            SimulatedActionCount = 0;
            Preview.GetMethod("Install", Hidden).Invoke(null, null);
            Portraits.GetMethod("Install", Hidden).Invoke(null, null);
            Selector.GetMethod("Install", Hidden).Invoke(null, null);
            Renderer.GetMethod("Install", Hidden).Invoke(null, null);
            Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.PartyPreview")),
                "Empty native action table prevents every preview hook from being installed");
            Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.TroopPortraits")),
                "Portrait hooks also wait for the preview startup readiness checks");
            Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.HeroSkinSelector")),
                "Hero selector hooks wait for the same safe startup point");
            Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.MissionVisuals")),
                "Mission rendering hooks also wait until native actions are ready");
            Check(EarlyActionLookups == 0, "Deferred installation never initializes action IDs against an empty table");
            SimulatedActionCount = 5000;
            Preview.GetMethod("Install", Hidden).Invoke(null, null);
            Portraits.GetMethod("Install", Hidden).Invoke(null, null);
            Selector.GetMethod("Install", Hidden).Invoke(null, null);
            Renderer.GetMethod("Install", Hidden).Invoke(null, null);
            var cacheType = typeof(MBAnimation).Assembly.GetType("TaleWorlds.MountAndBlade.ActionIndexCache");
            object cachedIdle = cacheType.GetField("act_inventory_idle").GetValue(null);
            Check((int)cacheType.GetProperty("Index").GetValue(cachedIdle, null) == 7 && EarlyActionLookups == 0,
                "Inventory idle action is cached only after the native action table is ready");
            string[] names = { "InitializeAgentVisuals", "RefreshCharacterTableau", "SetCharStringID", "OnTick" };
            foreach (string name in names)
            {
                var target = AccessTools.DeclaredMethod(typeof(CharacterTableau), name);
                var patches = Harmony.GetPatchInfo(target);
                Check(patches != null && patches.Owners.Contains("Brainrot.PartyPreview"),
                    "Harmony installed the hook on the real " + name + " method");
            }

            var characterFactory = AccessTools.DeclaredMethod(typeof(CharacterCode), "CreateFrom",
                new[] { typeof(BasicCharacterObject), typeof(Equipment) });
            var partyFactory = AccessTools.DeclaredMethod(AccessTools.TypeByName(
                "TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM"), "GetCharacterCode");
            Check(Harmony.GetPatchInfo(characterFactory).Owners.Contains("Brainrot.TroopPortraits"),
                "Troop-tree portrait hook is installed on the shared native character-code factory");
            Check(Harmony.GetPatchInfo(partyFactory).Owners.Contains("Brainrot.TroopPortraits"),
                "Party list and upgrade portrait hook is installed on the actual party character-code factory");
            var screenType = AccessTools.TypeByName("SandBox.GauntletUI.GauntletInventoryScreen");
            var logicType = typeof(Campaign).Assembly.GetType("TaleWorlds.CampaignSystem.Inventory.InventoryLogic");
            foreach (var target in new[] { AccessTools.DeclaredMethod(screenType, "OnFrameTick"),
                AccessTools.DeclaredMethod(screenType, "OnFinalize"), AccessTools.DeclaredMethod(logicType, "DoneLogic"),
                AccessTools.DeclaredMethod(logicType, "Reset") })
                Check(Harmony.GetPatchInfo(target).Owners.Contains("Brainrot.HeroSkinSelector"), "Stage 7 native hook installed: " + target.Name);
            foreach (var target in new[] { AccessTools.DeclaredMethod(typeof(Agent), "AddSkinMeshes", new[] { typeof(bool), typeof(bool), typeof(int) }),
                AccessTools.DeclaredMethod(AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.Screens.MissionScreen"),
                    "TaleWorlds.MountAndBlade.IMissionListener.OnEquipItemsFromSpawnEquipment"),
                AccessTools.DeclaredMethod(typeof(Agent), "EquipItemsFromSpawnEquipment", new[] { typeof(bool), typeof(bool), typeof(bool), typeof(int) }) })
                Check(Harmony.GetPatchInfo(target).Owners.Contains("Brainrot.MissionVisuals"), "Native mission rendering hook installed: " + target.Name);
            RunSkinChecks();
            var campaignVm = partyFactory.DeclaringType.Assembly;
            var unitVm = campaignVm.GetType("TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items.EncyclopediaUnitVM");
            var unitConstructor = unitVm.GetConstructors().Single();
            Check(PatchProcessor.GetOriginalInstructions(unitConstructor).Any(code => code.Calls(AccessTools.Method(
                typeof(CharacterCode), "CreateFrom", new[] { typeof(BasicCharacterObject) }))) &&
                PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(CharacterCode), "CreateFrom",
                new[] { typeof(BasicCharacterObject) })).Any(code => code.Calls(characterFactory)),
                "Installed troop-tree nodes reach the patched character-code factory");

            var portraitPostfix = Portraits.GetMethod("AfterCharacterCode", Hidden);
            var hero = (BasicCharacterObject)FormatterServices.GetUninitializedObject(typeof(BasicCharacterObject));
            hero.StringId = "battanian_volunteer";
            AccessTools.Field(typeof(BasicCharacterObject), "_isBasicHero").SetValue(hero, true);
            var heroArguments = new object[] { hero, characterFactory, nativeCode };
            portraitPostfix.Invoke(null, heroArguments);
            Check(ReferenceEquals(heroArguments[2], nativeCode), "Unselected hero portraits retain native data even when their ID matches a troop mapping");

            var configField = AccessTools.Field(Preview.Assembly.GetType("Brainrot.BrainrotSubModule"), "replacementConfig");
            object savedConfig = configField.GetValue(null);
            try
            {
                configField.SetValue(null, Activator.CreateInstance(Preview.Assembly.GetType("Brainrot.TroopReplacementConfig")));
                var unmapped = (BasicCharacterObject)FormatterServices.GetUninitializedObject(typeof(BasicCharacterObject));
                unmapped.StringId = "unmapped_portrait_test";
                var unmappedArguments = new object[] { unmapped, characterFactory, nativeCode };
                portraitPostfix.Invoke(null, unmappedArguments);
                Check(ReferenceEquals(unmappedArguments[2], nativeCode), "Unmapped troop portraits retain the original code and cache identity");
            }
            finally
            {
                configField.SetValue(null, savedConfig);
            }

            var tableau = (CharacterTableau)FormatterServices.GetUninitializedObject(typeof(CharacterTableau));
            var field = AccessTools.Field(typeof(CharacterTableau), "_equipment");
            var idField = AccessTools.Field(typeof(CharacterTableau), "_charStringId");
            var dirtyField = AccessTools.Field(typeof(CharacterTableau), "_isVisualsDirty");
            field.SetValue(tableau, source);
            tableau.SetCharStringID("looter");
            Check((string)idField.GetValue(tableau) == "looter" && !(bool)dirtyField.GetValue(tableau),
                "Patched character selection preserves vanilla behavior outside Party Manager");
            var arguments = new object[] { tableau, AccessTools.DeclaredMethod(typeof(CharacterTableau), "InitializeAgentVisuals"), null };
            Preview.GetMethod("BeforeBuild", Hidden).Invoke(null, arguments);
            Check(arguments[2] == null && ReferenceEquals(field.GetValue(tableau), source),
                "Mapped troops receive no preview substitution outside Party Manager");
            var stateType = Preview.GetNestedType("PreviewBuild", BindingFlags.NonPublic);
            object state = Activator.CreateInstance(stateType, true);
            stateType.GetField("OriginalEquipment", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state, source);
            stateType.GetField("PreviewEquipment", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(state, result);
            field.SetValue(tableau, result);
            var finish = Preview.GetMethod("FinishBuild", Hidden);
            var error = new InvalidOperationException("simulated native renderer failure");
            object returned = finish.Invoke(null, new object[] { tableau, state, error });
            Check(ReferenceEquals(field.GetValue(tableau), source), "Native equipment reference is restored after a renderer exception");
            Check(ReferenceEquals(returned, error), "Renderer exceptions retain their original identity");

            var newerEquipment = new Equipment();
            field.SetValue(tableau, newerEquipment);
            finish.Invoke(null, new object[] { tableau, state, null });
            Check(ReferenceEquals(field.GetValue(tableau), newerEquipment), "Restoration preserves a newer equipment assignment by another hook");
        }
        finally
        {
            Renderer.GetMethod("Uninstall", Hidden).Invoke(null, null);
            Selector.GetMethod("Uninstall", Hidden).Invoke(null, null);
            Portraits.GetMethod("Uninstall", Hidden).Invoke(null, null);
            Preview.GetMethod("Uninstall", Hidden).Invoke(null, null);
            mocks.UnpatchAll(mocks.Id);
        }
        Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.PartyPreview")),
            "Unloading removes Brainrot preview hooks");
        Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.TroopPortraits")),
            "Unloading removes Brainrot portrait hooks");
        Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.HeroSkinSelector")),
            "Unloading removes the inventory selector hooks");
        Check(!Harmony.GetAllPatchedMethods().Any(method => Harmony.GetPatchInfo(method).Owners.Contains("Brainrot.MissionVisuals")),
            "Unloading removes the native mission rendering hooks");
    }
}
'@
    [BrainrotPreviewChecks]::Run()
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
}
