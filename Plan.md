# Bannerlord Brainrot Mod Plan

## Confirmed project baseline

- The mod is being rebuilt against the current Bannerlord installation rather than restored as the complete 2021 prototype.
- Frozen target version: Bannerlord `1.5.7.125802`.
- The game distribution target is Steam Workshop.
- The available compatibility test save is `Osbjorngolfsson.sav`.
- The 2021 prototype is preserved at `C:\Users\User\Documents\Meme`; it is a historical reference and is not the active implementation source.
- Active module load order: `Native`, `SandBoxCore`, `Sandbox`, `CustomBattle`, `StoryMode`, then `Brainrot`.
- Native module descriptors are consistently recorded as `v1.5.7`; this is compatible with the frozen target recorded above.
- The Shrek armor test is active and its mesh follows the Bannerlord skeleton because its material's skinning property is enabled.
- The remaining Shrek armor-test issue is that the original character arms and legs remain visible.
- The final target remains the frozen Bannerlord version until Stage 6 package validation confirms that broader compatibility is safe.
- Available tools: Bannerlord Modding Kit, BannerlordEditor, Visual Studio, and Blender.
- The first proof of concept is Shrek replacing the Looter.
- The proof of concept must work on the campaign map and in battles.
- The Looter must retain its original stats, armor values, hitboxes, equipment, weapons, and shields.
- The mod is visual-only, must work with existing saves, must be removable without save corruption, and does not target Warsails.
- The mapping of meme characters to troops will be supplied by the artist.
- Shrek is considered Bannerlord-ready. If validation shows that it cannot work reliably, the Master Chief asset and its known 2021 method will be the fallback.

## Current workspace assessment

The workspace already contains:

- A module descriptor in `SubModule.xml`.
- A Bannerlord asset project in `ModuleData/project.mbproj`.
- Converted `.tpac` assets under `Assets/` (3 current Shrek package files; the 2021 reference copy contains 65 generated files).
- Source FBX and texture files under `AssetSources/` (18 FBX and 52 PNG files across the active workspace).
- Empty or template XML files for NPCs, items, skins, and action sets.
- A 2021 prototype reference copy under `C:\Users\User\Documents\Meme`.
- The active test save `Osbjorngolfsson.sav` under the local Bannerlord game-saves directory.
- Mike Wazowski assets are present under `AssetSources/battania/` and `Assets/battania/`.
- Mike's temporary visual head item intentionally has no `mesh` attribute. It is a zero-armor full-cover HeadArmor entry whose only purpose is suppressing native hair/beard/face state without rendering a helmet.
- Mike Wazowski diagnosis: the exported mesh object is named `Longmicheal`, not `LongMike`; the temporary item XML has been corrected to reference `Longmicheal`, while `LongMike_geo.tpac` remains the generated geometry package.
- Different native-body suppression method: Mike POC now regenerates skin meshes with `SkinMask.NoneVisible` instead of meshless helmet or direct `head_male_a` replacement.
- Mike Wazowski uses the same proven BodyArmor replacement method as Shrek: replace only the cloned agent Body equipment slot and refresh native visuals.
- Added head, hand, leg, or cape clearing items did not resolve native head or shoulder rendering and is not part of the accepted POC.

There is no visible C# source project or replacement configuration yet. The plan therefore treats the current files as an asset/module foundation, not as a finished implementation.

## Architecture decision process

The implementation will compare two approaches against the actual requirements:

1. **Runtime visual replacement module**
   - Add a small C# module that identifies troop agents and applies a replacement visual while leaving the underlying `CharacterObject`, equipment, attributes, armor values, and combat behavior unchanged.
   - Keep replacement definitions in a separate, readable configuration file.
   - Prefer this approach if it can reliably cover both campaign-map agents and battle agents in the current Bannerlord installation.

2. **XML and asset-only replacement**
   - Determine whether native skin, troop, or item XML definitions can replace only the rendered body without cloning or changing troop equipment and stats.
   - Reject this approach if it requires changing troop definitions, replacing equipment, hiding equipment through fragile item tricks, or creating save-impacting records.

The old “helmet hides the body” technique will be retained only if the runtime and XML approaches cannot provide the required visual result without changing gameplay data. It is not the default architecture because it couples a character replacement to equipment behavior and makes future mappings harder to maintain.

### Costume slot and body-hiding brainstorm

The native `1.5.7.125802` item system does not expose an obvious custom visual-only slot. Equipment slots are fixed: the relevant native slots are head, body, leg, hand, cape, weapon, shield, and horse-related slots. Adding a new `Costume` item type would require engine-enumeration, inventory, equipment-roster, save, UI, and mission-data changes that are much larger than the visual replacement itself. It would also create save data and equipment-slot behavior unless the game API already supports custom item types, which is not established by the XML files.

The preferred design is:

1. **Keep native troop and equipment data unchanged.**
   - Do not replace the Looter `NPCCharacter`.
   - Do not change its body armor, leg armor, hand armor, weapons, shields, stats, armor values, or hitboxes.
   - Do not add a costume item to an existing equipment roster.

