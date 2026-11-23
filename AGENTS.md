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
Example Implementation Case (Looter --> Shrek)
Model Rendering: Do not render standard armor meshes, as character proportions (e.g., Jimmy Neutron) do not fit default game armor.
Armor Stats & Hitboxes: Armor defense values and physical hitboxes remain unchanged and active.
Weapons & Shields: Characters hold and use the default weapons/shields assigned to the troop (e.g., Vlandian sword and shield).
Party-Manager tab: The meme characters must be shown as they are in battle, when previewed in the party manager tab.
The Shaders showing the meme characters must look as good as possible. No sharp shader lines, or strange shading problems.
3. Roles & Responsibilities
3.1 3D Artist (My Responsibilities)
I have extensive Blender experience (animating, rigging, prepping models, shading/material editing).
I will prepare all 3D models and define the mapping list of which meme characters replace which troops.
3.2 Developer / VS Copilot (Your Responsibilities)
Code & Architecture: You will write all necessary code and organize the most optimized modding framework/architecture for Bannerlord.
Asset Prepping Guidance: You will provide clear instructions on asset directory structure, file placement, and how I should prep models for the mod.