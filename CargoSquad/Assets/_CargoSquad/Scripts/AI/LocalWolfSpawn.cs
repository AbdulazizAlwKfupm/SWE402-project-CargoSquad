using CargoSquad.Combat;
using UnityEngine;

namespace CargoSquad.AI
{
    /// <summary>Spawns the mission's wolf on its assigned patrol route.</summary>
    public sealed class LocalWolfSpawn : MonoBehaviour
    {
        [SerializeField] private GameObject wolfPrefab;
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private EnemyDamageBurst damageEffect;

        private void Start()
        {
            var prefab = wolfPrefab ? wolfPrefab : Resources.Load<GameObject>("MutatedWolfV2");
            if (!prefab)
            {
                Debug.LogWarning("Assign a wolf prefab to this spawner or install the local Wolf V2 fallback.", this);
                return;
            }
            var instance = Instantiate(prefab, transform.position, transform.rotation);
            instance.GetComponent<MutatedWolfAI>().ConfigurePatrol(patrolPoints);
            if (damageEffect) instance.AddComponent<EnemyDamageFeedback>().SetEffect(damageEffect);
        }
    }
}
