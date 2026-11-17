# Animator editor graph reload repair

The local Unity editor logged `UnityEditor.Graphs.Edge.WakeUp` during script assembly reload. Saved controller file IDs were checked across the project; no dangling local references were found. Heavy_Player, Medic_Player and Navy_Survivor were also loaded and their state machines and transition destinations validated through Unity's Editor API.

Closing and reopening the Animator alone displayed the graph correctly, but the exception returned during the next script reload. The repair therefore discards the Animator's nonpersistent editor graph objects before assembly reload and restores the view afterwards. It does not delete or rebuild Animator controller assets, change animation transitions, or modify the open gameplay scene.

Implemented in `Assets/_CargoSquad/Editor/AnimatorGraphRecovery.cs`. Manual recovery is available at **Cargo Squad > Tools > Refresh Animator Window**; transition validation is available at **Validate Animator Controllers**. The reload workaround preserves the active controller when it can read the window's controller reference, falling back to the selected controller or Heavy_Player.

The fix was applied to the user's actual open Unity 6000.3.22f1 editor. The initial repair closed one Animator window and discarded two stale transient graph objects. A subsequent script compilation/reload completed with zero new Edge.WakeUp exceptions and the Animator view restored. The historical Console entries were not suppressed or erased.

No gameplay assets, scene objects or material assignments were changed for this repair. No commit or push.
