using CargoSquad.Cargo;
using CargoSquad.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoSquad.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float runSpeed = 7f;

        private Rigidbody body;
        private InputAction move;
        private InputAction sprint;
        private PlayerStamina stamina;
        private CargoInteractor carrier;
        private CharacterAbility ability;
        private Health health;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            stamina = GetComponent<PlayerStamina>();
            carrier = GetComponent<CargoInteractor>();
            ability = GetComponent<CharacterAbility>();
            health = GetComponent<Health>();
            if (!inputActions ||
                inputActions.FindAction("Player/Move") is not InputAction moveAction ||
                inputActions.FindAction("Player/Sprint") is not InputAction sprintAction)
            {
                Debug.LogError("Assign an input asset with Player/Move and Player/Sprint.", this);
                enabled = false;
                return;
            }

            move = moveAction.Clone();
            sprint = sprintAction.Clone();
        }

        private void OnEnable()
        {
            move?.Enable();
            sprint?.Enable();
        }

        private void OnDisable()
        {
            move?.Disable();
            sprint?.Disable();
        }

        private void OnDestroy()
        {
            move?.Dispose();
            sprint?.Dispose();
        }

        private void FixedUpdate()
        {
            if (health && health.IsDead)
            {
                body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
                return;
            }
            if (ability && ability.TryGetChargeVelocity(Time.fixedDeltaTime, out Vector3 chargeVelocity))
            {
                body.linearVelocity = new Vector3(chargeVelocity.x, body.linearVelocity.y, chargeVelocity.z);
                stamina?.Tick(false, 1f, Time.fixedDeltaTime);
                return;
            }
            Vector2 direction = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            bool moving = direction.sqrMagnitude > 0.0001f;
            var character = stamina ? stamina.Definition : null;
            var load = carrier && carrier.CarriedCargo ? carrier.CarriedCargo.Definition : null;
            float strength = character ? character.CarryStrength : 1f;
            bool sprinting = moving && sprint.IsPressed();
            if (stamina && character)
                sprinting = stamina.Tick(sprinting, load ? load.StaminaMultiplier(strength) : 1f, Time.fixedDeltaTime);
            float speed = character
                ? (sprinting ? character.RunSpeed : character.WalkSpeed)
                : (sprinting ? runSpeed : walkSpeed);
            Vector2 horizontalVelocity = direction * speed * (load ? load.SpeedMultiplier(strength) : 1f);
            body.linearVelocity = new Vector3(horizontalVelocity.x, body.linearVelocity.y, horizontalVelocity.y);
            if (carrier && carrier.isActiveAndEnabled)
                carrier.TickGrip(sprinting, moving, character, Time.fixedDeltaTime);
        }
    }
}
