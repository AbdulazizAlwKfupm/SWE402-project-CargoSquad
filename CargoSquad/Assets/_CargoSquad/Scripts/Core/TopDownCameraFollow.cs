using UnityEngine;

namespace CargoSquad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private BoxCollider playArea;
        [SerializeField] private Vector3 offset = new Vector3(0f, 22f, -22f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.18f;

        private Camera view;
        private Vector2 followVelocity;

        private void Awake()
        {
            view = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            followVelocity = Vector2.zero;
            if (target)
                SetPosition(GetFocus());
        }

        private void LateUpdate()
        {
            if (!target)
                return;

            Vector2 current = new Vector2(transform.position.x - offset.x, transform.position.z - offset.z);
            SetPosition(Vector2.SmoothDamp(current, GetFocus(), ref followVelocity, smoothTime));
        }

        private Vector2 GetFocus()
        {
            Vector2 focus = new Vector2(target.position.x, target.position.z);
            if (!playArea)
                return focus;

            Bounds bounds = playArea.bounds;
            // At the camera's pitch, the vertical viewport spans a longer distance on the floor.
            float pitch = Mathf.Max(0.01f, Mathf.Abs(transform.forward.y));
            float halfWidth = view.orthographicSize * view.aspect;
            float halfDepth = view.orthographicSize / pitch;
            return new Vector2(
                ClampToMap(focus.x, bounds.center.x, bounds.extents.x - halfWidth),
                ClampToMap(focus.y, bounds.center.z, bounds.extents.z - halfDepth));
        }

        private void SetPosition(Vector2 focus)
        {
            float floor = playArea ? playArea.bounds.max.y : 0f;
            transform.position = new Vector3(focus.x, floor, focus.y) + offset;
        }

        private static float ClampToMap(float focus, float center, float travel)
        {
            travel = Mathf.Max(0f, travel);
            return Mathf.Clamp(focus, center - travel, center + travel);
        }
    }
}
