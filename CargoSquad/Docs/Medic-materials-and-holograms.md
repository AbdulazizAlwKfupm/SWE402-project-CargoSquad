# Medic materials, live hologram heads and sharp UI

Updated 2026-11-18 in the local CargoSquad Unity project. No commit or push.

## Corrections

Medic's importer had unrelated remaps, including off-white armor assigned to Armored_Eye, skin assigned to Heavy_Accent, teal paint assigned to Heavy_vest, and boot leather assigned to a skin material. All 22 MR1 source slots now map to dedicated, identically named materials under `Assets/_CargoSquad/Characters/MedicMaterials/`. The core palette uses the existing Medic off-white, sage, teal and equipment materials; skin, rubber, eyes, metal, red polymer, webbing and lamps have matching detail materials. Existing shared material assets and both model meshes were preserved.

The rigid Medic prefab and scene renderer assignments were updated as well as the FBX remaps. This matters because an already-unpacked visual prefab stores explicit material references and does not automatically adopt later FBX remaps.

A stale cross-model dependency was also repaired: Heavy's eye-white and ammo-box slots referenced an embedded material inside Medic's FBX. Reimporting Medic removed that subasset, leaving null materials and pink eyes. These white slots now use an independent URP material, `Heavy_ImportedWhite.mat`. Other Heavy material assignments are unchanged. Material restoration during scene setup no longer restores unresolved/null slots over repaired materials.

## Actual, rotating heads

The selection UI no longer displays cached portrait textures. Each card clones only its own model's head hierarchy:

- Heavy: `HV4_J_HEAD`, using meshes from `Heavy_v4.fbx`.
- Medic: `MR1_J_HEAD`, using meshes from `Medic_Redesign_v1.fbx`.

Each has a separate 1024 x 1024 render texture with 4x MSAA, camera, cyan rim light, orbit outline and moving scan line. The heads rotate at 22 degrees per second using unscaled time, so they keep moving while character selection pauses gameplay. Their materials remain those of the actual characters. The preview contains no body, movement controller, Animator or gameplay physics. Cameras, lights and render textures are released when Start hides the selection screen.

Heavy's orange/olive helmet and Medic's off-white medical helmet/headlamp are visibly distinct. Missing or swapped head references now fail explicitly rather than silently showing a shared portrait.

## Clear text and timer

Selection labels, role descriptions, controls, the ability cooldown timer and the mission/stamina/cargo HUD now use TextMesh Pro signed-distance-field text. The project includes Unity's bundled Liberation Sans SDF resources and font license. Larger text, pixel-perfect canvases and screen-size scaling replace the small stretched bitmap labels and legacy IMGUI boxes. The cooldown is a separate 24-point label, with a 16-point ability title. Both HUD panels continue to block ability clicks.

## Changed assets and code

- `Characters/Medic_Redesign_v1.fbx.meta`, `Characters/MedicMaterials/*.mat`: correct explicit material remaps.
- `Characters/Heavy_v4.fbx.meta`, `Characters/Heavy_ImportedWhite.mat`: independent white material reference.
- `Prefabs/Characters/Heavy_new.prefab`, `Medic_redisgned.prefab`, `Scenes/IndustrialDistrict.unity`: repaired materials and selection references.
- `Scripts/UI/CharacterSelection.cs`, `CargoMissionOverlay.cs`: SDF selection and HUD text.
- `Scripts/UI/HologramHeadPreview.cs`, `HologramOrbit.cs`, `UiTypography.cs`: live head rendering, vector orbit and shared typography.
- `Editor/MedicMaterialSetup.cs`, `CharacterDeploymentSetup.cs`: repeatable remapping and setup. Existing facing/IK tuning is preserved.
- Runtime/test assembly references, presentation tests, and `Assets/TextMesh Pro/`: required font resources.

Paths above are under `Assets/_CargoSquad/` unless otherwise noted. Existing gameplay stats, abilities, movement, stamina, camera and cargo systems remain in place.

## Validation

Unity 6000.3.22f1: **17 Play-mode tests and 5 Edit-mode tests passed**, with no unexpected gameplay Console errors. Tests cover the correct FBX mesh source for each head, independent high-resolution render textures, head-only rendering, rotation with timeScale = 0, cleanup on Start, SDF font/timer setup, all Medic remaps, valid Heavy materials, selection, animation, abilities, collision and cargo regressions.

Unity-rendered selection and HUD-only previews were visually inspected. Tests ran in an isolated local copy; the saved production code and assets were compared with the tested copy. No manual playthrough in the already-open editor is claimed.

- [Selection with actual hologram heads](Validation/Holograms/hologram-selection.png)
- [HUD-only text/timer render at 1920 x 1080](Validation/Holograms/hologram-hud-1080.png)
- [Test summary](Validation/Holograms/test-summary.txt)
- [Exact Medic remaps](Validation/Holograms/medic-remaps.txt)
- [Play-mode results](Validation/Holograms/hologram-regression.xml)
- [Edit-mode results](Validation/Holograms/hologram-editmode-final.xml)

The assets and scene are saved on disk. Reload IndustrialDistrict in Unity. If retaining unsaved scene edits, decline reload and use **Cargo Squad > Deployment > Configure open Industrial District** to apply the saved setup to the open scene. Future rebuilds can use **Rebuild characters, portraits and selection**; the runtime selection heads are live models even though the menu still regenerates static portraits for other references.
