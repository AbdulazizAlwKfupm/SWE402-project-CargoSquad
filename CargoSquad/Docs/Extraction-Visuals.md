# Extraction beacon and brown cargo

IndustrialDistrict preserves the original CargoExtraction object, its position
(11, 1, -3), trigger size, references, and mission events. Only its placeholder
MeshRenderer is disabled. A separate visual-only prefab is positioned at ground
level (11, 0, -3). CargoExtraction remains the sole authority for delivery,
escape-only completion, duplicate prevention and survivor-loss priority.

## Assets and Inspector

- `Assets/_CargoSquad/Prefabs/Environment/ExtractionMarker.prefab`
- `Assets/_CargoSquad/Prefabs/Environment/CargoCrateVisual.prefab`
- `Assets/_CargoSquad/Art/Materials/CargoWeatheredBrown.mat`
- `Assets/_CargoSquad/Art/Materials/CargoWeatheredBrown.png`

The marker uses the existing original URP CombatIndicator shader/material.
ExtractionMarker exposes height (2.8m), radius (1m on the reusable prefab),
footprint aspect, cyan/teal color (0.18, 0.88, 0.86), transparency (0.15),
emission intensity (1.25), pulse speed (1.4 radians/s), ring size (1), label
visibility and segment count (64). In IndustrialDistrict radius is 0.5m and
aspect is (1, 2), fitting an elliptical illuminated ring inside the existing
1x2m rectangular trigger. This preserves the original narrow zone rather than
suggesting a wider gameplay boundary. The unmodified prefab is circular.

The cylinder has a vertical alpha gradient, bright ground/top rims, two thin
silhouette edges, and a slowly rising scan ring. Brightness varies by 12%; the
ground ring varies by 1.5% and is raised 0.04m above the floor. Bloom and dynamic
lights are unnecessary. Optional particles are omitted to keep the effect light.
There are no colliders, Rigidbodies, NavMesh obstacles, damage or mission
components on the marker. Weapon rays and navigation ignore it naturally.

The billboard EXTRACT label uses the existing LiberationSans SDF typography.
Its height accounts for the projected upper rim under the 45-degree camera,
keeping it above the ring rather than intersecting it. The beacon is depth-tested
against solid scenery and is visible when its world position is in the camera's
frustum; no camera or level-layout changes or off-screen arrows are added.

The crate has its own URP/Lit material with an original deterministic 256x256
procedural wood texture: main brown #79543A, dark edges #4C3527, and worn seams
#997251. Smoothness is 0.12, metallic 0.02. The scene cargo retains its existing
MeshRenderer, mesh, transform, condition definition and gameplay references;
only that renderer's material changes. The unit-scale visual prefab shares the
same material and has no gameplay or collision components. The shared gray
material and unrelated scene renderers remain untouched.

## Missing cargo feedback

The marker reads the existing mission, survivor health and authoritative trigger.
It tests overlap with the existing player capsule and exposes a read-only
MissingCargoPrompt flag. CargoMissionOverlay displays REQUIRED CARGO MISSING
when alive inside the zone without valid required cargo. The marker never calls
TryDeliver, changes condition/grip, emits completion events, or changes mission
state. Existing success, escape-only, and death feedback retain priority.

## Reproduction and validation

The editor command `Cargo Squad/Visuals/Install Extraction Beacon and Brown Cargo`
creates missing original visual assets and connects them to IndustrialDistrict.
It preserves existing tuned material/prefab assets and prompts to save unsaved
scenes when used interactively. It was run in an isolated project copy to
preserve open-editor work; the resulting visual scene edits were transferred
without reserializing unrelated scene objects.

Run ExtractionAssetTests and ExtractionVisualTests plus the existing full Edit
and Play Mode suites. Integration cases cover marker animation/non-collision,
missing cargo, carrying/slipping/delivery, exactly-once completion, destruction
and escape, death priority, reload/reset, real pellet rays and wolf navigation.
Evidence is captured from the actual running IndustrialDistrict gameplay camera
at 1280x800 under ignored `ValidationResults/Extraction/`.

Verified in Unity 6000.3.22f1: all 12 Edit Mode and all 57 Play Mode tests passed. The five extraction integration cases also passed after improving capture timing so normal Canvas updates finish before rendering. No new script/shader errors were logged. Seven actual Play Mode screenshots were inspected: marker, ground cargo, carry approach, missing-cargo entry, successful extraction, escape-only availability and escaped outcome. XML reports and PNGs are saved in ValidationResults/Extraction. The approved Heavy V4, Medic V1 and Mutated Wolf V2 Blender hashes remain unchanged; no commit or push was performed.
