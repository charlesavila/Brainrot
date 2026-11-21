Bannerlord Brainrot Mod - Project & Prompt Specification
1. Project Overview
This project is a visual-only modification for Mount & Blade II: Bannerlord. It is strictly focused on cosmetic replacements and must strictly meet the following scope conditions:
Visual Changes Only: Changes only visual aspects of the game without altering stats, armor values, or hitboxes.
Save Compatibility: Must be safely downloadable, playable on pre-existing save files, and removable without breaking or corrupting saves.
DLC Scope: Will not be a mod for the DLC Warsails.
Current Baseline: The prototype has been remade for the current Bannerlord installation and is no longer based on the old v1.5.7 file set. Asset and XML sources are being rebuilt incrementally under `AssetSources/`, `Assets/`, and `ModuleData/`.
2. v1.0 Feature Requirements & Visual Mechanics
Troop Replacements: Replace NPC troop models with specific meme characters (e.g., Shrek, Master Chief, Jimmy Neutron).
Stats & Equipment Preservation: Each replaced troop retains its original stats, armor stats, and equipped weapons/shields.
Example Implementation Case (Vlandian Swordsman -> Jimmy Neutron)
Model Rendering: Do not render standard armor meshes, as character proportions (e.g., Jimmy Neutron) do not fit default game armor.
Armor Stats & Hitboxes: Armor defense values and physical hitboxes remain unchanged and active.
Weapons & Shields: Characters hold and use the default weapons/shields assigned to the troop (e.g., Vlandian sword and shield).
3. Roles & Responsibilities
3.1 3D Artist (My Responsibilities)
I have extensive Blender experience (animating, rigging, prepping models, shading/material editing).
I will prepare all 3D models and define the mapping list of which meme characters replace which troops.
3.2 Developer / VS Copilot (Your Responsibilities)
Code & Architecture: You will write all necessary code and organize the most optimized modding framework/architecture for Bannerlord.
Asset Prepping Guidance: You will provide clear instructions on asset directory structure, file placement, and how I should prep models for the mod.
4. Functional Deliverables & Workflow System
Functional Proof-of-Concept: A working base mod where the Looter is replaced with a provided Shrek model file located in the project folder, verifiable in-game on the campaign map and during battles.
Configuration File (troop_replacement): An easy-to-read configuration file/system allowing troop replacement mappings (e.g., mapping Vlandian Swordsman to a specific model file).
Future Workflow: Modding workflow for remaining assets must consist purely of rigging models, importing them, and updating mapping definitions in the configuration file.
5. Current Shrek Test Status
The source FBX `AssetSources/bandits/shrek/Shrek.fbx`, material `AssetSources/bandits/shrek/ShrekShirt.png`, converted geometry `Assets/bandits/shrek/Shrek_geo.tpac`, material `Assets/bandits/shrek/shrek_mtl.tpac`, and texture `Assets/bandits/shrek/ShrekShirt_tex.tpac` are present. `SubModule.xml` loads `ModuleData/shrek_items.xml`, which defines item ID `shrek_body_armor` with mesh reference `Shrek`.

The following armor-item test works:
- The Shrek material and mesh load in game.
- The mesh name matches the item XML mesh reference and the imported asset name.
- `campaign.add_item_to_player_party shrek_body_armor` adds the test item.
- The model has been resized and is currently at the correct visual scale.

The armor test is not yet an animation-valid character replacement. The model may render while equipped, but it does not currently follow the character skeleton and native action-set animations.

6. Shrek Rigging Diagnosis
The FBX imported in Blender contains a rig named `human_skeleton_notused`, a mesh named `Shrek`, and an Armature modifier on the mesh. The mesh has 1,057 vertices and vertex groups for 28 weighted bones. The FBX is therefore not unrigged: it already contains an armature and skin weights.

However, the FBX uses the old names `pelvis`, `l_thigh`, `l_calf`, `l_foot`, `spine`, `l_clavicle`, and related variants. The current Bannerlord skeleton reference uses the exact names `bip01_pelvis_0`, `bip01_l_thigh_1`, `bip01_l_calf_2`, `bip01_l_foot_3`, `bip01_spine_9`, `bip01_l_clavicle_14`, and similarly indexed names. The names and the armature object name do not match the native skeleton exactly. Bone-name or skeleton-layout mismatch is therefore the first likely cause of the animation failure.

Before changing scale again, validate and fix the following:
- Replace or retarget the source armature to Bannerlord's `human_skeleton.fbx` rather than relying on an older character rig.
- Rename bones and preserve the indexed Bannerlord hierarchy and rest transforms.
- Re-parent and re-bind the Shrek mesh with skin weights assigned to the Bannerlord bone names.
- Verify root/pelvis/rest-pose alignment and keep the scale that already renders correctly.
- Export a clean FBX with armature, skin, all required weight groups, and no scale or coordinate-transform drift.
- Import the mesh through Bannerlord's body/armor asset workflow appropriate to the chosen final visual layer; do not assume that a successful `BodyArmor` item test proves character-skeleton compatibility.

Do not revert to the legacy `helmet hides the body` method unless the standard human skin/body replacement pipeline fails. It is an old equipment workaround, not the primary rigging pipeline.

For the final implementation, distinguish:
- the Shrek item XML: a temporary armor-item animation test and inventory item;
- the troop replacement skin/body: the planned visual-only runtime or XML mechanism that leaves troop stats, equipment, armor values, and hitboxes untouched.
7. Legacy Progress & Investigation Tasks
Original 2021 Mod Files: I am providing the original file folder of the mod started in 2021 for review.
2021 Prototype Context: Details of previous attempts include importing a Master Chief model by creating a helmet item that hid all other body parts when equipped.
Method Evaluation: Evaluate if the "helmet hiding body" method is optimal or if a cleaner, more easily modifiable architecture exists. Only use legacy methods if they are the most optimized solution.
Version Compatibility Audit: Investigate recent Bannerlord updates to determine if 2021 mod files/progress remain compatible.
Re-rigging Willingness: If starting from scratch is recommended for optimal architecture, advise accordingly so 3D models can be re-rigged.
