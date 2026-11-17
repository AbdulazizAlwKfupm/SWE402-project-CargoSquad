# Heavy / Medic deployment, rigs and abilities

> See the later [Medic materials and holograms update](Medic-materials-and-holograms.md) for repaired remaps, live rotating heads and SDF UI text.

Implemented in the local CargoSquad project on 2026-11-17. Unity version: 6000.3.22f1.

## Play flow

IndustrialDistrict opens a paused character-selection screen with Heavy and Medic head portraits, a selection border and a Start button. Portraits are rendered from the actual model head meshes, including headgear and face equipment. No stock or generated substitute portraits are used.

Start enables exactly one visual beneath PlayerPlaceholder, assigns its existing CharacterDefinition to PlayerStamina, initializes its health from that definition, reconnects the cargo socket, and selects its left-mouse ability. The selection screen hides and gameplay resumes. A release/frame guard prevents the Start interaction from triggering an ability.

PlayerPlaceholder retains the only gameplay Rigidbody, capsule, movement controller, stamina and cargo components. The camera target is unchanged. Both models keep their own axis/scale correction under identity facing/Animator roots, with root motion disabled.

## Medic rig

The actual source is `Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx`, with the Blender source `ArtSource/Characters/Survivors/Medic_Redesign_v1.blend`. Its `MR1_J_*` transforms are articulated empties with mesh pieces parented beneath them, matching Heavy's `HV4_J_*` structure. The shared builder now takes model paths, prefab names and joint prefixes.

`Medic_redisgned.prefab` uses an explicitly generated `Medic_Redesign_v1Avatar.asset`, mapping its own pelvis, chest/spine, head, upper/lower arms, hands, thighs, shins and feet. Its T reference pose and separate -90-degree X / 100-scale child correction follow the working Heavy solution. It reuses the existing Navy Humanoid locomotion and front-carry clips through its own `Medic_Player.controller`.

Heavy keeps its own valid Avatar and updated appearance. Original FBX files, Blender sources, material assignments, source model GUIDs and existing prefab GUIDs are preserved. The Medic source's currently imported light-colored materials are retained.

## Abilities and tuning

Select PlayerPlaceholder and inspect **CharacterAbility**:

| Setting | Default |
| --- | --- |
| Heavy charge speed | 11 m/s |
| Heavy charge distance | 3 m |
| Heavy damage | 35 |
| Heavy cooldown | 4 s |
| Medic cooldown | 12 s |

Heavy uses a short capsule-swept forward charge. The existing PlayerController remains the sole writer of gameplay velocity. Each physics step limits travel to the nearest solid collision; triggers and the player's own colliders are ignored. The first damageable obstacle receives damage once and stops the charge. Add the shared Combat/Health component, or implement IDamageable, on future enemy targets to receive bash damage. The current scene does not introduce new enemies.

The `Heavy_ShoulderBash.anim` clip animates real Humanoid arm, torso and leg muscles. Body reference translation/rotation are retained from the existing Idle clip so the pose stays above the ground. `Heavy_Player.controller` transitions into and out of the bash without root motion.

Medic restores the selected player's stamina to 100, capped at its maximum, and never reduces an already higher value. Restoration also clears exhaustion once the normal stamina recovery threshold is met.

Both abilities reject activation while dead, carrying cargo, paused, on cooldown or over UI. UI blocking covers both the new UGUI interface and the existing IMGUI cargo/stamina HUD. Only the selected role can activate; neither visual prefab contains a competing ability or movement controller.

Heavy's existing definition now walks at **2.8 m/s** and runs at **4.8 m/s**. Medic's existing definition stays at **4 / 6.8 m/s**. Each role's copied locomotion blend trees use thresholds suited to these speeds, so Heavy still reaches a proper Run clip while moving more slowly.

The scene's local Heavy tuning is retained: turn speed **200**, full-speed playback reference **1**, and front-carry IK lock speed **1**. Deployment setup also preserves local facing/IK tuning and material overrides when replacing the known visual branches.

## Cargo compatibility

Pickup, stowing, front-carry IK, grip/stamina, manual drop, slip, collision and extraction still use the existing components. New Medic hand, back and cargo sockets follow the same structure as Heavy. Back sockets attach to the chest, not a foot.

