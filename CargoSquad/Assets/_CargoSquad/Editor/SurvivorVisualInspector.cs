using CargoSquad.Visuals;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Editor
{
    [CustomEditor(typeof(SurvivorVisual))]
    public sealed class SurvivorVisualInspector : UnityEditor.Editor
    {
        private UnityEditor.Editor statsEditor;
        private bool showVisualSettings;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var definition=serializedObject.FindProperty("definition");
            EditorGUILayout.LabelField("Character Stats",EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(definition,new GUIContent("Character Definition"));
            serializedObject.ApplyModifiedProperties();
            var stats=definition.objectReferenceValue;
            if(stats)
            {
                EditorGUILayout.HelpBox("These saved stats belong to "+stats.name+". Max Health is applied when this character deploys. Other characters use their own definitions.",MessageType.Info);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    CreateCachedEditor(stats,null,ref statsEditor);
                    statsEditor.DrawDefaultInspector();
                }
                if(EditorApplication.isPlaying) EditorGUILayout.HelpBox("Stop Play mode to edit saved character stats. Live health is on PlayerPlaceholder.",MessageType.Info);
            }
            else EditorGUILayout.HelpBox("Assign this character's own CharacterDefinition to configure health, stamina, speed and damage.",MessageType.Warning);
            EditorGUILayout.Space();
            showVisualSettings=EditorGUILayout.Foldout(showVisualSettings,"Animation, facing and sockets",true);
            if(showVisualSettings)
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject,"m_Script","definition");
                serializedObject.ApplyModifiedProperties();
            }
        }
        private void OnDisable() { if(statsEditor) DestroyImmediate(statsEditor); }
    }
}
