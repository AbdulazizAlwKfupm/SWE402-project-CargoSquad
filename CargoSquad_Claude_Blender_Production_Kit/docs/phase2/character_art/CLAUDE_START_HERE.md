# Cargo Squad — Claude Code + Blender MCP: 3D Asset Production

You are the **3D technical artist and Blender automation developer** for our SWE402 Cargo Squad game. The Blender 5.3.0 MCP integration has already passed a real create/inspect/save test in Claude Code. Use it to produce **real editable game assets**, not concept images or promises.

## Workspace and required source material

**Unity repository**: `C:\kfupm\T261\SWE402\SWE402-project-CargoSquad`
**Unity project**: `C:\kfupm\T261\SWE402\SWE402-project-CargoSquad\CargoSquad`

The reference package is under `docs/phase2/character_art/` at the repository root:
- `Art_Direction_ITD460.md` — **read completely**. `[CANON]` is fixed; `[PROPOSAL]` is optional and not approved lore; `[TECH]` should be checked against the actual project.
- `references/Phase1_Survivors_Concept.png` — the primary concept image for the four survivors.
- `references/Phase1_Monsters_Concept.png` — the primary concept image for the three creatures.

Read/inspect those references **and** the existing Unity code/prefabs, including `PlayerController`, `PlayerStamina`, `CargoInteractor`, `CargoItem`, scene, CharacterDefinition assets, and existing cargo hold anchor. Summarize important findings briefly, then **begin asset production without requesting a broad planning approval**. Resolve small gaps using practical defaults and record them as proposals. Do not silently canonize ITD-proposed ages, genders, faces, or monster ancestry; use the Phase 1 images for visual alignment when they conflict.

## Actual game and art direction

Unity 6.3 LTS (`6000.3.22f1`), URP 17.3.0, angled **~45-degree orthographic top-down 2.5D** camera, free movement on the **XZ plane**, Y up. Zooba is only a *camera reference*, not an art-style reference. Gameplay emphasizes exploring a contaminated industrial district, hidden cargo, stealth/cover, resource recovery, and extraction; **not platforming**.

Visual style: **grounded stylized realism** combining post-apocalyptic horror with worn military-survival gear. Practical materials, battered tactical kits and readable silhouettes from above. **No bright cartoon characters, magical devices, fantasy armor or new gameplay abilities**.

Role silhouettes and colors from the supplied art direction (proposal palette; match supplied concept art where it differs):
- **Heavy**: widest, squared silhouette, armored load-bearing gear/back-frame, shotgun/heavy firearm; charcoal/olive `#303A32`, dirty tan `#82775F`, worn orange `#B86A3B`.
- **Scout**: leanest, tapered/asymmetric hooded scavenger with satchel, pistol; charcoal `#292F33`, faded plum-grey `#594C50`, dusty amber `#B48F55`.
- **Navy Soldier**: balanced symmetrical military kit with medium backpack, rifle; slate navy `#344754`, weathered grey `#737B7B`, pale blue `#86A4B3`. **Start here for the shared reference skeleton.**
- **Medic**: lightly armored survivor with tall medical backpack, identifiable pale pack top and stun device; dusty sage `#829186`, charcoal `#333D3C`, pale medical/teal details `#D3D8CC` / `#6BADA5`.
- **Mutated Wolf**: lean four-legged contaminated canid, clear ears/ridge, sound-alert and bite attack silhouettes.
- **Armored Creature**: slow, massive contaminated plated creature, visible armored upper back and long heavy-swing anticipation. **Do not assume its original species.**
- **Acid Spitter**: low hunched long-limbed creature with dorsal/throat corrosive reservoir, telegraphed spit. **Do not assume its original species.**

## Hard constraints: protect current game

