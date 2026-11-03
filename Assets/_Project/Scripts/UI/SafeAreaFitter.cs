using UnityEngine;

namespace TinySiege.UI
{
    /// <summary>
    /// Stretches this RectTransform to <see cref="Screen.safeArea"/> so UI children stay clear of
    /// notches, rounded corners and system bars on any resolution or orientation.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rectTransform;
        Rect appliedSafeArea;
        Vector2Int appliedScreenSize;

        void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != appliedSafeArea ||
                Screen.width != appliedScreenSize.x ||
                Screen.height != appliedScreenSize.y)
            {
                Apply();
            }
        }

        void Apply()
        {
            appliedSafeArea = Screen.safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            Vector2 anchorMin = appliedSafeArea.position;
            Vector2 anchorMax = appliedSafeArea.position + appliedSafeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
