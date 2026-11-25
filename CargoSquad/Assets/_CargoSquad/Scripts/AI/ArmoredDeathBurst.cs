using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class ArmoredDeathBurst : MonoBehaviour
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

        [SerializeField] ParticleSystem sparkParticles, fragmentParticles, dustParticles;
        [SerializeField, Range(0f, 1f)] float groundContactPhase = 44f / 65f;
        [SerializeField, Range(0f, 2f)] float intensity = 1f;
        [SerializeField] BurstSettings sparks = new BurstSettings
        {
            count = 16, size = .15f, lifetime = .35f, speed = 4.2f, lift = 1.2f, radius = .68f,
            color = new Color(1f, .69f, .22f)
        };
        [SerializeField] BurstSettings fragments = new BurstSettings
        {
            count = 9, size = .18f, lifetime = .59f, speed = 2.9f, lift = 1.5f, radius = .59f,
            color = new Color(.59f, .60f, .63f)
        };
        [SerializeField] BurstSettings dust = new BurstSettings
        {
            count = 18, size = 1.42f, lifetime = .85f, speed = 2.65f, lift = .25f, radius = 1.62f,
            color = new Color(.71f, .64f, .56f, .9f)
        };

        Animator animator;
        Transform torso;
        float startedAt, removeAt;
        bool landed;

        public void Begin(Animator deathAnimator, Transform anchor, Vector3 center)
        {
            animator = deathAnimator;
            torso = anchor;
            startedAt = Time.unscaledTime;
            Emit(sparkParticles, sparks, center + Vector3.up * .42f);
            Emit(fragmentParticles, fragments, center + Vector3.up * .34f);
        }

        void LateUpdate()
        {
            if (landed)
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
            if (pose.fullPathHash != Death || pose.normalizedTime < groundContactPhase) return;
            landed = true;
            Vector3 ground = transform.position;
            if (torso) ground = new Vector3(torso.position.x, ground.y, torso.position.z);
            Emit(dustParticles, dust, ground + Vector3.up * .38f);
            removeAt = Mathf.Max(Time.unscaledTime + dust.lifetime,
                startedAt + Mathf.Max(sparks.lifetime, fragments.lifetime)) + .06f;
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
