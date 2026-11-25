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

        [SerializeField, Tooltip("Keep the survivor clear of the HUD when the map edge stops normal camera follow.")]
        private Rect safeViewport = new Rect(.08f, .24f, .84f, .68f);
        private Collider targetCollider;
        private Camera view;
        private Vector2 followVelocity;
        private Vector3 damageOffset;
        public Vector3 FollowPosition => transform.position - damageOffset;

        public void SetDamageOffset(Vector2 screenOffset)
        {
            Vector3 next = transform.right * screenOffset.x + transform.up * screenOffset.y;
            transform.position += next - damageOffset;
            damageOffset = next;
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (target) targetCollider = target.GetComponent<Collider>();
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

            Vector3 follow = FollowPosition;
            Vector2 current = new Vector2(follow.x - offset.x, follow.z - offset.z);
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
            KeepSurvivorVisible();
            transform.position += damageOffset;
        }

        private void KeepSurvivorVisible()
        {
            if (!view || !target || !view.orthographic) return;
            // Protect the feet, rather than just the Rigidbody origin halfway up the body.
            Vector3 feet = target.position;
            if (targetCollider) feet.y = targetCollider.bounds.min.y;
            Vector3 point = view.WorldToViewportPoint(feet);
            float dx = point.x - Mathf.Clamp(point.x, safeViewport.xMin, safeViewport.xMax);
            float dy = point.y - Mathf.Clamp(point.y, safeViewport.yMin, safeViewport.yMax);
            Vector3 right = transform.right, up = transform.up;
            float determinant = right.x * up.z - right.z * up.x;
            if (Mathf.Abs(determinant) < .001f) return;
            // Solve the horizontal camera translation in screen space, preserving height and pitch.
            float horizontal = dx * 2f * view.orthographicSize * view.aspect;
            float vertical = dy * 2f * view.orthographicSize;
            transform.position += new Vector3((horizontal * up.z - right.z * vertical) / determinant, 0f,
                (right.x * vertical - horizontal * up.x) / determinant);
        }

        private static float ClampToMap(float focus, float center, float travel)
        {
            travel = Mathf.Max(0f, travel);
            return Mathf.Clamp(focus, center - travel, center + travel);
        }
    }
}
