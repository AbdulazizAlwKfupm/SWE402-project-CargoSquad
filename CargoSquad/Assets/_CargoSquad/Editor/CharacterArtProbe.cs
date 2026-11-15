using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Editor
{
    /// <summary>Diagnostic: samples every survivor clip on a prefab and reports key bone / socket positions.</summary>
    internal static class CharacterArtProbe
    {
        public static string Clips(string prefabName)
        {
            var text = new StringBuilder();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_CargoSquad/Prefabs/Characters/{prefabName}.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var animator = instance.GetComponent<Animator>();
                Transform muzzle = instance.GetComponentsInChildren<Transform>().First(t => t.name == "Muzzle");
                var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/_CargoSquad/Art/Characters/Survivors/Navy/Navy.fbx")
                    .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).OrderBy(c => c.name);
                foreach (AnimationClip clip in clips)
                {
                    float minFoot = float.MaxValue, maxDrift = 0f;
                    Vector3 muzzleAt = Vector3.zero, hips = Vector3.zero, hand = Vector3.zero;
                    for (int i = 0; i <= 10; i++)
                    {
                        clip.SampleAnimation(instance, clip.length * i / 10f);
                        Vector3 h = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                        minFoot = Mathf.Min(minFoot, animator.GetBoneTransform(HumanBodyBones.LeftToes).position.y,
                            animator.GetBoneTransform(HumanBodyBones.RightToes).position.y);
                        maxDrift = Mathf.Max(maxDrift, new Vector2(h.x, h.z).magnitude);
                        if (i == 0)
                        {
                            muzzleAt = muzzle.position;
                            hips = h;
                            hand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                        }
                    }
                    text.AppendLine($"{clip.name,-14} len={clip.length:F2} muzzle0={muzzleAt:F2} hips0={hips:F2} leftHand0={hand:F2} minToeY={minFoot:F3} maxHipsXZ={maxDrift:F3}");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
            return text.ToString();
        }
    }
}
