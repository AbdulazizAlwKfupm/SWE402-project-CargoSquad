using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class SpitterDeathBurst : MonoBehaviour
    {
        static readonly int Death = Animator.StringToHash("Base Layer.Death");

        [System.Serializable]
        struct BurstSettings
        {
            [Range(1, 24)] public int count;
            [Min(.01f)] public float size;
            [Min(.01f)] public float lifetime;
            [Min(0f)] public float speed;
            [Min(0f)] public float lift;
            [Min(0f)] public float radius;
            public Color color;
        }

        [SerializeField] ParticleSystem ruptureParticles, dropletParticles, vaporParticles;
        [SerializeField, Range(0f, 1f)] float vaporPhase = 33f / 65f;
        [SerializeField, Range(0f, 2f)] float intensity = 1f;
        [SerializeField] BurstSettings rupture = new BurstSettings
        {
            count = 18, size = 1.24f, lifetime = .44f, speed = 4.4f, lift = .56f, radius = .67f,
            color = new Color(.78f, 1f, .19f, .98f)
        };
        [SerializeField] BurstSettings droplets = new BurstSettings
        {
            count = 16, size = .17f, lifetime = .63f, speed = 4.5f, lift = 1.38f, radius = .70f,
            color = new Color(.89f, 1f, .39f)
        };
        [SerializeField] BurstSettings vapor = new BurstSettings
        {
            count = 17, size = 1.62f, lifetime = 1.12f, speed = 1.47f, lift = .23f, radius = 1.07f,
            color = new Color(.37f, .74f, .18f, .72f)
        };

        Animator animator;
        Transform torso;
        float startedAt, removeAt;
        bool vaporStarted;

        public void Begin(Animator deathAnimator, Transform anchor, Vector3 center)
        {
            animator = deathAnimator;
            torso = anchor;
            startedAt = Time.unscaledTime;
            Emit(ruptureParticles, rupture, center + Vector3.up * .30f);
            Emit(dropletParticles, droplets, center + Vector3.up * .24f);
        }

        void LateUpdate()
        {
            if (vaporStarted)
            {
                if (Time.unscaledTime >= removeAt) Destroy(gameObject);
                return;
            }
            if (!animator || !animator.isActiveAndEnabled || Time.unscaledTime - startedAt > 8f)
            {
                Destroy(gameObject);
                return;
            }
            if (Time.timeScale <= 0f) return;

            var pose = animator.GetCurrentAnimatorStateInfo(0);
            if (pose.fullPathHash != Death || pose.normalizedTime < vaporPhase) return;
            vaporStarted = true;
            Vector3 ground = transform.position;
            if (torso) ground = new Vector3(torso.position.x, ground.y, torso.position.z);
            Emit(vaporParticles, vapor, ground + Vector3.up * .53f);
            removeAt = Mathf.Max(Time.unscaledTime + vapor.lifetime,
                startedAt + Mathf.Max(rupture.lifetime, droplets.lifetime)) + .06f;
        }

        void Emit(ParticleSystem system, BurstSettings settings, Vector3 origin)
        {
            if (intensity <= 0f) return;
            system.Play(false);
            Color tint = settings.color;
            tint.a = Mathf.Clamp01(tint.a * intensity);
            for (int i = 0; i < settings.count; i++)
            {
                float angle = (i + Random.value) * 2f * Mathf.PI / settings.count;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + outward * (settings.radius * Random.Range(.6f, 1f)),
                    velocity = outward * (settings.speed * Random.Range(.55f, 1f)) +
                        Vector3.up * (settings.lift * Random.Range(.55f, 1.25f)),
                    startSize = settings.size * Random.Range(.65f, 1.15f),
                    startLifetime = settings.lifetime * Random.Range(.72f, 1f),
                    startColor = tint,
                    rotation3D = Random.insideUnitSphere * 180f
                }, 1);
            }
        }
    }
}
