# CARGO SQUAD
## 3D Character Art Direction & Design Specification
**Project:** SWE402 Cargo Squad  
**Stage:** Phase 2 — Systems Vertical Slice  
**Target:** Blender -> Unity 6.3 LTS (6000.3.22f1), URP 17.3.0  
**Camera:** Angled ~45-degree orthographic top-down, free XZ movement  
**Status:** Art-direction proposal for review; NOT a Claude Code implementation prompt

### Source authority and notation
- **[CANON]** means established by *Game Foundation.md* or by the supplied updated Phase 2 brief. It must not be altered through an art decision.
- **[PROPOSAL]** means a new visual, characterization, or production suggestion, subject to approval. Ages, genders, face details, costume styling, monster anatomy, numerical asset budgets, and animation timing are **not** established canon.
- **[TECH]** means a suggested implementation constraint designed to support the current Unity controller. Where the repo is not yet updated with current Phase 2 code, the technical direction depends on the user's Phase 2 status summary.

## 1. Art vision and character-development logic

**[CANON] World.** Years of chemical and industrial pollution contaminated air, water, soil, animals, and humans. Government and military containment failed; the survivors operate from an underground bunker and recover medical, military, survival, and technical cargo. Contamination mutates living organisms into threatening creatures. Missions create risk through scarce supplies, noise, weak or damaged cargo, stamina, and potentially permanent loss of a deployed survivor. Each mission deploys only one of four survivors, while the others remain at the bunker. Gameplay now emphasizes exploration, discovery, combat, hiding behind environmental cover, carrying cargo, and extraction; old platforming and jumping references in the Foundation are superseded by the Phase 2 direction.

**[CANON] Core identity.** *Post-apocalyptic horror + military survival*, not bright hero shooter, sci-fi magic, or Zooba-style cartoon art. Zooba is solely a camera/presentation reference. The current prototype uses a capsule and has no implemented combat, monster AI, character-selection UI, or complete character animations.

**[PROPOSAL] Style.** Use **grounded stylized realism**: believable adult anatomy, rough material surfaces, slightly simplified geometry and concentrated visual contrast for an elevated small-screen camera. This is not photoreal cinematic scanning and not chunky low-poly cartooning. Keep realistic boots, gloves, garments, kit and battered metal/plastic while moderately enlarging *readable* role-defining features (shoulder profile, backpack outline, tool housings) by ~5–12% where necessary. Avoid making human proportions childlike. Roughness, torn fabric, taped repairs, chemical stains, chipped paint, and faded markings express survival; grotesque mutations belong primarily to enemies.

### What ITD 460 contributes
The course's Character Development I asks who the character is; what external/internal goals and conflicts shape them; how their abilities fit the world; and why a player cares. Character Development II adds **visual shorthand** through five parameters: **face, body, costume, posture/movement, powers/interactions**. The silhouette must work without textures. Square forms generally suggest stability, narrow/angular forms can suggest speed or threat, and rounded forms can imply approachability; these are contextual design conventions rather than universal laws. Costume and props should carry backstory. Its suggested **60/30/10 color hierarchy** is a useful guideline, with each survivor's accent intentionally localized. Cargo Squad should also maintain ludonarrative harmony: the character's apparent competence, burdens, and fear should agree with what the game actually lets them do.

### Common visual-language rules
- Shared **squad language:** salvage-grade tactical harnesses, repair stitching, modular belts, protective gear, worn radio units, practical gloves/boots, muted and dusty base values, subtle personalized marks. No immaculate uniforms, giant high-tech powered armor, or gratuitous fantasy ornaments.
- **Human silhouettes must differ in black:** Heavy = wide square / load-bearing; Scout = narrow tapered / asymmetric; Navy = balanced upright / organized; Medic = narrower round-rectangular / upright vertical medical rig.
- **Enemies read as threats even without VFX:** Wolf = low lean four-legged wedge; Armored = massive plated tower; Spitter = low hunched tapered predator with a highly visible swollen corrosive reservoir.
- **Top surfaces matter most:** shoulders, helmet/hood, backpack, weapon orientation, and upper-back markings carry identity at a 45-degree view. Distinct brightness and shape are more useful than tiny badges or facial detail.
- **Contamination is selective:** consistent grime, corrosive pitting, ash and patched equipment. Survivor suits should not resemble obviously mutated bodies unless the lore later calls for it. Keep creature abnormalities traceable to physical contamination, not spellcasting.
- **Never imply unimplemented abilities:** no jetpacks, shields, full invisibility devices, powered suits, resurrection medicine, or magical strikes. Visuals may anticipate the stated shotgun, pistol, rifle, stun gear, detection, and cargo abilities without introducing mechanics.

