# Mutated Wolf redesign v1 — asset sources

Last updated: 2026-11-16.

## Selected base (used in `MutatedWolf_Redesign_v1.blend`)

- **Asset:** Dire Wolf Lite v1.0.0
- **Creator:** Literock Studios (Literock Ventures)
- **Source:** https://literock-studios.itch.io/direwolf-lite (name-your-price, free)
- **Local copy:** `ArtSource/External/Dire_Wolf_Lite_v1.0.0/` (unmodified download, with its `LICENSE.txt`)
- **Licence (bundled LICENSE.txt, "Dire Wolf Lite — Free Licence"):** free use in any number of games and other end
  products, commercial or not, no royalties; modification allowed; credit "Dire Wolf by Literock Studios" appreciated but
  not required.
- **Restrictions:** the asset, or a modified version of it, may **not** be redistributed, resold or re-uploaded on its own
  or in another asset pack; shipping it inside a built game is allowed. It may not be used as machine-learning training
  data. **Consequence for this repo: do not push the raw download or the modified .blend to a public repository.**
- **AI disclosure by the creator:** the base mesh and textures were generated with Meshy (paid commercial plan); the rig,
  weights and animations were made by the studio.
- **Imported components:** mesh (9,991 triangles), 35-bone rig with its skin weights, four actions (idle_loop, walk_loop,
  run_loop, bite), three 2K textures (base colour, normal, metal/roughness). The rig and actions are kept unchanged.
- **Changes made:** body reshaped (shoulder hump, neck ruff, starved flanks, rib grooves); painted masks for open tissue,
  bare skin and chemical staining; hide material built on the base textures; added growth clusters, matted-fur clumps,
  claws, fangs and glowing eyes, all skinned to the base rig; posed with the existing rig for review.

Credit line: "Dire Wolf by Literock Studios" — https://literock-studios.itch.io/direwolf-lite (modified).

## Other candidates reviewed and not used


| | Realistic wolf (demo) | Low-poly stylized wolf | Low-poly wolf (rigged) |
|---|---|---|---|
| Title | WOLF - Realistic 3D Model (DEMO FREE) | Low poly Stylized Wolf with Rigged and Animation | Wolf Low Poly (Rigged) |
| Creator | WildMesh 3D | Duc Tran | pomilecrown |
| URL | https://sketchfab.com/3d-models/wolf-realistic-3d-model-demo-free-8840b859a566485c874a27b1731e1db8 | https://sketchfab.com/3d-models/low-poly-stylized-wolf-with-rigged-and-animation-948b4e9527d54de5894f44c5508f3522 | https://blendswap.com/blend/17384 |
| Licence | **CC BY-NC 4.0** (attribution, non-commercial only) | CC BY 4.0 (attribution) | CC0 (no attribution needed) |
| Size | 16,262 faces / 8,447 vertices | 3,890 triangles / 2,056 vertices | 318 vertices (creator's figure) |
| Rig / animation | 2 animations (so rigged) | rigged, 2 animations | rigged, spline-IK tail; no animation mentioned |
| Formats | not listed by the API (tag: fbx) | Maya, Blender, FBX, glTF, Unity (per description) | .blend (Blender 2.7x), .obj, .3ds |
| Textures | not verified | base colour only, hand-painted | not verified |
| Fit as an anatomy base | Best: realistic proportions, enough geometry to sculpt mutations | Weak: stylized, simple forms | Weakest: extremely low-poly, angular |
| Download | Sketchfab login required | Sketchfab login required | download page asks to sign in |

Notes:

- The realistic wolf is a free demo of a paid animal pack. Its **non-commercial** licence allows modification and use in a
  non-commercial student project with credit, but not in anything sold or monetised. This needs a team decision.
- No sign that any of the three was ripped from a commercial game.
- A search of Sketchfab's CC0 and CC BY downloadable models for "wolf" found no better realistic, game-sized wolf: results were
  museum scans, unrelated items, a "COD: Ghost's Wolf" model (likely a game rip; rejected), and two 1.9-million-face models.
- Poly Haven (CC0) has no wolf or dog model. Its existing texture sets in `ArtSource/External/PolyHaven/` can still be reused.

## Attribution text to use if selected

- Realistic wolf: "WOLF - Realistic 3D Model (DEMO FREE)" by WildMesh 3D, CC BY-NC 4.0, https://sketchfab.com/3d-models/wolf-realistic-3d-model-demo-free-8840b859a566485c874a27b1731e1db8 — modified.
- Stylized wolf: "Low poly Stylized Wolf with Rigged and Animation" by Duc Tran, CC BY 4.0, https://sketchfab.com/3d-models/low-poly-stylized-wolf-with-rigged-and-animation-948b4e9527d54de5894f44c5508f3522 — modified.
- BlendSwap wolf: none required (CC0); credit pomilecrown as a courtesy.

## Where to put a manual download

`ArtSource/External/Wolf/<short-name>/` (keep the original archive and files unmodified).