1. Do **NOT** rebuild the Unity project, switch render pipeline, change Input System, edit character gameplay stats, invent combat/AI, or rewrite movement/stamina/cargo/mission systems. Existing working gameplay and scene GUIDs must survive.
2. Keep `IndustrialDistrict` working. **Do not replace the main PlayerPlaceholder permanently until the Navy visual/rig/animations have been validated.** Use a separate showcase/test scene or a visual child attached to a duplicate/test prefab.
3. The player Rigidbody/collider and logic remain on the existing gameplay root. Visual skinned meshes and Animator belong on a **child**. Set `applyRootMotion=false` for in-place motion. No duplicate gameplay rigidbodies/colliders on the visuals.
4. Cargo remains the **existing single physical cargo object**. Never bake a crate into character animations or spawn a duplicate purely for visual poses. Standardize weapon, backpack, hand-support, and cargo sockets and keep carry visuals aligned with the existing cargo mount.
5. Use current Blender MCP tools and repeatable Blender Python scripts as appropriate. **Test available tool names first**; don't invent MCP capabilities. Avoid launching another Blender instance that conflicts with the user's open scene. Never overwrite unsaved Blender work. Save production `.blend` files to new named paths.
6. Produce real meshes/rigs/animation data that Blender and Unity can load. Do not substitute screenshots, flat planes, rough generic cubes, or descriptive text for completed character assets. Don't falsely label non-skinned or unverified models as rigged/Unity-ready.
7. Prefer original content. Do not enable paid models, external AI model generators or asset libraries or download third-party assets without explicit approval and a compatible license. Any third-party licensed source must be documented.
8. Do not commit, push, force checkout, delete unrelated files or change Git branches without approval. Respect Unity `.meta` files and existing folder conventions. Avoid manual editing of `.unity` scene YAML: use the Unity Editor APIs for scene integration.

## Production order — do not skip validation gates

### Gate A: One production-ready Navy pipeline first (highest priority)
- Establish a consistent shared human skeleton in Blender with sensible hierarchy: hips, spine/chest, neck/head, arms/hands, legs/feet, fingers as feasible. Use clean weights, playable shoulder/hip deformation and an A- or T-pose. Aim for adult proportions, not cartoon/chibi.
- Model Navy Soldier with recognizable **3D** military kit, backpack, rifle and rough practical materials, based on the reference. Use independent removable gear meshes where sensible. Keep top-facing shoulders/backpack/rifle easy to read at gameplay distance.
- Maintain meters and ground-contact pivot; export for Unity Y-up with forward +Z after import. Verify axes, scale and foot position **in Unity**, not just in Blender. Keep geometry/material budgets as adjustable targets per the ITD doc, not absolute rules.
- Create **in-place** `Idle`, `Walk`, `Run` and `CarryIdle`, `CarryWalk` and `Pickup`, `Drop` clips. If complexity prevents that, complete a smaller coherent set and report exactly what's missing rather than making fake clips. Check skin deformation in the extreme poses.
- Save named, editable `.blend` source and FBX exports (or another Unity-validated rigged format). Import into Unity. Verify skeleton/avatar mapping (Humanoid preferred where valid), avatar pose, shaders, clip playback, no magenta material and no unintended root motion.
- Create a **non-destructive** `CharacterShowcase` Unity scene and `NavySoldier` visual prefab/test integration. Test it on the current 45° orthographic view at normal scale with movement and existing cargo pickup/drop/extraction, without altering gameplay logic.
- **Gate acceptance**: visible real model, valid readable rig+animations, compatible Unity import, working existing game. If impossible, report the exact blocker and use the remainder of the session to fix this gate, not to produce seven broken models.

