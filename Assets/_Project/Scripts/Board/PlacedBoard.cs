using UnityEngine;

namespace TinySiege.Board
{
    /// <summary>
    /// Root of the placed battlefield. Exposes the board's integration points so future gameplay
    /// (catapult, targets, shadow receiver) can attach to them without knowing about AR Foundation.
    /// Local space: origin on the detected surface, +Y up, +Z pointing away from the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacedBoard : MonoBehaviour
    {
        [SerializeField] Transform boardRoot;
        [SerializeField] BoxCollider physicsSurface;
        [SerializeField] Transform fortressRoot;
        [SerializeField] GameObject fortressFinalDebris;
        [SerializeField] Transform catapultSocket;
        [SerializeField] Transform targetsRoot;
        [SerializeField] Transform shadowReceiverSocket;

        /// <summary>Visual base of the board (no colliders).</summary>
        public Transform BoardRoot => boardRoot;

        /// <summary>Gameplay collision surface, independent from the AR plane and from the visuals.</summary>
        public BoxCollider PhysicsSurface => physicsSurface;

        /// <summary>Parent of the global fortress visuals (back row and gatehouse); hidden when the match is won.</summary>
        public Transform FortressRoot => fortressRoot;

        /// <summary>Ruins of the whole fortress, inactive until the match is won (visual only, no colliders).</summary>
        public GameObject FortressFinalDebris => fortressFinalDebris;

        /// <summary>Where the catapult will be spawned; its forward axis faces the fortress.</summary>
        public Transform CatapultSocket => catapultSocket;

        /// <summary>Parent for destructible targets.</summary>
        public Transform TargetsRoot => targetsRoot;

        /// <summary>Where the shadow-catching receiver will be attached.</summary>
        public Transform ShadowReceiverSocket => shadowReceiverSocket;
    }
}
