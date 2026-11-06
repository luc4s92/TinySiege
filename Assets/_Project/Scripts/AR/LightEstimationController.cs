using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using Debug = UnityEngine.Debug;

namespace TinySiege.AR
{
    /// <summary>
    /// Applies AR light estimation to a Light, using only the values the provider actually delivers
    /// and smoothly falling back to Inspector defaults for anything that is missing or stale.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightEstimationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] ARCameraManager cameraManager;
        [SerializeField] Light targetLight;

        [Header("Request")]
        [Tooltip("Also request main light direction/intensity. On ARCore this switches to Environmental HDR mode, " +
                 "which is not assumed to be available; ambient intensity and color are always requested.")]
        [SerializeField] bool requestMainLight;

        [Header("Fallback (used when no estimate is available)")]
        [SerializeField, Min(0f)] float fallbackIntensity = 1f;
        [SerializeField] Color fallbackColor = Color.white;
        [SerializeField, Range(1000f, 20000f)] float fallbackColorTemperature = 6500f;
        [SerializeField] Vector3 fallbackEulerAngles = new Vector3(50f, -30f, 0f);
        [Tooltip("Seconds without a value before that value returns to its fallback.")]
        [SerializeField, Min(0.1f)] float dataTimeout = 1f;

        [Header("Mapping")]
        [Tooltip("Light intensity = estimated brightness (0..1) x this scale, then clamped.")]
        [SerializeField, Min(0f)] float brightnessToIntensity = 2f;
        [SerializeField, Min(0f)] float minIntensity = 0.4f;
        [SerializeField, Min(0f)] float maxIntensity = 2f;

        [Header("Smoothing (higher = faster response)")]
        [SerializeField, Min(0.01f)] float intensitySmoothing = 3f;
        [SerializeField, Min(0.01f)] float colorSmoothing = 3f;
        [SerializeField, Min(0.01f)] float directionSmoothing = 2f;

        enum Channel { Intensity, Color, ColorTemperature, Direction }

        readonly float[] lastReceived = { float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity };

        float estimatedIntensity;
        Color estimatedColor;
        float estimatedColorTemperature;
        Quaternion estimatedRotation;

        float currentIntensity;
        Color currentColor;
        float currentColorTemperature;
        Quaternion currentRotation;

        int loggedState = -1;

        void Awake()
        {
            if (cameraManager == null || targetLight == null)
            {
                Debug.LogError($"{nameof(LightEstimationController)}: missing Inspector references; light estimation disabled.", this);
                enabled = false;
            }
        }

        void OnEnable()
        {
            if (cameraManager == null || targetLight == null)
                return;

            var requested = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor;
            if (requestMainLight)
                requested |= LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity;
            cameraManager.requestedLightEstimation = requested;

            // Start from the fallback so the board is never shown dark while waiting for estimates.
            for (int i = 0; i < lastReceived.Length; i++)
                lastReceived[i] = float.NegativeInfinity;
            currentIntensity = fallbackIntensity;
            currentColor = fallbackColor;
            currentColorTemperature = fallbackColorTemperature;
            currentRotation = Quaternion.Euler(fallbackEulerAngles);
            ApplyToLight();
            loggedState = -1;

            cameraManager.frameReceived += OnFrameReceived;
        }

        void OnDisable()
        {
            if (cameraManager != null)
                cameraManager.frameReceived -= OnFrameReceived;
        }

        void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            ARLightEstimationData data = args.lightEstimation;
            float now = Time.unscaledTime;

            // Intensity: main light brightness first (HDR / simulation), then ambient brightness.
            // Both properties already convert lumens to a 0..1 brightness when only lumens are provided.
            float? brightness = data.averageMainLightBrightness ?? data.averageBrightness;
            if (brightness.HasValue && IsFinite(brightness.Value))
            {
                estimatedIntensity = Mathf.Clamp(brightness.Value * brightnessToIntensity, minIntensity, maxIntensity);
                lastReceived[(int)Channel.Intensity] = now;
            }

            // Color: main light color first, then ambient color correction. Normalized so it only tints;
            // brightness is handled by the intensity channel.
            Color? color = data.mainLightColor ?? data.colorCorrection;
            if (color.HasValue && TryNormalizeTint(color.Value, out Color tint))
            {
                estimatedColor = tint;
                lastReceived[(int)Channel.Color] = now;
            }

