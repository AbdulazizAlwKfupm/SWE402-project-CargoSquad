using UnityEngine;

namespace CargoSquad.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerWeapon))]
    public sealed class ShotgunMuzzleFeedback : MonoBehaviour
    {
        [SerializeField] TacticalShotgunFlash effectPrefab;
        [SerializeField] Vector3 muzzleLocalPosition;
        PlayerWeapon weapon;
        TacticalShotgunFlash effect;

        void Awake() => weapon = GetComponent<PlayerWeapon>();

        void OnEnable()
        {
            if (effectPrefab) weapon.ShotgunFired += OnShot;
        }

        void OnDisable()
        {
            weapon.ShotgunFired -= OnShot;
        }

        void OnShot(Transform barrel)
        {
            if (!barrel) return;
            if (!effect) effect = Instantiate(effectPrefab, barrel, false);
            if (effect.transform.parent != barrel) effect.transform.SetParent(barrel, false);
            effect.transform.localPosition = muzzleLocalPosition;
            // Heavy V4 was authored with the barrel pointing along the gun joint's local -Y.
            effect.Burst(-barrel.up);
        }
    }
}
