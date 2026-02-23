using SyncedRush.Generics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SyncedRush.UI.Settings
{
    public class FloatSettingSliderBinder : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text textValue;
        [SerializeField] private FloatSettingKey settingKey;
        [SerializeField] private string valueFormat = "F0";

        bool _suppress;

        void Reset()
        {
            slider = GetComponentInChildren<Slider>(true);
            textValue = GetComponentInChildren<TMP_Text>(true);
        }

        void OnEnable()
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(HandleSliderChanged);

            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged += RefreshFromSettings;

            RefreshFromSettings();
        }

        void OnDisable()
        {
            if (slider != null) slider.onValueChanged.RemoveListener(HandleSliderChanged);

            var sm = SettingsManager.Instance;
            if (sm != null) sm.OnSettingsChanged -= RefreshFromSettings;
        }

        void RefreshFromSettings()
        {
            var sm = SettingsManager.Instance;
            if (sm == null || slider == null) return;

            _suppress = true;

            float v = sm.GetFloat(settingKey);

            slider.value = v;
            UpdateText(v);

            _suppress = false;
        }

        void HandleSliderChanged(float v)
        {
            if (_suppress) return;

            var sm = SettingsManager.Instance;
            if (sm == null) return;

            sm.SetFloat(settingKey, v);
            UpdateText(v);
        }

        void UpdateText(float v)
        {
            if (textValue != null)
                textValue.text = v.ToString(valueFormat);
        }
    }
}