using UnityEngine;

namespace CargoSquad.AI
{
    [RequireComponent(typeof(ArmoredCreatureAI))]
    public sealed class GroundSmashFeedback : MonoBehaviour
    {
        [SerializeField] SmashGroundShape warningPrefab;
        [SerializeField] GroundSmashBurst impactPrefab;
        [SerializeField] Color warningColor = new Color(1f, .48f, .13f);
        [SerializeField, Range(0f, 1f)] float warningOpacity = .34f;
        [SerializeField, Min(0f)] float impactForwardOffset = .95f;
        ArmoredCreatureAI ai;
        SmashGroundShape warning;

        void Awake() => ai = GetComponent<ArmoredCreatureAI>();

        void OnEnable() => ai.GroundSmashContact += OnContact;

        void OnDisable()
        {
            ai.GroundSmashContact -= OnContact;
            if (warning) warning.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!ai.TryGetSmashWindup(out float progress))
            {
                if (warning && warning.gameObject.activeSelf) warning.gameObject.SetActive(false);
                return;
            }

            if (!warning)
            {
                warning = Instantiate(warningPrefab, transform);
                warning.transform.localPosition = Vector3.up * .025f;
                warning.Build(ai.AttackHalfAngle, 0f);
            }

            warning.gameObject.SetActive(true);
            warning.transform.localScale = new Vector3(ai.AttackReach, 1f, ai.AttackReach);
            Color tint = warningColor;
            tint.a *= warningOpacity * Mathf.SmoothStep(.18f, 1f, progress);
            warning.Tint(tint);
        }

        void OnContact()
        {
            if (warning) warning.gameObject.SetActive(false);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Instantiate(impactPrefab, transform.position + forward * impactForwardOffset + Vector3.up * .035f,
                Quaternion.identity).Burst();
        }
    }
}
