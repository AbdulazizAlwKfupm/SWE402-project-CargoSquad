using UnityEngine;

namespace CargoSquad.Player
{
    [CreateAssetMenu(menuName = "Cargo Squad/Player Audio Settings")]
    public sealed class PlayerAudioSettings : ScriptableObject
    {
        public AudioClip[] footsteps = new AudioClip[0];
        public AudioClip pickup, drop, slip;
        public AudioClip missionSuccess;
        [Range(0f, 1f)] public float missionSuccessVolume = .25f;
        [Range(0f, 1f)] public float footstepVolume = .22f;
        [Range(0f, 1f)] public float cargoVolume = .3f;
        [Min(.1f)] public float metresPerStep = 1.65f;
    }
}
