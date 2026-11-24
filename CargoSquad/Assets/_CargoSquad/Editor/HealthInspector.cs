using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Editor
{
    [CustomEditor(typeof(Health))]
    public sealed class HealthInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var health=(Health)target;
            if(!health.GetComponent<PlayerController>()) DrawDefaultInspector();
            else
            {
                EditorGUILayout.HelpBox("This is the selected survivor's shared runtime health, not the saved health of both characters. Select Heavy_new or Medic_redisgned and edit Character Stats > Max Health instead.",MessageType.Info);
                using(new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField(EditorApplication.isPlaying?"Active Maximum":"Fallback Maximum (overridden at deployment)",health.Maximum);
                }
            }
            if(EditorApplication.isPlaying)
                using(new EditorGUI.DisabledScope(true)) EditorGUILayout.FloatField("Current Health",health.Current);
        }
    }
}
