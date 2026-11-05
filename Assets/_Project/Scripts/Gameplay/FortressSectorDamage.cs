using System;
using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Visual-only damage stages of one fortress sector. Listens to <see cref="DestructibleTarget.Damaged"/>
    /// and hides the pieces of each reached stage; it never detects impacts itself. The destroying hit needs
    /// no stage: the target deactivates (hiding the whole sector) and <see cref="TargetDebris"/> shows the rubble.
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

        DestructibleTarget target;
        int appliedStages;

        /// <summary>Current damage stage: 0 intact, HitsToDestroy destroyed.</summary>
        public int Stage => target != null ? target.Hits : 0;

        void Awake()
        {
            target = GetComponent<DestructibleTarget>();
            if (stages.Length != target.HitsToDestroy - 1)
                Debug.LogWarning($"{nameof(FortressSectorDamage)}: {name} has {stages.Length} stages for " +
                                 $"{target.HitsToDestroy} hits (expected {target.HitsToDestroy - 1}).", this);
        }

        void OnEnable()
        {
            target.Damaged += OnDamaged;
            ApplyStages(target.Hits);
        }

        void OnDisable() => target.Damaged -= OnDamaged;

        void OnDamaged(DestructibleTarget damaged) => ApplyStages(damaged.Hits);

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
