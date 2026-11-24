using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class AcidGlob : MonoBehaviour
    {
        [SerializeField, Min(.01f)] float radius = .10f;
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] AcidImpactSplash impactSplash;
        // Shared scratch space for synchronous physics queries on Unity's main thread.
        static readonly RaycastHit[] Contacts = new RaycastHit[16];
        Vector3 velocity;
        Transform source;
        float damage, remaining;

        public void Launch(Transform owner, Vector3 target, float speed, float amount, float range)
        {
            source = owner;
            velocity = (target - transform.position).normalized * speed;
            damage = amount;
            remaining = range;
            if (velocity.sqrMagnitude < .001f) { Destroy(gameObject); return; }
            transform.rotation = Quaternion.LookRotation(velocity);
        }

        void FixedUpdate()
        {
            float step = Mathf.Min(remaining, velocity.magnitude * Time.fixedDeltaTime);
            Vector3 from = transform.position;
            Vector3 direction = transform.forward;
            // Sweep the full step so thin cover and moving survivors register even at high speed.
            var hits = Contacts;
            int count = Physics.SphereCastNonAlloc(from, radius, direction, hits, step, collisionMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(from, radius, direction, step, collisionMask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            RaycastHit first = default;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(source) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                first = hit;
            }
            if (first.collider)
            {
                var player = first.collider.GetComponentInParent<PlayerController>();
                if (impactSplash)
                {
                    Vector3 normal = first.normal.sqrMagnitude > .001f ? first.normal : -direction;
                    Vector3 point = nearest > 0 ? first.point : from - normal * radius;
                    if (player && nearest <= 0f)
                    {
                        Vector3 surface = first.collider.ClosestPoint(from);
                        if ((from - surface).sqrMagnitude > .000001f)
                        {
                            point = surface;
                            normal = (from - surface).normalized;
                        }
                        else
                        {
                            float reach = first.collider.bounds.size.magnitude + radius;
                            if (first.collider.Raycast(new Ray(from + normal * reach, -normal), out var contact, reach))
                            {
                                point = contact.point;
                                normal = contact.normal;
                            }
                        }
                    }
                    Instantiate(impactSplash, point, Quaternion.FromToRotation(Vector3.up, normal))
                        .Burst(player ? first.collider.transform : null);
                }
                if (player && player.isActiveAndEnabled)
                    player.GetComponent<IDamageable>()?.ApplyDamage(damage);
                Destroy(gameObject);
                return;
            }
            transform.position += direction * step;
            remaining -= step;
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
