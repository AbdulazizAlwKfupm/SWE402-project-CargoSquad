using UnityEngine;
using UnityEngine.Rendering;

namespace CargoSquad.Combat
{
    [DefaultExecutionOrder(100)]
    public sealed class MedicElectricFeedback : MonoBehaviour
    {
        [SerializeField] Material arcMaterial;
        [SerializeField] MeshRenderer pulseCore;
        [SerializeField] Vector3 muzzleOffset = new Vector3(0f, -.00325f, .00058f);
        [SerializeField] Color electricColor = new Color(.22f, .86f, 1f);
        [SerializeField, Range(.1f, 2f)] float intensity = 1f;
        [SerializeField, Min(.02f)] float ringRadius = .15f;
        [SerializeField, Range(2, 8)] int sparkCount = 5;
        [SerializeField, Min(.02f)] float pulseSize = .15f;
        [SerializeField, Min(12f)] float pulseSpeed = 28f;
        [SerializeField, Min(.05f)] float trailLength = .7f;
        [SerializeField, Min(.05f)] float impactSize = .33f;
        [SerializeField, Range(.08f, .4f)] float impactDuration = .22f;

        PlayerWeapon weapon;
        LineRenderer ring, trail;
        LineRenderer[] arcs;
        bool charging, flying;
        float chargeStarted, flightStarted, flightDuration, impactStarted;
        Vector3 start, end, impactLocal;
        Transform target;
        const int RingSegments = 36;

        void Awake()
        {
            weapon = GetComponentInParent<PlayerWeapon>();
            ring = MakeLine("Charge Halo", RingSegments, true);
            trail = MakeLine("Pulse Trail", 5, false);
            arcs = new LineRenderer[sparkCount];
            for (int i = 0; i < arcs.Length; i++) arcs[i] = MakeLine("Electric Arc", 3, false);
            pulseCore.enabled = false;
        }

        void OnEnable() => weapon.StunShotResolved += Fire;

        void OnDisable()
        {
            weapon.StunShotResolved -= Fire;
            charging = flying = false;
            target = null;
            ring.enabled = trail.enabled = pulseCore.enabled = false;
            HideArcs();
        }

        LineRenderer MakeLine(string label, int points, bool loop)
        {
            var line = new GameObject(label).AddComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = arcMaterial;
            line.positionCount = points;
            line.loop = loop;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        void Style(LineRenderer line, float width, float alpha)
        {
            var tint = Color.Lerp(electricColor, Color.white, .23f);
            tint.a = electricColor.a * alpha * intensity;
            line.startColor = line.endColor = tint;
            line.startWidth = line.endWidth = width;
            line.enabled = true;
        }

        void Fire(Vector3 endpoint, Transform hit)
        {
            var muzzle = weapon.ActiveWeaponVisual;
            if (!muzzle) return;
            charging = false;
            ring.enabled = false;
            HideArcs();
            start = muzzle.TransformPoint(muzzleOffset);
            end = endpoint;
            target = hit;
            if (target) impactLocal = target.InverseTransformPoint(endpoint);
            flightStarted = Time.unscaledTime;
            flightDuration = Mathf.Max(.045f, Vector3.Distance(start, end) / pulseSpeed);
            flying = true;
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            var muzzle = weapon.ActiveWeaponVisual;
            bool wantsCharge = muzzle && weapon.CanUse && weapon.IsAiming && !weapon.Reloading &&
                weapon.Magazine > 0 && weapon.Definition.type == WeaponType.Stun;
            if (wantsCharge && !charging) chargeStarted = now;
            charging = wantsCharge;
            ring.enabled = false;
            HideArcs();

            if (flying)
            {
                float progress = Mathf.Clamp01((now - flightStarted) / flightDuration);
                if (progress < 1f) DrawPulse(progress, now);
                else
                {
                    flying = false;
                    impactStarted = now;
                    pulseCore.enabled = trail.enabled = false;
                }
            }

            if (charging) DrawCharge(muzzle, now);
            else if (!flying && target && now - impactStarted < impactDuration)
                DrawImpact(target.TransformPoint(impactLocal), now);
        }

        void DrawCharge(Transform muzzle, float now)
        {
            float buildup = Mathf.Clamp01((now - chargeStarted) / .24f);
            float radius = ringRadius * Mathf.Lerp(.72f, 1f, buildup) * (1f + .05f * Mathf.Sin(now * 39f));
            Vector3 position = muzzle.TransformPoint(muzzleOffset);
            Vector3 right = muzzle.right;
            // Tilt the ring slightly toward the camera so it reads even when the barrel is edge-on.
            Vector3 up = (muzzle.forward - muzzle.up * .42f).normalized;
            Style(ring, .018f, Mathf.Lerp(.64f, 1f, buildup));
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                ring.SetPosition(i, position + radius * (Mathf.Cos(angle) * right + Mathf.Sin(angle) * up));
            }
            for (int i = 0; i < arcs.Length; i++)
            {
                float angle = now * 8f + i * Mathf.PI * 2f / arcs.Length;
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                SetArc(arcs[i], position + radial * radius * .7f, position + radial * radius * 1.36f,
                    -muzzle.up * (.06f + .025f * Mathf.Sin(now * 37f + i)), .42f + buildup * .45f);
            }
        }

        void DrawPulse(float progress, float now)
        {
            Vector3 head = Vector3.Lerp(start, end, progress);
            Vector3 travel = (end - start).normalized;
            Vector3 tail = head - travel * Mathf.Min(trailLength, Vector3.Distance(start, head));
            Vector3 side = Vector3.Cross(travel, Vector3.up);
            Style(trail, pulseSize * .28f, .76f);
            for (int i = 0; i < trail.positionCount; i++)
            {
                float t = i / (trail.positionCount - 1f);
                trail.SetPosition(i, Vector3.Lerp(tail, head, t) + side *
                    (Mathf.Sin(i * 4f + now * 95f) * Mathf.Sin(t * Mathf.PI) * pulseSize * .24f));
            }
            pulseCore.transform.position = head;
            pulseCore.transform.localScale = Vector3.one * pulseSize;
            pulseCore.enabled = true;
        }

        void DrawImpact(Vector3 point, float now)
        {
            float alpha = 1f - (now - impactStarted) / impactDuration;
            for (int i = 0; i < arcs.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / arcs.Length + now * 14f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Cos(angle * 1.7f)).normalized;
                SetArc(arcs[i], point + radial * impactSize * .2f, point + radial * impactSize,
                    Vector3.up * Mathf.Sin(now * 67f + i) * .10f, alpha);
            }
        }

        void SetArc(LineRenderer line, Vector3 a, Vector3 b, Vector3 kink, float alpha)
        {
            Style(line, .014f, alpha);
            line.SetPosition(0, a);
            line.SetPosition(1, (a + b) * .5f + kink);
            line.SetPosition(2, b);
        }

        void HideArcs()
        {
            foreach (var arc in arcs) arc.enabled = false;
        }
    }
}
