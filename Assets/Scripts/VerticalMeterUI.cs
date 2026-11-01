using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Medidor vertical de pantalla. Se puede controlar con Configure, SetValue y Add.
/// </summary>
public class VerticalMeterUI : MonoBehaviour
{
    [Header("Valor del medidor")]
    [SerializeField, Min(0.01f)] private float maximumValue = 100f;
    [SerializeField] private float initialValue = 65f;

    [Header("Apariencia")]
    [SerializeField, Min(1f)] private float barWidth = 38f;
    [SerializeField, Min(1f)] private float barHeight = 240f;
    [SerializeField, Min(0f)] private float leftMargin = 28f;
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.8f);
    [SerializeField] private Color fillColor = new Color(0.1f, 0.75f, 1f, 1f);

    private Image fillImage;
    private float currentValue;

    public float CurrentValue => currentValue;
    public float MaximumValue => maximumValue;

    private void Awake()
    {
        maximumValue = Mathf.Max(0.01f, maximumValue);
        currentValue = Mathf.Clamp(initialValue, 0f, maximumValue);
        CreateBar();
        RefreshBar();
    }

    /// <summary>Configura el límite máximo y el valor actual del medidor.</summary>
    public void Configure(float maxValue, float startValue = 0f)
    {
        maximumValue = Mathf.Max(0.01f, maxValue);
        currentValue = Mathf.Clamp(startValue, 0f, maximumValue);
        RefreshBar();
    }

    /// <summary>Establece el valor actual, limitado entre cero y el máximo.</summary>
    public void SetValue(float value)
    {
        currentValue = Mathf.Clamp(value, 0f, maximumValue);
        RefreshBar();
    }

    /// <summary>Establece el medidor usando un valor normalizado entre cero y uno.</summary>
    public void SetNormalizedValue(float normalizedValue)
    {
        SetValue(Mathf.Clamp01(normalizedValue) * maximumValue);
    }

    /// <summary>Suma o resta una cantidad al valor actual.</summary>
    public void Add(float amount)
    {
        SetValue(currentValue + amount);
    }

    private void CreateBar()
    {
        GameObject canvasObject = new GameObject("Vertical Meter Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("Vertical Meter Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(leftMargin, 0f);
        backgroundRect.sizeDelta = new Vector2(barWidth, barHeight);

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Vertical Meter Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(5f, 5f);
        fillRect.offsetMax = new Vector2(-5f, -5f);

        fillImage = fillObject.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImage.raycastTarget = false;
    }

    private void RefreshBar()
    {
        if (fillImage != null)
            fillImage.fillAmount = maximumValue > 0f ? currentValue / maximumValue : 0f;
    }
}