## 2. Shared survivor production baseline

**[TECH] Human skeleton.** Create one anatomically consistent Unity-compatible humanoid bone layout and Avatar mapping for all four survivors. Body proportions, clothing and accessories vary, but every character must be able to use a shared core animation library. Prefer a single reference rig with retargetable variant meshes and character-specific additive posing, not four independently designed incompatible skeletons. Use hip/spine/chest/neck/head, arms, legs, hands/fingers as feasible, and optional twist bones only when they are reliably mapped. Preserve shoulder and hip deformation clearance around gear.

**[TECH] Scale and pivot.** Treat 1 Unity unit as 1 meter; foot level at local Y=0; upright character Y-up and forward +Z after Unity import. All character prefabs should have a stable root at the ground contact point and a **separate visual child** (skinned mesh + Animator) so the existing movement/collision/stamina/cargo controller need not be rewritten. Confirm FBX axis conversion and bone orientation with an actual Unity import. Suggested heights are relative art targets, not gameplay-stat modifiers. The renderer should be movable/rotatable independently from logic, as required by the existing controller.

**[TECH] Equipment sockets.** Standardize `RightHand_Weapon`, `LeftHand_Support`, `Back_Pack`, `Hip_Sidearm`, `Chest_Tool`, `Cargo_Front` and `Cargo_Back`/`Cargo_Side` transform anchors, plus optional effect emission at weapon/stun muzzle. Bind cargo to a stable scene/controller anchor, not an uncorrected bouncing hand bone; use hand constraints/IK to *visually* follow the crate. Separate weapons, backpacks, masks, tools and cargo straps as prefabs or independent meshes. Cargo should remain visible to the player and should not obscure aim, hands, HUD, or the survivor's entire head from the camera.

**[TECH] Animation architecture.** Default to *in-place* locomotion driven by the existing controller (no root-motion takeover). Share idle, locomotion, turn, interact, cargo and hit/death animations; customize timing/pose through Animator overrides or additive clips. Use direction-aware locomotion so aim and movement can differ, if/when the combat controller supports it. The upper body should be layerable for aiming/attacks; aim direction and weapon socket alignment must be tested from the real gameplay camera. Cargo-weight differences should appear through posture and rhythm rather than altering ScriptableObject values. A passive stance may not call an unimplemented stealth/crouch state.

**[PROPOSAL] Modeling and texture budget (starting targets, not hard engine limits):** survivors LOD0 roughly 12k–20k triangles each, LOD1 5k–9k, LOD2 1.5k–4k; 1–2 reasonably packed 2K texture sets for the hero variant at maximum quality, lower-resolution LOD/quality variants where suitable. Aim for 2–4 material slots per model plus separately batched weapon/gear. Creature LOD0 approximately 8k–24k depending on size, then reduced LODs. Use game-ready topology around shoulders, elbows, hips, knees, jaw, shoulder blades and spine. Reserve alpha cards for hair/straps only where visibly useful. Prefer sculpted/normal-map damage over geometry-heavy scratches. These budgets require profiling against the intended device and camera scale.

**[PROPOSAL] Shared animation core:** idle neutral + alert; walk/run/sprint and stops/turns; 8-directional movement or a suitable blend-tree equivalent; gun/stun aim; weapon discharge + recoil; reload/ready/holster if applicable; discover/look around; examine/interact; reach/pickup; carry light/medium/heavy while walking and running; brace/regrip when grip drops; cargo slip/stumble and uncontrolled drop; deliberate low-impact manual drop; damage flinch; immediate death/fall with no revive/downed loop; exhaustion breathing. Additional optional: a cover/hiding pose only when supported by real gameplay, terrain-aware foot IK, flashlight inspection, expressive bunker selection idles.

## 3. Survivor design: HEAVY — “The Load-Bearer”

**[CANON] Role:** military soldier, strongest and healthiest, slowest, heavy combat and cargo specialist; highest cargo stability; shotgun or heavy firearm; strong against armored enemies. **[PROPOSAL] Character reading:** a reluctant protector who instinctively puts himself between danger and fragile cargo. This is an art/behavior interpretation, not locked biography.

