using System;
using UnityEngine;

namespace CargoSquad.AI
{
    /// <summary>One enemy's sound set and mix. Assets live in Audio/Resources/EnemyAudio and are named after the enemy.</summary>
    [CreateAssetMenu(menuName = "Cargo Squad/Enemy Audio Profile")]
    public sealed class EnemyAudioProfile : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            public AudioClip clip;
            [Tooltip("Seconds into the clip where playback starts (skips lead-in silence or wind-up).")]
            [Min(0f)] public float startAt;
            [Tooltip("Stop after this many seconds. 0 plays to the end of the clip.")]
            [Min(0f)] public float maxLength;
            [Range(0f, 1f)] public float volume = .7f;
            [Tooltip("Random pitch range applied per play.")]
            public Vector2 pitch = new Vector2(1f, 1f);
        }

        [Header("Footsteps")]
        [Tooltip("A recording of several steps. Each step is played on its own as a short slice.")]
        public AudioClip footstepRecording;
        [Tooltip("Start time (seconds) of each usable step in the recording.")]
        public float[] stepStarts = new float[0];
        [Min(.05f)] public float maxStepLength = .55f;
        [Range(0f, 1f)] public float footstepVolume = .35f;
        public Vector2 footstepPitch = new Vector2(.94f, 1.06f);
        [Tooltip("Ground distance covered per footstep while walking or running.")]
        [Min(.1f)] public float metresPerStep = 1.1f;
        [Tooltip("Below this ground speed (m/s) the enemy counts as standing still.")]
        [Min(0f)] public float minimumSpeed = .25f;

        [Header("Attacks (played at the animation's contact or launch frame)")]
        public Cue primaryAttack = new Cue();
        public Cue secondaryAttack = new Cue();

        [Header("Taking damage and death")]
        public Cue hurt = new Cue();
        [Tooltip("Minimum gap between hurt sounds, so one shotgun blast plays once.")]
        [Min(0f)] public float hurtInterval = .3f;
        public Cue death = new Cue();

        [Header("3D attenuation (the camera listener sits about 31 m from the ground near the player)")]
        [Min(0f)] public float minDistance = 31f;
        [Min(1f)] public float maxDistance = 52f;
    }
}
