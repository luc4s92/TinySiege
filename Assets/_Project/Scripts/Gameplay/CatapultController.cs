using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Slingshot-style catapult. The player drags the loaded projectile backwards (touch or mouse):
    /// the pull direction aims and the pull distance sets the power. While held the projectile's
    /// Rigidbody is kinematic; on release it becomes dynamic and receives the launch velocity.
    /// Only one projectile exists at a time: the next one loads after the previous shot is resolved.
    /// Lives under <c>PlacedBoard.CatapultSocket</c>; its forward axis points at the targets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CatapultController : MonoBehaviour
    {
        enum State { Reloading, Ready, Dragging, InFlight }

        [Header("References")]
        [SerializeField] Projectile projectilePrefab;
        [Tooltip("Rest position of the loaded projectile. Its forward axis is the neutral aim direction.")]
        [SerializeField] Transform launchPoint;

        [Header("Drag")]
        [Tooltip("Maximum pull-back distance in meters (full power).")]
        [SerializeField, Min(0.01f)] float maxPullDistance = 0.08f;
        [Tooltip("Releasing with a shorter pull cancels the shot.")]
        [SerializeField, Min(0f)] float minPullDistance = 0.01f;
        [Tooltip("Maximum aim deviation from the catapult forward axis, in degrees.")]
        [SerializeField, Range(0f, 80f)] float maxAimAngle = 35f;
        [Tooltip("Press radius around the loaded projectile, as a fraction of the screen height.")]
        [SerializeField, Range(0.02f, 0.5f)] float grabRadius = 0.12f;

        [Header("Launch")]
        [SerializeField, Range(10f, 80f)] float launchElevation = 45f;
        [SerializeField, Min(0f)] float minLaunchSpeed = 1f;
        [SerializeField, Min(0f)] float maxLaunchSpeed = 2.6f;
        [SerializeField, Min(0f)] float reloadDelay = 0.5f;

        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

        State state = State.Reloading;
        Projectile loaded;
        Vector3 pull;
        float reloadAt;
        Camera viewCamera;
        PointerEventData uiPointerData;
        EventSystem uiPointerEventSystem;

        /// <summary>Set to false (e.g. no ammo, match over) to stop loading and firing.</summary>
        public bool FiringEnabled { get; set; } = true;

        /// <summary>Current pull strength, 0..1.</summary>
        public float Charge01 { get; private set; }

        /// <summary>Raised when a projectile is fired: one unit of ammunition was consumed.</summary>
        public event Action<Projectile> ProjectileLaunched;

        /// <summary>Raised when the fired projectile's shot is over.</summary>
        public event Action<Projectile> ShotResolved;

        /// <summary>Raised whenever <see cref="Charge01"/> changes.</summary>
        public event Action<float> ChargeChanged;

        /// <summary>Camera used to read the pointer. Defaults to <see cref="Camera.main"/>.</summary>
        public void SetCamera(Camera cameraToUse) => viewCamera = cameraToUse;

        void Awake()
        {
            if (projectilePrefab == null || launchPoint == null)
            {
                Debug.LogError($"{nameof(CatapultController)}: missing projectile prefab or launch point; catapult disabled.", this);
                enabled = false;
                return;
            }

            reloadAt = Time.time + reloadDelay;
        }

        void OnDisable()
        {
            if (state == State.Dragging)
                CancelDrag();
        }

        void Update()
        {
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null)
                return;

            switch (state)
            {
                case State.Reloading:
                    if (FiringEnabled && Time.time >= reloadAt)
                        Load();
                    break;

                case State.Ready:
                    if (FiringEnabled && TryGetPressThisFrame(out Vector2 pressPosition) &&
                        !IsOverUI(pressPosition) && IsNearLoadedProjectile(cam, pressPosition))
                        BeginDrag();
                    break;

                case State.Dragging:
                    Pointer pointer = Pointer.current;
                    if (pointer == null || !FiringEnabled)
                    {
                        CancelDrag();
                        break;
                    }

                    UpdateDrag(cam, pointer.position.ReadValue());
                    if (!pointer.press.isPressed)
                        Release();
                    break;
            }
        }

        void Load()
        {
            loaded = Instantiate(projectilePrefab, launchPoint.position, launchPoint.rotation, transform);
            // Held projectiles are kinematic: positioned by the catapult, unaffected by gravity or collisions.
            loaded.Body.isKinematic = true;
            pull = Vector3.zero;
            SetCharge(0f);
            state = State.Ready;
            LogPhysics("loaded, isKinematic = true");
        }

        void BeginDrag()
        {
            // Explicitly kinematic for the whole manipulation.
            loaded.Body.isKinematic = true;
            state = State.Dragging;
            LogPhysics("drag started, isKinematic = true");
        }

        void UpdateDrag(Camera cam, Vector2 screenPosition)
        {
            var dragPlane = new Plane(launchPoint.up, launchPoint.position);
            Ray ray = cam.ScreenPointToRay(screenPosition);
            if (!dragPlane.Raycast(ray, out float distance))
                return;

            // Pull is measured in the launch point's local horizontal plane and only backwards (-Z).
            Vector3 local = launchPoint.InverseTransformDirection(ray.GetPoint(distance) - launchPoint.position);
            local.y = 0f;
            if (local.z > 0f)
                local.z = 0f;

            float magnitude = Mathf.Min(local.magnitude, maxPullDistance);
            if (magnitude > 1e-5f)
            {
                float yaw = Mathf.Clamp(Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg, -maxAimAngle, maxAimAngle);
                local = -(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * magnitude;
            }
            else
            {
                local = Vector3.zero;
            }

            pull = launchPoint.TransformDirection(local);
            loaded.transform.position = launchPoint.position + pull;
            SetCharge(magnitude / maxPullDistance);
        }

        void Release()
        {
            if (pull.magnitude < minPullDistance)
            {
                CancelDrag();
                return;
            }

            Projectile projectile = loaded;
            Vector3 horizontal = -pull.normalized;
            float elevation = launchElevation * Mathf.Deg2Rad;
            Vector3 direction = (horizontal * Mathf.Cos(elevation) + launchPoint.up * Mathf.Sin(elevation)).normalized;
            float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, Charge01);

            // Detach so board/anchor pose updates never teleport a body in flight.
            projectile.transform.SetParent(null, true);

            // Release: dynamic body, gravity and collisions take over.
            projectile.Body.isKinematic = false;
            projectile.Body.linearVelocity = direction * speed;
            projectile.Body.angularVelocity = Vector3.zero;
            projectile.MarkLaunched();
            projectile.Resolved += OnShotResolved;

            loaded = null;
            pull = Vector3.zero;
            state = State.InFlight;
            LogPhysics($"launched, isKinematic = false, speed {speed:0.00} m/s, charge {Charge01:0.00}");
            SetCharge(0f);
            ProjectileLaunched?.Invoke(projectile);
        }

        void CancelDrag()
        {
            if (loaded != null)
            {
                loaded.transform.position = launchPoint.position;
                loaded.Body.isKinematic = true;
            }

            pull = Vector3.zero;
            SetCharge(0f);
            state = State.Ready;
            LogPhysics("drag cancelled, isKinematic = true");
        }

        void OnShotResolved(Projectile projectile)
        {
            projectile.Resolved -= OnShotResolved;
            ShotResolved?.Invoke(projectile);
            reloadAt = Time.time + reloadDelay;
            state = State.Reloading;
        }

        void SetCharge(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, Charge01))
                return;

            Charge01 = value;
            ChargeChanged?.Invoke(value);
        }

        bool IsNearLoadedProjectile(Camera cam, Vector2 screenPosition)
        {
            if (loaded == null)
                return false;

            Vector3 screenPoint = cam.WorldToScreenPoint(loaded.transform.position);
            if (screenPoint.z <= 0f)
                return false;

            return Vector2.Distance(screenPosition, screenPoint) <= grabRadius * Screen.height;
        }

        static bool TryGetPressThisFrame(out Vector2 position)
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                position = pointer.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            if (uiPointerData == null || uiPointerEventSystem != eventSystem)
            {
                uiPointerData = new PointerEventData(eventSystem);
                uiPointerEventSystem = eventSystem;
            }

            uiPointerData.position = screenPosition;
            s_UiHits.Clear();
            eventSystem.RaycastAll(uiPointerData, s_UiHits);
            return s_UiHits.Count > 0;
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void LogPhysics(string message) => Debug.Log($"{nameof(CatapultController)}: {message}.", this);
    }
}
