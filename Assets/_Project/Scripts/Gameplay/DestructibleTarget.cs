using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// A fortress weak point. Static collider (no Rigidbody): a valid hit is a launched, still-active
    /// <see cref="Projectile"/> hitting it fast enough, and each projectile counts at most once.
    /// Every valid hit raises <see cref="Damaged"/>; the last one (<see cref="HitsToDestroy"/>) also raises
    /// <see cref="Destroyed"/> and deactivates the target. It knows nothing about the match or AR.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class DestructibleTarget : MonoBehaviour
    {
        [Tooltip("Minimum relative impact speed (m/s) for a valid hit. Rolling stones below this do nothing.")]
        [SerializeField, Min(0f)] float minImpactSpeed = 0.3f;
        [Tooltip("Valid hits needed to destroy the target. Each projectile counts at most once.")]
        [SerializeField, Min(1)] int hitsToDestroy = 1;

        // The projectile that scored the last hit, so its further contacts (bounces, sliding) don't count again.
        Projectile lastHitBy;

        public bool IsDestroyed { get; private set; }
        public int Hits { get; private set; }
        public int HitsToDestroy => hitsToDestroy;

        /// <summary>Raised on every valid hit, after <see cref="Hits"/> is updated (also on the destroying hit, before <see cref="Destroyed"/>).</summary>
        public event Action<DestructibleTarget> Damaged;

        /// <summary>Raised once, just before the target deactivates.</summary>
        public event Action<DestructibleTarget> Destroyed;

        void OnCollisionEnter(Collision collision)
        {
            if (IsDestroyed)
                return;

            // Only projectiles count; anything else touching the target is ignored.
            Projectile projectile = collision.rigidbody != null ? collision.rigidbody.GetComponent<Projectile>() : null;
            if (projectile == null || !projectile.IsLaunched || projectile.IsResolved)
                return;

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < minImpactSpeed)
            {
                Log($"{name}: impact too weak ({impactSpeed:0.00} m/s < {minImpactSpeed:0.00})");
                return;
            }

            if (projectile == lastHitBy)
                return;

            lastHitBy = projectile;
            Hits++;
            if (Hits < hitsToDestroy)
            {
                Log($"{name}: hit {Hits}/{hitsToDestroy} by projectile ({impactSpeed:0.00} m/s)");
                Damaged?.Invoke(this);
                return;
            }

            Log($"{name}: destroyed by projectile, hit {Hits}/{hitsToDestroy} ({impactSpeed:0.00} m/s)");
            IsDestroyed = true;
            Damaged?.Invoke(this);
            Destroyed?.Invoke(this);
            gameObject.SetActive(false);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void Log(string message) => Debug.Log($"{nameof(DestructibleTarget)}: {message}.", this);
    }
}
