# Heavy v4 animation repair

> The later [deployment and abilities update](Character-deployment-and-abilities.md) adds Medic, role-specific controllers, selection and abilities. The measurements below describe the original Heavy-only repair.

Implemented and checked on 2026-11-17 with Unity 6000.3.22f1.

## Root cause and comparison

The scene's Heavy Animator used the **old Heavy FBX Avatar** (GUID `a6f087c4105bb4342bdb9d0036f2a322`). That Avatar expects the old skeleton's transform names. In a fresh import comparison, assigning it to Heavy v4 returned null for every Humanoid bone; sampling the run animation changed **zero** `HV4_J_*` rotations. A valid Avatar asset is not sufficient: it must bind to this model's hierarchy. Navy works because its own Avatar resolves its Hips/Spine/LeftUpperLeg/etc. transforms.

`ArtSource/Characters/Survivors/Heavy_v4.blend` was inspected directly with Blender 5.3. Its joints are EMPTY objects, with no armature modifiers or vertex groups on the character mesh parts. Unity imports 47 ordinary MeshRenderers, not SkinnedMeshRenderers. Each part follows a joint through transform parenting. This is a usable rigid articulated character, not an unriggable static mesh. A fresh import of the original FBX settings actually auto-generates a valid own Avatar and can animate those joints; the empty serialized mapping alone was **not** evidence of invalidity.

The separate orientation problem was also present on disk: SurvivorVisual and Animator were on the corrected, scaled model root. SurvivorVisual writes a world-facing quaternion, which overwrites the import correction. The open editor additionally contained an unsaved `HeavyVisualRoot.` wrapper; the saved scene differed from that hierarchy.

## Saved setup

- `Heavy_new.prefab` now has an identity, metre-scale root containing the sole Animator, SurvivorVisual and FrontCarryVisual.
- Its `Heavy_v4` child retains X = -90 degrees and scale = 100. These transforms are not used for movement-facing rotation.
- A generated `Heavy_v4Avatar.asset` explicitly maps Heavy's 15 required Humanoid joints. CHEST serves as Spine; optional chest, neck, shoulders, toes and fingers are omitted. Joint rotations are normalized and the arms extended into a T reference pose before generating the Avatar.
- The existing `Navy_Survivor.controller` supplies Humanoid Idle, Walk, Run and front-carry clips. Its Speed blend tree and Carrying transitions are unchanged. Animator root motion is off. SurvivorVisual drives this exact active Animator and retains the prior Heavy scene timing values (600, 3, 1, 0.06).
- FBX animation/avatar generation is disabled because the saved Avatar belongs to the corrected prefab hierarchy. Mesh import and all material remaps are preserved. Neither the FBX bytes nor Blender source were changed.
- The gun follows a right-hand weapon socket using its authored grip offset. Back_Weapon is attached to CHEST, never a foot. CargoMount_Front is on the metre-scale facing root at (0, 1.1, 0.3), with identity rotation/scale. FrontCarryVisual uses this socket and the same Animator.
- IndustrialDistrict uses the corrected prefab, centered at (0, -1, 0) under PlayerPlaceholder. CargoInteractor explicitly references its active cargo socket. Navy remains inactive. Existing player physics, character definition, movement, stamina, cargo, camera and other scene objects were preserved.
- Existing model, prefab, scene and material GUIDs were preserved. Only the Avatar and editor setup helper introduce new GUIDs.

## Validation

Validation ran against a disposable copy of the actual local project in the Windows temporary directory, using Unity's real importer, Animator and Play mode. The exact generated assets and saved scene were then copied back. GUI automation could not reliably target the already-open Unity window, so no claim is made about a manual playthrough in that session.

- Generated Avatar: valid Humanoid, all 15 required bones resolve to Heavy's own joints; reference-pose joint positions checked.
- Direct clip sampling: Idle -> Walk/Run blend -> Run -> Idle. Knee changes about 31 degrees walking and 52 degrees running over the sample interval; hand positions also change. Root position/rotation/scale remain unchanged.
- Play-mode simulated keyboard input: idle, W, Shift+W, stop, A, S, D, E pickup, W carrying, E drop.
- Walk speed approximately 3.88 m/s, run 6.88 m/s, reflecting the scene's unchanged character definition and physics. Sprint stamina decreased from 105 to approximately 90 and recovered afterwards.
- Runtime limb motion: walking knee range about 70 degrees and upper-arm range 16 degrees; running about 71 and 28 degrees. Stopping returns to Idle.
- Facing: north 0, west 270, south 180, east 90 degrees; pitch and roll stay zero. Model correction, scale and visual local position deltas remain zero throughout.
- CargoPlaceholder was positioned within reach **only during the disposable Play test**. The real E interaction picked it up, switched to front-carry animation, reached IK weight 1, moved while carrying, and dropped with IK returning to 0.
- One active Animator and one gameplay Rigidbody; the visual prefab contains no Rigidbody or CapsuleCollider. All 47 mesh parts remain present.
- No gameplay Console errors/exceptions in the final Play-mode run. Unity's batch startup emitted a licensing token-refresh message, but completed successfully.
- Unity-rendered static previews were inspected for visible articulated limbs and preservation of the updated model. See [run preview](Validation/Heavy-v4/run-preview.png).

Evidence: [baseline bindings](Validation/Heavy-v4/baseline-bindings.txt), [clip sampling](Validation/Heavy-v4/animation-sampling.txt), [Play-mode measurements](Validation/Heavy-v4/play-mode.txt).

## Using the open editor and rebuilding

The assets and scene are already saved. Refresh the project and reopen `Assets/_CargoSquad/Scenes/IndustrialDistrict.unity` to load the disk changes. If the open scene has unsaved edits you want to keep, instead run **Cargo Squad > Heavy v4 > Configure open Industrial District**, which replaces only the known Heavy visual branch, reconnects cargo, and saves that open scene with its other edits intact.

After a future FBX update, run **Cargo Squad > Heavy v4 > Rebuild animated visual prefab**, then **Configure open Industrial District**. The editor helper regenerates the Avatar and prefab in place while retaining their GUIDs. Keep geometry/material changes in the source FBX/material remaps; rebuilding intentionally regenerates this visual prefab's components and sockets.

The model remains rigid articulated geometry: cloth pieces do not gain smooth skin deformation or finger articulation. A short manual visual pass in the user's current editor is still recommended for preferred weapon/back placement, cargo clearance and overall animation style. No additional rigging or animation assignment is required for locomotion.
