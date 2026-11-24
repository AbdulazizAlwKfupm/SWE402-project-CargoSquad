# Cargo Squad Armored Creature

This enemy adapts Quaternius’s **Yeti**, model ID `ceRHrn8HHE`, from the *Ultimate Monsters* collection. The individual upload and the collection identify the base as **CC0 1.0 Universal**, which permits modification and redistribution. The downloaded GLB had SHA-256 `789fcdd3d32a1bf6ceaa0e30f9ac5d8d2c52d15fa0181d4d1d980a1b2a2ac869`. [Creator’s model listing](https://poly.pizza/m/ceRHrn8HHE) · [Creator’s pack](https://quaternius.com/packs/ultimatemonsters.html) · [CC0 dedication](LICENSE-CC0.txt)

The derivative retains the Yeti’s skinned trunk, limbs, articulated fingers, and skeleton. Cargo Squad’s concept sheet guided the broader torso, hunched stance, replacement face, alloy skull guard, spiked carapace, layered pauldrons, striped enamel panels, bolts, bracers, fangs, and claws. The two 256 × 256 pixel atlases were painted procedurally for this version. The FBX contains one skinned mesh with 12,890 triangles, 43 bones, and one material slot. The blend also contains a preview floor, camera, and lights.

## Motion

Blender stores eight Actions at 30 fps. The FBX contains those Actions as named takes on the same Generic rig. Idle, Walk, and Charge adapt the source breathing and gait cycles. Heavy Swipe uses the punch as a foundation for a one-arm sweep. Ground Smash has a two-handed overhead wind-up. Hurt and Stunned adapt the hit reaction. Death pitches the body forward, folds the legs, and holds the grounded pose.

| Take | Duration | Use |
|---|---:|---|
| Idle | 2.80 s | Guard stance, looping |
| Walk | 1.30 s | Return to guard, looping |
| Charge | 0.73 s | Pursuit, looping |
| HeavySwipe | 1.40 s | Contact at frame 23 of 42 |
| GroundSmash | 1.80 s | Contact at frame 32 of 54 |
| Hurt | 0.57 s | Nonlethal hit reaction |
| Stunned | 1.40 s | Stagger, with recovery controlled by gameplay |
| Death | 2.17 s | Collapse and hold |

Navigation supplies world-space movement. The AI samples the current Animator state at the authored contact frame, checks range, facing and line of sight, and spends the attack’s single hit. The prefab defaults are 220 HP, 22 swipe damage, 32 smash damage, 2.1 m reach, and 0.65 s of recovery after an attack.

## Review in Unity

Open `Assets/_CargoSquad/Art/PixelArmoredCreature_CC0/ArmoredCreatureReview.unity` and enter Play Mode. Select Heavy or Medic, then start. Hold and release the left mouse button to shoot. Heavy can bash with Q. As Medic, release a stun shot during the raised-arm wind-up to interrupt the attack. To see both attacks, stay within reach. To reset the encounter or change survivor, exit and re-enter Play Mode.

The standalone prefab is `PixelArmoredCreature.prefab` beside the review scene. Its URP/Lit material uses point sampling for the hide and separate eye emission atlas. The review scene uses the project’s deployment controls, gameplay camera, and navigation system. Blender views, the animation contact sheet, and Unity gameplay captures are in [Previews](Previews/).