**Body & face.** Adult roughly 38–45, masculine-presenting initial casting concept (gender/age not canon). 1.86–1.93 m; large, broad chest and back, thick neck, powerful forearms and thighs, heavy boots, substantial but not exaggerated adult head proportions. Wide squared jaw, weathered brow, minor healed brow scar, short cropped dark hair with gray at temples, short practical beard. Not a bodybuilder in a sleeveless costume—most mass comes from muscle **and** layered protective equipment.

**Clothing/armor.** Reinforced olive charcoal ballistic vest over worn military undershirt/jacket; broad square shoulder guards; thick forearm guards; reinforced knees; load-rated padded hip belt; steel-toe boots. Frayed straps and cross-chest compression harness. Wears a half-helmet with broad top plate or sturdy close-fitting combat helmet, clear at camera height. Collapsible filtered respirator remains clipped near collar, not permanently covering all facial cues. Gear has old military surplus finishes, not futuristic powered armor.

**Weapon, tools & cargo.** Short heavy shotgun carried in a pronounced two-hand stance with believable muzzle silhouette; optional alternate heavy firearm remains a future choice, not an additional gameplay capability. Wide rigid back-frame backpack with roll straps and two high rear uprights visible from top. Large protective gloves, thick load-bearing cargo sling, spare cartridge pouch and worn carabiners. Rig must allow crate held at waist/chest and gun animation while carrying if the existing mechanic permits; no magical hands-free heavy lifting.

**Palette/materials (approx. 60/30/10).** 60% charcoal/olive `#303A32`, 30% dirty tan/oxidized steel `#82775F`, 10% weathered rescue orange `#B86A3B` on shoulder and back-frame handle. Matte canvas, hard-worn polymer, brushed steel, dented edges. Rust and dry chemical residue concentrated at boot, knee, and lower vest edges.

**Silhouette/camera.** Very broad near-rectangular shoulders, deep chest, square backpack with visible twin load bars, widely planted feet; the widest survivor. The orange back-frame crossbar remains readable from behind/above while carrying cargo. Armor should never conceal the held cargo condition indicator or cover the character's position entirely. From top-down, one glance should imply “stable, durable, slow.”

**Pose/personality in motion.** Upright but weight-forward, controlled steps with heavy foot placement; slight shoulder lead and brief settling on stopping. Faster locomotion looks laborious rather than quick and athletic. Load pickup is a full-body squat/hip-hinge and bracing lift. On low grip, shoulders tense, arms clamp tighter, body rocks back to re-center. Damage animation absorbs force before recoil; death remains immediate in game logic. Shotgun firing uses measured high-recoil posture. Recommended character-specific animation clips: heavy_idle, heavy_walk_additive, heavy_sprint_additive, heavy_shotgun_recoil, heavy_cargo_brace, heavy_heavy_pickup, heavy_hit, heavy_death.

**Course justification.** Square shapes, wide stance and load hardware communicate stability and carrying strength; battered surplus armor communicates years of service and scarcity; slow weight shifts agree with lower speed and maximum cargo stability. Proposed internal conflict: duty toward the squad versus his reluctance to risk anyone else; external goal remains the shared recovery mission.

## 4. Survivor design: SCOUT — “The Pathfinder”

**[CANON] Role:** former thief and apocalypse survivor; fastest, stealthier with shorter monster detection radius, expert scavenger, weak health and heavy-cargo grip; pistol. **[PROPOSAL] Character reading:** resourceful, curious and wary; avoids a fight when a clever route or cache is available.

**Body & face.** Adult roughly 25–30, feminine-presenting initial casting concept (age/gender not canon). 1.65–1.73 m; lean functional musculature, narrow shoulders, flexible long-limbed stance, alert head tilt, asymmetrical pose. Sharp observant brows and an old small cheek scrape, energetic expression. Short uneven dark hair with cropped sides, visible beneath a thin removable hood; no eye-obscuring long hair at game camera distance.

**Clothing/armor.** Lightweight weathered dark hooded overshell, fitted layered shirt, slim reinforced trousers and flexible ankle boots. Only small chest inserts and a single asymmetric shoulder pad; tactical gloves with cut-resistant palms, useful for scavenging. Wrapped lower face or clipped filter is optional for contaminated areas. Small unobtrusive knee guards; no jangling giant key rings or huge armor plates that would contradict stealth.

