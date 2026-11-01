using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Configura la escena AR al iniciar y permite colocar marcadores sobre planos
/// horizontales tocando la pantalla.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ARPlacementController : MonoBehaviour
{
    [SerializeField] private GameObject placementPrefab;

    private ARRaycastManager raycastManager;
    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

    private void Awake()
    {
        Camera arCamera = GetComponent<Camera>();

        GameObject sessionObject = GameObject.Find("AR Session");
        if (sessionObject == null)
            sessionObject = new GameObject("AR Session");

        if (sessionObject.GetComponent<ARSession>() == null)
            sessionObject.AddComponent<ARSession>();
        if (sessionObject.GetComponent<ARInputManager>() == null)
            sessionObject.AddComponent<ARInputManager>();

        GameObject originObject = GameObject.Find("XR Origin");
        if (originObject == null)
            originObject = new GameObject("XR Origin");

        XROrigin origin = originObject.GetComponent<XROrigin>();
        if (origin == null)
            origin = originObject.AddComponent<XROrigin>();

        transform.SetParent(originObject.transform, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        origin.Camera = arCamera;

        if (GetComponent<ARCameraManager>() == null)
            gameObject.AddComponent<ARCameraManager>();
        if (GetComponent<ARCameraBackground>() == null)
            gameObject.AddComponent<ARCameraBackground>();

        TrackedPoseDriver poseDriver = GetComponent<TrackedPoseDriver>();
        if (poseDriver == null)
            poseDriver = gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        poseDriver.ignoreTrackingState = true;
        poseDriver.positionInput = new InputActionProperty(
            new InputAction("AR Camera Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
        poseDriver.rotationInput = new InputActionProperty(
            new InputAction("AR Camera Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));

        ARPlaneManager planeManager = originObject.GetComponent<ARPlaneManager>();
        if (planeManager == null)
            planeManager = originObject.AddComponent<ARPlaneManager>();
        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;

        raycastManager = originObject.GetComponent<ARRaycastManager>();
        if (raycastManager == null)
            raycastManager = originObject.AddComponent<ARRaycastManager>();
    }

    private void Update()
    {
        if (!TryGetTouchPosition(out Vector2 screenPosition))
            return;

        Hits.Clear();
        if (!raycastManager.Raycast(screenPosition, Hits, TrackableType.PlaneWithinPolygon))
            return;

        Pose hitPose = Hits[0].pose;
        if (placementPrefab != null)
        {
            Instantiate(placementPrefab, hitPose.position, hitPose.rotation);
            return;
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "AR Placement Marker";
        marker.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
        marker.transform.localScale = new Vector3(0.08f, 0.02f, 0.08f);
    }

    private static bool TryGetTouchPosition(out Vector2 position)
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            position = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            position = Mouse.current.position.ReadValue();
            return true;
        }

        position = default;
        return false;
    }
}
