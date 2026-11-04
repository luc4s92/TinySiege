using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace TinySiege.AR
{
    /// <summary>
    /// Editor / Development Build only: reports environment depth support, requested and effective
    /// occlusion mode, and whether occlusion is active or the no-depth fallback is in use.
    /// Logs only when that state changes. Does nothing in release builds and never affects rendering.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OcclusionDiagnostics : MonoBehaviour
    {
        [SerializeField] AROcclusionManager occlusionManager;
        [Tooltip("Seconds between state checks.")]
        [SerializeField, Min(0.1f)] float checkInterval = 0.5f;
        [Tooltip("Seconds without depth textures before reporting the fallback.")]
        [SerializeField, Min(0.1f)] float depthTimeout = 1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float nextCheck;
        float lastDepthFrame = float.NegativeInfinity;
        string loggedState;

        void OnEnable()
        {
            if (occlusionManager == null)
            {
                Debug.LogWarning($"{nameof(OcclusionDiagnostics)}: no AROcclusionManager assigned; diagnostics disabled.", this);
                enabled = false;
                return;
            }

            loggedState = null;
            nextCheck = 0f;
            occlusionManager.frameReceived += OnOcclusionFrame;
        }

        void OnDisable()
        {
            if (occlusionManager != null)
                occlusionManager.frameReceived -= OnOcclusionFrame;
        }

        void OnOcclusionFrame(AROcclusionFrameEventArgs args)
        {
            if (occlusionManager.enabled && args.externalTextures.Count > 0)
                lastDepthFrame = Time.unscaledTime;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now < nextCheck)
                return;
            nextCheck = now + checkInterval;

            var descriptor = occlusionManager.descriptor;
            Supported depthSupport = descriptor?.environmentDepthImageSupported ?? Supported.Unknown;
            Supported smoothingSupport = descriptor?.environmentDepthTemporalSmoothingSupported ?? Supported.Unknown;
            // Both modes are what the provider reports; providers that do not implement them report Disabled.
            EnvironmentDepthMode requested = occlusionManager.requestedEnvironmentDepthMode;
            EnvironmentDepthMode effective = occlusionManager.currentEnvironmentDepthMode;
            bool depthReceived = now - lastDepthFrame <= depthTimeout;

            string occlusion;
            if (!depthReceived)
                occlusion = "fallback: no recent environment depth, virtual content is never hidden";
            else if (effective.Enabled())
                occlusion = "occlusion active";
            else
                occlusion = "environment depth received but provider reports depth mode Disabled: occlusion not confirmed by provider";

            string state =
                $"depth support: {depthSupport} | " +
                $"requested mode (provider): {requested} | " +
                $"effective mode (provider): {effective} | " +
                $"temporal smoothing supported: {smoothingSupport}, enabled: {occlusionManager.environmentDepthTemporalSmoothingEnabled} | " +
                $"depth received recently: {depthReceived} | " +
                occlusion;

            if (state == loggedState)
                return;

            loggedState = state;
            Debug.Log($"{nameof(OcclusionDiagnostics)}: {state}.", this);
        }
#else
        void Awake() => enabled = false;
#endif
    }
}
