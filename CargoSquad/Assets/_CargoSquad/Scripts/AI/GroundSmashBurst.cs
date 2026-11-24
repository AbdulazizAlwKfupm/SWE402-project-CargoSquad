using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class GroundSmashBurst : MonoBehaviour
    {
        [SerializeField] SmashGroundShape ring;
        [SerializeField] ParticleSystem debris, dust;
        [SerializeField, Min(.1f)] float waveRadius = 2.45f;
        [SerializeField, Range(.3f, .5f)] float waveDuration = .42f;
        [SerializeField, Range(0f, 2f)] float dustIntensity = 1f;
        [SerializeField, Range(8, 12)] int debrisCount = 10;
        [SerializeField, Min(.01f)] float debrisSize = .11f;
        float startedAt;

        public void Burst()
        {
            startedAt = Time.unscaledTime;
            ring.Build(180f, .81f);
            Update();
            debris.Play(false);
            dust.Play(false);
            for (int i = 0; i < debrisCount; i++)
            {
                float angle = (i + Random.value) * 2f * Mathf.PI / debrisCount;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                debris.Emit(new ParticleSystem.EmitParams
                {
                    position = transform.position + Vector3.up * .10f + outward * .18f,
                    velocity = outward * Random.Range(1.6f, 3.3f) + Vector3.up * Random.Range(1.25f, 2.3f),
                    startSize = debrisSize * Random.Range(.55f, 1.15f),
                    startLifetime = Random.Range(.32f, .52f),
                    rotation3D = Random.insideUnitSphere * 180f
                }, 1);
            }
            int count = Mathf.RoundToInt(12f * dustIntensity);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value) * 2f * Mathf.PI / count;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = transform.position + outward * .25f + Vector3.up * .12f,
                    velocity = outward * Random.Range(.85f, 2.1f) + Vector3.up * Random.Range(.08f, .36f),
                    startSize = Random.Range(.23f, .40f),
                    startLifetime = Random.Range(.52f, .76f)
                }, 1);
            }
        }

        void Update()
        {
            float elapsed = Time.unscaledTime - startedAt;
            if (elapsed >= .85f) { Destroy(gameObject); return; }
            float progress = Mathf.Clamp01(elapsed / waveDuration);
            float size = Mathf.Lerp(.28f, waveRadius, 1f - (1f - progress) * (1f - progress));
            ring.transform.localScale = new Vector3(size, 1f, size);
            ring.Tint(new Color(.74f, .64f, .52f, .62f * (1f - progress)));
        }
    }
}
