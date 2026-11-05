using System;
using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Physical projectile (Rigidbody + SphereCollider). Reports impacts and when its shot is resolved:
    /// it has hit something and come to rest, fallen off the board, or exceeded its lifetime.
    /// It does not know about the catapult, the match or AR.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] float maxLifetime = 6f;
        [Tooltip("Below this speed (m/s) after an impact the projectile counts as settled.")]
        [SerializeField, Min(0f)] float settleSpeed = 0.05f;
        [SerializeField, Min(0f)] float settleTime = 0.4f;
        [Tooltip("Resolve when the projectile drops this far (m) below its launch height, e.g. off the table.")]
        [SerializeField, Min(0.05f)] float fallDistance = 0.5f;
        [SerializeField, Min(0f)] float destroyDelayAfterResolve = 2f;

        Rigidbody body;
        float launchTime;
        float launchHeight;
        float slowTime;

        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();
        public bool IsLaunched { get; private set; }
        public bool HasImpacted { get; private set; }
        public bool IsResolved { get; private set; }

        /// <summary>Raised on every collision after launch (targets react to it in a later phase).</summary>
        public event Action<Projectile, Collision> Impacted;

        /// <summary>Raised once when the shot is over.</summary>
        public event Action<Projectile> Resolved;

        /// <summary>Called by the launcher right after the body becomes dynamic.</summary>
        public void MarkLaunched()
        {
            IsLaunched = true;
            launchTime = Time.time;
            launchHeight = transform.position.y;
            slowTime = 0f;
        }

        void FixedUpdate()
        {
            if (!IsLaunched || IsResolved)
                return;

            if (Time.time - launchTime > maxLifetime || transform.position.y < launchHeight - fallDistance)
            {
                Resolve();
                return;
            }

            if (HasImpacted && !Body.isKinematic && Body.linearVelocity.sqrMagnitude < settleSpeed * settleSpeed)
            {
                slowTime += Time.fixedDeltaTime;
                if (slowTime >= settleTime)
                    Resolve();
            }
            else
            {
                slowTime = 0f;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!IsLaunched || IsResolved)
                return;

            HasImpacted = true;
            Impacted?.Invoke(this, collision);
        }

        void Resolve()
        {
            IsResolved = true;
            Resolved?.Invoke(this);
            Destroy(gameObject, destroyDelayAfterResolve);
        }
    }
}