**Weapon, tools & cargo.** Compact pistol in a hip holster or one-handed aim, detachable headlamp/compact flashlight, thin lock-pick/tool wallet visually suggesting pre-collapse theft expertise without inventing a hacking or lock-picking mechanic. A short asymmetric crossbody scavenging satchel, folded cloth roll, compact belt pouches, utility cord, and a carefully hidden tiny recovered keepsake. Cargo mounts to body-side or front as already supported by pickup; visibly poor stability is expressed by frequent regrips and a more unstable load pose, **not** new mechanical penalties.

**Palette/materials.** 60% charcoal graphite `#292F33`, 30% faded plum/grey-brown `#594C50`, 10% dusty amber `#B48F55` on hood edge, satchel buckle, and small shoulder seam. Soft dirty ripstop textile, worn leather, scuffed elastic, muted metal. No active glowing neon gear.

**Silhouette/camera.** Narrow tapered triangular figure, one-shoulder satchel, short hood tip, slender arms with pistol held out to the side. Scout is visually the *lightest* unit; even in black silhouette the upper-body asymmetry differentiates Scout from Navy. A limited amber satchel flap is the top-down color locator; avoid tiny pockets as primary identifiers.

**Pose/personality in motion.** Weight on the balls of the feet; frequent glance shifts and efficient small movements. Quick acceleration/deceleration and short stride frequency; brief alert pause on discovery. Stays compact during aiming and moves with low upper-body bounce. With heavy cargo, visibly leans and struggles, repositions hands, and nearly loses balance as grip weakens; this reinforces a lore weakness. Character-specific additions: scout_alert_idle, scout_fast_run_additive, scout_inspect_cache, scout_pistol_aim/recoil/reload, scout_light_pickup, scout_cargo_wobble, scout_stumble, scout_hit, scout_death.

**Course justification.** Tight silhouette, angular edges and asymmetry communicate speed, caution, and improvisation. Repaired secondhand gear communicates a survivalist thief rather than an elite special-forces operator. Proposed inner tension: autonomy versus learning to trust a squad; external goal remains discovering/collecting critical resources. Explorer/Trickster are design *interpretations*, not canon personality traits.

## 5. Survivor design: NAVY SOLDIER — “The Anchor”

**[CANON] Role:** former Navy soldier and balanced all-rounder; medium health, movement, stamina, carrying and cargo stability; rifle. **[PROPOSAL] Character reading:** disciplined, even-tempered field operative who is adaptable rather than the strongest at any one task.

**Body & face.** Adult roughly 32–38, masculine-presenting initial casting concept (not canon). 1.77–1.83 m; proportional athletic frame; squared but not massive shoulders; balanced waist-to-leg ratio, controlled resting posture. Clean-shaven or subtle stubble, calm eyes, small healed nose bridge mark, close-cropped practical dark hair. Optional soft waterproof cap over a low-profile helmet for alternate noncombat presentation.

**Clothing/armor.** Reinforced navy-blue-grey field jacket with a practical layered vest, modest symmetrical chest armor and shoulder straps; durable utility pants with patch-repaired knees; maritime-inspired wet-weather collar, sealed radio pouch and abrasion-resistant gloves. An old *generic* naval-service insignia or anchor-shaped stitching remnant can hint at former service; no unsupported real-world unit rank, national flag, or service history. He should read as an experienced ex-service survivor, not an active dress-uniform sailor.

**Weapon, tools & cargo.** Standard-length rifle with readable stock and barrel line; two-hand shoulder firing pose, sling, spare magazines, utility knife sheathed as equipment only. Medium rectangular backpack with clearly segmented side pockets, communications antenna, spare water filter and rope coil. Simple cargo straps over vest; arms and backpack must allow all carrying variants without intersections.

**Palette/materials.** 60% desaturated slate navy `#344754`, 30% weathered cool grey `#737B7B`, 10% pale muted blue `#86A4B3` on back panel and collar strip. Waxed fabric, scratched polymer, worn steel buckles. Chemical discoloration and saline-like staining imply previous harsh outdoor work without asserting literal past missions.

**Silhouette/camera.** Medium-width vertical rectangle; symmetric chest, centered small/medium backpack, long diagonal rifle creates a clean identifying line. Headgear/collar should retain a blue-grey upper plane; better posture and consistent midline distinguish Navy from both Heavy and Medic. Clear neutral baseline for comparing other survivor proportions.

**Pose/personality in motion.** Smooth steady walk/run cycles with controlled footfalls and natural rifle stabilization; measured head turns, minimal wasted gestures. Lifts cargo with practiced technique but still shows effort on heavy loads. Weapon ready/reload is calm and economical; hit and death reactions are believable but not melodramatic. Character-specific clips: navy_formal_idle, navy_rifle_aim/recoil/reload, navy_tactical_walk_additive, navy_medium_carry, navy_cargo_regrip, navy_hit, navy_death.