2. **Add a separate visual layer.**
   - Prepare Shrek as a skinned full-body mesh with its own skinning material and stable `human_skeleton` animation compatibility.
   - Map `looter` to a character visual asset in a readable `troop_replacement` configuration.
   - At agent visual creation or refresh, attach Shrek as the replacement body while leaving the underlying `CharacterObject`, equipment elements, and combat state intact.

3. **Hide native body layers at render time, not data time.**
   - Suppress native skin body, legs, hands, underwear, and any default armor or costume meshes that cover the replacement.
   - Keep native weapons and shields attached and visible.
   - Hide or cover the native head only if the character asset includes a suitable head replacement.
   - Preserve native hitboxes, collision, damage calculations, armor values, and inventory state.

4. **Do not use a custom slot as the primary implementation.**
   - A `Costume` slot is a higher-risk fallback for a future inventory or player-character feature.
   - It should be designed only if runtime APIs cannot isolate the body or existing equipment indices cannot be used safely without save or UI integration.
   - If an extra slot cannot be isolated as runtime-only visual state, reject it for this project.

The next acceptable fallback is to use a **hidden body through existing visual XML/item data**:

- Test native skin XML with custom `body_meta_mesh`, `legs_mesh`, and `hands_mesh` entries for Shrek.
- Test whether a zero-armor BodyArmor item can replace or suppress only the visible body while native equipment is left in place.
- Compare `covers_body`, `covers_legs`, `covers_hands`, `body_mesh_type`, and native armor variants.
- Reject this fallback if it requires replacing the troop equipment, sets artificial armor values, or makes weapons and shields disappear.
- The legacy helmet-hides-body workaround remains last resort because it depends on equipment and may affect inventory and combat visuals.

The recommended validation order is:

1. Runtime/mission agent visual replacement with native body suppression.
2. XML skin/body replacement that affects only rendering.
3. Existing-item-layer suppression with no gameplay side effects.
4. Custom costume slot or legacy helmet workaround only if the first three cannot satisfy campaign and battle rendering.

## Current Shrek asset status

- `AssetSources/bandits/shrek/Shrek.fbx` is present and is a Blender 2.92-era binary FBX source file.
- `AssetSources/bandits/shrek/ShrekShirt.png` is the source texture.
- `Assets/bandits/shrek/Shrek_geo.tpac`, `Assets/bandits/shrek/shrek_mtl.tpac`, and `Assets/bandits/shrek/ShrekShirt_tex.tpac` are generated game assets.
- `ModuleData/shrek_items.xml` defines test item `shrek_body_armor` as a `BodyArmor` with mesh `Shrek`.
- `SubModule.xml` loads `shrek_items.xml` for Campaign and CampaignStoryMode.
- The item can be given with `campaign.add_item_to_player_party shrek_body_armor`.
- The Shrek mesh and material load and its corrected size is visually acceptable.
- Enabling the material's skinning property makes the Shrek mesh follow Bannerlord character animations.
- The Shrek mesh does not yet hide the underlying native arms and legs in the armor-item test.
- Blender inspection confirms Shrek uses a 28-bone `human_skeleton_notused` rig with historical bone names such as `pelvis`, `spine`, `l_thigh`, and `l_hand`.
- The native reference rig uses 31 bones under `bip01_notused`, including indexed names such as `bip01_pelvis_0`, `bip01_spine_9`, and `bip01_l_hand_19`.
- A successful in-game skinning test should be considered a Bannerlord compatibility proof, but exact source bone-name correspondence should still be preserved in any final re-export workflow.

The first asset test confirms mesh and material import, item XML loading, in-game scale, and skeleton animation. It does not yet confirm that the native body or limb hiding method is suitable for the final character replacement.

## Staged implementation plan

### Stage 0: Freeze the target and document the baseline

1. Record the exact Bannerlord game/editor version used for the rebuild.
2. Record the module load order and the native module versions used by the prototype.
3. Inventory the existing converted assets and identify which files are source material, generated output, or obsolete prototype data.
4. Preserve the existing prototype files while creating the new implementation structure.

**Exit criteria:** The target version and asset pipeline are documented, and no implementation depends on an unverified newer game version.

### Stage 1: Validate the Shrek asset pipeline

Stage 1 is a hands-on Blender and Bannerlord visual-validation pass. No code or troop XML changes are made during this stage.

**Blender validation**

1. Open [Shrek.fbx](/c:/Program%20Files%20%28x86%29/Steam/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/Modules/Brainrot/AssetSources/bandits/shrek/Shrek.fbx) and the native [human_skeleton.fbx](/c:/Program%20Files%20%28x86%29/Steam/steamapps/common/Mount%20&%20Blade%20II%20Bannerlord/modding_resources/skeletons/human_skeleton.fbx).
2. Preserve the currently accepted visual scale; do not rescale unless an in-game or skeleton-alignment defect proves correction is necessary.
3. Confirm the Shrek mesh remains parented to and weighted by the in-game-compatible rig.
4. Confirm the material's skinning property remains enabled and the mesh follows representative poses without detached limbs.
5. Check root/pelvis alignment, facing direction, rest pose, hand position, foot position, and weapon-grip alignment against the native skeleton.
6. Do not re-export or replace the generated `.tpac` assets unless Blender or Bannerlord testing exposes a transform, weight, or material defect.

