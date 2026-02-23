using SyncedRush.Generics;
using UnityEngine;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public class CrosshairPreviewer : MonoBehaviour
    {
        [Header("Elements")]
        [SerializeField] private RectTransform centerDot;
        [SerializeField] private RectTransform top;
        [SerializeField] private RectTransform bottom;
        [SerializeField] private RectTransform left;
        [SerializeField] private RectTransform right;

        [Header("Target Settings")]
        public float targetLineLength = 10f;
        public float targetThickness = 2f;
        public float targetGap = 6f;
        public float targetCenterDotSize = 2f;

        [Header("Smoothing")]
        [SerializeField] private float smoothTime = 0.08f;

        [Header("Appearance")]
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private float opacity = 1f; // can be 0..1 or 0..100
        [SerializeField] private bool showCenterDot = true;

        float currentLength, currentThickness, currentGap, currentDotSize;
        float lengthVel, thicknessVel, gapVel, dotVel;

        Image[] images;

        void Awake()
        {
            images = GetComponentsInChildren<Image>(true);

            currentLength = targetLineLength;
            currentThickness = targetThickness;
            currentGap = targetGap;
            currentDotSize = targetCenterDotSize;

            ApplyColor();
        }

        void OnEnable()
        {
            ApplyFromManager();

            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged += ApplyFromManager;
        }

        void OnDisable()
        {
            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged -= ApplyFromManager;
        }

        void Update()
        {
            currentLength = Mathf.SmoothDamp(currentLength, targetLineLength, ref lengthVel, smoothTime);
            currentThickness = Mathf.SmoothDamp(currentThickness, targetThickness, ref thicknessVel, smoothTime);
            currentGap = Mathf.SmoothDamp(currentGap, targetGap, ref gapVel, smoothTime);
            currentDotSize = Mathf.SmoothDamp(currentDotSize, targetCenterDotSize, ref dotVel, smoothTime);

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

        void ApplyFromManager()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            var c = sm.Data.crosshair;

            targetLineLength = c.lineLength;
            targetThickness = c.thickness;
            targetGap = c.gap;
            targetCenterDotSize = c.dotSize;

            smoothTime = c.smoothTime;
            crosshairColor = c.color;
            opacity = c.opacity;
            showCenterDot = c.showDot;

            ApplyColor();
        }

        void SetLine(RectTransform line, Vector2 dir, float gap, Vector2 size)
        {
            if (!line) return;
            line.anchoredPosition = dir * gap;
            line.sizeDelta = size;
        }

        void ApplyColor()
        {
            Color c = crosshairColor;

            float a = (opacity > 1f) ? (opacity / 100f) : opacity;
            c.a = Mathf.Clamp01(a);

            foreach (var img in images)
                if (img) img.color = c;
        }
    }
}