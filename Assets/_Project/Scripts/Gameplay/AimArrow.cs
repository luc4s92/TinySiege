using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Visual-only aim feedback: a flat arrow on the board, child of the catapult, that points along
    /// <see cref="CatapultController.AimDirection"/> while the player drags. Its length grows with the
    /// charge. It never affects physics or launch logic.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class AimArrow : MonoBehaviour
    {
        [SerializeField] CatapultController catapult;
        [Tooltip("Distance (m) from the catapult center to the arrow tail, so it starts in front of the base.")]
        [SerializeField, Min(0f)] float startOffset = 0.05f;
        [Tooltip("Arrow length (m) at zero and full charge.")]
        [SerializeField, Min(0.01f)] float minLength = 0.08f;
        [SerializeField, Min(0.01f)] float maxLength = 0.12f;
        [SerializeField, Min(0.001f)] float shaftWidth = 0.008f;
        [SerializeField, Min(0.001f)] float headWidth = 0.024f;
        [SerializeField, Min(0.001f)] float headLength = 0.025f;

        readonly Vector3[] vertices = new Vector3[7];
        static readonly int[] s_Triangles = { 0, 3, 2, 0, 2, 1, 4, 6, 5 };

        MeshRenderer meshRenderer;
        Mesh mesh;
        float builtLength = -1f;

        void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;
            mesh = new Mesh { name = "AimArrow" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            BuildMesh(minLength);
            // Topology and normals never change; only the vertex positions follow the charge.
            mesh.triangles = s_Triangles;
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
                normals[i] = Vector3.up;
            mesh.normals = normals;
        }

        void OnEnable()
        {
            if (catapult == null)
            {
                Debug.LogError($"{nameof(AimArrow)}: missing catapult reference; aim arrow disabled.", this);
                enabled = false;
                return;
            }

            catapult.AimingChanged += OnAimingChanged;
            catapult.AimDirectionChanged += OnAimDirectionChanged;
            catapult.ChargeChanged += OnChargeChanged;
        }

        void OnDisable()
        {
            if (catapult != null)
            {
                catapult.AimingChanged -= OnAimingChanged;
                catapult.AimDirectionChanged -= OnAimDirectionChanged;
                catapult.ChargeChanged -= OnChargeChanged;
            }

            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }

        void OnDestroy()
        {
            if (mesh != null)
                Destroy(mesh);
        }

        void OnAimingChanged(bool aiming)
        {
            if (aiming)
            {
                OnAimDirectionChanged(catapult.AimDirection);
                OnChargeChanged(catapult.Charge01);
            }

            meshRenderer.enabled = aiming;
        }

        void OnAimDirectionChanged(Vector3 worldDirection)
        {
            Transform parent = transform.parent;
            Vector3 local = parent != null ? parent.InverseTransformDirection(worldDirection) : worldDirection;
            local.y = 0f;
            if (local.sqrMagnitude < 1e-8f)
                return;

            transform.localRotation = Quaternion.LookRotation(local.normalized, Vector3.up);
        }

        void OnChargeChanged(float charge01) => BuildMesh(Mathf.Lerp(minLength, maxLength, Mathf.Clamp01(charge01)));

        /// <summary>Flat arrow in the local XZ plane pointing along +Z, facing up.</summary>
        void BuildMesh(float length)
        {
            if (Mathf.Approximately(length, builtLength))
                return;

            builtLength = length;
            float tail = startOffset;
            float tip = startOffset + length;
            float headBase = tip - Mathf.Min(headLength, length);
            float shaft = shaftWidth * 0.5f;
            float head = headWidth * 0.5f;

            vertices[0] = new Vector3(-shaft, 0f, tail);
            vertices[1] = new Vector3(shaft, 0f, tail);
            vertices[2] = new Vector3(shaft, 0f, headBase);
            vertices[3] = new Vector3(-shaft, 0f, headBase);
            vertices[4] = new Vector3(-head, 0f, headBase);
            vertices[5] = new Vector3(head, 0f, headBase);
            vertices[6] = new Vector3(0f, 0f, tip);

            mesh.vertices = vertices;
            mesh.RecalculateBounds();
        }
    }
}