**Bannerlord testing**

1. Launch Bannerlord with `Brainrot` enabled.
2. Use an existing compatible save or controlled campaign.
3. Add the test item with `campaign.add_item_to_player_party shrek_body_armor`.
4. Equip `Shrek Costume` on the test character.
5. Test idle, walk, run, melee attacks, shield blocking, and death/post-battle rendering.
6. Test on the campaign map and in a battle. Works in battle
7. Record every native body region still visible:
   - torso;
   - upper arms;
   - forearms;
   - hands;
   - thighs;
   - calves;
   - feet;
   - head/face;
   - underwear;
   - native armor or clothing.
The forearms, hands, calves, feet, head/face and armor attached to those are still visible
8. Confirm native weapons and shields remain attached and usable. Yes they remain attached and usable.
9. Keep native troop stats, armor values, equipment, and hitboxes unchanged during testing. They are unchanged, except for the chest armor piece switched out with the shrek costume

**Candidate hiding tests**

Test existing render controls first, one variable at a time:

- Body coverage: `covers_body`;
- Leg coverage: `covers_legs`;
- Hand coverage: `covers_hands`;
- Body mesh variant: `body_mesh_type`;
- Native skin fields: `body_meta_mesh`, `legs_mesh`, and `hands_mesh`;
- Underwear fields: `underwear_bottom_mesh` and `underwear_top_mesh`.

Do not replace the Looter troop or add a costume item to its equipment roster during this pass.

**Result fields**

Record:

for the chest armor piece called "shrek costume"
- Skeleton motion: pass/fail. Pass
- Visible native regions after each coverage setting. Don't know what this means
- Hands and feet hidden: pass/fail. still visible
- Head/face behavior: pass/fail. still visible
- Weapon grip: pass/fail. pass - looks great
- Shield attachment: pass/fail. pass - looks great
- Campaign-map appearance: pass/fail. pass - looks great
- Battle appearance: pass/fail. pass - looks great
- Accepted body-hiding mechanism. Not sure

**Stage 1 recorded test outcome**

- Shrek skeleton motion passes.
- Shrek has not replaced or hidden the native forearms, hands, calves, feet, or head/face.
- Native armor attached to those regions remains visible.
- Native weapons and shields remain attached and usable.
- The Shrek chest costume replaces the test character's chest armor slot but does not consume or replace native troop equipment in the saved Looter definition.
- Native stats, armor values, hitboxes, weapons, and shields remain unchanged in the test.
- Campaign-map appearance passes.
- Battle appearance passes.
- No existing coverage setting or skin field has yet been identified as the complete body-hiding mechanism.

Stage 1 is therefore **partially complete**: the mesh import and in-game animation/weapon/shield compatibility are accepted, but the full native-limb, head, and armor hiding requirement remains unresolved.

**Exit criteria:** The POC asset animates correctly and a body-hiding mechanism is identified through direct Blender and in-game testing. Weapons, shields, troop stats, armor values, and hitboxes must remain unchanged.

**Complete**

### Stage 2: Build the smallest visual-replacement proof of concept

Stage 2 may begin, but it must treat native body suppression as part of the POC rather than assuming the chest armor item is sufficient. The POC will first determine whether runtime visual construction or XML skin replacement can suppress the remaining forearms, hands, calves, feet, head/face, and attached armor meshes.

1. The Stage 2 POC uses a single C# mission behavior and fixed Looter mapping.
2. The behavior identifies agents whose native `Character.StringId` is `looter`.
3. It loads the generated `Shrek` MetaMesh and attaches it to the agent chest/body visual component.
4. It clears native visual components while preserving the native agent skeleton and agent equipment state.
5. It does not change `CharacterObject`, equipment rosters, inventory, stats, armor values, or hitboxes.
6. Detect the visual representation of Looter agents in:
   - campaign map parties;
   - battle scenes;
   - newly spawned and existing agents where the game lifecycle requires separate handling.
7. Replace only the rendered character body or skin.
8. Do not edit the troop definition, inventory, equipment, stats, armor values, hitboxes, party templates, or save data.
9. Keep native weapons and shields visible and usable.
10. Ensure the replacement is inactive when the module is disabled or removed.


**Exit criteria:** A new campaign and a pre-existing compatible save can show the replacement Looter on the campaign map and in battle, while native combat behavior remains unchanged.

**Complete**

### Stage 3: Add the replacement configuration

1. Stage 3 uses `ModuleData/troop_replacements.xml`, with one entry per native troop identifier.
2. Each entry stores a visual-only `visual_item_id`; gameplay data remains sourced exclusively from the native troop and spawn equipment.
3. The runtime resolves namespaced and unnamespaced troop IDs, then looks up the matching visual item.
4. Missing files, missing entries, duplicate entries, empty attributes, and missing visual items print explicit diagnostics.
5. Invalid mappings do not silently fall back to another troop or visual item.
6. A mapping change requires XML/configuration edits only; ordinary additions do not require C# changes.
7. The implementation does not add fields for stats, equipment, armor values, weapons, shields, hitboxes, party templates, or save records.