**Course justification.** Centered symmetry and blue-grey authority cues imply reliable composure, not exceptional strength or stealth. Balanced posture agrees with balanced gameplay. Proposed inner tension: leadership responsibility versus uncertainty after institutional collapse; shared external objective is resource recovery. Hero/steady-guide archetype is interpretive, not a new combat ability.

## 6. Survivor design: MEDIC SOLDIER — “The Lifeline”

**[CANON] Role:** military medic specializing in support/monster control; low health, medium movement and carrying, low-to-medium cargo stability; dedicated stun weapon/ability used to escape, recover cargo, control powerful monsters and create openings. **[PROPOSAL] Character reading:** focused, brave under pressure, protective and practically resourceful, not a magically healing character.

**Body & face.** Adult roughly 30–36, feminine-presenting initial casting concept (age/gender not canon). 1.70–1.78 m; lean practical build with compact shoulders and strong working hands; alert but approachable facial expression. Short tied-back dark hair or tight braid under a light protective helmet with raised top strap; faint undereye fatigue and a minor scar/taped cheek wound. Face remains at least partially visible in selection renders.

**Clothing/armor.** Dusty light sage outer shell with charcoal reinforcement panels, flexible padded sleeves, water-resistant work trousers, nitrile-compatible utility gloves under rugged protective gauntlets, compact respirator stored at collar. A medium **tall, rounded-rectangular medical pack** sits high at the back, with a light-valued top panel visible from above. Distinctive medicine patch uses a custom geometric icon and letters **MED** rather than copying protected real-world relief emblems. Lightweight protective vest rather than bulky armor, matching lower health.

**Weapon, tools & cargo.** Compact specialized stun launcher/handheld device with insulated grip and a visibly recognizable coil or pronged emitter housing. Electric-looking *equipment* is credible; avoid an always-active magical aura. Belt-mounted bandage rolls, tourniquet case, chemical detector, capped medical container and shears as visual props. These props **do not** add healing powers or items beyond established bandages and the stun ability. The tall med pack has a quick-release frame, leaving room for cargo carried in front. Tiny warm status LEDs only on the stun device.

**Palette/materials.** 60% dusty sage/green-grey `#829186`, 30% charcoal `#333D3C`, 10% high-value off-white/medical teal `#D3D8CC` / `#6BADA5` on the top of the pack and sleeve marking; keep electrical stun flash brief and locally brighter. Matte treated fabric, scuffed polymer canisters, coated wire, soft rubber. Fraying and stains indicate frequent emergency work.

**Silhouette/camera.** Relatively narrow body + unusually **tall, upright back-pack spine** and rounded upper profile. Distinct pale pack-top and teal band should remain obvious from 45 degrees. The stun instrument is held nearer the body's center than Scout's pistol. Should appear technically competent but physically less protected than Heavy.

**Pose/personality in motion.** Attentive slight forward lean; looks toward threats and escape routes. Light quick steps but no Scout-style sprint flourish. Does not perform long, casual gun poses; the stun should use a short **prepare -> aim -> emit -> recovery** motion that clearly communicates a temporary control opportunity. Brief glance to cargo and regrip conveys concern for the objective. Character-specific clips: medic_careful_idle, medic_scan, medic_stun_ready/emit/recover, medic_cargo_regrip, medic_pickup, medic_hit, medic_death. A medical inspection idle is acceptable; a new active healing ability is not.

**Course justification.** Slightly softer shapes and clear medium-light colors can signal care, while rugged gear and stun tool signal survival and control. Limited armor corresponds to low health. Proposed inner tension: the desire to save everyone versus knowing that a lone field mission cannot guarantee it; the established external goal is recovery and survival. Caregiver archetype is an interpretation.

## 7. Enemy designs — distinct combat information, not just scary decoration

### 7.1 Mutated Wolf — “The Listening Hunter”
**[CANON]** Originally a wolf. Fast ground hunter, medium health, unusually strong hearing, bite attack, dangerous when the player fires or cargo lands noisily. **[PROPOSAL]** A lean contaminated canid with elongated shoulders, corded legs, pronounced ears and rib shapes, uneven patches of matted fur and hardened scar tissue. It is still recognizably a wolf; avoid extra heads, wings, magic eyes, or humanoid armor. A low forward wedge silhouette, rearward muscular haunches, asymmetrical ear heights and bright dust-stained spine strip give recognition from above.

