# PixelMutatedWolf_CC0_v2

This Cargo Squad enemy is a derivative of Daniel Gruginski's Zombie Wolf, published on itch.io as **algumacoisaqq**. The source repository at revision `7158db36ad97b968c861f830185d78c3a39f70d6` ships the procedural Blender sources. The creator identifies the creature pack as CC0, and this directory includes the upstream CC0 1.0 Universal license verbatim. [Asset listing](https://algumacoisaqq.itch.io/fantasy-creatures-wolf-giant-rat-spider-wasp-centipede) · [Pinned repository](https://github.com/danielgruginski/Beasts/tree/7158db36ad97b968c861f830185d78c3a39f70d6) · [Pinned license](https://github.com/danielgruginski/Beasts/blob/7158db36ad97b968c861f830185d78c3a39f70d6/LICENSE)

## Source inventory

| Source under `src/` at the pinned revision | Contribution |
|---|---|
| `zwolf_body.py`, `zwolf_rig.py`, `zwolf_paint.py`, `zwolf_anim.py` | Zombie Wolf geometry, wounds, skinning, coat and action definitions |
| `wolf_body.py`, `wolf_rig.py`, `wolf_paint.py`, `wolf_anim.py` | Shared wolf anatomy, joint layout, UV conventions, painting and locomotion |
| `beast_common.py`, `beast_body.py`, `beast_rig.py`, `beast_paint.py`, `beast_anim.py` | Geometry, rigging, UVs, surface color and skeletal pose utilities |

The reviewed scripts were run in isolated Blender 5.3 workers launched through Blender MCP. The upstream grey coat was evaluated from the rest-space UVs and anatomical part labels. The source also defines dark and pale variants. Only the finished Cargo Squad character and its two atlases enter Unity.

## Cargo Squad adaptation

The concept sheet guided the sloped neck, swept mantle, cheek ruff, larger limbs and brush tail. The mesh retains the CC0 body and weighted skeleton. New skinned geometry adds layered fur, shoulder muscles, torn rib strands, lobed wounds, toes, claws, brows and amber irises. Two 256 × 256 atlases share one material. The base map uses charcoal, plum, exposed reds and horn colors. The emission mask isolates the eyes.

The playable mesh has 13,556 triangles and the source armature has 41 bones including attachment and motion bones. The Blender review floor, camera and lights are excluded from the FBX. The prefab uses the existing Cargo Squad AI, Health, health indicator, navigation, capsule and animation driver.

## Animation choices

The source defines twelve takes: Idle, IdlePant, Walk, Trot, Run, Bite, Lunge, Threat, Howl, Hit, Death and GetUp. Seven in-place takes are exported at 30 fps. Idle, Walk and Run loop.

| Take | Length | Adaptation |
|---|---:|---|
| Idle | 4.00 s | Retains the head turns and alternating ear flicks. Lowers the stance and opens the jaw. |
| Walk | 0.80 s | Uses the source four-leg patrol gait with a lower neck and raised brush. |
| Run | 0.73 s | Retimes the gallop for Cargo Squad pursuit. Travel remains with the navigation agent. |
| Bite | 1.00 s | Maps the source snap to the gameplay impact at 0.45 s and retains its recovery shake. The existing AI owns damage. |
| Hurt | 0.40 s | Retimes the source Hit recoil. |
| Stunned | 1.20 s | Adapts the Hit pose with lateral sway and a lowered stance. The AI controls recovery. |
| Death | 1.70 s | Retimes the side collapse and vertically corrects the larger silhouette against the floor. |

## Review

Open the blend for the editable mesh, packed atlases and Actions. The separate prefab is at `Assets/_CargoSquad/Art/PixelMutatedWolf_CC0_v2/PixelMutatedWolf_CC0_v2.prefab`. See `Previews/` for six Blender renders, the animation contact sheet and actual Unity camera captures. The Unity comparison uses the same position, rotation, camera and normalized Idle time for both characters.