```xml
<TroopReplacements>
  <TroopReplacement troop_id="looter" visual_item_id="shrek_body_armor" />
</TroopReplacements>
```

**Stage 3 test**

1. Exit Bannerlord completely.
2. Run `build.bat` and confirm the runtime DLL is copied to `bin/Win64_Shipping_Client`.
3. Relaunch with `Brainrot` enabled.
4. Enter a battle with native Looters.
5. Confirm the current mapping replaces Looters with `shrek_body_armor`.
-Result - pass. Works perfectly.

6. Temporarily change only `troop_replacements.xml` to a valid alternate BodyArmor item and verify the POC rebuild behavior.
-Result - My battanian Volunteers did not appear any different when I went into the party manager and clicked on the volunteer. This is a feature we can add later to the game. In the looter battle I saw that the Battanian volunteers did have the shrek skin applied, however their heads, forearms, hands, calves and feet were still visible. I didn't look at the looters.

7. Change `troop_id` to an unknown troop and verify that native Looters remain unchanged.
-Result - the looters looked great, they were the normal base game looters.

8. Change `visual_item_id` to an unknown item and verify that Looters remain unchanged and the missing-item diagnostic appears.
-Result - The battanian volunteers returned to the normal base game battanian volunteers. Looked great

**Stage 3 completion**

- Unknown troop mapping: passes; native Looters remain unchanged.
- Unknown visual item: passes; native Battanian Volunteers remain unchanged.
- Invalid mappings fail safely without replacement or a crash.
- The known-good mapping is restored for Looter and Battanian Volunteer.
- Stage 3 is complete.

**Exit criteria:** Changing a mapping does not require code changes, and invalid mappings fail visibly rather than producing a misleading successful load.

**Complete**

### Stage 4: Add Mike Wazowski and solve native body/head hiding

Stage 4 is the first second-character implementation pass. It begins after the Looter/Shrek POC and the troop replacement configuration are working. The objective is to map Mike Wazowski to `battanian_volunteer` while removing native Battanian head, face, facial hair, forearms/hands, calves/feet, shoulders, and visible chest/body regions without changing troop combat data.

**Mike asset state**

- Source asset: `AssetSources/battania/mike_wazowski/LongMike.fbx`
- Mesh object inside the FBX: `Longmicheal`
- Generated geometry: `Assets/battania/mike_wazowski/LongMike_geo.tpac`
- Generated texture: `Assets/battania/mike_wazowski/MikeBody_tex.tpac`
- Generated normal/bump texture: `Assets/battania/mike_wazowski/MikeBodyBump_tex.tpac`
- Generated material: `Assets/battania/mikebody_mtl.tpac`
- Intended visual item: `mike_body_armor`
- Intended mapping: `battanian_volunteer -> mike_body_armor`
- Looter mapping remains: `looter -> shrek_body_armor`

**What was tried**

1. Added a temporary Mike `BodyArmor` item named `mike_body_armor` using the same proven BodyArmor/`UpdateSpawnEquipmentAndRefreshVisuals` method that made Shrek work.
2. Changed the item mesh from `LongMike` to the exact FBX mesh object name `Longmicheal`; Mike then appeared.
3. Tested Mike on Battanian Volunteers. The Mike mesh appeared, but native head, forearms, hands, shoulders, calves, and feet remained visible.
4. Added zero-armor visual-only HandArmor and LegArmor items and assigned them to the cloned visual equipment. This hid the legs and feet, but the native head and shoulder pieces remained visible.
5. Added a visual-only HeadArmor item. It initially used a real full helm mesh, which placed a visible helmet and face over the Mike model. It was rejected because no helmet geometry is wanted.
6. Removed the HeadArmor `mesh` attribute and created a meshless, zero-armor, full-cover head item with `hair_cover_type="all"` and `beard_cover_type="all"`. Mike still had native heads/bald heads visible.
7. Cleared `EquipmentIndex.Cape` and `EquipmentIndex.Head` in the cloned visual equipment. This did not remove the native heads or shoulders.
8. Tried direct native head mesh replacement: created an empty `MetaMesh`, loaded `head_male_a` as the native old mesh, and called `MBAgentVisuals.ReplaceMeshWithMesh(..., BodyMeshTypes.Head)`. The bald/native head remained visible.
9. Tried native skin regeneration with `SkinGenerationParams((int)SkinMask.NoneVisible, ...)` and `AddSkinMeshes(...)`. Native forearms, legs, facial hair, and the head returned.
10. Reordered `SkinMask.NoneVisible` application to happen after `UpdateSpawnEquipmentAndRefreshVisuals`. The exact result remained unchanged.
11. The direct Mike POC was retried using only the same proven BodyArmor method as Shrek. This restored native feet, forearms, shoulder pieces, and heads.
12. The latest test confirmed native Mike/agent regions remain after the skin-mask timing attempt. No additional code changes should be made while this stage is paused.

**Current observed result**