**Size/materials:** shoulder ~0.75–0.95 m, nose-to-tail ~1.7–2.2 m; dark ash-brown hair and grey-black skin with dirty yellow-green chemical lesions (localized, not neon). Fur can be stylized sculpted clumps or short alpha cards; use a proper four-legged rig with hips, spine, tail, neck, jaw and articulated ears. **Animation set:** scent/listening idle with ears rising, short patrol, sudden sound-orienting turn, crouch/investigate, rapid sprint/chase, bite windup + lunge + recovery, stagger/hit, stunned, dead/collapse. **Telegraph:** visibly raises ears and freezes when it hears a sound; lowers shoulders just before the bite. These pose ideas should map to existing/future AI states, not implement detection rules by animation alone.

### 7.2 Armored Polluted Creature — “The Blockade”
**[CANON]** A large slow enemy with very high health, high melee damage, hardened polluted skin, resistance to weak weapons and strong knockback. Heavy and Medic are natural counters through high damage and stun. **[PROPOSAL]** A towering hunched bipedal mutated organism rather than a person in manufactured armor. Approx. 2.2–2.6 m tall, huge irregular stone-like keratin/chemical crust across shoulders, back and forearms, a small head sunk between plated shoulder shelves, thick legs and asymmetrical plating gaps. The former species is deliberately **unspecified**. The upper back is the main visual read from the camera, with a large broken-plating pattern and rust/mineral buildup; underlayers appear leathery charcoal, not luminous fantasy lava.

**Shape/materials:** enormous squared mass with jagged slopes, near-black iron/rust/umber and limited dull sick-green fissure marks. Heavy breathing/shoulder lift and stiff articulated plate overlaps create perceived weight. Dedicated humanoid-ish/creature rig, NOT forced onto the exact survivor Avatar. **Animation set:** oppressive slow idle, heavy stomp patrol, head/shoulder turn, investigate, chase lumber, full upper-body slam anticipation, arm sweep/strike, slam impact/knockback follow-through, stunned with visible shoulder sag, heavy hit/armor flinch, death/crumple. **Telegraph:** long unmistakable windup with shoulder plates rolling back before high-impact strikes. Do not imply that armor is a detachable collectible or damageable part unless the mechanic is added later.

### 7.3 Acid Spitter — “The Corrosive Stalker”
**[CANON]** Ranged contaminated creature; keeps distance, fires acid that can damage both survivor and carried/grounded cargo. Scout's detection advantage can help avoid it. **[PROPOSAL]** A low hunched, four-limbed organism of unspecified former species; narrow extended forelimbs, long folded neck and swelling dorsal/throat chemical reservoir visible from above. A triangular pointed upper silhouette and protruding mouth nozzle distinguish it from Wolf's canine profile and Armored's huge bulk. Do **not** assert a definite animal or human origin absent foundation lore.

**Shape/materials:** length ~1.5–1.9 m in normal stance; desaturated grey-beige hide, blistered olive sacs, stained acidic yellow edges on the gland, small translucent wet membrane patches. Acid effect uses a restrained luminous stream and impact splash through Unity VFX, not a continuous magic beam. **Animation set:** crouched breathing idle, cautious step/patrol, scan, investigate, backstep/reposition to hold distance, gland inflate/charge, head lift and aim, spit release, recoil/recover, interrupt/hit, stunned, death/collapse; optional drip loop. **Telegraph:** reservoir inflates or brightens briefly and head rises before discharge, readable at gameplay distance. The actual projectile, damage, and AI distance control remain future systems.

### Enemy readability matrix
| Enemy | Seen from above | Warning motion | Expected threat |
|---|---|---|---|
| Mutated Wolf | Lean low wedge, ear/spine ridge | Ear alert, crouch, fast lunge | Speed, hearing, bite |
| Armored Creature | Huge blocky plated upper back | Shoulder windup, deliberate slam | High damage, armor, knockback |
| Acid Spitter | Low triangular frame, visible acid sac | Sac swells, neck aims, spit | Ranged player and cargo damage |

All monsters share the same physically contaminated world, but **not** a skeleton. They do not attack one another in established lore. Actual behavior, hitboxes, and damage require separate future gameplay implementation.

## 8. Animation coverage, states and integration

