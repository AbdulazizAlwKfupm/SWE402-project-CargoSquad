using System.Collections.Generic;
using CargoSquad.Cargo;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.AI
{
    /// <summary>Builds navigation from this scene's static collision geometry.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        [SerializeField] private BoxCollider ground;
        private NavMeshData data;
        private NavMeshDataInstance instance;

        private void Awake()
        {
            if (!ground) { enabled = false; return; }
            var sources = new List<NavMeshBuildSource>();
            foreach (var box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
            {
                if (box.gameObject.scene != gameObject.scene || !box.enabled || box.isTrigger ||
                    box.attachedRigidbody || box.GetComponentInParent<CargoItem>()) continue;
                sources.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = box.transform.localToWorldMatrix * Matrix4x4.Translate(box.center),
                    size = box.size,
                    area = 0
                });
            }
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.4f;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 45f;
            var bounds = ground.bounds;
            bounds.Expand(new Vector3(0, 12, 0));
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data) instance = NavMesh.AddNavMeshData(data);
        }

        private void OnDestroy()
        {
            if (instance.valid) instance.Remove();
            if (data) Destroy(data);
        }
    }
}
