using SyncedRush.Generics;
using UnityEngine;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public struct CrosshairSettings
    {
        public float targetLineLength;
        public float targetThickness;
        public float targetGap;
        public float targetCenterDotSize;

        public float smoothTime;

        public Color crosshairColor;
        public float opacity;
        public bool showCenterDot;
    }

    public class CrosshairPreviewer : MonoBehaviour
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
            images = GetComponentsInChildren<Image>();
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
            Color c = crosshairColor;
            c.a = opacity;

            foreach (var img in images)
                img.color = c;
        }

        // ===== API for Settings / Gameplay =====

        public void LoadAndApplySettings()
        {
            ApplySettings(SettingsManager.Instance.CrosshairSettings);
        }

        public void ApplySettings(CrosshairSettings settings)
        {
            targetLineLength = settings.targetLineLength;
            targetThickness = settings.targetThickness;
            targetGap = settings.targetGap;
            targetCenterDotSize = settings.targetCenterDotSize;

            smoothTime = settings.smoothTime;
            crosshairColor = settings.crosshairColor;
            opacity = settings.opacity;
            showCenterDot = settings.showCenterDot;

            ApplyColor();
        }

        public void SetGap(float value) => targetGap = value;
        public void SetThickness(float value) => targetThickness = value;
        public void SetLength(float value) => targetLineLength = value;
        public void SetDotSize(float value) => targetCenterDotSize = value;
    }
}