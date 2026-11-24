using CargoSquad.Combat;
using CargoSquad.UI;
using UnityEngine;

namespace CargoSquad.Player
{
    [DisallowMultipleComponent]
    public sealed class HealthHologramSpawner : MonoBehaviour
    {
        [SerializeField] private BoxCollider ground;
        [SerializeField] private Health survivor;
        [SerializeField] private CharacterSelection selection;
        [SerializeField] private HealthHologram pickupPrefab;
        [SerializeField, Min(1f)] private float minimumRespawnSeconds = 8f;
        [SerializeField, Min(1f)] private float maximumRespawnSeconds = 16f;
        private readonly HealthHologram[] pickups = new HealthHologram[2];
        private readonly float[] nextSpawn = new float[2];
        public int ActiveCount { get { int count=0; foreach(var p in pickups) if(p && p.Available) count++; return count; } }
        public HealthHologram[] Pickups => (HealthHologram[])pickups.Clone();
        private void Update()
        {
            if (!ground || !survivor || survivor.IsDead || (selection && !selection.HasStarted) || Time.timeScale <= 0f) return;
            var body = survivor.GetComponent<Rigidbody>();
            Vector3 survivorPosition = body ? body.position : survivor.transform.position;
            for (int i=0; i<2; i++)
            {
                if (pickups[i] && pickups[i].Available)
                {
                    Vector3 delta = survivorPosition - pickups[i].transform.position;
                    if (Mathf.Abs(delta.y) < 1.6f && new Vector2(delta.x,delta.z).sqrMagnitude < .9f * .9f)
                        pickups[i].TryCollect(survivor);
                    continue;
                }
                if (Time.time < nextSpawn[i]) continue;
                if (!TryFindPosition(out var position)) { nextSpawn[i] = Time.time + 1f; continue; }
                if (!pickups[i])
                {
                    pickups[i] = pickupPrefab ? Instantiate(pickupPrefab, transform) : new GameObject("Health Hologram").AddComponent<HealthHologram>();
                    pickups[i].transform.SetParent(transform, true);
                    int slot = i;
                    pickups[i].Collected += () => nextSpawn[slot] = Time.time + Random.Range(minimumRespawnSeconds, Mathf.Max(minimumRespawnSeconds, maximumRespawnSeconds));
                }
                pickups[i].Spawn(position);
            }
        }
        private bool TryFindPosition(out Vector3 position)
        {
            Bounds area = ground.bounds; position = default;
            for (int attempt=0; attempt<60; attempt++)
            {
                var candidate = new Vector3(Random.Range(area.min.x+1.2f,area.max.x-1.2f),area.max.y,Random.Range(area.min.z+1.2f,area.max.z-1.2f));
                if (Vector3.Distance(candidate, survivor.transform.position) < 3f) continue;
                bool blocked = false;
                foreach(var p in pickups) if(p && p.Available && Vector3.Distance(candidate,p.transform.position)<3f) { blocked=true; break; }
                if (blocked) continue;
                foreach(var hit in Physics.OverlapCapsule(candidate+Vector3.up*.55f,candidate+Vector3.up*1.3f,.5f,~0,QueryTriggerInteraction.Ignore))
                    if(hit != ground) { blocked=true; break; }
                if (blocked) continue;
                // Reject upper floors/props and gaps: this map's ground must be the surface under the hologram.
                if (!Physics.Raycast(candidate+Vector3.up*.15f,Vector3.down,out var floor,.4f,~0,QueryTriggerInteraction.Ignore) || floor.collider != ground) continue;
                position = candidate; return true;
            }
            return false;
        }
    }
}
