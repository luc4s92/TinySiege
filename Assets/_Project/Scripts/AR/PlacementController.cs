using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TinySiege.AR
{
    /// <summary>
    /// Shows a placement preview over detected horizontal planes and places exactly one
    /// anchored object when the player clicks (XR Simulation) or taps (Android) a valid plane.
    /// All AR managers and prefabs are assigned in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementController : MonoBehaviour
    {
        enum PlacementState
        {
            Searching,
            Anchoring,
            Placed,
        }

        [Header("AR managers")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("Prefabs")]
        [SerializeField] GameObject placementPrefab;
        [SerializeField] GameObject previewPrefab;

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        static readonly List<RaycastResult> s_UiHits = new List<RaycastResult>();

        PlacementState state = PlacementState.Searching;
        GameObject previewInstance;
        PointerEventData uiPointerData;
        EventSystem uiPointerEventSystem;

        /// <summary>Raised once, after the placed object has been parented to its anchor.</summary>
        public event Action<GameObject> ObjectPlaced;

        /// <summary>The placed object, or null while nothing has been placed.</summary>
        public GameObject PlacedObject { get; private set; }

        void Awake()
        {
            if (raycastManager == null || planeManager == null || anchorManager == null ||
                arCamera == null || placementPrefab == null || previewPrefab == null)
            {
                Debug.LogError($"{nameof(PlacementController)}: missing Inspector references; placement disabled.", this);
                enabled = false;
                return;
            }

            previewInstance = Instantiate(previewPrefab);
            previewInstance.name = previewPrefab.name;
            previewInstance.SetActive(false);
        }

        void OnDisable()
        {
            SetPreviewVisible(false);
        }

        void OnDestroy()
        {
            if (previewInstance != null)
                Destroy(previewInstance);
        }

        void Update()
        {
            if (state != PlacementState.Searching)
                return;

            // Reject placement while the device pose is not reliable.
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                SetPreviewVisible(false);
                return;
            }

            UpdatePreview();

            if (!TryGetPressThisFrame(out Vector2 pressPosition))
                return;

            if (IsOverUI(pressPosition))
                return;

            if (!TryGetHorizontalPlaneHit(pressPosition, out Pose placementPose))
                return;

            PlaceAnchored(placementPose);
        }

        void UpdatePreview()
        {
            if (TryGetHorizontalPlaneHit(GetPreviewScreenPosition(), out Pose previewPose))
            {
                previewInstance.transform.SetPositionAndRotation(previewPose.position, previewPose.rotation);
                SetPreviewVisible(true);
            }
            else
            {
                SetPreviewVisible(false);
            }
        }

        async void PlaceAnchored(Pose pose)
        {
            // Block further input before awaiting so no second press can start another placement.
            state = PlacementState.Anchoring;
            SetPreviewVisible(false);

            Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(pose);

            if (this == null)
                return;

            if (!result.status.IsSuccess() || result.value == null)
            {
                Debug.LogWarning($"{nameof(PlacementController)}: anchor creation failed ({result.status}); try again.", this);
                state = PlacementState.Searching;
                return;
            }

            ARAnchor anchor = result.value;
            PlacedObject = Instantiate(placementPrefab, anchor.transform);
            PlacedObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            state = PlacementState.Placed;
            ObjectPlaced?.Invoke(PlacedObject);
        }

        bool TryGetHorizontalPlaneHit(Vector2 screenPosition, out Pose pose)
        {
            pose = default;
            s_Hits.Clear();

            if (!raycastManager.Raycast(screenPosition, s_Hits, TrackableType.PlaneWithinPolygon))
                return false;

            // Hits are sorted by distance; take the closest upward-facing horizontal plane.
            foreach (ARRaycastHit hit in s_Hits)
            {
                ARPlane plane = planeManager.GetPlane(hit.trackableId);
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp)
                    continue;

                pose = new Pose(hit.pose.position, FaceCamera(hit.pose.position, plane.normal));
                return true;
            }

            return false;
        }

        Quaternion FaceCamera(Vector3 position, Vector3 up)
        {
            Vector3 toCamera = Vector3.ProjectOnPlane(arCamera.transform.position - position, up);
            if (toCamera.sqrMagnitude < 1e-6f)
                toCamera = Vector3.ProjectOnPlane(-arCamera.transform.forward, up);
            if (toCamera.sqrMagnitude < 1e-6f)
                return Quaternion.FromToRotation(Vector3.up, up);

            return Quaternion.LookRotation(-toCamera.normalized, up);
        }

        Vector2 GetPreviewScreenPosition()
        {
            // Mouse hover drives the preview in the Editor; touch devices use the screen center.
            if (Touchscreen.current == null && Mouse.current != null)
                return Mouse.current.position.ReadValue();

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        static bool TryGetPressThisFrame(out Vector2 position)
        {
            // Touch takes priority; only one press source is accepted per frame.
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
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

            // Raycast the UI at this exact pointer position instead of relying on last-frame pointer state.
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

        void SetPreviewVisible(bool visible)
        {
            if (previewInstance != null && previewInstance.activeSelf != visible)
                previewInstance.SetActive(visible);
        }
    }
}
