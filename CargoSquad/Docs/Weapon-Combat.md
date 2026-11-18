# Cargo Squad weapon combat

IndustrialDistrict adds one `PlayerWeapon` to the existing player. CharacterSelection
equips the appropriate weapon after deploying Heavy or Medic. Selection, health,
cargo, movement, stamina, and mission state retain their existing owners.

## Controls

- Mouse position: ray from the orthographic camera onto the ground plane.
- Hold LMB: show range/spread sector and face the aim direction.
- Release LMB: fire one shot toward the cursor. Short clicks also fire once. RMB is unused.
- Q: existing shoulder bash / stamina restoration, with existing restrictions.
- R: reload. WASD, Shift, and E are unchanged.

Input reads use Unity Input System devices, matching the existing ability input.
Deployment waits for a mouse release before accepting fire. A world-started press arms a single release shot; UI capture, pause, death, reload, disable, and focus loss cancel it. HUD/UI raycasts block
fire and aiming. Death, selection, pause, charge, disabled components, and mission
completion suppress weapons. Reload cancels when weapon use becomes unavailable.

## Data and Inspector

Weapon assets are under `Assets/_CargoSquad/Data/Weapons/`:

| Definition | Cone | Range | Damage | Pellets | Magazine / reserve | Rate | Reload | Noise |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| HeavyShotgun | 60° | 5m | 12 per pellet | 7 | 5 / 20 | 1.25/s, semi | 1.6s | 20m |
| MedicStun | 20° | 6m | 6 | 1 | 4 / 12 | 1.5/s, semi | 1.3s | 9m |
| ScoutPistol | 18° | 8m | 22 | 1 | 8 / 32 | 3/s, semi | 1.3s | 14m |
| NavyRifle | 12° | 10m | 14 | 1 | 24 / 96 | 7/s, automatic | 1.8s | 22m |

Scout and Navy are data only; no new selectable characters are added. The current hold/release input fires exactly once per gesture for every definition, including the future automatic rifle; its automatic flag remains data for a future input mode. Shotgun
damage falls linearly to 35% at maximum range. Other weapons have no falloff.
Each asset also exposes spread pattern, carry support, carry/sprint spread,
effect color, optional audio clips/volume, and stun duration.

The shotgun samples one independent random yaw in each of seven equal strata.
A single-shot weapon samples the central half of its cone. Seed 402 resets on
equip for repeatable tests. The indicator always encloses the possible shots.
Each pellet ray resolves its nearest solid collider, skipping the player's
hierarchy (including held cargo/socket obstruction). World collision and range
limit damage. A target merely standing inside the cone receives no damage.
`IDamageable` remains the sole damage interface. The hit mask is configurable.
RaycastAll intentionally avoids overflowing a bounded hit buffer and losing walls;
its small per-shot allocations are limited to firing, never the aiming loop.

## Visuals, cargo, and noise

The reusable `Prefabs/Combat/AimingConeIndicator.prefab` creates a 40-segment
sector and outline, with a vertex-alpha teal fill and original URP unlit shader.
Segment count, outline, material, and color are configurable. It sits 0.035m above
the ground and has no collider. Depth testing preserves solid-world occlusion.

Visual facing uses the existing SurvivorVisual follower, including its safe cargo
turn checks. Shooting never owns Rigidbody movement. Carrying multiplies spread
by 1.5 (Heavy 90°, Medic 30°). Moving with Shift adds a configurable 1.2 multiplier.
Shooting does not drain grip or damage held cargo. The existing front-carry pose,
carry socket, obstruction geometry, and weapon stowing remain intact. Dedicated
firing poses and a hand-held weapon pose while carrying are not provided by the
current rig; functional shots use the body-height plane and nearby muzzle cue.

A bounded reusable line pool supplies muzzle stars, pellet tracers, impact
crosses, and a cyan electrical zigzag for the stun shot. No cinematic effects or
shared material edits are required. AudioSource hooks are 2D; no legally usable
weapon audio was present, so the fire/empty/reload clip fields remain unassigned.

Every trigger pull emits one existing GameplayNoise through CargoInteractor.
Wolves hear that same event used for cargo slips, with their existing hearing
radius and investigation behavior. Cargo noise and bite damage remain unchanged.

## Stun