The carry-turn check now considers the whole planned turn before starting it. This avoids entering an unfinished partial turn that wedges the capsule/cargo in a narrow corridor. Overlap checks use the full cargo box extents. A character can keep its current safe facing and back out when there is no room to turn.

Existing cargo scene tests now press Start before exercising gameplay. Their waits accommodate the preserved slow turning and IK settings. A test-only fixture routes simulated input to gameplay in an unfocused batch-mode Game view and restores the previous settings afterwards.

## Editor workflow

The corrected assets and IndustrialDistrict scene are saved on disk. Reload the scene when Unity detects the disk change.

If you have unsaved scene edits to keep, decline reload and run **Cargo Squad > Deployment > Configure open Industrial District**. This reconnects the saved Heavy/Medic assets in the open scene and preserves its other objects and local character visual settings before saving. The old Heavy scene-configuration menu delegates to this deployment setup when a deployment object exists.

For future model/animation updates, open IndustrialDistrict in Edit mode and run **Cargo Squad > Deployment > Rebuild characters, portraits and selection**. It rebuilds the two visual prefabs, Avatars, head portraits and character controllers, then updates the scene. Source models and material assets are not replaced.

## Verification

Validation uses Unity's real importer, rendering and automated Play-mode tests in an isolated local copy of the project. The source scripts and their actual project GUIDs are synchronized into that copy; the resulting prefab/Avatar/controller/portrait/scene assets are copied back. This avoids closing or taking over the user's editor.

All 15 Play-mode cases passed across the final main run, corridor rerun and selected-character run; both Edit-mode tests passed. Actual left-mouse activation was exercised for bash and stamina restoration, and the redesigned Medic completed pickup, hand IK, drop and extraction. The main run's settled-contact assertion was rerun after releasing input before measuring settled contact, rather than treating an ongoing driven impact as settled. The transient penetration checks remain intact. Test evidence and rendered previews are stored alongside this report under `Validation/Deployment/`.

- [Test summary](Validation/Deployment/test-summary.txt)
- [Main Play-mode results](Validation/Deployment/playmode-main.xml)
- [Passing corridor rerun](Validation/Deployment/playmode-corridor.xml)
- [Actual mouse input and redesigned Medic cargo results](Validation/Deployment/playmode-selected-characters.xml)
- [Edit-mode results](Validation/Deployment/editmode.xml)
- [Selection screen](Validation/Deployment/character-selection.png)
- [Heavy bash pose](Validation/Deployment/heavy-bash.png)

 A manual playthrough in the user's already-open editor is not claimed. Both characters retain rigid articulated mesh pieces rather than smooth skin deformation or finger articulation.

## Changed files and assets

- `Scenes/IndustrialDistrict.unity`: deployment references and shared player ability/health components.
- `Prefabs/Characters/Heavy_new.prefab`, `Medic_redisgned.prefab`: corrected visual rigs, animation controllers and sockets.
- `Characters/Heavy_v4Avatar.asset`, `Medic_Redesign_v1Avatar.asset`: own-hierarchy Avatars.
- `Art/Animations/Humanoid/Heavy_Player.controller`, `Medic_Player.controller`, `Heavy_ShoulderBash.anim`: role speed thresholds and bash motion.
- `Art/UI/Heavy_Head.png`, `Medic_Head.png`: model-rendered head portraits.
- `ScriptableObjects/Characters/Heavy.asset`: walk/run speed changes; Medic's existing definition is retained.
- `Scripts/Player/CharacterAbility.cs`, `Scripts/Combat/Health.cs`, `Scripts/UI/CharacterSelection.cs`: abilities, damage/death state and deployment UI.
- `PlayerController.cs`, `PlayerStamina.cs`, `CargoInteractor.cs`, `CargoCarrySocket.cs`, `SurvivorVisual.cs`, `CargoMissionOverlay.cs`: narrow integration points for charge velocity, selected stats, restoration, cargo sockets/turn safety and UI input blocking.
- `Editor/HeavyVisualSetup.cs`, `Editor/CharacterDeploymentSetup.cs`: shared rig building and repeatable deployment setup with local tuning/material preservation.
- Runtime and Play-mode test assembly definitions: UGUI references. New deployment/input tests and existing cargo tests: selection setup and timing-aware regression checks.

All paths above are under `Assets/_CargoSquad/`. Existing unrelated package/project settings edits were left intact. No commit or push was made.
