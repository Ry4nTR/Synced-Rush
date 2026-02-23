using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("Elements")]
    [SerializeField] private RectTransform centerDot;
    [SerializeField] private RectTransform top;
    [SerializeField] private RectTransform bottom;
    [SerializeField] private RectTransform left;
    [SerializeField] private RectTransform right;

    [Header("Target Settings (User / Gameplay)")]
    public float targetLineLength = 10f;
    public float targetThickness = 2f;
    public float targetGap = 6f;
    public float targetCenterDotSize = 2f;

    [Header("Smoothing")]
    [SerializeField] private float smoothTime = 0.08f;

    [Header("Appearance")]
    [SerializeField] private Color crosshairColor = Color.white;
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 1f;
    [SerializeField] private bool showCenterDot = true;

    // Runtime smoothed values
    private float currentLength;
    private float currentThickness;
    private float currentGap;
    private float currentDotSize;

    // SmoothDamp velocities
    private float lengthVel;
    private float thicknessVel;
    private float gapVel;
    private float dotVel;

    private Image[] images;

    private void Awake()
    {
        if (images == null || images.Length == 0)
            images = GetComponentsInChildren<Image>(true);
        ApplyColor();

        // Initialize smoothed values
        currentLength = targetLineLength;
        currentThickness = targetThickness;
        currentGap = targetGap;
        currentDotSize = targetCenterDotSize;
    }

    private void Update()
    {
        SmoothValues();
        UpdateCrosshair();
    }

    private void SmoothValues()
    {
        currentLength = Mathf.SmoothDamp(
            currentLength, targetLineLength, ref lengthVel, smoothTime);

        currentThickness = Mathf.SmoothDamp(
            currentThickness, targetThickness, ref thicknessVel, smoothTime);

        currentGap = Mathf.SmoothDamp(
            currentGap, targetGap, ref gapVel, smoothTime);

        currentDotSize = Mathf.SmoothDamp(
            currentDotSize, targetCenterDotSize, ref dotVel, smoothTime);
    }

    private void UpdateCrosshair()
    {
        SetLine(top, Vector2.up, currentGap, new Vector2(currentThickness, currentLength));
        SetLine(bottom, Vector2.down, currentGap, new Vector2(currentThickness, currentLength));
        SetLine(left, Vector2.left, currentGap, new Vector2(currentLength, currentThickness));
        SetLine(right, Vector2.right, currentGap, new Vector2(currentLength, currentThickness));

        if (centerDot != null)
        {
            centerDot.gameObject.SetActive(showCenterDot);
            centerDot.sizeDelta = Vector2.one * currentDotSize;
        }
    }

    private void SetLine(RectTransform line, Vector2 dir, float gap, Vector2 size)
    {
        line.anchoredPosition = dir * gap;
        line.sizeDelta = size;
    }

    private void ApplyColor()
    {
        if (images == null || images.Length == 0)
            images = GetComponentsInChildren<Image>(true);

        Color c = crosshairColor;

        float a = (opacity > 1f) ? (opacity / 100f) : opacity;
        c.a = Mathf.Clamp01(a);

        for (int i = 0; i < images.Length; i++)
        {
            var img = images[i];
            if (img == null) continue;
            img.color = c;
        }
    }

    // ===== API for Settings / Gameplay =====

    public void SetGap(float value) => targetGap = value;
    public void SetThickness(float value) => targetThickness = value;
    public void SetLength(float value) => targetLineLength = value;
    public void SetDotSize(float value) => targetCenterDotSize = value;
    public void ApplySettings(SyncedRush.Generics.CrosshairConfig settings)
    {
        targetLineLength = settings.lineLength;
        targetThickness = settings.thickness;
        targetGap = settings.gap;
        targetCenterDotSize = settings.dotSize;
        smoothTime = settings.smoothTime;
        crosshairColor = settings.color;

        // Keep raw value as stored, ApplyColor() will normalize safely
        opacity = settings.opacity;

        showCenterDot = settings.showDot;
        ApplyColor();
    }
    public void LoadAndApplySettings()
    {
        var sm = SyncedRush.Generics.SettingsManager.Instance;
        if (sm != null)
            ApplySettings(sm.Data.crosshair);
    }
}
