using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// A fortress weak point. Static collider (no Rigidbody): it is destroyed once when a launched,
    /// still-active <see cref="Projectile"/> hits it fast enough. It raises <see cref="Destroyed"/>
    /// and deactivates itself; it knows nothing about the match or AR.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class DestructibleTarget : MonoBehaviour
    {
        [Tooltip("Minimum relative impact speed (m/s) that destroys the target. Rolling stones below this do nothing.")]
        [SerializeField, Min(0f)] float minImpactSpeed = 0.3f;

        public bool IsDestroyed { get; private set; }

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

            Log($"{name}: destroyed by projectile ({impactSpeed:0.00} m/s)");
            IsDestroyed = true;
            Destroyed?.Invoke(this);
            gameObject.SetActive(false);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void Log(string message) => Debug.Log($"{nameof(DestructibleTarget)}: {message}.", this);
    }
}
