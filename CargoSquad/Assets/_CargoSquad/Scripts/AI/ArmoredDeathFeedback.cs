using System;
using CargoSquad.Combat;
using UnityEngine;

namespace CargoSquad.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(ArmoredCreatureAI), typeof(Health))]
    public sealed class ArmoredDeathFeedback : MonoBehaviour
    {
        [SerializeField] ArmoredDeathBurst effectPrefab;
        Health health;
        Animator animator;
        Transform torso;
        bool played;

        void Awake()
        {
            health = GetComponent<Health>();
            animator = GetComponentInChildren<Animator>();
            var skin = GetComponentInChildren<SkinnedMeshRenderer>();
            if (skin) torso = Array.Find(skin.bones, bone => bone && bone.name == "Torso");
        }

        void OnEnable() => health.Died += OnDeath;
        void OnDisable() => health.Died -= OnDeath;

        void OnDeath()
        {
            if (played || !effectPrefab || !animator) return;
            played = true;
            Vector3 center = torso ? torso.position : transform.position + Vector3.up * 1.2f;
            Instantiate(effectPrefab, transform.position, Quaternion.identity).Begin(animator, torso, center);
        }
    }
}
