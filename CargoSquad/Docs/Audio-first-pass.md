# Cargo Squad audio handoff

The approved shotgun reloads insert individual shells. Medic now uses the user-approved original energy discharge and electrical recharge previews. Mission success has a brief electronic completion cue; all other sounds are preserved.

## Behavior and tuning

- Medic audio only: the approved original procedural previews are applied byte-for-byte to `StunFire.wav` (0.48-second energy discharge) and `StunReload.wav` (exactly 1.30 seconds, ready snap at 1.17). No sampled audio or Call of Duty recordings were used. Fire/reload peaks are −9/−12 dBFS; volume remains 0.28. Existing fire/reload callbacks, ammo timing, GUIDs, gameplay and approved Heavy audio are unchanged.

- Shotgun: each committed shell adds 1 to magazine, removes 1 from reserve and plays one insertion cue in the same frame. Only missing shells are loaded. Full magazine or empty reserve does nothing. The finishing/rack cue plays once after the final insertion. The legacy fixed reload clip is excluded from the commit; shotgun reloads reference only the insertion and rack clips.
- `HeavyShotgun.asset` controls `shellInsertInterval` (0.65 seconds), `shellInsertClip` and `reloadFinishClip`. There is an initial insertion delay, one interval per inserted shell, then time for the last insertion and rack. Approximate totals: empty 0/5 → 5 inserts, 4.42 seconds; 2/5 → 3 inserts, 3.12 seconds; 0/5 with 2 reserve → 2 inserts, 2.47 seconds.
- Repeated reload requests cannot overlap. Fire with a loaded shell, explicit `CancelReload`, disable, focus loss, pause, death or scene unload cancels pending insertions and rack audio; already committed shells remain. Other weapons retain their timed magazine reload. Reload sounds never emit AI `GameplayNoise`.
- Existing character controllers have no reload animation/state/parameters, so there is no supported animation to synchronize. Character assets and controllers are unchanged. `ReloadCuePlayed` exposes the exact ammo/cue moment for future animation integration.
- `MissionSuccessAudio` subscribes to the existing `ResultFinalized` event. Only `CargoRecovered` plays, once per scene. Entering extraction without cargo, cargo-loss escape and death remain silent. The 1.3-second cue plays independently of gameplay time/listener pause, without delaying or changing the result screen or mission logic.
- Player audio settings remain in `Audio/Resources/CargoSquadPlayerAudio.asset`: success volume 0.25, footsteps 0.22, cargo 0.30. Weapon volume remains 0.28. Footsteps still require resolved grounded movement; cargo uses one pickup/drop/slip voice. The optional local Configure First Pass helper is excluded from the commit; all required references are serialized in the weapon/settings assets.

## Credits and source records

| Original creator and source | Selected audio |
| --- | --- |
| [zer0_sol — Shotgun Reload Sound effects](https://opengameart.org/content/shotgun-reload-sound-effects) | `Subsequent Shells.mp3` for individual insertions and `Rack.mp3` for finishing. Trimmed, faded and level-balanced; insertion tempo 1.25× without pitch change. |
| [SnakeF8 — SECOND Authentic Gun Sounds](https://f8studios.itch.io/snakes-second-authentic-gun-sounds-pack) | Shotgun/rifle/pistol fire, dry fire and other mechanical reload cues. |
| [Kenney — Impact Sounds](https://kenney.nl/assets/impact-sounds) | Concrete footsteps; light/heavy wood for cargo pickup/drop/slip. |
| Original procedural synthesis — CC0 | Approved Medic fire/reload, synthesized from scratch without input audio. `MedicEnergy-CC0.txt` retains the grant and preview origin. |
| [Kenney — Sci-fi Sounds](https://kenney.nl/assets/sci-fi-sounds) | Success cue derived from `forceField_002.ogg` as three ascending pitched accents with soft fades, capped at −12 dBFS. This is a designed electronic cue, not a pre-existing victory jingle. |

All source audio is CC0. Licenses, creator permission records and source links are retained in `Audio/Licenses`; the gun ZIPs contain no standalone licenses, so their public CC0 declarations are documented there. `SelectedClips.json` records original archive members, SHA-256 hashes and edits. Original ZIPs remain outside Assets. [CC0 permits modification and public redistribution of the audio files](https://creativecommons.org/publicdomain/zero/1.0/).

## Validation and listening

Medic approved-preview application: all 3 targeted Unity 6000.3.22f1 Play Mode tests passed in the isolated copy. Tests verified nonzero fire and reload DSP output (including the late recharge), 48 kHz source playback, exact 0.48/1.30-second clip lengths, ammo staying unchanged before 1.3 seconds and refilling at the original reload boundary. All-weapon audio and non-shotgun magazine reload regressions passed. Both WAVs match the approved previews byte-for-byte; all other sounds, gameplay, GUIDs and existing local work are unchanged. The CC0 grant is retained. Checks were programmatic; the owner approved the previews by listening.

Earlier full-suite validation:
Unity 6000.3.22f1: all 12 focused Play Mode tests passed with audio enabled in an isolated project copy; the owner's open editor was untouched. Cases verified 0/5 → 5 insertion sounds, 2/5 → 3, full → 0, 0/5 with 2 reserve → 2, no reserve → 0, and 4/5 with 1 reserve → 1; each successful reload finishes once. Tests cover repeated R, API and real mouse fire interruption, cancellation, disable/focus/pause/equip, death before/after an insertion, and scene unload. Other weapons retain timed magazine reloads. Success produced nonzero DSP output while the result panel and listener were paused, played once on cargo recovery, and stayed silent for other outcomes. Existing footstep/cargo checks also passed. No perceptual listening has been performed. In IndustrialDistrict, compare 0/5, 2/5, full and low-reserve reloads; interrupt loading; then deliver cargo and listen over the paused report. Confirm the insertion count matches ammo and the finishing cue plays once.

Manual commit scope: 18 active WAVs and their metas, CC0/source records, the audio settings asset, four weapon assets, runtime scripts, two Play Mode test files and this handoff. Exclude the unused `ShotgunReload.wav`/meta, optional `AudioFirstPassSetup.cs`/meta, preview/source recordings, caches, the Medic material and old asphalt files. Existing tracked parent/assembly/meta dependencies are already in main.

Commit-candidate validation: Unity 6000.3.22f1 passed all 12 focused Play Mode tests from HEAD plus exactly the 73-file audio whitelist, with the optional setup helper and retired fixed shotgun clip absent. The smoke test checks the active shotgun insert/rack clips; non-shotgun timer assertions allow 1 ms for float timestamp subtraction. This preparation does not change approved audio or runtime behavior.
