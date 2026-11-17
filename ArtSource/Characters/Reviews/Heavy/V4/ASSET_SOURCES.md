# Heavy V4 — external asset sources

Last updated: 2026-11-15.

## Used in `Heavy_v4.blend`

- **Models (scaled, parented to the pack):** `old_military_crate` (closed crate, lid, loop and latch; strapped on top of
  the pack), `metal_jerrycan_green` (pack, his right side), `ammo_box` (pack, his left side). 22,688 triangles in total.
- **Texture sets (box-projected into the existing materials: diffuse detail, normal, roughness):**
  `denim_fabric` → charcoal clothing; `rough_linen` → olive pouches and tan webbing; `fabric_leather_01` → leather straps,
  boots, gloves; `metal_plate_02` → olive armour; `rusty_metal_03` → pack shell and dark steel.
- **Downloaded but not used:** `old_gas_mask` (a full-face hose mask; it does not match the concept's twin-filter half mask),
  `garden_gloves_01` and `rubber_boots` (work gloves and wellingtons, not tactical). Gloves and boots were modelled instead.
- The texture images are referenced from `ArtSource/External/PolyHaven/` by path; they are not packed into the .blend.

## Downloaded (Poly Haven, CC0)

Source: https://polyhaven.com — every Poly Haven asset is published under **CC0 1.0** (public domain dedication):
free for any use, modification allowed, **no attribution required**. Credit is given here anyway.
Fetched through Poly Haven's public API without a login. Local copies: `ArtSource/External/PolyHaven/` (26 MB, 1k maps).

| Asset id | Type | Author(s) | Triangles | Intended use on Heavy | Page |
|---|---|---|---|---|---|
| `old_gas_mask` | model (glTF) | Michał Wiśniewski | 18,900 | respirator reference / replacement parts (filters, straps) | https://polyhaven.com/a/old_gas_mask |
| `old_military_crate` | model (glTF) | Jack Mava | 10,476 | backpack cargo container panels | https://polyhaven.com/a/old_military_crate |
| `ammo_box` | model (glTF) | DanKit | 4,382 | pack / belt utility attachment | https://polyhaven.com/a/ammo_box |
| `metal_jerrycan_green` | model (glTF) | Ulan Cabanilla | 9,636 | pack side attachment | https://polyhaven.com/a/metal_jerrycan_green |
| `garden_gloves_01` | model (glTF) | Satyaki Mandal | 6,822 | glove shape reference (work gloves, not tactical; may not be used) | https://polyhaven.com/a/garden_gloves_01 |
| `rubber_boots` | model (glTF) | L | 35,004 | boot shape reference (rubber wellingtons, not military; may not be used) | https://polyhaven.com/a/rubber_boots |
| `denim_fabric` | texture set | Rob Tuytel | – | tactical clothing weave | https://polyhaven.com/a/denim_fabric |
| `rough_linen` | texture set | colormass, Rico Cilliers | – | pouches / canvas | https://polyhaven.com/a/rough_linen |
| `fabric_leather_01` | texture set | Rob Tuytel | – | straps, gloves, boots | https://polyhaven.com/a/fabric_leather_01 |
| `rusty_metal_03` | texture set | Amal Kumar | – | pack frame, worn steel | https://polyhaven.com/a/rusty_metal_03 |
| `metal_plate_02` | texture set | Rob Tuytel | – | painted armour plates | https://polyhaven.com/a/metal_plate_02 |

Triangle counts are the figures reported by Poly Haven's API. Texture sets include diffuse, OpenGL normal, roughness and
packed AO/roughness/metal maps.

## Reviewed, not downloaded (need a manual, logged-in download)

Licence and counts are as shown on each page on 2026-11-15; the download flow itself was not tested.

| Asset | Creator | Licence on page | Size | Notes |
|---|---|---|---|---|
| Soldier Full Tactical Gear (LowPolyGameReady) — https://sketchfab.com/3d-models/soldier-full-tactical-gear-lowpolygameready-850593a8c7114c188395ba1849a66eb9 | DanlyVostok | CC BY 4.0 | 84.2k tris | Free download; attribution required. Whole soldier — only parts (gloves, boots, clothing) would be borrowed. |
| Military tactical suit (LowPolyGameReady) — https://sketchfab.com/3d-models/military-tactical-suit-lowpolygameready-ef698ce36b1545a78ce592dd3db4c7ed | DanlyVostok | CC BY 4.0 | 31.8k tris | Free download; attribution required. Tagged "3dscan" with no explanation of the source. |
| military_character_kit_1.1 — https://blendswap.com/blend/27959 | Britdawgmasterfunk | CC0 | 99.4 MB .blend | Kit-bash parts (plate carrier, pouches, helmet, MOLLE). Best fit of the three; no attribution required. |

If any CC BY 4.0 asset is used, the credit line must name the title, the creator, the licence and the source URL, and say
that it was modified.

## Not used

- No paid assets, no AI-generated meshes, no assets ripped from games.
