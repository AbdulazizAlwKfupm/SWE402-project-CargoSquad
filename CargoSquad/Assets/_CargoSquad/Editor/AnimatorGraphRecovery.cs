using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CargoSquad.Editor
{
    /// <summary>Discards transient Animator view graphs before assembly reload, then restores the editor view.</summary>
    [InitializeOnLoad]
    public static class AnimatorGraphRecovery
    {
        const string Request = "Library/CargoSquadAnimatorRefresh.request";
        const string Report = "Library/CargoSquadAnimatorRefresh.txt";
        const string ReopenKey = "CargoSquad.Animator.ReopenAfterReload";
        const string ControllerKey = "CargoSquad.Animator.ControllerPath";
        static AnimatorGraphRecovery()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            if (File.Exists(Request))
            {
                File.Delete(Request); EditorApplication.delayCall += RefreshAnimatorWindow;
            }
            else if (SessionState.GetBool(ReopenKey, false)) EditorApplication.delayCall += RestoreAfterReload;
        }
        static EditorWindow[] AnimatorWindows() => Resources.FindObjectsOfTypeAll<EditorWindow>()
            .Where(w => w.GetType().FullName == "UnityEditor.Graphs.AnimatorControllerTool").ToArray();
        static int DiscardViewGraphs()
        {
            var graphs = Resources.FindObjectsOfTypeAll<ScriptableObject>().Where(obj => {
                string type = obj.GetType().FullName ?? "";
                return !EditorUtility.IsPersistent(obj) &&
                    (type.StartsWith("UnityEditor.Graphs.AnimationStateMachine.") || type.StartsWith("UnityEditor.Graphs.BlendTree")) &&
                    IsLegacyGraph(obj.GetType());
            }).ToArray();
            foreach (var graph in graphs) UnityEngine.Object.DestroyImmediate(graph);
            return graphs.Length;
        }
        static bool IsLegacyGraph(Type type)
        {
            for (; type != null; type = type.BaseType) if (type.FullName == "UnityEditor.Graphs.Graph") return true;
            return false;
        }
        static AnimatorController CurrentController(EditorWindow[] windows)
        {
            // Preserve the view's controller even when a GameObject is selected in the Inspector.
            foreach (var window in windows)
                for (var type = window.GetType(); type != null; type = type.BaseType)
                    foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                        if (typeof(AnimatorController).IsAssignableFrom(field.FieldType) && field.GetValue(window) is AnimatorController controller && controller)
                            return controller;
            return null;
        }
        static void BeforeReload()
        {
            var windows = AnimatorWindows();
            if (windows.Length == 0) return;
            var selected = CurrentController(windows) ?? Selection.activeObject as AnimatorController;
            SessionState.SetString(ControllerKey, selected ? AssetDatabase.GetAssetPath(selected) :
                "Assets/_CargoSquad/Art/Animations/Humanoid/Heavy_Player.controller");
            SessionState.SetBool(ReopenKey, true);
            foreach (var window in windows) window.Close();
            int discarded = DiscardViewGraphs();
            File.AppendAllText(Report, "Before script reload: closed Animator view; discarded temporary graphs: " + discarded + ".\n");
        }
        static void RestoreAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RestoreAfterReload; return;
            }
            SessionState.SetBool(ReopenKey, false);
            string path = SessionState.GetString(ControllerKey, "Assets/_CargoSquad/Art/Animations/Humanoid/Heavy_Player.controller");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) { Selection.activeObject = controller; AssetDatabase.OpenAsset(controller); }
            File.AppendAllText(Report, "After script reload: restored Animator view on " + path + ".\n");
        }
        [MenuItem("Cargo Squad/Tools/Refresh Animator Window")]
        public static void RefreshAnimatorWindow()
        {
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    EditorApplication.delayCall += RefreshAnimatorWindow; return;
                }
                ValidateProjectControllers();
                var windows = AnimatorWindows();
                foreach (var window in windows) window.Close();
                int discarded = DiscardViewGraphs();
                if (windows.Length > 0)
                {
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                        "Assets/_CargoSquad/Art/Animations/Humanoid/Heavy_Player.controller");
                    Selection.activeObject = controller; AssetDatabase.OpenAsset(controller);
                }
                File.WriteAllText(Report, "Heavy, Medic and Navy controller transitions valid.\nClosed Animator windows: " + windows.Length +
                    ". Discarded temporary graphs: " + discarded + ".\nController assets and open scene were not modified.\n");
                Debug.Log("Animator editor view refreshed; controller transition checks passed.");
            }
            catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Cargo Squad/Tools/Validate Animator Controllers")]
        public static void ValidateProjectControllers()
        {
            foreach (string name in new[] { "Heavy_Player", "Medic_Player", "Navy_Survivor" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    "Assets/_CargoSquad/Art/Animations/Humanoid/" + name + ".controller");
                if (!controller) throw new InvalidOperationException("Missing controller: " + name);
                Validate(controller);
            }
        }
        public static void Validate(AnimatorController controller)
        {
            foreach (var layer in controller.layers) ValidateMachine(layer.stateMachine, controller.name);
        }
        static void ValidateMachine(AnimatorStateMachine machine, string path)
        {
            if (!machine) throw new InvalidOperationException(path + ": missing state machine.");
            path += "/" + machine.name;
            foreach (var child in machine.states)
            {
                if (!child.state) throw new InvalidOperationException(path + ": missing state.");
                foreach (var transition in child.state.transitions) ValidateTransition(transition, path + "/" + child.state.name);
            }
            foreach (var transition in machine.anyStateTransitions) ValidateTransition(transition, path + "/Any State");
            foreach (var transition in machine.entryTransitions) ValidateTransition(transition, path + "/Entry");
            foreach (var child in machine.stateMachines)
            {
                foreach (var transition in machine.GetStateMachineTransitions(child.stateMachine)) ValidateTransition(transition, path);
                ValidateMachine(child.stateMachine, path);
            }
        }
        static void ValidateTransition(AnimatorTransitionBase transition, string path)
        {
            if (!transition || (!transition.isExit && !transition.destinationState && !transition.destinationStateMachine))
                throw new InvalidOperationException(path + ": missing transition or destination.");
        }
    }
}