Medic stun lasts 1.2s. The existing wolf state machine gains `Stunned`; it stops
navigation and cancels the pending bite without bypassing the bite cooldown.
Recovery resumes search/chase. Death overrides stun. The wolf Inspector caps stun
at 3s and grants 2s immunity after recovery. Further stuns do not extend an active
stun or bypass immunity.

## Reproduction and validation

Committed-ready weapon assets are independent of licensed wolf art. Local Wolf
V2 installation remains described in Local-Mutated-Wolf-V2.md. The optional
`Cargo Squad/Combat/Install Weapons in IndustrialDistrict` editor command creates
missing combat assets and assigns the scene component, preserving existing weapon
asset tuning. It should be run in Edit Mode.

Run WeaponSpreadTests and WeaponCombatTests plus the existing ImpactValueTests,
MutatedWolfTests, WolfImpactTests, CargoSceneTests, CharacterDeploymentTests, and
FrontCarryTests. Weapon tests use cloned runtime definitions so test tuning never
edits the saved assets. Rendered captures and XML reports are local ignored files
under ValidationResults/Weapons.

## Verified results (2026-11-18)

Unity 6000.3.22f1 compiled the isolated project without new script/shader errors.
All 10 Edit Mode and 44 Play Mode tests passed (the prior 40 plus 14 new cases).
After making ammo labels event-driven, all 11 weapon Play Mode cases passed again.
IndustrialDistrict Heavy/Medic cone and electrical-shot frames were inspected at
1280x800. No audio playback was claimed: no weapon audio clips are installed.

New files: Scripts/Combat/{WeaponDefinition,PlayerWeapon,AimingConeIndicator,
WeaponFeedback}.cs; Editor/WeaponCombatSetup.cs; Art/Materials/CombatIndicator.shader
and .mat; Prefabs/Combat/AimingConeIndicator.prefab; four Data/Weapons definitions;
Tests/EditMode/WeaponSpreadTests.cs; Tests/PlayMode/WeaponCombatTests.cs; this document;
and accompanying Unity metadata. Existing CharacterAbility, CharacterSelection,
SurvivorVisual, CargoInteractor, CargoMissionOverlay, MutatedWolfAI,
CharacterDeploymentTests, IndustrialDistrict, and the targeted validation ignore
rule were updated. Approved Blender sources and existing bite/cargo damage values
were preserved. No commit or push was made.
## Wolf health view and hold/release input

Each MutatedWolfAI installs one WolfHealthIndicator on its own GameObject at
Awake. This uses the existing Health.Changed event, adds no Health component,
and needs no licensed prefab or scene edits. The per-instance world-space Canvas
has a dark border/track and a green (>60%), orange (>30%), or red HP fill. Damage
flashes for 0.16s and smooths over an 0.08s damping interval. Zero HP hides it
immediately. Its default world offset is (0, 2.8, 0), adjusted after inspecting
the approved wolf under the gameplay camera. It billboards to Camera.main and
compensates for orthographic zoom or perspective distance. The Inspector exposes
offset, dimensions, smoothing and flash duration. Images reject raycasts and the
Canvas has no GraphicRaycaster or physics colliders.

Aiming now arms only on a valid LMB press in the world. Holding tracks the ground
cursor and shows the cone; releasing consumes one shot and clears the indicator.
RMB has no role. Cancelled gestures require a release before another press can
arm, so selection clicks, UI drags, focus loss, pause, death and reload cannot
leave an armed shot waiting. Fire-rate and reload checks remain in TryFire; a
release rejected by those checks is discarded, never delayed into an extra shot.
Damage, pellet spread, noise, ammunition and cargo accuracy values are unchanged.

The tests now exercise held and short-click gestures, cancellation, actual shotgun
and Medic damage, stun, Q bash, threshold colors, independent health views, death
hiding and camera alignment. Screenshot helpers preserve world-space Canvases
while converting only screen-space HUD Canvases for rendered captures.
Final hold/release validation: all 52 Play Mode tests and all 10 Edit Mode tests passed in Unity 6000.3.22f1. Actual IndustrialDistrict damage, stun, bar placement, and zero-HP hiding captures were inspected at 1280x800. Reports are release-all-play.xml and release-all-edit.xml under the ignored ValidationResults/Weapons directory. No scene, approved model, or licensed prefab edits were needed for this update.