- Mike Wazowski's own mesh is visible and working.
- The shrek looters also have their heads still visible.
- The native Battanian head remains visible.
- A bald/no-hair head or native bald head remains visible for some variants.
- Native facial hair can remain visible.
- Native forearms and hands may remain visible.
- Native calves and feet may remain visible depending on the tested method.
- Native shoulder pieces remain visible in some methods.
- The Mike body visual can render without crashing.
- Looters remain visually stable with the Shrek replacement.
- The exact native-skin hiding mechanism is not yet known.

**Paused hypothesis**

Mike likely needs the native skin/body layers split or suppressed at a lower asset level than a BodyArmor item. Bannerlord may regenerate the face, head, hands, or limbs after item refresh, and the Mike FBX may include or require a native-compatible full-character skin export rather than a costume overlay alone. Retargeting, mesh grouping, and native body-skin assets may need to be considered before further runtime code.

**update**
I changed the mike_items file and deleted the head piece we made. I added covers_head= "true" to the body armor piece and it hid the head. I also added covers_hands= true, and I made these changes to shrek and mike. I deleted all the other armor pieces we added. I have noticed that the feet, hands are gone, but the wrist armor and the boots are still on the character. Earlier versions of tests we did solved that problem so I need you to go in and use whatever you did to hide the wrist armor and the foot armor. The shoulder armor is still hidden.

**update implementation**
The visual refresh now clears the head, leg, glove, and cape equipment slots on the cloned visual equipment before refreshing the agent. The original troop equipment and combat values are not changed; only the temporary clone used for rendering is modified. The failed `SkinMask.NoneVisible` post-refresh skin regeneration block has been removed, leaving the BodyArmor coverage flags responsible for hiding native head, hand, and leg skin.

**Stage 4 exit criteria**

The second-character POC is complete only when:

- Mike renders as Mike Wazowski on `battanian_volunteer`.
- No native head, face, facial hair, forearms, hands, calves, feet, or shoulder armor remains.
- Shrek on Looters remains unaffected.
- Native weapons and shields remain visible.
- Combat damage, armor values, hitboxes, stats, and equipment are unchanged.
- The Mike implementation remains stable with no crashes or save-state changes.

**Complete**

### Stage 5: Verify compatibility and removal behavior

Before expanding the library, finish the compatibility and import workflow for Mike and Shrek.

**Targeted character catalog workflow**

Artists and mod maintainers will use one character catalog for ordinary additions:

```xml
<BrainrotCharacters>
  <Character id="shrek"
             mesh="Shrek"
             troop_id="looter" />
  <Character id="mike"
             mesh="Longmicheal"
             troop_id="battanian_volunteer" />
</BrainrotCharacters>
```

The catalog file will be `ModuleData/character_replacements.xml`. Each entry contains only:

- `id`: unique character key used to generate an internal visual item ID;
- `mesh`: exact generated/imported mesh name;
- `troop_id`: native troop ID to replace.

The runtime will read this file, generate/register one temporary zero-stat visual `BodyArmor` item for each entry, and apply that item to a cloned visual equipment set. It must not modify the native troop, native equipment roster, armor values, weapons, shields, hitboxes, or party templates.

The preferred implementation is to create and register the generated item objects from synthetic Item XML nodes at submodule load or mission initialization. This uses Bannerlord's existing runtime object-registration APIs rather than adding a hand-written XML item file for every character. If runtime object registration is not reliable, the fallback is one consolidated `ModuleData/meme_characters.xml` file containing all character entries, while `character_replacements.xml` remains the only artist-maintained mapping file. This fallback must not require one `*_items.xml` file per character.

**Catalog validation**

The loader must:

- resolve exact and namespaced troop IDs;
- skip duplicate character IDs and troop mappings;
- skip entries with missing `id`, `mesh`, or `troop_id`;
- verify that the referenced mesh exists when visual registration succeeds;
- print explicit diagnostics that identify the character and problem;
- leave the native character unchanged when an entry is invalid;
- never require C# changes for a new character or mapping;
- reload cleanly after an exit/relaunch when the catalog or assets change.

**Compatibility matrix**

Test Mike and Shrek using:

- a new campaign and a pre-existing save;
- campaign map party visibility;
- battle deployment, active combat, death, and post-battle screens;
- native weapons and shields;
- native armor statistics and damage behavior;
- agent selection, portraits, and equipment UI where applicable;
- module enabled, disabled, and removed;
- repeated save/load cycles;
- mixed battles containing replaced and unreplaced troops;
- an invalid mesh, unknown troop ID, duplicate entry, and missing catalog entry;
- removal of one catalog entry followed by a clean relaunch.

The test will specifically check that disabling or removing the mod does not leave custom troop records, modified equipment, or required save-state data behind.

**Exit criteria**

- One catalog entry can select one mesh and one native troop with no C# edit.
- Invalid entries fail visibly and leave native troops unchanged.
- Mike and Shrek pass the visual-only and save-safety matrix.
- Wrist, boot, head, face, hand, shoulder, and remaining native attached equipment behavior is stable.
- The implementation used for expansion is proven reliable on clean launch and repeated save/load.
- A specific limitation is documented before expansion, rather than discovered after 100 entries.

**Complete**

### Stage 6: Party-Manager Troop Preview of Meme Troops

