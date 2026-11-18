# IndustrialExpansion prototype

## Scene

`Assets/_CargoSquad/Scenes/IndustrialExpansion.unity` — open the `CargoSquad/`
project with Unity **6000.3.22f1**. The saved scene is ready to play; no setup
command is required.

## Map contents

A 30 × 24 m industrial yard with boundary fencing, an open-roof warehouse,
shelving, containers, crates, cover, pipes and lamps. Wide cargo routes lead to
the existing cyan extraction zone. A helicopter and hanging ropes are visual only.
Evening lighting combines dim neutral/cool-gray surroundings with warm lamps;
the floor uses subtle filtered seams and camera antialiasing to reduce flicker.
Existing Heavy/Medic characters, selection, HUD and gameplay systems are reused.
IndustrialDistrict is preserved.

## Controls and play

Press Play, select Heavy or Medic, then press Start. Find the warehouse cargo
near `(0, 0.225, 7.4)`, pick it up and carry it through the central entrance to
EXTRACT near `(11, 0, -3)`.

- **WASD:** move; **Shift:** sprint, draining stamina and cargo grip.
- **E:** pick up or drop cargo; **Q:** character ability.
- **Hold left mouse:** aim; **release:** fire; **R:** reload.

## Known limitations

- Simplified prototype scenery; helicopter and ropes have no flight or rope gameplay.
- The optional approved `Resources/MutatedWolfV2` asset is not included. Without
  it, the loader warns and no wolf spawns; live enemy behavior needs separate review.
- Restart Mission and standalone builds require adding this scene through
  **File > Build Profiles > Scene List > Add Open Scenes**. Preserve existing entries.
- **Cargo Squad > Maps > Polish Industrial Expansion Section** rebuilds generated
  scenery and dedicated art, replacing manual edits within that generated content.
- Validation captures, recordings, caches and backups are local files excluded
  from the prototype commit.
