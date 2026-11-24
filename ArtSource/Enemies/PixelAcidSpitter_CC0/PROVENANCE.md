# Cargo Squad Acid Spitter

Quaternius’s **Yeti**, model ID `ceRHrn8HHE`, supplies the skinned torso, limbs, fingers, skeleton and base movements. The creator publishes the individual animated model and the Ultimate Monsters collection under CC0 1.0 Universal, permitting modification and redistribution. The input was the individual GLB with SHA-256 `789fcdd3d32a1bf6ceaa0e30f9ac5d8d2c52d15fa0181d4d1d980a1b2a2ac869`. [Individual model](https://poly.pizza/m/ceRHrn8HHE) · [Creator’s pack](https://quaternius.com/packs/ultimatemonsters.html) · [License text](LICENSE-CC0.txt)

The Cargo Squad design sheet guided the narrowed torso, respirator, twin reservoirs, hose fittings, mouth grille, hanging acid, sores, spurs, claw extensions, work trousers and boots. The two point-sampled 256 × 256 atlases supply base color and emission to one URP/Lit skin material. The FBX has 9,722 triangles and 44 bones including the new mouth socket. Unity scales the model to approximately 2.18 m at rest.

## Motion and combat

All nine takes use the same Generic rig and run at 30 fps. Idle, Walk and Run loop in place. The NavMeshAgent handles world movement. SpitWindup pulls the mask back over 23 frames. SpitAttack pitches it forward and releases an acid glob from the animated SpitSocket on frame 8 of 24. The glob travels toward the position the survivor occupied at the end of the windup. The survivor can dodge or move behind cover. MeleeSwipe tests range, facing and sight at frame 23 of 42. Hurt interrupts an attack. Stunned holds until recovery. Death ends in a grounded collapse.

The prefab has 95 HP. The preferred firing distance is 4–7 m. Default spit damage is 14, speed is 7 m/s and cooldown is 1.75 s from launch. Within 1.85 m the claw swipe does 11 damage with a 1 s cooldown. The Acid Spitter AI Inspector exposes these settings. Both attacks affect survivor HP. Cargo, grip and movement speed retain their existing rules.

## Review

Open `Assets/_CargoSquad/Art/PixelAcidSpitter_CC0/SpitterReview.unity`, enter Play Mode and deploy Heavy or Medic. Stand at medium distance for spit, approach for the claw, or move away to see pursuit. Hold and release left mouse to fire. Heavy’s Q bash works at close range. Medic’s stun shot interrupts the windup. Exit and re-enter Play Mode to reset the encounter.

The standalone enemy is `PixelAcidSpitter.prefab`. Its projectile is `AcidGlob.prefab`. A gameplay instance needs a baked navigation surface and the project’s survivor. The review scene supplies both. Blender renders, the animation contact sheet and Unity camera captures are in [Previews](Previews/).