Goal
In the campaign map, pressing P opens the Party Manager. When a troop is selected, it should display in the large 3D character preview with its assigned meme character mesh, while continuing to show its normal weapons and shields.
Examples:
Looter → Shrek
Battanian Volunteer → Mike Wazowski
Battanian Wood Runner → Luigi
The native troop stats, weapons, shields, equipment values, hitboxes, upgrades, and identity must remain unchanged.
The desired result is the same as the meme appearance in battles, but rendered in the Party Manager’s center character tableau.


### Stage 7: player Skin-Selection-Tree

Goal - The player will be able to select what skin he wants in the character equipment screen. It will not change any of his armor stats, and will just have a dropdown menu of the names of each meme model, of which you can choose from and change whenever from the character equipment screen. I would like this to work for companions aswell.

**Implementation plan**

1. **Confirm the equipment-screen integration and preserve the working baseline.**
   - Inspect the installed inventory/equipment screen's view model, character-switching controls, preview renderer, and open/close lifecycle before choosing hook targets. The dropdown belongs beside the currently displayed character's equipment and appearance preview.
   - Back up the accepted battle, Party Manager, and portrait implementation. Keep the late Harmony initialization and animation-readiness checks that fixed the folded-pose problem.
   - Scope the first version to the player and companions the player can equip. Do not expose changes for merchants, enemies, or unrelated heroes shown by other screens.

2. **Create one reusable list of available skins.**
   - Populate the dropdown from the existing character catalog and registered costume items, using stable character IDs internally and readable model names in the UI. Do not hard-code a separate list in C# or require a troop mapping change to select a skin for a hero.
   - Include a `Default appearance` option that restores the hero's normal body and equipped armor visuals.
   - Validate the item and imported mesh before offering a skin. Log unavailable entries and safely retain the default appearance if a selected asset is later removed or renamed.

3. **Store selections independently for each eligible hero.**
   - Resolve the actual hero currently displayed by the equipment screen. Store the selected character key against that hero's stable identity; the player and each companion must be able to choose different skins.
   - Keep an explicit hero-selection resolver separate from the existing native-troop replacement resolver. An unselected hero keeps their default appearance; choosing a companion skin must not change every troop with a similar name or equipment set.
   - Refresh the dropdown and preview when switching characters. Guard against UI binding updates accidentally applying the previous character's choice to the next one.

4. **Prove that applying a skin changes only rendering.**
   - Reuse the accepted costume coverage and visual-slot clearing rules for previews: replace the body in a separate visual equipment copy, hide native head/cape/glove/leg meshes, and preserve native weapons and shields.
   - Audit the live-agent application path before extending it to heroes. A cloned equipment object alone does not prove armor calculations are preserved; verify whether the selected refresh API changes the agent's combat equipment or cached armor values. Use a rendering-only path if it does.
   - Leave the hero's real battle/civilian equipment, inventory, item modifiers, weight/encumbrance, armor values, stats, skeleton, and hitboxes intact. Do not implement selection by equipping or granting costume items to the player or companion.

5. **Add the dropdown with immediate preview and clear confirmation behavior.**
   - Selecting a skin immediately updates the current character's equipment-screen preview. Selecting `Default appearance` reverses that preview without requiring an equipment change.
   - Track pending selections per hero for the current screen session. Commit them when the equipment screen is confirmed; discard them when the screen is canceled. Verify the installed screen's actual confirmation/cancellation behavior and handle other close paths explicitly.
   - Support repeated changes and character switching without duplicate controls, repeated event subscriptions, or per-frame equipment replacement. Use the screen's UI lifecycle and change notifications.

6. **Apply confirmed selections consistently across character views.**
   - Extend the shared appearance resolver so explicitly selected player/companion skins appear in missions, the equipment preview, the Party Manager's large display, and relevant hero portraits. Cover battle and civilian contexts where those heroes are displayed.
   - The existing portrait code deliberately skips heroes. Change that rule only for heroes with an explicit selection; preserve native portraits for all other heroes, including undiscovered/hidden-character placeholders.
   - Give changed hero portraits equipment-bearing cache keys and refresh the affected UI after a selection changes. Verify already-created mission agents and regenerated visuals pick up the confirmed choice, and switching back to default removes the previous mesh completely.

7. **Persist cosmetic preferences without making saves depend on the mod.**
   - Prefer a small versioned JSON preferences file outside the module, under the user's local application data, containing only campaign/profile identity, hero IDs, and character keys. Do not serialize custom item, troop, or hero objects into campaign saves.
   - First verify a stable campaign identifier available in this installation. Define how save copies and branches share preferences, and avoid using a filename alone or mixing unrelated campaigns. If independent preferences per save branch are required, agree that behavior before implementing persistence.
   - Write preferences safely when the screen is confirmed. Missing files mean default appearances; invalid records, missing heroes, and removed skins fall back safely with diagnostics. Disabling or removing Brainrot leaves native saves loadable and normal appearances available.

