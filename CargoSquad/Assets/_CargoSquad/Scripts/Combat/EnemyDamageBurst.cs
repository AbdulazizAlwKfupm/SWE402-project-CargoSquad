using UnityEngine;

namespace CargoSquad.Combat
{
    public sealed class EnemyDamageBurst : MonoBehaviour
    {
        [SerializeField] ParticleSystem spray, accent;
        [SerializeField, Min(.01f)] float effectScale = 1f;
        [SerializeField, Range(1, 12)] int particleCount = 8;
        [SerializeField, Range(0, 5)] int accentCount = 2;
        [SerializeField] Color particleColor = Color.white, accentColor = Color.white;
        [SerializeField, Range(.12f, .5f)] float particleLifetime = .32f;
        [SerializeField, Min(0f)] float burstStrength = 1.65f;
        [SerializeField, Min(.01f)] float particleSize = .065f;
        [SerializeField, Min(.01f)] float accentSize = .29f;
        [SerializeField] bool sparkFlash;
        [SerializeField, Min(.01f)] float sparkFlashSize = .19f;
        [SerializeField] EnemyHitFlash.Settings hitFlash = new EnemyHitFlash.Settings
        {
            color = Color.white,
            intensity = .75f,
            duration = .10f
        };

        public EnemyHitFlash.Settings HitFlash => hitFlash;

        public void Burst(Vector3 point, Vector3 normal)
        {
            Quaternion basis = Quaternion.FromToRotation(Vector3.up, normal);
            if (!spray.isPlaying) spray.Play(false);
            if (!accent.isPlaying) accent.Play(false);
            for (int i = 0; i < particleCount; i++)
                Emit(spray, point, basis, particleSize, particleLifetime, burstStrength, particleColor);
            for (int i = 0; i < accentCount; i++)
                Emit(accent, point, basis, accentSize, particleLifetime * .87f, burstStrength * .43f, accentColor);
            if (sparkFlash)
                spray.Emit(new ParticleSystem.EmitParams
                {
                    position = point, startSize = sparkFlashSize * effectScale,
                    startLifetime = .065f, startColor = new Color(1f,.88f,.48f)
                }, 1);
        }

        void Emit(ParticleSystem system, Vector3 point, Quaternion basis, float size, float lifetime,
            float speed, Color color)
        {
            Vector2 spread = Random.insideUnitCircle;
            Vector3 direction = basis * new Vector3(spread.x, Random.Range(.32f, .86f), spread.y);
            system.Emit(new ParticleSystem.EmitParams
            {
                position = point,
                velocity = direction * (speed * Random.Range(.7f, 1.3f)),
                startSize = effectScale * size * Random.Range(.65f, 1.2f),
                startLifetime = lifetime * Random.Range(.75f, 1f),
                startColor = color,
                rotation3D = Random.insideUnitSphere * 180f
            }, 1);
        }
    }
}
