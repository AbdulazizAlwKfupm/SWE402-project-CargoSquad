using System;
using UnityEngine;

namespace CargoSquad.Cargo
{
    [DisallowMultipleComponent]
    public sealed class CargoGrip : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("Percentage points lost per second at unit load and stability.")]
        private float sprintDrain = 30f;
        [SerializeField, Min(0)] private float walkingRecovery = 8f;
        [SerializeField, Min(0)] private float restingRecovery = 20f;

        public float Percent { get; private set; } = 100f;
        public bool SlipPending { get; private set; }
        public event Action<float> Changed;

        public void BeginCarry()
        {
            SlipPending = false;
            SetPercent(100f);
        }

        public void Tick(bool sprinting, bool moving, float drainMultiplier, float seconds)
        {
            if (SlipPending || seconds <= 0f)
                return;

            float rate = sprinting ? -sprintDrain * Mathf.Max(0f, drainMultiplier) :
                moving ? walkingRecovery : restingRecovery;
            SetPercent(Mathf.Clamp(Percent + rate * seconds, 0f, 100f));
        }

        private void SetPercent(float percent)
        {
            SlipPending = percent <= 0f;
            if (Percent == percent)
                return;

            Percent = percent;
            Changed?.Invoke(Percent);
        }
    }
}
