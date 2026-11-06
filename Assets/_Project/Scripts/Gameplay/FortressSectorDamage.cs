using System;
using System.Collections;
using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Visual-only damage stages of one fortress sector. Listens to <see cref="DestructibleTarget.Damaged"/>
    /// and hides the pieces of each reached stage; it never detects impacts itself. The destroying hit needs
    /// no stage: the target deactivates (hiding the whole sector) and <see cref="TargetDebris"/> shows the rubble.
    /// Each non-destroying hit also plays a short scale punch on <c>punchVisual</c>, a visual child (never the
    /// target's own transform, so the collider does not move).
    /// </summary>
    [RequireComponent(typeof(DestructibleTarget))]
    [DisallowMultipleComponent]
    public sealed class FortressSectorDamage : MonoBehaviour
    {
        [Serializable]
        struct DamageStage
        {
            [Tooltip("Pieces hidden when the sector reaches this stage.")]
            public GameObject[] hide;
        }

        [Tooltip("Element 0 is applied after the first hit, element 1 after the second, and so on. " +
                 "Expected length: HitsToDestroy - 1.")]
        [SerializeField] DamageStage[] stages = Array.Empty<DamageStage>();
        [Tooltip("Visual child punched on each hit. Must not be the target itself (its collider must stay still).")]
        [SerializeField] Transform punchVisual;
        [SerializeField, Min(0f)] float punchScale = 0.12f;
        [SerializeField, Min(0.01f)] float punchDuration = 0.18f;

        DestructibleTarget target;
        int appliedStages;
        Vector3 punchRestScale = Vector3.one;
        Coroutine punch;

        /// <summary>Current damage stage: 0 intact, HitsToDestroy destroyed.</summary>
        public int Stage => target != null ? target.Hits : 0;

        void Awake()
        {
            target = GetComponent<DestructibleTarget>();
            if (punchVisual == transform)
                punchVisual = null;
            if (punchVisual != null)
                punchRestScale = punchVisual.localScale;
            if (stages.Length != target.HitsToDestroy - 1)
                Debug.LogWarning($"{nameof(FortressSectorDamage)}: {name} has {stages.Length} stages for " +
                                 $"{target.HitsToDestroy} hits (expected {target.HitsToDestroy - 1}).", this);
        }

        void OnEnable()
        {
            target.Damaged += OnDamaged;
            ApplyStages(target.Hits);
        }

        void OnDisable()
        {
            target.Damaged -= OnDamaged;
            StopPunch();
        }

        void OnDamaged(DestructibleTarget damaged)
        {
            ApplyStages(damaged.Hits);
            if (!damaged.IsDestroyed && punchVisual != null && isActiveAndEnabled)
            {
                StopPunch();
                punch = StartCoroutine(Punch());
            }
        }

        // Scale up and back with a sine bump; runs only for the punch, no permanent Update.
        IEnumerator Punch()
        {
            for (float t = 0f; t < punchDuration; t += Time.deltaTime)
            {
                punchVisual.localScale = punchRestScale * (1f + punchScale * Mathf.Sin(Mathf.PI * t / punchDuration));
                yield return null;
            }
            punchVisual.localScale = punchRestScale;
            punch = null;
        }

        void StopPunch()
        {
            if (punch != null)
                StopCoroutine(punch);
            punch = null;
            if (punchVisual != null)
                punchVisual.localScale = punchRestScale;
        }

        // Stages only move forward; pieces are never shown again.
        void ApplyStages(int reached)
        {
            int count = Mathf.Min(reached, stages.Length);
            for (; appliedStages < count; appliedStages++)
            {
                GameObject[] pieces = stages[appliedStages].hide;
                if (pieces == null)
                    continue;
                foreach (GameObject piece in pieces)
                    if (piece != null)
                        piece.SetActive(false);
            }
        }
    }
}
