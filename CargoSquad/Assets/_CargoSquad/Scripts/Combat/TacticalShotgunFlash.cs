using UnityEngine;

namespace CargoSquad.Combat
{
    public sealed class TacticalShotgunFlash : MonoBehaviour
    {
        [SerializeField] ParticleSystem flash, sparks, smoke;
        [SerializeField, Min(.01f)] float flashSize = .18f;
        [SerializeField, Range(0f, 2f)] float particleIntensity = 1f;

        public void Burst(Vector3 forward)
        {
            if (particleIntensity <= 0f) return;
            Vector3 position = transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Emit(flash, position + forward * .045f, forward * 1.2f, flashSize, .083f,
                new Color(1f, .40f, .08f, .83f));
            Emit(flash, position + forward * .034f, forward * .75f, flashSize * .62f, .065f,
                new Color(1f, .95f, .65f));
            int count = Mathf.RoundToInt(4 * particleIntensity);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = forward * Random.Range(1.9f, 3.2f) +
                    side * Random.Range(-.65f, .65f) + Vector3.up * Random.Range(-.25f, .65f);
                Emit(sparks, position, velocity, Random.Range(.018f, .029f),
                    Random.Range(.10f, .18f), new Color(1f, .54f, .13f));
            }
            Emit(smoke, position + forward * .03f, forward * .25f + Vector3.up * .43f,
                .13f, .34f, new Color(.47f, .46f, .45f, .31f * particleIntensity));
        }

        static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity,
            float size, float lifetime, Color color)
        {
            if (!system.isPlaying) system.Play(false);
            var particle = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size,
                startLifetime = lifetime, startColor = color
            };
            system.Emit(particle, 1);
        }
    }
}
