using UnityEngine;

namespace CargoSquad.AI
{
    /// <summary>The licensed prefab is installed locally; the public scene contains no restricted asset GUIDs.</summary>
    public sealed class LocalWolfSpawn : MonoBehaviour
    {
        [SerializeField] private Transform[] patrolPoints;

        private void Start()
        {
            var prefab = Resources.Load<GameObject>("MutatedWolfV2");
            if (!prefab)
            {
                Debug.LogWarning("Approved Mutated Wolf V2 is missing locally. Run Cargo Squad/Enemies/Install Local Wolf V2.", this);
                return;
            }
            var instance = Instantiate(prefab, transform.position, transform.rotation);
            instance.GetComponent<MutatedWolfAI>().ConfigurePatrol(patrolPoints);
        }
    }
}