            if (data.averageColorTemperature.HasValue && IsFinite(data.averageColorTemperature.Value))
            {
                estimatedColorTemperature = Mathf.Clamp(data.averageColorTemperature.Value, 1000f, 20000f);
                lastReceived[(int)Channel.ColorTemperature] = now;
            }

            if (data.mainLightDirection.HasValue)
            {
                Vector3 direction = data.mainLightDirection.Value;
                if (direction.sqrMagnitude > 1e-6f && IsFinite(direction.x) && IsFinite(direction.y) && IsFinite(direction.z))
                {
                    direction.Normalize();
                    Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
                    estimatedRotation = Quaternion.LookRotation(direction, up);
                    lastReceived[(int)Channel.Direction] = now;
                }
            }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            bool hasIntensity = IsFresh(Channel.Intensity, now);
            bool hasColor = IsFresh(Channel.Color, now);
            bool hasTemperature = IsFresh(Channel.ColorTemperature, now);
            bool hasDirection = IsFresh(Channel.Direction, now);

            float targetIntensity = hasIntensity ? estimatedIntensity : fallbackIntensity;
            Color targetColor = hasColor ? estimatedColor : fallbackColor;
            // A color estimate already carries the tint; using temperature as well would tint twice.
            float targetTemperature = hasTemperature && !hasColor ? estimatedColorTemperature : fallbackColorTemperature;
            Quaternion targetRotation = hasDirection ? estimatedRotation : Quaternion.Euler(fallbackEulerAngles);

            float dt = Time.unscaledDeltaTime;
            currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, SmoothingFactor(intensitySmoothing, dt));
            float colorT = SmoothingFactor(colorSmoothing, dt);
            currentColor = Color.Lerp(currentColor, targetColor, colorT);
            currentColorTemperature = Mathf.Lerp(currentColorTemperature, targetTemperature, colorT);
            currentRotation = Quaternion.Slerp(currentRotation, targetRotation, SmoothingFactor(directionSmoothing, dt));

            ApplyToLight();
            LogStateChange(hasIntensity, hasColor, hasTemperature, hasDirection);
        }

        void ApplyToLight()
        {
            // Never switch the light off: intensity is kept inside [minIntensity, maxIntensity].
            targetLight.intensity = Mathf.Clamp(currentIntensity, minIntensity, Mathf.Max(minIntensity, maxIntensity));
            targetLight.color = currentColor;
            if (targetLight.useColorTemperature && GraphicsSettings.lightsUseColorTemperature)
                targetLight.colorTemperature = currentColorTemperature;
            targetLight.transform.rotation = currentRotation;
        }

        bool IsFresh(Channel channel, float now) => now - lastReceived[(int)channel] <= dataTimeout;

        static float SmoothingFactor(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static bool TryNormalizeTint(Color color, out Color tint)
        {
            float max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (!IsFinite(max) || max <= 1e-4f)
            {
                tint = default;
                return false;
            }

            tint = new Color(color.r / max, color.g / max, color.b / max, 1f);
            return true;
        }

        // Editor and development builds only; logs when the set of received values changes, never per frame.
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void LogStateChange(bool hasIntensity, bool hasColor, bool hasTemperature, bool hasDirection)
        {
            int state = (hasIntensity ? 1 : 0) | (hasColor ? 2 : 0) | (hasTemperature ? 4 : 0) | (hasDirection ? 8 : 0);
            if (state == loggedState)
                return;
            loggedState = state;

            var received = new List<string>(4);
            if (hasIntensity) received.Add("intensity");
            if (hasColor) received.Add("color");
            if (hasTemperature) received.Add(hasColor ? "color temperature (ignored: color estimate in use)" : "color temperature");
            if (hasDirection) received.Add("direction");

            string message = received.Count == 0 ? "no estimate, using fallback for all values" : "receiving " + string.Join(", ", received);
            Debug.Log($"{nameof(LightEstimationController)}: {message} (provider mode: {cameraManager.currentLightEstimation}).", this);
        }
    }
}
