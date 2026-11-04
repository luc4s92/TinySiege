using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace TinySiege.AR
{
    /// <summary>
    /// Hides the AR plane visualization once the board has been placed. Plane detection and tracking keep
    /// running: only each plane's <see cref="ARPlaneMeshVisualizer"/> is disabled, which turns its renderer off.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaneVisualizationController : MonoBehaviour
    {
        [SerializeField] PlacementController placementController;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] bool hidePlanesAfterPlacement = true;

        bool planesHidden;

        void OnEnable()
        {
            if (placementController == null || planeManager == null)
            {
                Debug.LogError($"{nameof(PlaneVisualizationController)}: missing Inspector references; plane visibility unchanged.", this);
                enabled = false;
                return;
            }

            placementController.ObjectPlaced += OnObjectPlaced;
            planeManager.trackablesChanged.AddListener(OnPlanesChanged);

            if (placementController.PlacedObject != null)
                OnObjectPlaced(placementController.PlacedObject);
        }

        void OnDisable()
        {
            if (placementController != null)
                placementController.ObjectPlaced -= OnObjectPlaced;
            if (planeManager != null)
                planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        }

        void OnObjectPlaced(GameObject placed)
        {
            if (!hidePlanesAfterPlacement || planesHidden)
                return;

            planesHidden = true;
            foreach (ARPlane plane in planeManager.trackables)
                HideVisualization(plane);
        }

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> changes)
        {
            if (!planesHidden)
                return;

            foreach (ARPlane plane in changes.added)
                HideVisualization(plane);
        }

        static void HideVisualization(ARPlane plane)
        {
            if (plane != null && plane.TryGetComponent(out ARPlaneMeshVisualizer visualizer))
                visualizer.enabled = false;
        }
    }
}