### Proposed clip priority matrix
| State / action | All survivors | Extra personality / enemy needs | Phase |
|---|---|---|---|
| Idle / alert idle / turn | Shared humanoid | Distinct posture overlays | P0 |
| Walk, run, sprint, start/stop, direction changes | Shared humanoid | Heavy weights steps; Scout is brisk | P0 |
| Reach, pickup, hold, walk with cargo, set down | Shared humanoid | Role-specific lift/carry posture | P0 |
| Grip slipping, regrip, involuntary fall/drop | Shared humanoid | Scout unstable; Heavy braces | P0 |
| Aim, fire/stun, recoil, recovery | Shared base upper-body | Per-weapon muzzle alignment | P1; art-ready before combat exists |
| Reload / equip / holster | Shared or weapon class | Pistol vs rifle vs shotgun vs stun | P1 |
| Hit reaction / immediate death | Shared humanoid | Heavy absorbs, Scout loses footing | P0 for assets; event hookups when available |
| Examine/discovery/inspect | Shared upper body | Scout scavenging, Medic scanning | P1 |
| Hiding/cover pose | Optional | Only if supported by actual movement/state controller | P2 |
| Wolf/Armored/Spitter idle, patrol, alert, chase, attack, stunned, hit, death | Separate creature rigs | Distinct warning anticipation / recovery | P1/P2 depending on enemy phase |

**Cargo visual states.** Cargo should be a *single existing physical gameplay object* under the relevant carry/drop logic, not extra crates baked into animation clips. Arm/hand constraints and cargo anchors should track the existing pickup system. Heavy-cargo animations may be slower and heavier-looking, but no visual-only clip may move the collision capsule or override the calculated stamina/grip values. When grip reaches zero and the gameplay system drops the cargo, the animation should release it and the gameplay object's physics/logic should govern the result. A deliberate manual drop should be visibly gentler than an accidental slip. Stun equipment should show a short prefire pose, hit feedback if supported later, and recovery timing adjustable to actual cooldowns—not hard-coded invented durations.

**Animation blending.** Use Animator parameters consistent with the existing code after inspection (do not assume names). Suggested conceptual parameters: movement magnitude, local movement X/Z, is sprinting, is carrying, cargo weight class, aim state, attack trigger, hit trigger, death trigger, grip risk and stun use. The *parameter names are examples only*. Separate upper/lower layers, IK and animation events where feasible. For the future enemy system, the Foundation's conceptual states are `IDLE/PATROL -> INVESTIGATE -> CHASE -> ATTACK -> SEARCH -> RETURN`, plus `STUNNED` and `DEAD`; use clip names that can be mapped into that future logic.

## 9. Deliverable requirements for the future Blender/Unity production handoff

**Per survivor asset package:** original editable `.blend`, export `.fbx` or equivalent Unity-tested rigged interchange, mesh + texture sets, separate weapon/tool and pack objects/prefabs, shared Avatar mapping, Animator controller or compatible override, animation clips, Unity prefab with animator under the existing player gameplay root, screenshot of a neutral A/T pose and a screenshot at real angled top-down camera distance. Avoid creating new `PlayerController`/stamina/cargo rules as a shortcut. Equipment should detach and be replaced without editing the skeleton or code.

**Per monster package:** editable `.blend`, retarget-independent creature rig, animation clips including telegraphed attacks, textures/materials, Unity prefab using a **visual-only model** initially, camera-distance preview, validated collision volume placeholder. Future AI, damage and projectiles remain separate systems.

**Suggested folder layout:** `Assets/_CargoSquad/Art/Characters/Survivors/{Heavy,Scout,Navy,Medic}`; `Assets/_CargoSquad/Art/Characters/Enemies/{MutatedWolf,ArmoredCreature,AcidSpitter}`; `Assets/_CargoSquad/Art/Animations/{Humanoid,Enemies}`; `Assets/_CargoSquad/Art/Shared/{Materials,Textures,Equipment}`. Treat as a proposal: inspect and preserve the existing project's actual conventions before adding directories.

**Technical checks:** all roots have correct meter scale and +Z forward in Unity; feet align with the existing ground surface; no double movement from root motion; no duplicate colliders/rigidbodies interfering with cargo; no missing bones/avatar errors; skinned mesh deforms under all core clips; strap/pack parts do not clip badly; clear armor/cloth weights at shoulders during pickup and aim; hands align to weapon muzzle and crate grip; Unity URP materials render correctly, without magenta shaders; no warnings on asset import; no unintended scene changes.

