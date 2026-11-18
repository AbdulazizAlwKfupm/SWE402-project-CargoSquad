# Local Mutated Wolf V2 integration

The approved V2 model derives from Dire Wolf Lite by Literock Studios. Obtain the
base and approved Blender source through the team's permitted private handoff.
The licence permits use in a packaged game and restricts redistribution of the
standalone original or modified model. Keep the complete generated folder local.
The bundled licence and source notes are in
`ArtSource/External/Dire_Wolf_Lite_v1.0.0/` and
`ArtSource/Characters/Reviews/MutatedWolf/RedesignV1/ASSET_SOURCES.md`.

## Reproduce

1. Use Unity 6000.3.22f1 and Blender 5.3. Put the approved source at
   `ArtSource/Characters/Creatures/MutatedWolf_Redesign_v2.blend`.
2. Close Play Mode. From the repository root run this PowerShell command.

   ```powershell
   & 'C:/Program Files/Blender Foundation/Blender 5.3/blender.exe' --background --python ArtSource/Characters/Scripts/export_wolf_v2_unity.py -- (Get-Location).Path
   ```

3. Let Unity import the generated assets, then run
   **Cargo Squad → Enemies → Install Local Wolf V2**.
   This rebuilds the local prefab and adds the spawn and navigation objects to
   IndustrialDistrict if they are absent. It keeps the existing original wolf
   prefab available.
4. Open `Assets/_CargoSquad/Scenes/IndustrialDistrict.unity`, enter Play Mode and
   deploy Heavy or Medic. The wolf starts at (8, 0, -7) and patrols the three
   **Wolf Patrol** transforms under **Mutated Wolf V2 Spawn**.

The exporter reads the approved source without saving it. It exports the same
35-bone rig, five skinned meshes, and idle/walk/run/bite actions. Procedural colour,
normal, emission, and roughness maps are baked locally and converted to URP Lit
materials. Movement comes from the NavMeshAgent, with root motion disabled.

The ignored folder
`CargoSquad/Assets/_CargoSquad/Art/LocalMutatedWolfV2/` contains the generated
FBXs, textures, materials, animation controller, prefab and their Unity metadata.
Preserve its metadata for subsequent local rebuilds. The shared scene refers to
the loader and patrol transforms. The loader requests the local resource
`MutatedWolfV2` and reports a warning if the installation is missing.
The raw `ArtSource.zip` archive is also ignored because it contains restricted
source files.

## AI and damage

- Inspector controls on the local prefab configure patrol and chase speeds,
  sight and hearing ranges, target loss, search time, bite range, damage, cooldown,
  and the bite impact fraction.
- Navigation is built once from the district's existing static box colliders.
  Player, cargo, triggers, and enemy physics are excluded from the bake.
- Idle/patrol, investigate, chase, attack, search/return, and death are explicit
  states. Detection checks distance and obstruction. Chase periodically repaths.
- Hearing subscribes to the existing player's
  `CargoInteractor.NoiseEmitted` event. Currently that event reports cargo slips.
  Future supported noise producers can call `HearNoise(GameplayNoise)`.
- Bites apply the existing `IDamageable` interface once at the configured
  normalized impact point in the imported bite clip. Escaping range or breaking
  line of sight before impact avoids damage. The existing Heavy capsule cast
  finds the wolf's existing `Health` component.
- The existing defaults remain 15 player HP and a 1.4-second cooldown. Carried
  cargo additionally loses 10 condition and 25 grip percentage points per bite,
  configurable on the same wolf component. Grip recovery and continuous load
  modifiers continue through their existing APIs.
- The hit snapshots the held cargo, applies player HP damage, then condition
  damage and grip loss. A surviving item at zero grip uses the existing slip
  placement, noise, and condition consequence exactly once. A destroyed item
  is discarded and cannot be delivered. Blocked slip placement stays pending
  without repeating bite or slip damage.
- Survivor death is recorded in the existing extraction controller. Delivery
  resolves after attack updates so death wins over a delivery queued in the
  same frame. The mission HUD shows failure and zero HP. Surviving with
  destroyed cargo still permits the existing escape-only outcome.
- The approved source has no death clip. Death stops navigation and attacks,
  freezes idle and tips the visual sideways, then disables its collider.

## Verification

Run the Play Mode `CargoSquad.Tests.MutatedWolfTests` tests after local installation,
plus the existing cargo and character deployment tests. Wolf tests explicitly
skip with an installation message on machines without the licensed local prefab.
The open Editor project can be verified in a separate copy so unsaved scene
editing is preserved.

The integration run passed six Wolf V2 Play Mode tests. Twelve existing cargo
and character deployment tests passed in the same validation project. A Generic
Avatar import issue found by the expanded animation test was fixed, then all
six wolf tests passed again. A final rendering and spawn-clearance check also
passed. Local XML results and a Unity render are under
`CargoSquad/ValidationResults/WolfV2/`.

## Health, cargo, and grip validation (2026-11-18)

The expanded isolated Unity 6000.3.22f1 run passed 7 Edit Mode and 33 Play Mode
tests with no failures. This includes 12 new impact/HUD scenarios, six wolf AI
tests, and existing cargo, character deployment, and front-carry regressions.
Coverage includes both characters with/without cargo, animation-timed damage,
blocked and fatal slips, direct/drop cargo destruction, death versus delivery
priority, scene restart, Heavy bash, and Medic stamina recovery.

The older front-carry regression now isolates its cargo checks from combat and
allows 60 physics steps for the current selected character's initial movement.
Actual bite tests run with the wolf active. Gameplay movement was not changed.

The existing upper-left mission panel contains the animated health bar and
immediate numeric HP; it retains stamina, cargo condition, grip, and warnings.
Rendered 1280x800 damage and zero-health failure captures were inspected.
`ValidationResults/WolfV2/bite-final.xml` and `impact-edit-final.xml` hold the
successful runs; `bite-health-cargo-grip.png` and `survivor-death.png` hold the
captures. These local validation artifacts remain ignored by Git.