8. **Validate the complete player-and-companion workflow.**
   - Check different skins on the player and at least two companions; repeated changes; character switching; confirmation/cancellation; default restoration; clean relaunch; and existing-save loading.
   - Check inventory, Party Manager, portraits, battles, civilian scenes, mounted animations, weapons/shields, and death visuals. Recheck the accepted NPC troop replacements and normal poses on unmapped characters.
   - Compare actual equipment, armor, encumbrance, and damage behavior before and after selection, including changing real armor while a skin is active. Confirm no costume items enter inventories or equipment rosters.
   - Add meaningful managed checks for selection isolation, removed-skin fallback, persistence, and preview data. The user verifies rendered results and combat preservation in-game.
   - Complete Stage 7 when the player and companions can choose and reset their skins through the equipment-screen dropdown, confirmed choices survive relaunch, all supported views agree, and save/removal compatibility and native combat values are verified.

**Stage 7 implementation - selector accepted; revised battle rendering awaiting testing**

- Added `GUI/Prefabs/BrainrotSkinSelector.xml`, loaded as an additional movie in the existing inventory Gauntlet layer. It uses the native scrollable dropdown and appears at the top center of the equipment screen as `Meme appearance`. After the first user test it was lowered from a top margin of 175 to 230 to clear the inventory mode controls.
- `BrainrotSkinSelector.cs` tracks the inventory screen and selected character. It offers `Default appearance` plus validated catalog skins for the main hero and player companions, and follows native character switching. Reset restores pending selections; native `InventoryLogic.DoneLogic` commits only on success; finalization discards remaining pending changes and releases the additional movie. The native cancel path resets before calling Done, so it saves the original choices.
- `BrainrotHeroSkins.cs` resolves explicit selections per hero. `display_name` may optionally override a catalog entry's dropdown label; otherwise the costume item's name is used. A catalog character may be available for hero selection without having an NPC troop mapping.
- `HeroSkinPreferences.cs` stores a versioned JSON sidecar under `%LOCALAPPDATA%/Brainrot/HeroSkins/`, using a hash of the installed `Campaign.UniqueGameId` for the filename. Only campaign IDs, hero IDs and character keys are written. Save branches from the same campaign intentionally share appearance preferences; unrelated campaigns use separate files. Writes use a temporary file followed by atomic replacement, retaining a `.bak` of the previous preferences.
- Extended the existing tableau and portrait hooks for explicitly selected heroes. Inventory previews refresh when choices change, including resetting to default. Unselected heroes and hidden portrait placeholders keep native image data. NPC mappings continue to use the established catalog.
- The API audit confirmed `Agent.UpdateSpawnEquipmentAndRefreshVisuals` changes `SpawnEquipment`, rebuilds mission equipment and recalculates agent properties. It remains removed from Brainrot's replacement path. The initial Stage 7 adapter borrowed menu-preview mesh builders and cleared body components after native equipment construction; the user's first battle tests crashed with and without a selected hero skin. That adapter and its post-equipment rebuild callback were removed. The replacement described below stays inside the native skin/armor construction lifecycle and does not assign cosmetic equipment to the actual agent.
- Startup remains deferred until native actions are loaded. Existing previews, portrait hooks, the new inventory hooks and agent refresh hooks are checked against the installed game/Harmony assemblies. Backups of the pre-Stage-7 implementation are under `%LOCALAPPDATA%/Brainrot/Backups/Stage7-20261122-203110/`.

**Developer validation**

The Release build and managed regression checks pass. Checks cover existing troop mappings and previews, delayed startup, actual native hook installation, JSON save/reload and atomic replacement, independent hero choices, cancellation/reset/commit, failed writes, character switching through the actual selector view model, default restoration, and the body renderer's absence of combat-equipment/skeleton/weapon-mesh replacement calls. They do not substitute for testing native rendering or the in-game UI layout.

**User test checklist**

1. Fully restart Bannerlord with Harmony before Brainrot, then load the existing test save and open Inventory/Equipment with I.
2. Find `Meme appearance` near the top center. Select a skin for the player and check the immediate 3D preview. Confirm with Done, reopen, and verify the selected name remains.
3. Switch to two companions and give each a different skin. Switch repeatedly between characters and confirm their dropdown selections and previews remain independent.
4. Change a selection and cancel the screen; reopen and check the previous confirmed choice. Also test the inventory Reset button and `Default appearance` followed by Done.
5. Check the player and companions in Party Manager, their portraits, a battle, and a civilian scene. Check mounted/weapon/shield animations and death visuals where practical. Recheck NPC troop replacement bodies, poses and weapons after the rendering-path change.
6. Compare real armor slots, armor values and encumbrance with and without a skin. Change actual armor while a skin is active and confirm its normal stats still apply. Test combat behavior; ensure no costume items were added to inventory or real equipment.
7. Save, fully relaunch and load again to verify preferences persist. Check a separate campaign for default choices, and check disabling/removing Brainrot against the existing save as part of the compatibility pass.

Diagnostics remain in `%LOCALAPPDATA%/Brainrot/party-preview.log`; relevant entries include `Stage 7`, `skin selector movie loaded`, `Hero skin choices saved`, and `Mission body applied`. Preserve the log before relaunching if the dropdown is missing, a selection is lost, or a character renders incorrectly.

**First Stage 7 user test and battle-crash revision**

