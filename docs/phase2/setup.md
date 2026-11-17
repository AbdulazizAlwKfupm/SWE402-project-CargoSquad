# Cargo Squad Phase 2 setup

Open the existing `CargoSquad/` project with Unity 6.3 LTS (`6000.3.22f1`). Phase 1 submissions are in `docs/phase1/`. The original sample scene and template assets remain in their original locations under `CargoSquad/Assets/`.

## Asset layout

All new game assets belong under `CargoSquad/Assets/_CargoSquad/`.

```text
_CargoSquad/
  Scenes/
  Scripts/
    Core/
    Player/
    Cargo/
    Combat/
    AI/
    Missions/
    UI/
  Prefabs/
  Art/
  Audio/
  Materials/
  ScriptableObjects/
  Tests/
```

Use `Scenes/` for game scenes, `Prefabs/` for reusable objects, and `ScriptableObjects/` for shared data assets. Reserve `Scripts/Core/` for shared code. The remaining script folders group code by feature. Use `Tests/` for future Unity Test Framework tests.

## Technical decisions

- Use 3D characters and environments with a 2.5D side-view presentation. Retain the existing Universal 3D URP setup. Graphics Settings reference `PC_RPAsset`, and the PC and Mobile quality levels reference their corresponding URP assets and Universal Renderers in `Assets/Settings/`.
- Design one connected industrial mission with multiple routes that reward exploration and discovery. Keep platforming secondary to exploration, survival, and extraction.
- Plan for four selectable survivors. Cargo extraction, combat, enemy AI, and survivor progression will be implemented incrementally.
- Use the existing project manifest and lock file as the package baseline. Relevant packages include URP 17.3.0, Input System 1.20.0, AI Navigation 2.0.14, Unity Test Framework 1.6.0, Timeline 1.8.12, and Unity UI 2.0.0.
- Keep Visible Meta Files and Force Text enabled. Track `Assets/`, `Packages/`, `ProjectSettings/`, and source asset metadata. The repository-root `.gitignore` excludes generated caches, local settings, build output, and temporary files.

## Preparation status

Unity imported the scaffold and generated the folder metadata. The empty leaf folders contain `.gitkeep` files so Git can preserve them alongside their Unity-generated `.meta` files. This setup contains no new scenes, gameplay systems, or tests. The package manifest, lock file, and render pipeline settings were preserved.

Git uses `main` with upstream `origin/main` on the existing Cargo Squad GitHub repository. The Phase 2 setup is local and awaiting review.

## Next Unity session

In the Project window, expand `Assets/_CargoSquad` and check the Console for import or compilation errors. When mission work begins, create the industrial mission scene through the Unity Editor and save it under `Assets/_CargoSquad/Scenes/`. Use the new scene to block out the route connections and evaluate side-view camera framing with 3D placeholders.
