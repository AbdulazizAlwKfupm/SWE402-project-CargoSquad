using System;
using CargoSquad.Combat;
using UnityEngine;

namespace CargoSquad.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class WolfDeathFeedback : MonoBehaviour
    {
        Health health;
        Animator animator;
        SkinnedMeshRenderer skin;
        Transform chest;
        WolfDeathBurst prefab;
        bool played;

        public void SetEffect(WolfDeathBurst effect) => prefab = effect;

        void Awake()
        {
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin) chest = Array.Find(skin.bones, bone => bone && bone.name == "Chest");
        }

        void OnEnable() => health.Died += OnDeath;
        void OnDisable() => health.Died -= OnDeath;

        void OnDeath()
        {
            if (played || !prefab || !animator) return;
            played = true;
            Vector3 torso = chest ? chest.position : skin ? skin.bounds.center : transform.position + Vector3.up * .75f;
            Instantiate(prefab, transform.position, Quaternion.identity).Begin(animator, torso, chest);
        }
    }
}
