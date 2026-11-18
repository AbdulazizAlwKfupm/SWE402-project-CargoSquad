using UnityEngine;

namespace CargoSquad.Combat
{
    public enum WeaponType { Shotgun, Stun, Pistol, Rifle }
    public enum SpreadPattern { Stratified, Random }

    [CreateAssetMenu(menuName = "Cargo Squad/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public WeaponType type;
        public string displayName = "SHOTGUN";
        [Min(0)] public float damage = 12;
        [Min(.1f)] public float range = 5;
        [Range(1, 160)] public float coneAngle = 60;
        [Min(.1f)] public float shotsPerSecond = 1.25f;
        public bool automatic;
        [Min(1)] public int magazineCapacity = 5;
        [Min(0)] public int reserveAmmunition = 20;
        [Min(.05f)] public float reloadDuration = 1.6f;
        [Range(1, 32)] public int pelletCount = 7;
        public SpreadPattern spreadPattern;
        [Range(0, 1)] public float endRangeDamageMultiplier = .35f;
        [Min(0)] public float noiseRadius = 20;
        [Min(0)] public float stunDuration;
        public bool supportsCarrying = true;
        [Min(1)] public float carryingSpreadMultiplier = 1.5f;
        [Min(1)] public float sprintSpreadMultiplier = 1.2f;
        public Color effectColor = new Color(1, .75f, .35f);
        public AudioClip fireClip, emptyClip, reloadClip;
        [Range(0, 1)] public float volume = .5f;

        // Each pellet samples its own stratum; a single-shot weapon samples the central half.
        public static float SampleYaw(int pellet, int count, float angle, SpreadPattern pattern, float sample)
        {
            float t = pattern == SpreadPattern.Stratified ? (pellet + sample) / Mathf.Max(1, count) : sample;
            if (count == 1) t = .25f + t * .5f;
            return (t - .5f) * angle;
        }
    }
}