### Gate B: The other three survivors (same animation system)
- From the validated common rig/Avatar and animation library, build **Heavy, Scout, and Medic** with visibly different silhouette, gear, proportions, and palette. Retarget or share core clips rather than creating four unrelated skeletons/Animator systems.
- Supply separate weapons/stun device, helmet/hood and pack equipment as reusable game objects where practical; no new gameplay weapon implementations.
- Create or reuse upper-body `Aim`/`Shoot` (or short `StunPrepare/Emit/Recover` as suitable), `Hit`, `Death`, `Regrip` and `CargoSlip` clips **after** essential core clips are confirmed; use believable character-specific pose overlays if feasible.
- Maintain standard attachment sockets: `RightHand_Weapon`, `LeftHand_Support`, `Back_Pack`, `Hip_Sidearm`, `Chest_Tool`, `Cargo_Front`, `Cargo_Back`/`Cargo_Side`, plus muzzle or stun effect locator. Align sockets to real weapon/cargo placement.
- Create distinct prefabs/test previews and verify **all four silhouettes** as solid black at the actual 45° game angle and 64–128px on-screen height, including front/back travel and carrying cargo. Iterate if they cannot be distinguished.

### Gate C: Creature artwork only after four survivors are genuinely usable
- Build original Blender creatures for Mutated Wolf, Armored Creature, Acid Spitter from concept image and ITD descriptions. Their rigs **do not** need to be Unity Humanoid and should be anatomy-specific.
- First produce distinct real 3D models, URP imports, and gameplay-camera silhouettes. If time remains, add appropriate creature skeletal rigs and `Idle`, `Move`, `Alert`, `AttackWindup`, `Attack`, `Hit`, `Stunned`, `Death` animations with obvious visual telegraphs.
- Create visual-only Unity prefabs, separate from the future enemy AI/damage/controller. Do not claim creature AI or combat is implemented.

## Reusability / file organization

Adapt to the actual repository conventions; do not replace established directories. Suggested targets:
- Blender source: `ArtSource/Characters/Survivors/`, `ArtSource/Characters/Creatures/` at repository root (or equivalent documented editable-source directory).
- Unity exports: `CargoSquad/Assets/_CargoSquad/Art/Characters/Survivors/`, `.../Enemies/`, `.../Animations/Humanoid/`, `.../Shared/`.
- Prefabs: `CargoSquad/Assets/_CargoSquad/Prefabs/Characters/`.
- Showcase scene: `CargoSquad/Assets/_CargoSquad/Scenes/CharacterShowcase.unity`.
- Technical documentation: `docs/phase2/character_art/PRODUCTION_STATUS.md`.

Preserve `.blend`, `.fbx`, material/texture sources, clips, prefab assets, and Unity `.meta` references. Standardize naming and avoid overwriting old models during revisions. Keep original versions where revisions would otherwise lose substantial work.

## Validation and evidence

For each finished model:
- Save at least one front and one camera-angle screenshot/render and an additional flat-black silhouette at real game distance. Compare to the user-supplied Phase 1 art.
- Inspect mesh integrity, unweighted vertices, rig skinning in walk/carry/aim poses, foot placement, gear collision, and clear cargo visibility. Ensure animations don't move gameplay colliders or modify configured stamina/grip/durability.
- Use actual Unity import and Play Mode checks when available, not just command success. Log what was tested in Blender vs Unity; do not invent tests.
- Check the existing gameplay scene has no new compile errors and that old movement/cargo/extraction tests still pass where possible.
- Document file paths, completed vs partial states, any licensing, and limitations.

## Efficiency given limited Claude Code availability

Work incrementally in coherent verified batches: **A Navy rig + motion + Unity test → B all four survivors → C three creatures if time**. Do not spend the whole window on a beautiful static render or an elaborate planning document. Prefer polished, reusable gameplay assets over cinematic-only detail. In every report, identify the current gate, completed/verified file paths, what's truly working in Unity, and the next executable task. Continue autonomously while tools are accessible; ask only when there's a real destructive or rights/licensing decision.

## First actions now

1. Inspect files, image references, Unity project structure and Blender MCP tool availability; do not overwrite Blender's current scene.
2. Confirm the reference folder exists. Read `Art_Direction_ITD460.md` **fully** and inspect both PNGs.
3. Inspect existing `PlayerPlaceholder` visual hierarchy, colliders/cargo mount, and existing controller/input implementation so planned visual integration fits it.
4. Create the first Navy model + shared rig, save a Blender source and test export; begin Unity validation. Do not stop after writing a plan.
