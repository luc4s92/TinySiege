using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Visual-only rubble for one <see cref="DestructibleTarget"/>: keeps the debris hidden and shows it
    /// when the target raises <see cref="DestructibleTarget.Destroyed"/>. Lives on a sibling of the target
    /// (the target deactivates itself on destruction). No colliders, no physics, no match logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetDebris : MonoBehaviour
    {
        [SerializeField] DestructibleTarget target;
        [Tooltip("Child object with the rubble visuals. Must not be this GameObject.")]
        [SerializeField] GameObject debris;

        void Awake()
        {
            if (debris != null && debris != gameObject)
                debris.SetActive(target != null && target.IsDestroyed);
        }

        void OnEnable()
        {
            if (target == null || debris == null || debris == gameObject)
            {
                Debug.LogError($"{nameof(TargetDebris)}: missing target or debris reference; debris disabled.", this);
                enabled = false;
                return;
            }

            if (target.IsDestroyed)
            {
                debris.SetActive(true);
                return;
            }

            target.Destroyed += OnTargetDestroyed;
        }

        void OnDisable()
        {
            if (target != null)
                target.Destroyed -= OnTargetDestroyed;
        }

        void OnTargetDestroyed(DestructibleTarget destroyed)
        {
            destroyed.Destroyed -= OnTargetDestroyed;
            debris.SetActive(true);
        }
    }
}
