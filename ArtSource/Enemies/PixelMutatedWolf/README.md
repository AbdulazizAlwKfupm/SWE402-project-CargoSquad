# Pixel Mutated Wolf

An original quadruped for Cargo Squad, modeled from the supplied enemy design sheet. The editable Blender scene contains the skinned mesh, armature, seven Actions, two packed atlases and a separate review studio. The FBX contains only the skinned character, rig and animation takes. The procedural authoring source is `build_wolf.py`. Running it regenerates the Blender file, FBX and PNGs in its own directory.

## Review the character

Open `PixelMutatedWolf.blend` in Blender 5.3. Select `PixelWolfRig` and choose a take in the Action Editor. The scene runs at 30 fps. The floor is at Z = 0 and forward is local −Y. The mesh has 7,566 triangles, one material, a 26-bone armature and normalized skin weights. Rigid claws and teeth follow their own paws and jaw. The segmented fur coat overlaps at the articulation points.

`PeltAtlas.png` is a 128 × 128 pixel atlas with sixteen panels for fur, skin, wounds, gums, horn and eyes. `IrisEmission.png` uses the same UVs and isolates the amber eyes. Both atlases use nearest sampling. The review studio and its lights are confined to the Blender source.

`Previews/` contains six orthographic renders, a motion contact sheet and an animated GIF for each take. The GIFs sample ten poses per take. The source and FBX include every authored frame.

`Previews/GameplayUnity.png` and `Previews/DeathUnity.png` are actual Play Mode captures from the IndustrialDistrict camera at its normal 45° angle and orthographic size 6. The validation fixture spawned the prefab for that session only.

| Take | Duration | Movement |
|---|---:|---|
| Idle | 2.00 s | Looping breath, jaw and tail movement |
| Walk | 0.80 s | In-place four-beat patrol gait |
| Run | 0.50 s | In-place bounding gait |
| Bite | 1.00 s | Windup, forward thrust, jaw contact at 0.45 s, recovery |
| Hurt | 0.33 s | Brief backward recoil |
| Stunned | 1.20 s | Unsteady stance, holds its last pose if the gameplay stun lasts longer |
| Death | 1.07 s | Folds its legs and settles onto the floor |

## Unity

The ready-to-use prefab is `Assets/_CargoSquad/Art/PixelMutatedWolf/PixelMutatedWolf.prefab`. For a private playtest, instantiate it over a baked Cargo Squad navigation surface with a deployed survivor and assign patrol points on the existing Mutated Wolf AI. Neither production mission spawns this prefab by default.

The root contains the existing AI, Health, health indicator, navigation agent, kinematic body and capsule. The nested visual uses a Generic Animator with root motion disabled. The prefab's Pixel Wolf Animation component owns the Animator. The existing AI's optional Animator field is intentionally empty so its V2-specific death placeholder cannot freeze the new Death clip.

Damage, stun recovery and navigation remain under the existing gameplay components. HP is 100, patrol speed is 1.5 m/s, chase speed is 4.5 m/s, and bite cooldown is 1.4 s. The existing one-second attack applies 15 survivor damage and, when applicable, 10 condition damage and 25 grip loss. The attack clock owns the single damage check. The animations contain no damage events. Death takes visual priority over stun, stun over bite, and bite over ordinary recoil.

The Unity asset uses a URP/Lit material with a point-filtered base atlas and emissive eye mask. It has no dependency on a licensed enemy model, rig, take, material or texture.

