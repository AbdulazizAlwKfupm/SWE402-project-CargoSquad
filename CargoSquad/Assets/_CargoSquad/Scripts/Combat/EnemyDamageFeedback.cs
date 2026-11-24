using UnityEngine;

namespace CargoSquad.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class EnemyDamageFeedback : MonoBehaviour
    {
        [SerializeField] EnemyDamageBurst effectPrefab;
        [SerializeField, Min(0f)] float fallbackHeight = 1f;
        [SerializeField, Min(0f)] float minimumBurstInterval = .08f;
        Health health;
        EnemyDamageBurst effect;
        EnemyHitFlash hitFlash;
        float nextBurst;

        public void SetEffect(EnemyDamageBurst prefab) => effectPrefab = prefab;

        void Awake() => health = GetComponent<Health>();
        void OnEnable() => health.Damaged += OnDamaged;
        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            if (hitFlash) hitFlash.Clear();
        }

        void OnDamaged(Vector3? point, Vector3 normal)
        {
            if (!effectPrefab || Time.unscaledTime < nextBurst) return;
            nextBurst = Time.unscaledTime + minimumBurstInterval;
            if (!health.IsDead)
            {
                if (!hitFlash) hitFlash = gameObject.AddComponent<EnemyHitFlash>();
                hitFlash.Play(effectPrefab.HitFlash);
            }
            if (!effect) effect = Instantiate(effectPrefab, transform);
            normal = normal.sqrMagnitude > .001f ? normal.normalized : Vector3.up;
            Vector3 origin = point ?? (transform.position + Vector3.up * fallbackHeight);
            effect.Burst(origin + normal * .13f, normal);
        }
    }
}