### Gameplay-camera validation (mandatory)
1. In `IndustrialDistrict`, place each character at normal orthographic zoom and ~45-degree pitch, including shadow, night/low-light and contaminated surface contexts.
2. Compare grayscale **black silhouettes** at a typical play size (also try roughly 64–128 pixels tall). Four survivors must be recognizable *without looking at names, health bars or color accents*; enemy classes must be distinguishable from each other.
3. Repeat with flat-color textures and after one survivor picks up **light, medium, and heavy cargo**. Character, crate, hand pose, equipped gun and nearby hazards must remain discernible.
4. Check orientation in all XZ directions, including heading toward and away from the camera; back/upper body details should remain useful.
5. Verify animations under real movement, stamina and grip changes. The character model does not alter mechanics or cargo condition.

### High-impact two-day priority recommendation
**A. Shared foundation (first):** build one working clean **Navy** humanoid mesh/rig, import to the existing controller, validate directional locomotion and core cargo pickup/carry/drop in Unity. This de-risks the full pipeline before making four isolated polished characters.

**B. Four distinct survivors (next):** derive Heavy, Scout and Medic from compatible shared rig, then differentiate silhouette, material palette, backpack, weapon, posture and role-specific animation overlays. Aim for *four credible production-style game characters with useful core motion*, not four cinematic-only high-resolution renders.

**C. Monsters (after survivors work):** at minimum produce readable creature blockouts/silhouettes and a rig/test animation for each; prioritize detailed creature animation only after survivor functionality is verified. Enemy AI/combat implementation should remain a separate planned phase.

This is a production ordering recommendation, **not** a change to the game's approved feature scope or an assertion that all models can necessarily be completed within two days.

## 10. Acceptance checklist and no-go rules

- [ ] Canon survivor roles and fixed weapon categories remain unchanged; no new survivor is stronger at everything.
- [ ] The four humans share a Unity-compatible humanoid skeleton and usable core clips.
- [ ] Four silhouettes are distinguishable at actual top-down camera size, both textured and solid black.
- [ ] Heavy = strongest visually; Scout = lightest/stealthiest; Navy = balanced; Medic = clear support/stun profile.
- [ ] All props connect to identity and world history; no unexplained futuristic powers, bright cartoon suits or fantasy designs.
- [ ] Existing movement, stamina, grip, cargo damage, extraction and outcome logic are unaffected.
- [ ] Cargo stays a separate existing game object and is visible while held, released and dropped.
- [ ] All four have playable-state core animation support and sensible weapon/stun attachment points.
- [ ] Monsters have three unambiguous shapes, with visible hearing/chase, melee-windup and acid-spit cues.
- [ ] Blender project sources, imports, materials, rigs and Unity assets are editable/reusable and can be validated in the scene.
- [ ] All current gameplay feature gaps (combat, enemy AI, character selection) are acknowledged; art work does not falsely mark them implemented.

## 11. Lore-versus-design decisions to approve before production

| Topic | Established by Foundation/Phase 2 | Suggested design decision, not canon |
|---|---|---|
| Age, gender, appearance, names | Not specified for the four survivors | Heavy older masculine, Scout younger feminine, Navy masculine, Medic feminine; can be changed without altering gameplay |
| Team motivation | Recover essential supplies; protect bunker, survive; squad formed after apocalypse | Heavy protector, Scout fiercely independent, Navy disciplined anchor, Medic compassionate pragmatist |
| Equipment | Heavy firearm, pistol, rifle, stun; carrying cargo | Specific silhouettes, pack shapes, clothing layers, pouches and protective masks |
| Monster origins | Wolf is a wolf; armored and spitter have contamination-based creature identities, exact original species not specified | Armored hunched biped and spitter four-limbed crawler; no invented confirmed ancestry |
| Character mechanics | Stats, stealth, stun, grip/stamina, cargo and combat per existing design | Animation/personality expression only, with no new mechanics |
| Technical budgets | Unity 6.3 URP and in-scene game characters | LOD triangles, textures, color hexes, skeleton socket names and folder structure |
| Perspective | Angled top-down XZ 3D exploration, not platforming | Upper-body-driven readable costumes, subtle semi-realistic exaggeration |

**Final creative standard:** A player should identify the survivor and anticipate their strengths by looking at the figure from the gameplay camera. A threat should be recognizable before it attacks. Each model should look like it belongs to the same failed industrial/military world and be demonstrably usable as a rigged animated Unity asset—not only as concept art.