The user confirmed the selector works as intended, with its original placement slightly overlapping the controls above it. Entering battle crashed regardless of whether a personal skin was selected. The log showed mission body rebuilds before the crash; the native watchdog recorded a crash, but dump/report generation was canceled, so the exact native faulting instruction is not available. The post-construction rebuild's handling of native body/cloth state is the leading cause identified by code inspection, pending the next battle test.

The revised `BrainrotAgentBodyRenderer` installs three late-initialized native-lifecycle hooks:

- In `Agent.AddSkinMeshes`, equipment reads used to generate skin coverage read a separate visual equipment clone.
- In `MissionScreen.IMissionListener.OnEquipItemsFromSpawnEquipment`, geometry reads use the same clone. The final read for body armor material remains native, preserving its sounds/impact-material behavior. Every native instruction other than those selected equipment getter calls is retained.
- At the beginning of native `Agent.EquipItemsFromSpawnEquipment`, the visual lookup cache is invalidated and the previous managed cape-simulator reference is dropped before the engine clears/rebuilds components. The native armor builder registers the replacement simulator when needed. The actual `Agent.SpawnEquipment` getter is never globally patched and the actual equipment reference is never replaced.

NPC replacements and initial hero appearances are now built by the engine's normal spawn path. The mission behavior no longer clears/adds meshes on agent build or forcibly rebuilds NPCs afterward. It watches only already-built, active hero agents for an actual appearance change; those changes call native equipment construction using the unchanged real equipment and existing mission weapon data. Native property recalculation therefore sees real armor, while the two visual construction methods see the costume clone.

The managed checks now verify the installed rendering call patterns, exact replacement counts, retention of the last native armor-material read, preservation of instruction count, rejection of unknown patterns, absence of a global equipment getter patch, removal of the former post-equipment rebuild callback, cape reference reset, and the lowered selector position. The previous preference, selector, portrait and equipment-isolation checks remain. Native battle stability still requires the user's next test.

The failing DLL, source and logs are preserved under `%LOCALAPPDATA%/Brainrot/Backups/Stage7-battle-crash-20261122-210143/`. The revised startup log includes `Mission visuals v2`; successful native appearance construction logs `Native mission appearance built` with `source-unchanged=True`. First test entering battle with `Default appearance`, then a selected player/companion skin, and recheck NPC bodies, weapons, armor values and the selector placement.

**Completed**

### Stage 8: Expand the character library

My workflow -
Import the FBX and generated assets in the Bannerlord editor.
Add the mesh to ModuleData/meme_characters.xml as a BodyArmor item.
Add matching <Character ... /> entry to ModuleData/character_replacements.xml.
Restart Bannerlord.

current problems - 
gigachad uv mapping is bugged
pepsi man uv mapping is bugged
pepsi man doesn't show up for mercenary 7, the new name for that troop is hired pikeman?
homer simpson texturing looks awful
minecraft steve will be duplicated to have pewdiepie and herobrine, but their textures are not working. Also I need to export fbx with new mesh names
most character meshes have a shading bug that puts a sharp line down their face
Squidward is having his mesh fly everywhere in a crazy glitchy way.
No way to tell what troops are on which side.


### Stage 9: Package and maintain the release
1. Separate source assets, generated game assets, module files, and development-only files. (remove mike_items.xml and integrate him into the character_replacement.xml and/or meme_characters.xml)
2. Keep the release package limited to required runtime files.
3. Document installation, load order, supported game version, removal procedure, and known limitations.
4. Re-run the compatibility matrix after any Bannerlord update or runtime-module change.
5. Keep `character_replacements.xml` as the artist-facing source of truth for character-to-troop mappings.
6. Package only accepted character assets; do not ship unvalidated FBX source files or diagnostic/debug packages.
7. For each release, verify a clean install, upgrade from the previous release, disable/remove behavior, and a pre-existing-save load.

## Artist asset requirements

For each character, provide:

- a clean FBX based on Bannerlord's human-skeleton reference with exact accepted bone names and hierarchy;
- preserved skin weights and no orphan bones or duplicate armatures;
- the mesh parented to and bound to the Bannerlord armature;
- a verified rest pose and unit-scale/transform check;
- consistent scale and orientation;
- named materials with local texture references;
- color, normal, roughness/specular maps where used;
- a record of whether the character is intended to replace the full body, head, or another visual layer;
- a quick Blender preview showing weapon-hand alignment;
- permission to redistribute any third-party model and texture content included in the release.

The final source files should remain under `AssetSources/<Character>/`; generated Bannerlord assets should remain under the matching `Assets/<Character>/` directory.

## Information needed for implementation after this plan

No additional decision is required to write this plan. Before implementation begins, the following concrete inputs will be needed:

1. The local Bannerlord installation path and module deployment/test location.
2. Confirmation that the Shrek model may be redistributed with the mod, or replacement assets with confirmed redistribution rights.
3. The final troop mapping list, supplied incrementally or all at once.
4. A test save or permission to create a controlled test campaign.
5. Confirmation of whether the release should support only Steam or another distribution.

These are implementation and release inputs, not blockers for the architecture plan.
