using SyncedRush.Generics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public class CrosshairColorBinder : MonoBehaviour
    {
        [Header("UI Sliders (0..1)")]
        [SerializeField] private Slider r;
        [SerializeField] private Slider g;
        [SerializeField] private Slider b;

        [Header("Value Labels (optional)")]
        [SerializeField] private TMP_Text rValue;
        [SerializeField] private TMP_Text gValue;
        [SerializeField] private TMP_Text bValue;
        [SerializeField] private string valueFormat = "F2";

        [Header("Preview Image (optional)")]
        [SerializeField] private Image preview;

        bool suppress;

        void OnEnable()
        {
            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged += RefreshFromSettings;

            if (r) r.onValueChanged.AddListener(_ => Push());
            if (g) g.onValueChanged.AddListener(_ => Push());
            if (b) b.onValueChanged.AddListener(_ => Push());

            RefreshFromSettings();
        }

        void OnDisable()
        {
            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged -= RefreshFromSettings;

            if (r) r.onValueChanged.RemoveAllListeners();
            if (g) g.onValueChanged.RemoveAllListeners();
            if (b) b.onValueChanged.RemoveAllListeners();
        }

        void RefreshFromSettings()
        {
            var sm = SettingsManager.Instance;
            if (sm == null) return;

            suppress = true;

            Color c = sm.GetCrosshairColor();

            if (r) r.value = c.r;
            if (g) g.value = c.g;
            if (b) b.value = c.b;

            if (preview) preview.color = new Color(c.r, c.g, c.b, 1f);

            UpdateLabels();

            suppress = false;
        }

        void Push()
        {
            if (suppress) return;

            var sm = SettingsManager.Instance;
            if (sm == null) return;

            float rf = r ? Mathf.Clamp01(r.value) : 0f;
            float gf = g ? Mathf.Clamp01(g.value) : 0f;
            float bf = b ? Mathf.Clamp01(b.value) : 0f;

            var c = new Color(rf, gf, bf, 1f);

            sm.SetCrosshairColor(c);

            if (preview) preview.color = c;

            UpdateLabels();
        }

        void UpdateLabels()
        {
            if (rValue && r) rValue.text = r.value.ToString(valueFormat);
            if (gValue && g) gValue.text = g.value.ToString(valueFormat);
            if (bValue && b) bValue.text = b.value.ToString(valueFormat);
        }
    }
}