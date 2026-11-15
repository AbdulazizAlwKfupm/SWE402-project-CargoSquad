using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using UnityEngine;

namespace CargoSquad.UI
{
    [DisallowMultipleComponent]
    public sealed class CargoMissionOverlay : MonoBehaviour
    {
        [SerializeField] private CargoInteractor player;
        [SerializeField] private CargoExtraction mission;
        [SerializeField] private PlayerStamina stamina;

        private float staminaFraction;

        private void OnEnable()
        {
            if (stamina)
                stamina.Changed += UpdateStamina;
        }

        private void Start()
        {
            if (stamina)
                UpdateStamina(stamina.Current, stamina.Maximum);
        }

        private void OnDisable()
        {
            if (stamina)
                stamina.Changed -= UpdateStamina;
        }

        private void UpdateStamina(float current, float maximum) => staminaFraction = current / maximum;

        private void OnGUI()
        {
            if (!player || !mission)
                return;

            CargoItem carried = player.CarriedCargo;
            string objective = mission.IsComplete
                ? (mission.RecoveryFailed ? "Recovery Failed — Survivor Escaped" : "Mission Complete")
                : mission.CargoDestroyed ? "Escape Alive"
                : mission.IsPrimaryCargo(carried) ? "Deliver Cargo to Extraction" : "Find the Cargo";
            GUI.Box(new Rect(12f, 12f, 260f, 28f), objective);
            if (stamina)
                GUI.Box(new Rect(12f, 44f, 150f, 25f), $"Stamina {staminaFraction:P0}");

            CargoCondition condition = carried ? carried.Condition : mission.PrimaryCondition;
            if (!mission.IsComplete && condition)
            {
                float fraction = condition.Current / condition.Maximum;
                GUI.Box(new Rect(12f, 73f, 150f, 25f), $"Condition {fraction:P0}");
                if (fraction <= 0.25f && fraction > 0f)
                    GUI.Box(new Rect(166f, 73f, 130f, 25f), "Critical condition");
            }
            if (carried && player.Grip)
            {
                float grip = player.Grip.Percent;
                GUI.Box(new Rect(12f, 102f, 150f, 25f), $"Grip {grip:F0}%");
                if (grip <= 25f)
                    GUI.Box(new Rect(166f, 102f, 130f, 25f), "Critical grip");
            }
            if (mission.IsComplete && !mission.RecoveryFailed)
                GUI.Box(new Rect(12f, 73f, 220f, 28f), "Mission Success");
            else if (!mission.IsComplete && !string.IsNullOrEmpty(player.Prompt))
                GUI.Box(new Rect((Screen.width - 300f) * 0.5f, Screen.height - 50f, 300f, 28f), player.Prompt);
        }
    }
}
