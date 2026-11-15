# Cargo Squad — Character Art Production Status

Last updated: 2026-11-15. Nothing is committed or pushed.

## Current gate

| Gate | State |
|---|---|
| A — Navy pipeline (model, shared rig, core clips, Unity integration) | Done and validated in Unity play mode |
| B — Heavy, Scout, Medic on the shared rig + action clips | Done; validated in the lineup play-mode test |
| C — Mutated Wolf, Armored Creature, Acid Spitter | Models, own rigs, 8 first-pass clips each, visual-only prefabs; clips checked only by state playback and screenshots |

## Front carry in the real game (integrated 2026-11-15)

Front carry is the game's carry style and is live in `Scenes/IndustrialDistrict.unity`.

**What changed in gameplay code**

- `Scripts/Cargo/CargoCarrySocket.cs` (new): a reusable attachment point on the survivor model. The socket marks the plane the
  cargo rests against and pushes each cargo out by half its own depth, so cargo of any size sits against the body.
- `Scripts/Cargo/CargoInteractor.cs`: on pickup the cargo is parented to the `CargoCarrySocket` found on the model under the
  player (or to an optional serialized `carrySocket`). With no socket it falls back to the old `carryOffset`. Nothing else changed.
- `Scripts/Cargo/CargoItem.cs`: one new public property, `Size` (the cargo's own collider size × scale).
- Originals are saved in `ArtSource/Backups/pre_front_carry_integration/` because `Assets/_CargoSquad` is not tracked by git.

**What changed in the scene and assets**

- `IndustrialDistrict`: `NavySoldier_Visual` is a child of `PlayerPlaceholder`; only the capsule `MeshRenderer` is disabled.
  The example cargo is resized to 0.60 × 0.45 × 0.45 m (a per-object scale; no code holds that size). The scene GUID is unchanged.
- All four survivor prefabs now carry a `CargoMount_Front` socket and `FrontCarryVisual` (hand IK only; it never moves the cargo).
  `<Name>_Survivor.controller` uses the front-carry clips; `<Name>_Survivor_Overhead.controller` keeps the overhead set.
- Reference scenes, rebuilt by the pipeline: `CharacterShowcase.unity` (overhead carry, 1.5 m cube, via scene overrides) and
  `CharacterShowcase_FrontCarry.unity` (the real scene plus a four-survivor lineup).
- Rebuilding prefabs recreates the controllers, so rebuild the two reference scenes afterwards (menu **Run All** does this in order).
  `IndustrialDistrict` uses prefab defaults only and is not affected.

**Tests (`FrontCarryTests`, all pass; `CargoSceneTests` passes unmodified)**

- `IndustrialDistrict_PickupFrontCarryDropSlipAndExtraction`: gameplay root intact; movement; pickup parents the cargo to the socket with
  its collider off; the cargo's local pose never changes while walking in four directions and sprinting (no per-frame repositioning) and
  it stays in front; the head is never covered from the gameplay camera; sprinting still drains grip; manual drop restores parent,
  ground orientation, size and collision; the dropped crate blocks the player; automatic slip waits for clear ground, then drops with
  slip damage and one noise event; extraction completes and delivers.
- `IndustrialDistrict_EverySurvivorCarriesOnItsOwnSocket`: Heavy, Scout, Medic and Navy swapped in as the player model each pick up,
  hold (wrist error under 3 cm) and drop the same cargo on their own socket.
- `Lineup_AllSurvivorsHoldTheSameCargoWithHandIk`: all four hold the example crate and a second shape (0.80 × 0.30 × 0.60 m) with the
  hands on its sides.
- Screenshots: `validation/unity/50`–`58` and `60_*` (real scene), `39`–`43` (lineup).

**Known issues**

- (Resolved) Carried cargo is now stopped by walls; see "Wall obstruction for carried cargo" below.
- The crate snaps to the chest on pickup while the hands are still reaching down (about 0.3 s); there is no lift motion.
- The placeholder crate is the same grey as the ground and reads weakly. Walking away from the camera, the body hides most of it.
- Scenery blocks around the depot hide the player from the 45° camera in places. This predates the character work.
- Very wide cargo will exceed arm reach; the IK clamps and the hands will float short of the sides. Not tested beyond 0.80 m.
- `FrontRegrip` and `FrontCargoSlip` clips exist, but nothing triggers them; a slip currently plays the normal drop.
- Cargo size is per object (scale). `CargoDefinition.Size` (Small/Medium/Large) is not linked to dimensions.

## Wall obstruction for carried cargo (added 2026-11-15)

Carried cargo no longer passes through walls, buildings or obstacles.

- **How it works.** While cargo is held, `CargoCarrySocket` owns an obstruction box sized from that cargo's own `Size`. The box is an
  ordinary collider of the player's existing Rigidbody, so the unchanged velocity-driven `PlayerController` is stopped by walls and
  slides along them exactly as the capsule does. The cargo itself stays collider-off and rigidly parented to the socket.
- **Turning.** `SurvivorVisual` asks the socket before each turn step (`CanTurnTo`). A turn that would put the cargo in a wall is
  allowed only if the carrier has room to be pushed clear (up to 0.3 m); otherwise the model keeps its facing. Movement is never blocked
  by this check, so the player can always walk away.
- **Dropping.** `CargoInteractor.TryDrop` switches the obstruction box off while it searches for a clear spot, and every way of losing
  the cargo (drop, slip, delivery, destruction) releases it.
- **Files touched:** `CargoCarrySocket.cs`, `CargoInteractor.cs` (socket hold/release, drop search), `SurvivorVisual.cs` (turn check).
  `PlayerController`, stamina, grip, condition and extraction code are unchanged.
- **Tests (`CargoObstructionTests`, both pass, run in `IndustrialDistrict` with test-built walls):**
  - Head-on: cargo front stops exactly at the wall face (0.000 m past it); the body stops 0.675 m back instead of 0.50 m.
    Deepest momentary overlap 3.1 cm for one physics step.
  - Sliding diagonally: 2.5 m of travel along the wall in 1 s; walking away is immediate.
  - Turning beside a wall: deepest overlap 1.0 cm; the model stopped 52° short of facing the wall.
  - 1.1 m corridor: walks through carrying cargo, cannot swing the cargo into either wall (0.000 m), can leave, and a drop inside
    the corridor lands clear of both walls.
  - Drop while pressed against a wall succeeds, lands clear, cargo undamaged; without cargo the capsule reaches the wall again.
  - All four survivors with four cargo shapes (0.80×0.30×0.60, 0.60×0.45×0.45, 0.40×0.40×0.90, 0.70×0.50×0.30 m): each stops with
    the cargo front at the wall; deepest overlap 2.0 cm.
  - The level has no clear patch large enough, so the tests switch off one scenery piece (`Concrete Barrier West`) for the play
    session only. Nothing is saved to the scene.
- **Collision limitations:**
  - A blocked turn leaves the model facing its old direction while it moves (it can side-step or back away without turning).
  - On impact or a fast turn the cargo can overlap a wall by up to about 3 cm for a physics step before it is pushed out.
  - The box starts 0.875 m above the ground for the example crate, so obstacles lower than the cargo's underside do not block it.
  - Picking cargo up while pressed against a wall makes the box appear inside the wall; physics then pushes the player back.
  - The box also counts as part of the player for trigger zones, so extraction triggers when the cargo's front enters the zone.
  - Only static geometry was tested; other moving bodies (future enemies, other players) were not.

## What was tested, and where

**Blender 5.3 (via Blender MCP)**

- Every skinned mesh: 0 unweighted vertices, 0 non-normalised vertices, lowest vertex at Z ≈ 0.001 m.
- Poses inspected visually: Navy idle, run cycle, pickup; all four survivors in their weapon stances; three creatures in rest pose.

**Unity 6000.3.22f1, URP (editor open, tests run through the Test Framework)**

- Play mode: 9 of 9 pass (2 existing gameplay, 2 overhead reference, 3 front carry, 2 wall obstruction). Edit mode: 2 of 2 pass. No compile errors.
- `CargoSceneTests` (the two existing gameplay tests on `IndustrialDistrict`) still pass unchanged.
- `CharacterShowcaseTests.NavyVisualWorksWithExistingMovementAndCargoLoop`:
  valid humanoid avatar, no colliders/rigidbodies on the visual, URP/Lit materials, feet at ground (−0.003 m),
  faces +Z, movement with WASD/shift drives facing and locomotion, visual stays at local (0, −1, 0) (no root motion),
  pickup → carry → drop → pickup → extraction through the existing `CargoInteractor`/`CargoExtraction`,
  cargo stays the single object at its existing mount (0, 1.8, 0), wrists 3.5 cm under the crate bottom,
  rifle moves to the back socket while carrying.
- `CharacterLineupTests.LineupReadsFromGameplayCamera`:
  all four survivors have valid humanoid avatars and all ten sockets; Locomotion, CarryLocomotion, Regrip, Aim,
  Shoot, Hit and Death states are reached; survivors end Death with the head below 0.25 m.
- Measured at the gameplay camera (45°, orthographic size 6, 1280×720):

| Survivor | Height | Body width | On-screen height |
|---|---|---|---|
| Heavy | 1.88 m | 0.90 m | 110 px |
| Navy | 1.78 m | 0.70 m | 102 px |
| Medic | 1.73 m | 0.55 m | 95 px |
| Scout | 1.68 m | 0.58 m | 86 px |

- Clip probe on the Navy prefab: toes never go below 0.021 m, hips drift under 2 cm in all loops.

**Not tested**

- Creature clips are not asserted beyond "the state plays"; feet can slide or dip because they are FK only.
- Low-light / contaminated-surface contexts, LODs, performance, builds on a device.
- Light/medium/heavy cargo classes (the scene has one cargo item).

## Files

**Editable sources (repo root)**

- `ArtSource/Characters/Survivors/{Navy,Heavy,Scout,Medic}.blend` — Navy.blend also holds the 33 humanoid actions.
- `ArtSource/Characters/Creatures/{MutatedWolf,ArmoredCreature,AcidSpitter}.blend`
- `ArtSource/Characters/Scripts/` — the repeatable build:
  `cs_core.py` (mesh/rig/export helpers), `cs_survivors.py`, `cs_anim.py`, `cs_creatures.py`, `cs_build.py`,
  plus `unity_cmd.sh` / `run_tests.sh` (drive the open Unity editor).
  Rebuild everything in Blender with `cs_build.build_all_survivors()` and `cs_build.build_creature_assets(name)`.

**Unity (`CargoSquad/Assets/_CargoSquad/`)**

- `Art/Characters/Survivors/<Name>/<Name>.fbx` — Navy.fbx is the reference rig and carries all humanoid clips.
- `Art/Characters/Enemies/<Name>/<Name>.fbx` — Generic rigs with their own 8 clips.
- `Art/Shared/Materials/` — `cs_materials.json` palette manifest and the URP/Lit materials generated from it.
- `Art/Animations/Humanoid/<Name>_Survivor.controller`, `Art/Animations/Enemies/<Name>.controller`
- `Prefabs/Characters/` — `NavySoldier_Visual`, `Heavy_Visual`, `Scout_Visual`, `Medic_Visual`,
  `MutatedWolf_Visual`, `ArmoredCreature_Visual`, `AcidSpitter_Visual` (all visual-only).
- `Scenes/CharacterShowcase.unity` — copy of IndustrialDistrict with the Navy visual under `PlayerPlaceholder`
  (capsule renderer hidden in this scene only) and a seven-character lineup. `IndustrialDistrict.unity` is untouched.
- `Scripts/Visuals/SurvivorVisual.cs` — new, visual-only: reads Rigidbody velocity and `CargoInteractor.CarriedCargo`,
  turns the model, sets Animator `Speed`/`Carrying`, moves the weapon between hand and stow sockets.
- `Editor/CharacterArtPipeline.cs`, `CharacterArtMaterials.cs`, `CharacterArtProbe.cs` — menu
  **Cargo Squad ▸ Character Art** (configure imports, build prefabs, build showcase).
- `Tests/PlayMode/CharacterShowcaseTests.cs`, `CharacterLineupTests.cs`, `Tests/EditMode/CharacterArtTestBridge.cs`

**Evidence**

- `docs/phase2/character_art/validation/unity/` — gameplay-camera captures from the tests:
  `01`–`10` Navy in play, `20`–`24` lineup in colour plus `_silhouette` versions (front, moving away, carrying, aim, death).

No existing script, prefab, ScriptableObject or the `IndustrialDistrict` scene was edited. No third-party assets were used.

## Clips

- Shared humanoid (authored on the Navy rig, retargeted by Humanoid): `Idle`, `Walk`, `Run`, `Aim`, `Shoot`, `Hit`,
  `CarryIdle`, `CarryWalk`, `CarryRun`, `Pickup`, `Drop`, `Regrip`, `CargoSlip`, `Death`.
- Weapon sets: `Pistol_*` (Scout) and `Stun_*` (Medic) versions of Idle/Walk/Run/Aim/Shoot/Hit. Heavy uses the rifle set with the shotgun.
- Creatures: `Idle`, `Move`, `Alert`, `AttackWindup`, `Attack`, `Hit`, `Stunned`, `Death`.
  Telegraphs: wolf ears up and crouch before the bite; armored creature raises both arms with the shoulder plates for 1.2 s before the slam;
  spitter's dorsal sac scales up to 1.42× with the neck lifted before the spit.
- Animator parameters: `Speed` (float), `Carrying`, `Aiming` (bool), `Shoot`, `Hit`, `Death`, `Regrip`, `CargoSlip` (triggers).
  Only `Speed` and `Carrying` are driven today; the rest wait for combat/grip events.

## Proposals recorded (not canon)

- **Decided by the team (2026-11-15): the game will use the front carry.** It is not yet in the gameplay scene; the overhead
  clips and `CharacterShowcase.unity` remain as a baseline until it is. The first pass was an **overhead** carry, because that is where the existing mount puts the crate. `Cargo_Front` and `Cargo_Back`
  sockets exist for a later front/back carry.
- The Armored Creature is a hunched knuckle-walker (follows the Phase 1 image rather than the ITD "biped" text).
  The Acid Spitter stands taller on spidery limbs than the ITD "low" description, again following the image.
- All survivors are masked; no age, gender or face was fixed. The Medic uses a teal diamond icon instead of a cross.
- Trousers, boots and gloves are slightly lighter than the ITD hex values so they do not collapse to black at gameplay scale.
- Suggested future collision volumes (not added): wolf capsule 0.35 × 1.8 m lying along Z; armored box 1.9 × 2.4 × 1.9 m; spitter capsule radius 0.7, height 1.8 m.

## Limitations

1. **The existing crate hides the carrier.** The 1.5 m cube mounted overhead covers the survivor's head, pack and shoulders from the gameplay camera.
   Fixing that means a smaller crate and/or a different `carryOffset`, which is a gameplay decision.
2. In the overhead carry, hands only meet the crate for Navy (Heavy 8 cm above the crate bottom, Scout 16 cm and Medic 11 cm below). The front-carry prototype fixes this with hand IK; the overhead carry does not have it.
3. Flat-colour materials only: no textures, normal maps, or weathering. 13–16 material slots per survivor (target was 2–4).
4. Survivors are 4.8k–5.8k triangles, below the 12k–20k target; no LODs.
5. No finger bones (hands are modelled in a relaxed grip). No facial detail.
6. Feet slide at game speeds (walk 4 m/s, run 7 m/s are faster than the clip strides); playback speeds up to 1.3× above 6 m/s.
7. Navy and Medic are the closest pair in flat black; they separate by rifle line and pack height more than by body shape.
8. Creature animation is first-pass FK. No creature AI, damage, projectiles or colliders exist.
9. (Resolved) `IndustrialDistrict` now shows the Navy model with the front carry.

## Next executable task

Give the crate a readable material, then decide whether carried cargo should collide with walls and whether cargo dimensions should be driven by `CargoDefinition.Size`.
