using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class WolfDeathBurst : MonoBehaviour
    {
        static readonly int Death = Animator.StringToHash("Base Layer.Death");
        [SerializeField] ParticleSystem crimson, infected, mist, dust;
        [SerializeField, Range(1, 20)] int crimsonCount = 13;
        [SerializeField, Range(1, 16)] int infectedCount = 9;
        [SerializeField, Range(1, 12)] int mistCount = 7;
        [SerializeField, Range(1, 12)] int dustCount = 6;
        [SerializeField, Min(.1f)] float sizeMultiplier = 1f;
        [SerializeField, Min(.1f)] float spread = 2.35f;
        [SerializeField] Color crimsonColor = new Color(.88f, .075f, .16f);
        [SerializeField] Color infectedColor = new Color(.63f, .17f, .47f);
        [SerializeField] Color mistColor = new Color(.58f, .24f, .32f, .86f);
        [SerializeField] Color dustColor = new Color(.78f, .67f, .56f, .76f);
        [SerializeField, Range(0f, 1f)] float groundContactPhase = 36f / 51f;

        Animator animator;
        Transform chest;
        bool landed;
        float startedAt, removeAt;

        public void Begin(Animator deathAnimator, Vector3 torso, Transform contactAnchor)
        {
            animator = deathAnimator;
            chest = contactAnchor;
            startedAt = Time.unscaledTime;
            Emit(crimson, crimsonCount, torso + Vector3.up * .18f, .23f, .34f, spread * 1.45f, .87f, crimsonColor);
            Emit(infected, infectedCount, torso + Vector3.up * .13f, .15f, .47f, spread * 1.55f, 1.02f, infectedColor);
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
            removeAt = Time.unscaledTime + .88f;
            Vector3 ground = transform.position;
            if (chest) ground = new Vector3(chest.position.x, ground.y, chest.position.z);
            Emit(mist, mistCount, ground + Vector3.up * .67f, .89f, .76f, 1.46f, .27f, mistColor);
            Emit(dust, dustCount, ground + Vector3.up * .19f, .56f, .46f, 1.46f, .21f, dustColor);
        }

        void Emit(ParticleSystem system, int count, Vector3 origin, float size, float life,
            float speed, float lift, Color color)
        {
            system.Play(false);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value) * 2f * Mathf.PI / count;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                system.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + outward * Random.Range(.07f, .19f),
                    velocity = outward * (speed * Random.Range(.55f, 1f)) + Vector3.up * lift,
                    startSize = size * sizeMultiplier * Random.Range(.72f, 1.15f),
                    startLifetime = life * Random.Range(.74f, 1f),
                    startColor = color,
                    rotation3D = Random.insideUnitSphere * 180f
                }, 1);
            }
        }
    }
}
